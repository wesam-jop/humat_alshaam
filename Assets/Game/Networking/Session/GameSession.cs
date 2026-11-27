using System;
using System.Collections.Generic;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Identity;
using UnityEngine;

namespace Adventure3D.Networking
{
    public struct SessionStats
    {
        public int Corrections;
        public float MaxCorrection;
        public float LastCorrection;
        public int SnapshotsReceived;
    }

    /// <summary>
    /// One play session in any mode. All modes share the same commands, the same
    /// <see cref="PlayerMotor"/> and the same <see cref="WorldSimulation"/>:
    ///  - Offline: local commands are simulated directly. No sockets are created.
    ///  - Host: same as offline plus a <see cref="ServerRole"/> for remote players.
    ///  - DedicatedServer: ServerRole only, no local player.
    ///  - Client: sends commands, predicts its own player, reconciles with server snapshots and
    ///    interpolates remote players.
    /// The session is a plain object owned by <see cref="SessionRunner"/>; disposing it destroys every
    /// player and closes the transport, so starting a new session never inherits old state.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        public static int LiveSessions { get; private set; }

        /// <summary>Seconds between server_state log lines (multi-process tests compare positions with them).</summary>
        public static float ServerLogInterval = 0.2f;

        readonly GameConfig _config;
        readonly PlayerIdentity _identity;
        readonly LaunchRequest _request;
        readonly ServerRole _server;
        readonly ClientRole _client;
        readonly RemoteInterpolation _interpolation;
        readonly List<PlayerCommand> _commandHistory = new List<PlayerCommand>(256);
        readonly List<PlayerSimState> _stateHistory = new List<PlayerSimState>(256);

        float _accumulator;
        float _now;
        float _lastServerLog;
        uint _sequence;
        ushort _nextPlayerId = 1;
        bool _disposed;
        SessionStats _stats;

        // ---- combat
        readonly List<CombatEvent> _tickEvents = new List<CombatEvent>(64);
        readonly Dictionary<ushort, PlayerCommand> _commandsThisTick = new Dictionary<ushort, PlayerCommand>();
        readonly Dictionary<ushort, BotBrain> _bots = new Dictionary<ushort, BotBrain>();
        readonly List<PlayerAvatar> _playerScratch = new List<PlayerAvatar>(16);
        uint _roundStartTick;
        uint _lastServerTick;
        float _sinceServerTick;
        bool _initializingBot;
        ushort _nextBotId = 100;
        static readonly string[] BotNames = { "أسد", "رامي", "أبو العز", "طارق", "معتز", "سامر", "ناصر", "قنّاص الساحل", "صقر", "ليث" };

