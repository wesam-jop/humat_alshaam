using System;
using System.Collections.Generic;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Unity.Collections;
using Unity.Networking.Transport;
using DisconnectReason = Unity.Networking.Transport.Error.DisconnectReason;

namespace Adventure3D.Networking
{
    /// <summary>Client side transport: connect, handshake, send inputs, receive snapshots.</summary>
    internal sealed class ClientRole : IDisposable
    {
        readonly NetworkProfile _net;
        HelloMsg _hello;
        readonly List<SnapshotEntry> _snapshotScratch = new List<SnapshotEntry>(16);

        NetworkDriver _driver;
        TransportFactory.Pipelines _pipes;
        NetworkConnection _connection;
        float _connectStartedAt;
        float _handshakeStartedAt;
        bool _serverAnnouncedShutdown;
        System.Threading.Tasks.Task<System.Net.IPAddress[]> _dns;
        ushort _pendingPort;

        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public SessionEndReason EndReason { get; private set; }
        public ushort LocalPlayerId { get; private set; }
        public int ServerTickRate { get; private set; }

        public event Action<uint, uint, List<SnapshotEntry>> SnapshotReceived;
        public event Action<List<CombatEvent>> EventsReceived;
        public event Action<List<RosterEntry>> RosterReceived;
        readonly List<CombatEvent> _eventScratch = new List<CombatEvent>(32);
        readonly List<RosterEntry> _rosterScratch = new List<RosterEntry>(16);
        public event Action Welcomed;

        public ClientRole(NetworkProfile net, HelloMsg hello)
        {
            _net = net;
            _hello = hello;
        }

        /// <summary>Testing hook for the incompatible-version path.</summary>
        public void OverrideProtocol(ushort version) => _hello.ProtocolVersion = version;

        public bool Connect(string address, ushort port, float now)
        {
            var kind = AddressTools.Classify(address);
            if (kind == AddressTools.Kind.Invalid || port == 0)
            {
                Fail(SessionEndReason.InvalidAddress);
                return false;
            }

            _driver = TransportFactory.CreateDriver(_net, true, out _pipes);
            address = address.Trim();
            if (kind == AddressTools.Kind.HostName)
            {
                // Resolve on a worker thread; Pump() connects once an IPv4 address is known.
                _pendingPort = port;
                _dns = System.Net.Dns.GetHostAddressesAsync(address);
                Status = SessionStatus.Connecting;
                _connectStartedAt = now;
                GameLog.Info($"Resolving {address}");
                return true;
            }
            else
            {
                if (!NetworkEndpoint.TryParse(address, port, out var endpoint,
                        kind == AddressTools.Kind.IPv6 ? NetworkFamily.Ipv6 : NetworkFamily.Ipv4))
                {
                    Fail(SessionEndReason.InvalidAddress);
                    return false;
                }
                _connection = _driver.Connect(endpoint);
            }
            if (!_connection.IsCreated)
            {
                Fail(SessionEndReason.InvalidAddress);
                return false;
            }
            Status = SessionStatus.Connecting;
            _connectStartedAt = now;
            GameLog.Info($"Connecting to {address}:{port}");
            return true;
        }

        public void Pump(float now)
        {
            if (!_driver.IsCreated || Status == SessionStatus.Ended) return;
            if (_dns != null)
            {
                if (!_dns.IsCompleted) return;
                var task = _dns;
                _dns = null;
                System.Net.IPAddress ip = null;
                if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    foreach (var a in task.Result)
                        if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null || !NetworkEndpoint.TryParse(ip.ToString(), _pendingPort, out var resolved, NetworkFamily.Ipv4))
                {
                    Fail(SessionEndReason.HostNotFound);
                    return;
                }
                _connection = _driver.Connect(resolved);
                GameLog.Info($"Resolved to {ip}, connecting");
            }
            _driver.ScheduleUpdate().Complete();

            NetworkEvent.Type type;
            while (_connection.IsCreated && (type = _driver.PopEventForConnection(_connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                        Status = SessionStatus.Handshaking;
                        _handshakeStartedAt = now;
                        Send(_pipes.Reliable, (ref DataStreamWriter w) => NetMessages.WriteHello(ref w, _hello));
                        GameLog.Info("Transport connected, handshake sent");
                        break;
                    case NetworkEvent.Type.Data:
                        HandleData(ref reader);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        var reason = reader.Length > 0 ? (DisconnectReason)reader.ReadByte() : DisconnectReason.Default;
                        _connection = default;
                        OnTransportDisconnect(reason);
                        return;
                }
                if (Status == SessionStatus.Ended) return;
            }

            // App-level timeouts on top of the transport ones.
            float connectBudget = (_net.ConnectTimeoutMs * _net.MaxConnectAttempts) / 1000f + 2f;
            if (Status == SessionStatus.Connecting && now - _connectStartedAt > connectBudget) Fail(SessionEndReason.ConnectTimeout);
            if (Status == SessionStatus.Handshaking && now - _handshakeStartedAt > _net.HandshakeTimeout) Fail(SessionEndReason.HandshakeTimeout);
        }

