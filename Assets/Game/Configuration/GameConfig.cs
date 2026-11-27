using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>
    /// Root configuration asset. Lives at Resources/Adventure3D/GameConfig so the bootstrapper, the
    /// dedicated server and tests all read the same shared profiles. Per-player runtime state is never
    /// stored here (see PlayerSimState).
    /// </summary>
    [CreateAssetMenu(menuName = "Adventure3D/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "Adventure3D/GameConfig";

        public MovementProfile Movement;
        public AirMovementProfile Air;
        public PhysicsProfile Physics;
        public CameraProfile Camera;
        public InputProfile Input;
        public NetworkProfile Network;

        public static GameConfig Load()
        {
            var cfg = Resources.Load<GameConfig>(ResourcePath);
            if (cfg == null)
            {
                Debug.LogWarning("[A3D] GameConfig asset missing at Resources/" + ResourcePath + ", using code defaults.");
                cfg = CreateDefault();
            }
            cfg.FillMissing();
            return cfg;
        }

        /// <summary>Runtime instance with default values (tests and fallback).</summary>
        public static GameConfig CreateDefault()
        {
            var cfg = CreateInstance<GameConfig>();
            cfg.FillMissing();
            return cfg;
        }

        public void FillMissing()
        {
            if (Movement == null) Movement = CreateInstance<MovementProfile>();
            if (Air == null) Air = CreateInstance<AirMovementProfile>();
            if (Physics == null) Physics = CreateInstance<PhysicsProfile>();
            if (Camera == null) Camera = CreateInstance<CameraProfile>();
            if (Input == null) Input = CreateInstance<InputProfile>();
            if (Network == null) Network = CreateInstance<NetworkProfile>();
        }

        /// <summary>Copy so runtime overrides (command line, tests) never modify the shared assets.</summary>
        public GameConfig CloneForRuntime()
        {
            var c = CreateInstance<GameConfig>();
            c.Movement = Instantiate(Movement);
            c.Air = Instantiate(Air);
            c.Physics = Instantiate(Physics);
            c.Camera = Instantiate(Camera);
            c.Input = Instantiate(Input);
            c.Network = Instantiate(Network);
            c.name = name + " (runtime)";
            return c;
        }
    }
}
