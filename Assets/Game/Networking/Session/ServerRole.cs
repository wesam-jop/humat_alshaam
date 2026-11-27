using System;
using System.Collections.Generic;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

namespace Adventure3D.Networking
{
    /// <summary>
    /// Authoritative server side of a session (LAN host and dedicated server). Accepts connections,
    /// validates the handshake, binds every command to the connection that sent it, simulates on the
    /// shared <see cref="WorldSimulation"/> and broadcasts snapshots.
    /// </summary>
    internal sealed class ServerRole : IDisposable
    {
        sealed class Client
        {
            public NetworkConnection Connection;
            public ushort PlayerId;          // 0 until welcomed
            public string ProfileId;
            public string DisplayName;
            public float ConnectedAt;
            public float CloseAt = -1f;      // pending disconnect after a Reject
            public readonly Queue<PlayerCommand> Commands = new Queue<PlayerCommand>();
            public uint LastReceivedSequence;
            public uint LastProcessedSequence;
            public PlayerCommand LastCommand;
            public bool Welcomed => PlayerId != 0;
        }

        readonly WorldSimulation _world;
        readonly NetworkProfile _net;
        readonly List<Client> _clients = new List<Client>();
        readonly Dictionary<ushort, Client> _byPlayer = new Dictionary<ushort, Client>();
        readonly Dictionary<ushort, MotorEvents> _pendingEvents = new Dictionary<ushort, MotorEvents>();
        readonly List<PlayerCommand> _inputScratch = new List<PlayerCommand>(16);
        readonly List<SnapshotEntry> _snapshotScratch = new List<SnapshotEntry>(16);
        readonly Func<ushort> _allocatePlayerId;

        NetworkDriver _driver;
        TransportFactory.Pipelines _pipes;

        public ushort Port { get; private set; }
        public int ConnectedPlayers => _byPlayer.Count;
        public ushort ProtocolVersion = ProtocolInfo.ProtocolVersion;

        public event Action<ushort, string> ClientJoined;
        public event Action<ushort, string> ClientLeft;
        /// <summary>A client asked to buy an item (player id, item id). The session validates and applies it.</summary>
        public event Action<ushort, string> BuyRequested;

        readonly List<CombatEvent> _eventQueue = new List<CombatEvent>(64);
        readonly List<RosterEntry> _rosterScratch = new List<RosterEntry>(16);
        bool _rosterDirty = true;
        public Func<PlayerAvatar, bool> IsBot;

        public void QueueEvents(List<CombatEvent> events) => _eventQueue.AddRange(events);
        public void MarkRosterDirty() => _rosterDirty = true;

        public ServerRole(WorldSimulation world, NetworkProfile net, Func<ushort> allocatePlayerId)
        {
            _world = world;
            _net = net;
            _allocatePlayerId = allocatePlayerId;
            _world.PlayerAdded += a => { a.MotorEventsRaised += OnMotorEvents; _rosterDirty = true; };
            _world.PlayerRemoved += a => _rosterDirty = true;
        }

        void OnMotorEvents(PlayerAvatar a, MotorEvents e)
        {
            _pendingEvents.TryGetValue(a.PlayerId, out var prev);
            _pendingEvents[a.PlayerId] = prev | e;
        }

        public bool Start(ushort port, out string error)
        {
            error = null;
            _driver = TransportFactory.CreateDriver(_net, false, out _pipes);
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
            if (_driver.Bind(endpoint) != 0)
            {
                error = "bind failed on port " + port;
                return false;
            }
            if (_driver.Listen() != 0)
            {
                error = "listen failed on port " + port;
                return false;
            }
            Port = _driver.GetLocalEndpoint().Port;
            GameLog.Info($"Server listening on 0.0.0.0:{Port} protocol={ProtocolVersion}");
            return true;
        }