        void HandleData(ref DataStreamReader reader)
        {
            var type = (MsgType)reader.ReadByte();
            switch (type)
            {
                case MsgType.Welcome:
                {
                    var w = NetMessages.ReadWelcome(ref reader);
                    LocalPlayerId = w.PlayerId;
                    ServerTickRate = w.TickRate;
                    Status = SessionStatus.Running;
                    GameLog.Info($"Welcomed as player {w.PlayerId}, server tick rate {w.TickRate}");
                    Welcomed?.Invoke();
                    break;
                }
                case MsgType.Reject:
                {
                    var r = NetMessages.ReadReject(ref reader);
                    GameLog.Warn($"Rejected by server: {r.Reason} (server protocol {r.ServerProtocolVersion}, ours {_hello.ProtocolVersion})");
                    Fail(r.Reason switch
                    {
                        RejectReason.IncompatibleVersion => SessionEndReason.IncompatibleVersion,
                        RejectReason.ServerFull => SessionEndReason.ServerFull,
                        RejectReason.ReplacedByNewConnection => SessionEndReason.ReplacedByNewConnection,
                        _ => SessionEndReason.TransportError,
                    });
                    break;
                }
                case MsgType.Events:
                    if (Status != SessionStatus.Running) break;
                    NetMessages.ReadEvents(ref reader, _eventScratch);
                    EventsReceived?.Invoke(_eventScratch);
                    break;
                case MsgType.Roster:
                    NetMessages.ReadRoster(ref reader, _rosterScratch);
                    RosterReceived?.Invoke(_rosterScratch);
                    break;
                case MsgType.Shutdown:
                    _serverAnnouncedShutdown = true;
                    Fail(SessionEndReason.ServerClosed);
                    break;
                case MsgType.Snapshot:
                {
                    if (Status != SessionStatus.Running) break;
                    uint tick = NetMessages.ReadSnapshot(ref reader, _snapshotScratch, out uint roundStart);
                    SnapshotReceived?.Invoke(tick, roundStart, _snapshotScratch);
                    break;
                }
            }
        }

        void OnTransportDisconnect(DisconnectReason reason)
        {
            if (Status == SessionStatus.Ended) return;
            SessionEndReason mapped;
            if (_serverAnnouncedShutdown) mapped = SessionEndReason.ServerClosed;
            else if (Status == SessionStatus.Connecting)
                mapped = reason == DisconnectReason.HostNotFound ? SessionEndReason.HostNotFound : SessionEndReason.ConnectTimeout;
            else if (reason == DisconnectReason.ClosedByRemote) mapped = SessionEndReason.ServerClosed;
            else mapped = SessionEndReason.ConnectionLost;
            GameLog.Info($"Transport disconnected ({reason}) -> {mapped}");
            Fail(mapped);
        }

        void Fail(SessionEndReason reason)
        {
            if (Status == SessionStatus.Ended) return;
            EndReason = reason;
            Status = SessionStatus.Ended;
            if (_driver.IsCreated && _connection.IsCreated)
            {
                _driver.Disconnect(_connection);
                _driver.ScheduleUpdate().Complete();
            }
            _connection = default;
        }

        delegate void WriteAction(ref DataStreamWriter w);

        void Send(NetworkPipeline pipe, WriteAction write)
        {
            if (!_connection.IsCreated) return;
            if (_driver.BeginSend(pipe, _connection, out var w) == 0)
            {
                write(ref w);
                _driver.EndSend(w);
            }
        }

        public void SendInputs(IReadOnlyList<PlayerCommand> history, int redundancy)
        {
            if (Status != SessionStatus.Running || history.Count == 0) return;
            int count = Math.Min(redundancy, history.Count);
            int start = history.Count - count;
            if (!_connection.IsCreated) return;
            if (_driver.BeginSend(_pipes.Unreliable, _connection, out var w) == 0)
            {
                NetMessages.WriteInput(ref w, history, start, count);
                _driver.EndSend(w);
            }
        }

        public void SendBuy(string item)
        {
            if (Status != SessionStatus.Running || !_connection.IsCreated) return;
            if (_driver.BeginSend(_pipes.Reliable, _connection, out var w) == 0)
            {
                NetMessages.WriteBuy(ref w, item);
                _driver.EndSend(w);
            }
        }

        /// <summary>Graceful leave: the server sees the disconnect immediately and removes our player.</summary>
        public void Leave()
        {
            if (Status == SessionStatus.Ended) return;
            EndReason = SessionEndReason.UserLeft;
            Status = SessionStatus.Ended;
            if (_driver.IsCreated && _connection.IsCreated)
            {
                _driver.Disconnect(_connection);
                _driver.ScheduleFlushSend().Complete();
                _driver.ScheduleUpdate().Complete();
            }
            _connection = default;
        }

        public void Dispose()
        {
            if (_driver.IsCreated) _driver.Dispose();
        }
    }
}
