using System;
using System.Collections.Generic;
using System.Linq;
using Adventure3D.Configuration;
using Adventure3D.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// Owns the player entities of one session and advances them one tick at a time. Contains no
    /// camera, UI or input code so the dedicated server can run it in a headless process.
    /// </summary>
    public sealed class WorldSimulation : IDisposable
    {
        public const string PlayerPrefabResource = "Adventure3D/Player";

        readonly SortedDictionary<ushort, PlayerAvatar> _players = new SortedDictionary<ushort, PlayerAvatar>();
        readonly PlayerAvatar _prefab;
        readonly GameConfig _config;
        readonly List<Pose> _spawnPoses = new List<Pose>();
        readonly Transform _container;
        int _nextSpawn;
        /// <summary>Rotates through team spawn positions (per session, so runs are reproducible).</summary>
        public int TeamSpawnCursor;

        public MotorSettings Motor { get; private set; }
        public float TickDelta { get; }
        public GameConfig Config => _config;
        public IEnumerable<PlayerAvatar> Players => _players.Values;
        public int Count => _players.Count;

        public event Action<PlayerAvatar> PlayerAdded;
        public event Action<PlayerAvatar> PlayerRemoved;

        public WorldSimulation(GameConfig config, PlayerAvatar prefab = null)
        {
            _config = config;
            _prefab = prefab != null ? prefab : Resources.Load<PlayerAvatar>(PlayerPrefabResource);
            if (_prefab == null) throw new InvalidOperationException("Player prefab not found at Resources/" + PlayerPrefabResource);
            Motor = MotorSettings.From(config);
            TickDelta = config.Physics.TickDelta;
            _container = new GameObject("Players").transform;
            CollectSpawnPoints();
        }

        public void RefreshMotorSettings() => Motor = MotorSettings.From(_config);

        void CollectSpawnPoints()
        {
            _spawnPoses.Clear();
            var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).OrderBy(p => p.Index);
            foreach (var p in points) _spawnPoses.Add(new Pose(p.transform.position, p.transform.rotation));
            if (_spawnPoses.Count == 0)
                for (int i = 0; i < 8; i++) _spawnPoses.Add(new Pose(new Vector3(i * 2f - 7f, 0.05f, -4f), Quaternion.identity));
        }

        public Pose NextSpawnPose()
        {
            var pose = _spawnPoses[_nextSpawn % _spawnPoses.Count];
            _nextSpawn++;
            return pose;
        }

        public PlayerAvatar Spawn(ushort playerId, bool isLocal, Pose? pose = null)
        {
            if (_players.ContainsKey(playerId)) throw new InvalidOperationException("Player already exists: " + playerId);
            Pose p = pose ?? NextSpawnPose();
            var avatar = Object.Instantiate(_prefab, p.position, Quaternion.identity, _container);
            avatar.Initialize(playerId, isLocal, _config.Physics, p.position, p.rotation.eulerAngles.y);
            avatar.ApplyMotorLimits(_config.Movement);
            PlayerMotor.RefreshGround(avatar.Controller, ref avatar.State, Motor);
            _players.Add(playerId, avatar);
            PlayerAdded?.Invoke(avatar);
            return avatar;
        }

        public bool Despawn(ushort playerId)
        {
            if (!_players.TryGetValue(playerId, out var avatar)) return false;
            _players.Remove(playerId);
            PlayerRemoved?.Invoke(avatar);
            if (avatar != null) Object.Destroy(avatar.gameObject);
            return true;
        }

        public bool TryGet(ushort playerId, out PlayerAvatar avatar) => _players.TryGetValue(playerId, out avatar);
        public bool Contains(ushort playerId) => _players.ContainsKey(playerId);

        /// <summary>Advances every player by one tick using the supplied command provider.</summary>
        public void Tick(Func<PlayerAvatar, PlayerCommand> commandFor)
        {
            Physics.SyncTransforms();
            foreach (var avatar in _players.Values)
                if (avatar != null) avatar.Simulate(commandFor(avatar), Motor, TickDelta);
        }

        public void UpdateViews(float alpha, float deltaTime)
        {
            bool interpolate = _config.Camera.InterpolateView;
            foreach (var avatar in _players.Values)
                if (avatar != null) avatar.UpdateView(alpha, interpolate, deltaTime);
        }

        public void Dispose()
        {
            foreach (var id in _players.Keys.ToList()) Despawn(id);
            if (_container != null) Object.Destroy(_container.gameObject);
        }
    }
}