        public void Pump(float now)
        {
            if (!_driver.IsCreated) return;
            _driver.ScheduleUpdate().Complete();

            NetworkConnection c;
            while ((c = _driver.Accept()) != default)
            {
                _clients.Add(new Client { Connection = c, ConnectedAt = now });
                GameLog.Info("Connection accepted, waiting for hello");
            }

            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                var client = _clients[i];
                NetworkEvent.Type type;
                while (client.Connection.IsCreated &&
                       (type = _driver.PopEventForConnection(client.Connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
                {
                    if (type == NetworkEvent.Type.Data) HandleData(client, ref reader, now);
                    else if (type == NetworkEvent.Type.Disconnect)
                    {
                        var reason = reader.Length > 0 ? (Unity.Networking.Transport.Error.DisconnectReason)reader.ReadByte() : default;
                        GameLog.Info($"Client {client.PlayerId} disconnected ({reason})");
                        RemoveClient(client, false);
                        break;
                    }
                }
            }

            // Handshake timeouts and pending rejects.
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                var client = _clients[i];
                if (client.CloseAt >= 0f && now >= client.CloseAt) RemoveClient(client, true);
                else if (!client.Welcomed && client.CloseAt < 0f && now - client.ConnectedAt > _net.HandshakeTimeout)
                {
                    GameLog.Warn("Handshake timeout, dropping connection");
                    RemoveClient(client, true);
                }
            }
        }

        void HandleData(Client client, ref DataStreamReader reader, float now)
        {
            var type = (MsgType)reader.ReadByte();
            switch (type)
            {
                case MsgType.Hello:
                    if (!client.Welcomed && client.CloseAt < 0f) HandleHello(client, NetMessages.ReadHello(ref reader), now);
                    break;
                case MsgType.Buy:
                    if (client.Welcomed) BuyRequested?.Invoke(client.PlayerId, NetMessages.ReadString(ref reader));
                    break;
                case MsgType.Input:
                    if (!client.Welcomed) return;
                    NetMessages.ReadInput(ref reader, _inputScratch);
                    foreach (var cmd in _inputScratch)
                    {
                        if (cmd.Sequence <= client.LastReceivedSequence) continue; // redundant copy
                        client.LastReceivedSequence = cmd.Sequence;
                        client.Commands.Enqueue(cmd.Sanitized());
                    }
                    break;
            }
        }

        void HandleHello(Client client, HelloMsg hello, float now)
        {
            if (hello.ProtocolVersion != ProtocolVersion)
            {
                GameLog.Warn($"Rejecting client: protocol {hello.ProtocolVersion} != {ProtocolVersion}");
                Reject(client, RejectReason.IncompatibleVersion, now);
                return;
            }

            // Same local profile connecting again (crash / quick rejoin): drop the stale copy.
            foreach (var other in _clients.ToArray())
            {
                if (other != client && other.Welcomed && other.ProfileId == hello.ProfileId)
                {
                    GameLog.Info($"Profile rejoined, replacing stale player {other.PlayerId}");
                    SendReject(other, RejectReason.ReplacedByNewConnection);
                    RemoveClient(other, true);
                }
            }

            if (_world.Count >= _net.MaxPlayers)
            {
                Reject(client, RejectReason.ServerFull, now);
                return;
            }

            client.PlayerId = _allocatePlayerId();
            client.ProfileId = hello.ProfileId;
            client.DisplayName = hello.DisplayName;
            _byPlayer[client.PlayerId] = client;
            var avatar = _world.Spawn(client.PlayerId, false);
            avatar.DisplayName = hello.DisplayName;

            if (_driver.BeginSend(_pipes.Reliable, client.Connection, out var w) == 0)
            {
                NetMessages.WriteWelcome(ref w, new WelcomeMsg
                {
                    ProtocolVersion = ProtocolVersion,
                    PlayerId = client.PlayerId,
                    TickRate = (ushort)_world.Config.Physics.TickRate,
                    ServerTick = 0,
                });
                _driver.EndSend(w);
            }
            GameLog.Info($"Player {client.PlayerId} joined ({hello.DisplayName})");
            GameLog.Event("server_player_joined", "\"player\":" + client.PlayerId + ",\"profile\":" + GameLog.Q(hello.ProfileId));
            ClientJoined?.Invoke(client.PlayerId, hello.DisplayName);
        }

        void Reject(Client client, RejectReason reason, float now)
        {
            SendReject(client, reason);
            client.CloseAt = now + 0.5f; // let the reliable message leave before disconnecting
        }

        void SendReject(Client client, RejectReason reason)
        {
            if (_driver.BeginSend(_pipes.Reliable, client.Connection, out var w) == 0)
            {
                NetMessages.WriteReject(ref w, new RejectMsg { Reason = reason, ServerProtocolVersion = ProtocolVersion });
                _driver.EndSend(w);
            }
        }

        void RemoveClient(Client client, bool disconnect)
        {
            if (disconnect && client.Connection.IsCreated)
            {
                _driver.Disconnect(client.Connection);
            }
            client.Connection = default;
            _clients.Remove(client);
            if (client.Welcomed && _byPlayer.Remove(client.PlayerId))
            {
                _world.Despawn(client.PlayerId);
                _pendingEvents.Remove(client.PlayerId);
                GameLog.Event("server_player_left", "\"player\":" + client.PlayerId);
                ClientLeft?.Invoke(client.PlayerId, client.DisplayName);
            }
        }

        /// <summary>Command for a remote player for the next tick. Repeats the last move if input is late.</summary>
        public PlayerCommand NextCommand(ushort playerId, PlayerAvatar avatar)
        {
            if (!_byPlayer.TryGetValue(playerId, out var client))
                return PlayerCommand.Neutral(avatar.State.Yaw, avatar.State.Pitch);

            // Catch up if the client got ahead (e.g. after a hitch): drop the oldest commands.
            while (client.Commands.Count > _net.MaxQueuedCommands) client.Commands.Dequeue();

            if (client.Commands.Count > 0)
            {
                var cmd = client.Commands.Dequeue();
                client.LastCommand = cmd;
                client.LastProcessedSequence = cmd.Sequence;
                return cmd;
            }
            var repeat = client.LastCommand;
            repeat.Buttons &= ~(CommandButtons.JumpPressed | CommandButtons.Reload | CommandButtons.SwitchWeapon); // never repeat an edge
            repeat.SlotRequest = 0;
            return repeat;
        }

        public void BroadcastSnapshot(uint tick, uint roundStartTick)
        {
            _snapshotScratch.Clear();
            foreach (var a in _world.Players)
            {
                var state = a.State;
                _pendingEvents.TryGetValue(a.PlayerId, out var ev);
                state.Events = ev;
                uint ack = _byPlayer.TryGetValue(a.PlayerId, out var owner) ? owner.LastProcessedSequence : 0u;
                _snapshotScratch.Add(new SnapshotEntry { PlayerId = a.PlayerId, AckSequence = ack, State = state, Combat = a.Combat });
            }
            _pendingEvents.Clear();

            foreach (var client in _clients)
            {
                if (!client.Welcomed || !client.Connection.IsCreated) continue;
                if (_driver.BeginSend(_pipes.Unreliable, client.Connection, out var w) == 0)
                {
                    NetMessages.WriteSnapshot(ref w, tick, roundStartTick, _snapshotScratch);
                    _driver.EndSend(w);
                }
            }

            if (_rosterDirty) SendRoster(null);
            if (_eventQueue.Count > 0)
            {
                for (int start = 0; start < _eventQueue.Count; start += NetMessages.MaxEventsPerMessage)
                {
                    int count = Math.Min(NetMessages.MaxEventsPerMessage, _eventQueue.Count - start);
                    foreach (var client in _clients)
                    {
                        if (!client.Welcomed || !client.Connection.IsCreated) continue;
                        if (_driver.BeginSend(_pipes.Reliable, client.Connection, out var w) == 0)
                        {
                            NetMessages.WriteEvents(ref w, _eventQueue, start, count);
                            _driver.EndSend(w);
                        }
                    }
                }
                _eventQueue.Clear();
            }
        }

        void SendRoster(Client only)
        {
            _rosterDirty = false;
            _rosterScratch.Clear();
            foreach (var a in _world.Players)
                _rosterScratch.Add(new RosterEntry { PlayerId = a.PlayerId, Name = a.DisplayName, IsBot = a.Combat.IsBot });
            foreach (var client in _clients)
            {
                if (only != null && client != only) continue;
                if (!client.Welcomed || !client.Connection.IsCreated) continue;
                if (_driver.BeginSend(_pipes.Reliable, client.Connection, out var w) == 0)
                {
                    NetMessages.WriteRoster(ref w, _rosterScratch);
                    _driver.EndSend(w);
                }
            }
        }

        public string DescribePlayers()
        {
            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (var a in _world.Players)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(a.PlayerId).Append(",\"pos\":").Append(GameLog.V3(a.State.Position)).Append('}');
            }
            return sb.Append(']').ToString();
        }

        /// <summary>Tells every client the host is closing, then disconnects them.</summary>
        public void Shutdown()
        {
            if (!_driver.IsCreated) return;
            foreach (var client in _clients)
            {
                if (!client.Connection.IsCreated) continue;
                if (_driver.BeginSend(_pipes.Reliable, client.Connection, out var w) == 0)
                {
                    NetMessages.WriteShutdown(ref w);
                    _driver.EndSend(w);
                }
            }
            _driver.ScheduleFlushSend().Complete();
            _driver.ScheduleUpdate().Complete();
            foreach (var client in _clients)
                if (client.Connection.IsCreated) _driver.Disconnect(client.Connection);
            _driver.ScheduleUpdate().Complete();
            _clients.Clear();
            _byPlayer.Clear();
            GameLog.Info("Server shut down");
        }

        public void Dispose()
        {
            if (_driver.IsCreated) _driver.Dispose();
        }
    }
}