        public RoundPhase RoundPhase { get; private set; } = RoundPhase.Live;
        public int DefendersScore { get; private set; }
        public int AttackersScore { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public float FreezeTimeRemaining { get; private set; }
        public bool BombPlanted { get; private set; }
        public Vector3 BombPosition { get; private set; }
        public float BombTimer { get; private set; }
        public float PlantProgress { get; private set; }
        public float DefuseProgress { get; private set; }
        int _defLossStreak, _attLossStreak;
        float _roundEndTimer;

        public SessionMode Mode => _request.Mode;
        public LaunchRequest Request => _request;
        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public SessionEndReason EndReason { get; private set; }
        public WorldSimulation World { get; }
        public ushort LocalPlayerId { get; private set; }
        public uint Tick { get; private set; }
        public float InterpolationAlpha { get; private set; }
        public SessionStats Stats => _stats;
        public ushort ListenPort => _server != null ? _server.Port : (ushort)0;
        public int PlayerCount => World.Count;
        public bool IsAuthority => Mode != SessionMode.Client;
        public bool HasLocalPlayer => Mode != SessionMode.DedicatedServer;

        /// <summary>Local input. Null for the dedicated server.</summary>
        public ICommandSource LocalCommands { get; set; }

        /// <summary>Offline only: freezes the simulation (pause menu).</summary>
        public bool Paused { get; set; }

        /// <summary>Network pause menu: local player stands still, the session continues for everyone.</summary>
        public bool LocalInputSuspended { get; set; }

        /// <summary>Testing hook: protocol version this client announces (default = current).</summary>
        public ushort ProtocolVersionOverride { get; set; } = ProtocolInfo.ProtocolVersion;

        public PlayerAvatar LocalAvatar => LocalPlayerId != 0 && World.TryGet(LocalPlayerId, out var a) ? a : null;

        public event Action<SessionStatus, SessionEndReason> StatusChanged;
        public event Action<PlayerAvatar> LocalPlayerSpawned;
        /// <summary>Raised after every simulation tick (tests, diagnostics, future gameplay systems).</summary>
        public event Action<uint> Ticked;

        /// <summary>Every combat event (local authority or received from the server), for HUD, effects and audio.</summary>
        public event Action<CombatEvent> CombatEventRaised;

        /// <summary>Length of one match in seconds; scores reset when it runs out.</summary>
        public float MatchLength { get; set; } = 600f;

        /// <summary>Number of AI players added to an offline session (half allies, half enemies).</summary>
        public int OfflineBots { get; set; } = 5;
        public float BotSkill { get; set; } = 0.4f;

        public float MatchTimeRemaining
        {
            get
            {
                uint now = IsAuthority ? Tick : _lastServerTick;
                float elapsed = (now - _roundStartTick) * World.TickDelta + (IsAuthority ? 0f : _sinceServerTick);
                return Mathf.Max(0f, MatchLength - elapsed);
            }
        }

        public GameSession(LaunchRequest request, GameConfig config, PlayerIdentity identity, WorldSimulation world)
        {
            _request = request ?? LaunchRequest.Offline();
            _config = config;
            _identity = identity;
            World = world;
            LiveSessions++;
            World.PlayerAdded += OnPlayerAdded;
            CombatRules.CrouchSpreadMultiplier = config.Movement.CrouchSpreadMultiplier;

            if (Mode == SessionMode.Host || Mode == SessionMode.DedicatedServer)
                _server = new ServerRole(world, config.Network, () => _nextPlayerId++);
            if (_server != null) _server.BuyRequested += (id, item) => { if (World.TryGet(id, out var a)) ApplyBuy(a, item); };
            if (Mode == SessionMode.Client)
            {
                _interpolation = new RemoteInterpolation(config.Physics.TickDelta, config.Network.InterpolationDelay);
                _client = new ClientRole(config.Network, new HelloMsg
                {
                    ProtocolVersion = ProtocolInfo.ProtocolVersion,
                    ProfileId = identity?.LocalProfileId ?? "anonymous",
                    DisplayName = identity?.DisplayName ?? "",
                    AuthToken = identity?.SessionToken ?? "",
                });
            }
        }

        /// <summary>Starts the session. Returns false if it failed immediately (see EndReason).</summary>
        public bool Start()
        {
            switch (Mode)
            {
                case SessionMode.Offline:
                    SpawnLocalAuthorityPlayer();
                    SpawnBots(OfflineBots);
                    SetStatus(SessionStatus.Running, SessionEndReason.None);
                    GameLog.Info("Offline session started (no network)");
                    return true;

                case SessionMode.Host:
                case SessionMode.DedicatedServer:
                    _server.ProtocolVersion = ProtocolInfo.ProtocolVersion;
                    if (!_server.Start(_request.Port, out var error))
                    {
                        GameLog.Error("Server start failed: " + error);
                        End(SessionEndReason.BindFailed);
                        return false;
                    }
                    if (Mode == SessionMode.Host) SpawnLocalAuthorityPlayer();
                    SetStatus(SessionStatus.Running, SessionEndReason.None);
                    GameLog.Event(Mode == SessionMode.Host ? "host_started" : "server_started", "\"port\":" + _server.Port);
                    return true;

                case SessionMode.Client:
                    if (ProtocolVersionOverride != ProtocolInfo.ProtocolVersion)
                        _client.OverrideProtocol(ProtocolVersionOverride);
                    _client.Welcomed += OnWelcomed;
                    _client.SnapshotReceived += OnSnapshot;
                    _client.EventsReceived += OnRemoteEvents;
                    _client.RosterReceived += OnRoster;
                    if (!_client.Connect(_request.Address, _request.Port, _now))
                    {
                        End(_client.EndReason);
                        return false;
                    }
                    SetStatus(SessionStatus.Connecting, SessionEndReason.None);
                    return true;
            }
            return false;
        }

        void SpawnLocalAuthorityPlayer()
        {
            LocalPlayerId = _nextPlayerId++;
            var avatar = World.Spawn(LocalPlayerId, true);
            avatar.DisplayName = _identity?.DisplayName ?? "";
            LocalCommands?.SetLook(avatar.State.Yaw, 0f);
            LocalPlayerSpawned?.Invoke(avatar);
        }

        // ------------------------------------------------------------------ frame update

        public void Update(float deltaTime)
        {
            if (_disposed || Status == SessionStatus.Ended) return;
            _now += deltaTime;

            _server?.Pump(_now);
            if (_client != null)
            {
                _client.Pump(_now);
                if (_client.Status == SessionStatus.Handshaking && Status != SessionStatus.Handshaking)
                    SetStatus(SessionStatus.Handshaking, SessionEndReason.None);
                if (_client.Status == SessionStatus.Ended)
                {
                    End(_client.EndReason);
                    return;
                }
            }

            bool frozen = Mode == SessionMode.Offline && Paused;
            if (frozen)
            {
                _accumulator = 0f;
                InterpolationAlpha = 1f;
                return;
            }

            float tickDt = World.TickDelta;
            _accumulator += deltaTime;
            int maxTicks = _config.Physics.MaxTicksPerFrame;
            int ticks = 0;
            while (_accumulator >= tickDt && ticks < maxTicks)
            {
                _accumulator -= tickDt;
                RunTick();
                Ticked?.Invoke(Tick);
                ticks++;
                if (Status == SessionStatus.Ended) return;
            }
            if (ticks == maxTicks && _accumulator > tickDt) _accumulator = 0f; // drop backlog after a long hitch
            InterpolationAlpha = Mathf.Clamp01(_accumulator / tickDt);

            if (_interpolation != null) ApplyRemoteInterpolation(deltaTime);

            if (_server != null && _now - _lastServerLog >= ServerLogInterval)
            {
                _lastServerLog = _now;
                GameLog.Event("server_state", "\"tick\":" + Tick + ",\"players\":" + _server.DescribePlayers());
            }
        }

        public void LateUpdate(float deltaTime)
        {
            if (_disposed) return;
            World.UpdateViews(InterpolationAlpha, deltaTime);
        }

        void RunTick()
        {
            if (IsAuthority)
            {
                _commandsThisTick.Clear();
                float eye = _config.Physics.EyeHeight;
                World.Tick(avatar =>
                {
                    PlayerCommand cmd;
                    if (avatar.PlayerId == LocalPlayerId) cmd = SampleLocalCommand(avatar);
                    else if (_bots.TryGetValue(avatar.PlayerId, out var brain)) cmd = brain.Think(avatar, World, World.TickDelta, eye);
                    else cmd = _server != null ? _server.NextCommand(avatar.PlayerId, avatar) : PlayerCommand.Neutral(avatar.State.Yaw, 0f);
                    cmd = cmd.Sanitized();
                    _commandsThisTick[avatar.PlayerId] = cmd;
                    return cmd;
                });

                // Combat after movement, on a stable copy (deaths do not change the player list).
                _tickEvents.Clear();
                _playerScratch.Clear();
                _playerScratch.AddRange(World.Players);
                Physics.SyncTransforms();

                if (RoundPhase == RoundPhase.FreezeTime)
                {
                    FreezeTimeRemaining = Mathf.Max(0f, FreezeTimeRemaining - World.TickDelta);
                    if (FreezeTimeRemaining <= 0f)
                    {
                        RoundPhase = RoundPhase.Live;
                        _roundStartTick = Tick;
                        _tickEvents.Add(new CombatEvent { Type = CombatEventType.RoundRestart });
                    }
                }
                else if (RoundPhase == RoundPhase.RoundEnd)
                {
                    _roundEndTimer -= World.TickDelta;
                    if (_roundEndTimer <= 0f)
                    {
                        RoundNumber++;
                        _roundStartTick = Tick;
                        FreezeTimeRemaining = Mathf.Max(0f, Configuration.Cs16ConVars.MpFreezeTime);
                        RoundPhase = FreezeTimeRemaining > 0f ? RoundPhase.FreezeTime : RoundPhase.Live;
                        foreach (var p in _playerScratch)
                            CombatRules.InitPlayer(p, p.Combat.Team, p.Combat.IsBot, World);
                        BombPlanted = false;
                        BombTimer = 0f;
                        PlantProgress = 0f;
                        DefuseProgress = 0f;
                    }
                }

                foreach (var a in _playerScratch)
                {
                    _commandsThisTick.TryGetValue(a.PlayerId, out var cmd);
                    if (RoundPhase == RoundPhase.FreezeTime)
                    {
                        cmd.Move = Vector2.zero;
                        cmd.Buttons &= ~(CommandButtons.Fire | CommandButtons.JumpPressed | CommandButtons.JumpHeld);
                    }
                    CombatRules.Tick(a, cmd, World, World.TickDelta, eye, _tickEvents);
                }
                if (RoundPhase == RoundPhase.Live) UpdateBomb(World.TickDelta, _tickEvents);
                Tick++;
                if (RoundPhase == RoundPhase.Live && !BombPlanted && (Tick - _roundStartTick) * World.TickDelta >= MatchLength)
                {
                    EndRoundWithWinner(Team.Defenders, _tickEvents);
                }
                DispatchEvents(_tickEvents);
                if (_server != null) _server.QueueEvents(_tickEvents);
                if (_server != null && Tick % (uint)Mathf.Max(1, _config.Network.SnapshotIntervalTicks) == 0)
                    _server.BroadcastSnapshot(Tick, _roundStartTick);
            }
            else
            {
                Tick++;
                var local = LocalAvatar;
                if (local == null || _client.Status != SessionStatus.Running) return;
                if (!local.Alive)
                {
                    _commandHistory.Clear();
                    _stateHistory.Clear();
                    SampleLocalCommand(local); // consume input edges; nothing is sent while dead
                    return;
                }

                var cmd = SampleLocalCommand(local);
                cmd.Sequence = ++_sequence;
                Physics.SyncTransforms();
                local.Simulate(cmd, World.Motor, World.TickDelta);
                _commandHistory.Add(cmd);
                _stateHistory.Add(local.State);
                if (_commandHistory.Count > 240)
                {
                    _commandHistory.RemoveAt(0);
                    _stateHistory.RemoveAt(0);
                }
                _client.SendInputs(_commandHistory, _config.Network.InputRedundancy);
            }
            _sinceServerTick += World.TickDelta;
        }

        PlayerCommand SampleLocalCommand(PlayerAvatar avatar)
        {
            if (LocalCommands == null) return PlayerCommand.Neutral(avatar.State.Yaw, avatar.State.Pitch);
            var cmd = LocalCommands.SampleCommand();
            if (LocalInputSuspended) cmd = PlayerCommand.Neutral(cmd.Yaw, cmd.Pitch);
            return cmd;
        }

        // ------------------------------------------------------------------ client side

        void OnWelcomed()
        {
            LocalPlayerId = _client.LocalPlayerId;
            SetStatus(SessionStatus.Running, SessionEndReason.None);
            GameLog.Event("client_welcomed", "\"player\":" + LocalPlayerId);
        }

        readonly HashSet<ushort> _seenScratch = new HashSet<ushort>();
        readonly List<ushort> _removeScratch = new List<ushort>();

        void OnSnapshot(uint serverTick, uint roundStartTick, List<SnapshotEntry> entries)
        {
            _stats.SnapshotsReceived++;
            if (serverTick > _lastServerTick) { _lastServerTick = serverTick; _sinceServerTick = 0f; }
            _roundStartTick = roundStartTick;
            _seenScratch.Clear();
            foreach (var e in entries)
            {
                _seenScratch.Add(e.PlayerId);
                bool isLocal = e.PlayerId == LocalPlayerId;
                if (!World.TryGet(e.PlayerId, out var avatar))
                {
                    var pose = new Pose(e.State.Position, Quaternion.Euler(0f, e.State.Yaw, 0f));
                    avatar = World.Spawn(e.PlayerId, isLocal, pose);
                    avatar.SetState(e.State, true);
                    if (isLocal)
                    {
                        LocalCommands?.SetLook(e.State.Yaw, e.State.Pitch);
                        LocalPlayerSpawned?.Invoke(avatar);
                    }
                }
                bool wasAlive = avatar.Alive;
                avatar.Combat = e.Combat;
                avatar.Combat.Flags = e.Combat.Flags; // keep flags exactly as the server sent them
                avatar.SetAlive(e.Combat.Alive);
                if (isLocal && !wasAlive && e.Combat.Alive)
                {
                    // Respawned by the server: start predicting from the new authoritative state.
                    _commandHistory.Clear();
                    _stateHistory.Clear();
                    avatar.SetState(e.State, true);
                    LocalCommands?.SetLook(e.State.Yaw, e.State.Pitch);
                }
                if (isLocal) Reconcile(avatar, e);
                else _interpolation.Push(e.PlayerId, serverTick, e.State);
            }

            _removeScratch.Clear();
            foreach (var a in World.Players)
                if (!_seenScratch.Contains(a.PlayerId)) _removeScratch.Add(a.PlayerId);
            foreach (var id in _removeScratch)
            {
                World.Despawn(id);
                _interpolation.Remove(id);
            }
            _interpolation.OnSnapshotTime(serverTick);
        }

        void Reconcile(PlayerAvatar local, SnapshotEntry e)
        {
            uint ack = e.AckSequence;
            int index = -1;
            for (int i = 0; i < _commandHistory.Count; i++)
                if (_commandHistory[i].Sequence == ack) { index = i; break; }

            if (index < 0)
            {
                // Nothing of ours processed yet (or history lost): accept the server state if far off.
                if (_commandHistory.Count == 0 && (local.State.Position - e.State.Position).sqrMagnitude > 0.0001f)
                    local.SetState(e.State, true);
                return;
            }

            float error = Vector3.Distance(_stateHistory[index].Position, e.State.Position);
            _commandHistory.RemoveRange(0, index + 1);
            _stateHistory.RemoveRange(0, index + 1);
            if (error <= _config.Network.ReconciliationThreshold) return;

            // Rewind to the authoritative state and replay the commands the server has not seen yet.
            Vector3 before = local.State.Position;
            local.SetState(e.State, false);
            for (int i = 0; i < _commandHistory.Count; i++)
            {
                local.Simulate(_commandHistory[i], World.Motor, World.TickDelta);
                _stateHistory[i] = local.State;
            }
            Vector3 after = local.State.Position;
            local.AddCorrectionOffset(before - after, _config.Network.CorrectionSmoothingTime);

            _stats.Corrections++;
            _stats.LastCorrection = error;
            _stats.MaxCorrection = Mathf.Max(_stats.MaxCorrection, error);
        }

        void ApplyRemoteInterpolation(float deltaTime)
        {
            _interpolation.Advance(deltaTime);
            foreach (var a in World.Players)
            {
                if (a.PlayerId == LocalPlayerId) continue;
                if (_interpolation.TrySample(a.PlayerId, out var pos, out var yaw, out var latest))
                    a.SetRemotePose(pos, yaw, latest);
            }
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ combat

        void OnPlayerAdded(PlayerAvatar a)
        {
            if (!IsAuthority || _initializingBot) return;
            CombatRules.InitPlayer(a, BalancedTeam(), false, World);
        }

        Team BalancedTeam()
        {
            int defenders = 0, attackers = 0;
            foreach (var p in World.Players)
            {
                if (!p.Alive && p.Combat.Health == 0 && p.Combat.Kills == 0 && p.Combat.Deaths == 0) continue; // not initialised yet
                if (p.Combat.Team == Team.Defenders) defenders++; else attackers++;
            }
            return defenders <= attackers ? Team.Defenders : Team.Attackers;
        }

        /// <summary>Adds AI players (authority only). Half join the local player's team.</summary>
        public void SpawnBots(int count)
        {
            if (!IsAuthority) return;
            for (int i = 0; i < count; i++)
                AddBot(i % 2 == 0 ? Team.Attackers : Team.Defenders);
            _server?.MarkRosterDirty();
        }

        /// <summary>Adds a single AI bot on the requested team (or auto-balanced).</summary>
        public PlayerAvatar AddBot(Team? team = null)
        {
            if (!IsAuthority) return null;
            ushort id = _nextBotId++;
            _initializingBot = true;
            var a = World.Spawn(id, false);
            _initializingBot = false;
            a.DisplayName = BotNames[(id - 100) % BotNames.Length];
            Team chosen = team ?? BalancedTeam();
            CombatRules.InitPlayer(a, chosen, true, World);
            _bots[id] = new BotBrain(id, 1234 + id * 7919, BotSkill);
            _server?.MarkRosterDirty();
            return a;
        }

        /// <summary>Removes all AI bots from the match.</summary>
        public int KickBots()
        {
            if (!IsAuthority) return 0;
            int removed = 0;
            foreach (var id in new List<ushort>(_bots.Keys))
            {
                if (World.Despawn(id)) removed++;
            }
            _bots.Clear();
            _server?.MarkRosterDirty();
            return removed;
        }

        /// <summary>Eliminates all currently alive AI bots.</summary>
        public int KillBots()
        {
            if (!IsAuthority) return 0;
            int killed = 0;
            foreach (var id in _bots.Keys)
            {
                if (World.TryGet(id, out var bot) && bot.Alive)
                {
                    bot.Combat.Health = 0;
                    bot.Combat.RespawnTimer = 3f;
                    killed++;
                }
            }
            return killed;
        }

        /// <summary>Updates bot difficulty skill (0..1) for existing and future bots.</summary>
        public void SetBotSkill(float skill)
        {
            BotSkill = Mathf.Clamp01(skill);
            if (!IsAuthority) return;
            foreach (var id in new List<ushort>(_bots.Keys))
                _bots[id] = new BotBrain(id, 1234 + id * 7919, BotSkill);
        }

        void UpdateBomb(float dt, List<CombatEvent> events)
        {
            if (BombPlanted)
            {
                BombTimer -= dt;
                if (BombTimer <= 0f)
                {
                    BombPlanted = false;
                    DefuseProgress = 0f;
                    events.Add(new CombatEvent { Type = CombatEventType.BombExploded, Point = BombPosition });
                    EndRoundWithWinner(Team.Attackers, events);
                    return;
                }
                bool defusing = false;
                foreach (var p in _playerScratch)
                {
                    if (!p.Alive || p.Combat.Team != Team.Defenders) continue;
                    if (_commandsThisTick.TryGetValue(p.PlayerId, out var cmd) && cmd.SlotRequest == 8
                        && Vector3.Distance(p.State.Position, BombPosition) <= 2.8f)
                    {
                        defusing = true;
                        float defuseTime = (p.Combat.Flags & CombatFlags.Defuser) != 0 ? 4.5f : 8.0f;
                        DefuseProgress += dt / defuseTime;
                        if (DefuseProgress >= 1f)
                        {
                            BombPlanted = false;
                            DefuseProgress = 0f;
                            events.Add(new CombatEvent { Type = CombatEventType.BombDefused, Actor = p.PlayerId, Point = BombPosition });
                            EndRoundWithWinner(Team.Defenders, events);
                            return;
                        }
                        break;
                    }
                }
                if (!defusing) DefuseProgress = 0f;
                return;
            }

            bool planting = false;
            foreach (var p in _playerScratch)
            {
                if (!p.Alive || p.Combat.Team != Team.Attackers) continue;
                bool inSite = Vector3.Distance(p.State.Position, TestArenaLayout.BombSiteA) <= TestArenaLayout.BombSiteRadius
                           || Vector3.Distance(p.State.Position, TestArenaLayout.BombSiteB) <= TestArenaLayout.BombSiteRadius;
                if (inSite && _commandsThisTick.TryGetValue(p.PlayerId, out var cmd) && cmd.SlotRequest == 8)
                {
                    planting = true;
                    PlantProgress += dt / 3.0f;
                    if (PlantProgress >= 1f)
                    {
                        PlantProgress = 0f;
                        BombPlanted = true;
                        BombPosition = p.State.Position;
                        BombTimer = 45f;
                        events.Add(new CombatEvent { Type = CombatEventType.BombPlanted, Actor = p.PlayerId, Point = BombPosition });
                    }
                    break;
                }
            }
            if (!planting) PlantProgress = 0f;
        }

        /// <summary>Ends the current round, awards CS 1.6 win/loss money, and schedules FreezeTime.</summary>
        public void EndRoundWithWinner(Team winner, List<CombatEvent> events)
        {
            if (RoundPhase == RoundPhase.RoundEnd) return;
            RoundPhase = RoundPhase.RoundEnd;
            _roundEndTimer = 5f;
            if (winner == Team.Defenders) { DefendersScore++; _defLossStreak = 0; _attLossStreak = Mathf.Min(4, _attLossStreak + 1); }
            else { AttackersScore++; _attLossStreak = 0; _defLossStreak = Mathf.Min(4, _defLossStreak + 1); }

            int defReward = winner == Team.Defenders ? 3250 : 1400 + _defLossStreak * 500;
            int attReward = winner == Team.Attackers ? 3250 : 1400 + _attLossStreak * 500;
            foreach (var p in World.Players)
            {
                int add = p.Combat.Team == Team.Defenders ? defReward : attReward;
                p.Combat.Money = Mathf.Min(CombatRules.MaxMoney, p.Combat.Money + add);
            }
            short packedScore = (short)((DefendersScore & 0xFF) | ((AttackersScore & 0xFF) << 8));
            events.Add(new CombatEvent { Type = CombatEventType.RoundWin, Actor = (ushort)winner, Amount = packedScore });
        }

        /// <summary>Restarts the round immediately (sv_restart) by respawning all players.</summary>
        public void RestartRound()
        {
            if (!IsAuthority) return;
            _roundStartTick = Tick;
            foreach (var p in World.Players)
                CombatRules.InitPlayer(p, p.Combat.Team, p.Combat.IsBot, World);
        }

        /// <summary>Grants money to the local player (used by impulse 101 with sv_cheats 1).</summary>
        public void GiveLocalMoney(int amount)
        {
            var local = LocalAvatar;
            if (local == null || !IsAuthority) return;
            local.Combat.Money = Mathf.Clamp(local.Combat.Money + amount, 0, 16000);
        }

        void DispatchEvents(List<CombatEvent> events)
        {
            foreach (var e in events) CombatEventRaised?.Invoke(e);
        }

        void OnRemoteEvents(List<CombatEvent> events) => DispatchEvents(events);

        void OnRoster(List<RosterEntry> roster)
        {
            foreach (var r in roster)
                if (World.TryGet(r.PlayerId, out var a)) a.DisplayName = r.Name;
            _roster.Clear();
            foreach (var r in roster) _roster[r.PlayerId] = r.Name;
        }

        readonly Dictionary<ushort, string> _roster = new Dictionary<ushort, string>();

        /// <summary>Display name of any player id (works on clients before the avatar exists).</summary>
        public string NameOf(ushort playerId)
        {
            if (World.TryGet(playerId, out var a) && !string.IsNullOrEmpty(a.DisplayName)) return a.DisplayName;
            return _roster.TryGetValue(playerId, out var n) ? n : "#" + playerId;
        }

        /// <summary>Buy menu entry point for the local player. The authority validates price and money.</summary>
        public void RequestBuy(string itemId)
        {
            if (Mode == SessionMode.Client) { _client?.SendBuy(itemId); return; }
            var local = LocalAvatar;
            if (local != null) ApplyBuy(local, itemId);
        }

        void ApplyBuy(PlayerAvatar a, string itemId)
        {
            _tickEvents.Clear();
            var result = CombatRules.Buy(a, itemId, _tickEvents);
            if (result != CombatRules.BuyResult.Ok) GameLog.Info($"Buy {itemId} by {a.PlayerId}: {result}");
            DispatchEvents(_tickEvents);
            _server?.QueueEvents(_tickEvents);
            _tickEvents.Clear();
        }

        // ------------------------------------------------------------------ lifecycle

        void SetStatus(SessionStatus status, SessionEndReason reason)
        {
            if (Status == status && EndReason == reason) return;
            Status = status;
            EndReason = reason;
            GameLog.Event("session_status", "\"mode\":\"" + Mode + "\",\"status\":\"" + status + "\",\"reason\":\"" + reason + "\"");
            StatusChanged?.Invoke(status, reason);
        }

        void End(SessionEndReason reason)
        {
            if (Status == SessionStatus.Ended) return;
            SetStatus(SessionStatus.Ended, reason);
        }

        /// <summary>Player chose to leave (back to menu). Closes the network cleanly.</summary>
        public void Leave()
        {
            if (Status == SessionStatus.Ended) return;
            _client?.Leave();
            _server?.Shutdown();
            End(SessionEndReason.UserLeft);
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (Status != SessionStatus.Ended) Leave();
            _disposed = true;
            _client?.Dispose();
            _server?.Dispose();
            World.Dispose();
            LiveSessions--;
            StatusChanged = null;
            LocalPlayerSpawned = null;
            CombatEventRaised = null;
            Ticked = null;
        }
    }
}
