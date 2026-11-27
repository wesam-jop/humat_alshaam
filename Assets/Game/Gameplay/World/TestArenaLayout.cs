using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// Geometry of the engineering test arena, built from Unity primitives. The editor setup bakes it
    /// into TestArena.unity and the Play Mode tests build the same layout at runtime, so the measured
    /// values in the verification report refer to exactly these numbers (meters, degrees).
    /// </summary>
    public static class TestArenaLayout
    {
        // Floor top is y = 0.
        public static readonly Vector3 CorridorStart = new Vector3(0f, 0f, 0f);
        public const float CorridorLength = 20f;

        public static readonly Vector3 StairsBase = new Vector3(8f, 0f, 1f);
        public const int StairCount = 8;
        public const float StairRise = 0.25f;
        public const float StairRun = 0.4f;
        public const float StairsTopHeight = StairCount * StairRise; // 2.0 m

        public const float WalkableRampAngle = 40f;
        public const float SteepRampAngle = 50f;
        public static readonly Vector3 WalkableRampBase = new Vector3(-8f, 0f, 2f);
        public static readonly Vector3 SteepRampBase = new Vector3(-14f, 0f, 2f);
        public const float RampLength = 8f;

        public static readonly Vector3 LedgeCenterTop = new Vector3(21f, 3f, 5f);
        public const float LedgeHeight = 3f;
        public const float LedgeEdgeX = 24f;

        public static readonly Vector3 CeilingCenter = new Vector3(-22f, 0f, 5f);
        public const float CeilingClearance = 2.3f;

        public static readonly Vector3 TunnelEntrance = new Vector3(-28f, 0f, 3f);
        public const float TunnelClearance = 1.5f;

        public static readonly Vector3[] SpawnPositions =
        {
            new Vector3(0f, 0.05f, -4f), new Vector3(2f, 0.05f, -4f), new Vector3(-2f, 0.05f, -4f), new Vector3(4f, 0.05f, -4f),
            new Vector3(-4f, 0.05f, -4f), new Vector3(0f, 0.05f, -6f), new Vector3(2f, 0.05f, -6f), new Vector3(-2f, 0.05f, -6f),
        };

        public struct Palette
        {
            public Material Floor, Marker, Stairs, Walkable, Steep, Ledge, Ceiling;
        }

        public static Transform Build(Transform parent = null, Palette palette = default)
        {
            var root = new GameObject("TestArenaGeometry").transform;
            root.SetParent(parent, false);

            Box(root, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), palette.Floor);

            // Corridor of known length with start / end markers (no colliders on markers).
            Marker(root, "Corridor_Start", CorridorStart + new Vector3(0f, 0.005f, 0f), new Vector3(2f, 0.01f, 0.1f), palette.Marker);
            Marker(root, "Corridor_End", CorridorStart + new Vector3(0f, 0.005f, CorridorLength), new Vector3(2f, 0.01f, 0.1f), palette.Marker);
            for (int m = 5; m < CorridorLength; m += 5)
                Marker(root, "Corridor_" + m + "m", CorridorStart + new Vector3(0f, 0.005f, m), new Vector3(1f, 0.01f, 0.05f), palette.Marker);

            // Stairs: solid columns so the step faces are vertical.
            for (int i = 0; i < StairCount; i++)
            {
                float h = (i + 1) * StairRise;
                Box(root, "Stair_" + i, StairsBase + new Vector3(0f, h * 0.5f, i * StairRun + StairRun * 0.5f), new Vector3(3f, h, StairRun), palette.Stairs);
            }
            Box(root, "Stairs_Top", StairsBase + new Vector3(0f, StairsTopHeight * 0.5f, StairCount * StairRun + 1.5f), new Vector3(3f, StairsTopHeight, 3f), palette.Stairs);

            Ramp(root, "Ramp_Walkable_" + WalkableRampAngle, WalkableRampBase, WalkableRampAngle, RampLength, 3f, palette.Walkable);
            Ramp(root, "Ramp_Steep_" + SteepRampAngle, SteepRampBase, SteepRampAngle, RampLength, 3f, palette.Steep);

            // Ledge to fall from.
            Box(root, "Ledge", new Vector3(LedgeCenterTop.x, LedgeHeight * 0.5f, LedgeCenterTop.z), new Vector3(6f, LedgeHeight, 6f), palette.Ledge);

            // Low ceiling (player fits, jump hits it).
            Box(root, "Ceiling_Slab", CeilingCenter + new Vector3(0f, CeilingClearance + 0.25f, 0f), new Vector3(4f, 0.5f, 4f), palette.Ceiling);
            foreach (var c in new[] { new Vector2(-1.8f, -1.8f), new Vector2(1.8f, -1.8f), new Vector2(-1.8f, 1.8f), new Vector2(1.8f, 1.8f) })
                Box(root, "Ceiling_Pillar", CeilingCenter + new Vector3(c.x, CeilingClearance * 0.5f, c.y), new Vector3(0.3f, CeilingClearance, 0.3f), palette.Ceiling);

            // Tunnel lower than the capsule: the player must be blocked.
            Box(root, "Tunnel_Roof", TunnelEntrance + new Vector3(0f, TunnelClearance + 0.5f, 2f), new Vector3(3f, 1f, 4f), palette.Ceiling);
            Box(root, "Tunnel_WallL", TunnelEntrance + new Vector3(-1.75f, 1f, 2f), new Vector3(0.5f, 2f, 4f), palette.Ceiling);
            Box(root, "Tunnel_WallR", TunnelEntrance + new Vector3(1.75f, 1f, 2f), new Vector3(0.5f, 2f, 4f), palette.Ceiling);

            return root;
        }

        // ------------------------------------------------------------------ combat layout

        /// <summary>Team spawn rows at opposite ends of the arena (feet positions, facing the centre).</summary>
        public static readonly Vector3[] DefenderSpawns =
        {
            new Vector3(-10f, 0.05f, -33f), new Vector3(-5f, 0.05f, -34f), new Vector3(0f, 0.05f, -33f), new Vector3(5f, 0.05f, -34f), new Vector3(10f, 0.05f, -33f),
            new Vector3(-14f, 0.05f, -30f), new Vector3(14f, 0.05f, -30f), new Vector3(0f, 0.05f, -36f),
        };

        public static readonly Vector3[] AttackerSpawns =
        {
            new Vector3(10f, 0.05f, 33f), new Vector3(5f, 0.05f, 34f), new Vector3(0f, 0.05f, 33f), new Vector3(-5f, 0.05f, 34f), new Vector3(-10f, 0.05f, 33f),
            new Vector3(14f, 0.05f, 30f), new Vector3(-14f, 0.05f, 30f), new Vector3(0f, 0.05f, 36f),
        };

        public static readonly Vector3 BombSiteA = new Vector3(14f, 0f, 12f);
        public static readonly Vector3 BombSiteB = new Vector3(-14f, 0f, 12f);
        public const float BombSiteRadius = 4.5f;

        public const string CombatCoverName = "CombatCover";

        /// <summary>
        /// Cover between the two spawn rows (crates, low walls, a few tall walls) so fights are not just
        /// open-field duels. Built identically on every machine, so server and clients share colliders.
        /// </summary>
        public static Transform BuildCombatCover(Transform parent = null, Material crate = null, Material wall = null)
        {
            var root = new GameObject(CombatCoverName).transform;
            root.SetParent(parent, false);
            // (x, z, width, height, depth)
            float[,] blocks =
            {
                { -12f, -24f, 2.4f, 1.2f, 1.2f }, { 3f, -25f, 1.2f, 1.2f, 1.2f }, { 12f, -23f, 4f, 1.1f, 0.6f },
                { -4f, -18f, 5f, 2.6f, 0.6f }, { 9f, -16f, 1.2f, 1.2f, 1.2f }, { -16f, -14f, 1.2f, 2.4f, 4f },
                { 3f, -11f, 2.4f, 1.2f, 1.2f }, { 16f, -8f, 4f, 2.6f, 0.6f }, { -6f, -7f, 1.2f, 1.2f, 1.2f },
                { 12f, 13f, 2.4f, 1.2f, 1.2f }, { -3f, 23f, 1.2f, 1.2f, 1.2f }, { -12f, 23f, 4f, 1.1f, 0.6f },
                { 4f, 18f, 5f, 2.6f, 0.6f }, { -9f, 16f, 1.2f, 1.2f, 1.2f }, { 16f, 21f, 1.2f, 2.4f, 4f },
                { -16f, 12f, 4f, 2.6f, 0.6f }, { 6f, 10f, 1.2f, 1.2f, 1.2f }, { 24f, -18f, 1.2f, 1.2f, 1.2f },
                { -24f, 18f, 1.2f, 1.2f, 1.2f }, { 26f, 14f, 0.6f, 2.6f, 5f }, { -26f, -14f, 0.6f, 2.6f, 5f },
            };
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                float x = blocks[i, 0], z = blocks[i, 1], w = blocks[i, 2], h = blocks[i, 3], d = blocks[i, 4];
                bool tall = h > 2f;
                Box(root, tall ? "Wall_" + i : "Crate_" + i, new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), tall ? wall : crate);
            }
            return root;
        }

        public static void Ramp(Transform root, string name, Vector3 baseCenter, float angle, float length, float width, Material mat)
        {
            const float thickness = 0.5f;
            var rot = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 fwd = rot * Vector3.forward;
            Vector3 up = rot * Vector3.up;
            Vector3 topCenter = baseCenter + fwd * (length * 0.5f);
            Vector3 center = topCenter - up * (thickness * 0.5f);
            var go = Box(root, name, center, new Vector3(width, thickness, length), mat);
            go.transform.rotation = rot;
        }

        static GameObject Box(Transform root, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.isStatic = true;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Marker(Transform root, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = Box(root, name, position, scale, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
