using System;
using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>
    /// Counter-Strike 1.6 ConVars &amp; GoldSrc-to-Unity physics bridge (1 Hammer unit = 0.0254 m).
    /// Applies live ConVar values to a runtime GameConfig clone without mutating shared assets.
    /// </summary>
    public static class Cs16ConVars
    {
        public const float HammerToMeters = 0.0254f;
        public const float BaseRunUnitsPerSec = 250f;
        public const float JumpHeightUnits = 45f;

        // Physics & Movement ConVars (CS 1.6 defaults)
        public static float SvGravity = 800f;
        public static float SvMaxSpeed = 320f;
        public static float SvAccelerate = 5f;
        public static float SvAirAccelerate = 10f;
        public static float SvFriction = 4f;
        public static float SvStopSpeed = 75f;
        public static float SvStepSize = 18f;
        public static bool SvCheats = false;
        public static bool UseCs16Physics = true;

        // Match & Server Rules ConVars
        public static float MpRoundTime = 2.5f;
        public static int MpFreezeTime = 5;
        public static int MpBuyTime = 45;
        public static int MpStartMoney = 800;
        public static bool MpFriendlyFire = false;
        public static int MpTimeLimit = 30;
        public static int MpWinLimit = 16;

        // Bot ConVars
        public static int BotDifficulty = 1; // 0=Easy, 1=Normal, 2=Hard, 3=Expert
        public static int BotQuota = 5;

        public static float BotSkillFromDifficulty(int diff) => diff switch
        {
            0 => 0.25f,
            1 => 0.55f,
            2 => 0.78f,
            3 => 0.95f,
            _ => 0.55f,
        };

        /// <summary>Applies current CS 1.6 ConVars onto a runtime GameConfig clone.</summary>
        public static void ApplyPhysicsTo(GameConfig config)
        {
            if (config == null || !UseCs16Physics) return;

            float baseRun = Mathf.Min(BaseRunUnitsPerSec, SvMaxSpeed) * HammerToMeters; // 6.35 m/s
            float maxSprint = SvMaxSpeed * HammerToMeters;                             // 8.128 m/s

            if (config.Movement != null)
            {
                config.Movement.WalkSpeed = baseRun;
                config.Movement.SprintSpeed = maxSprint;
                config.Movement.CrouchSpeedMultiplier = 0.333f;
                config.Movement.GroundAcceleration = Mathf.Max(1f, SvAccelerate * baseRun);
                config.Movement.GroundDeceleration = Mathf.Max(1f, SvFriction * baseRun * 1.4f);
                config.Movement.StepHeight = Mathf.Max(0f, SvStepSize * HammerToMeters);
            }

            if (config.Air != null)
            {
                float g = Mathf.Max(0.5f, SvGravity * HammerToMeters);
                float h = JumpHeightUnits * HammerToMeters;
                float tApex = Mathf.Sqrt(2f * h / g);
                config.Air.JumpHeight = h;
                config.Air.TimeToApex = Mathf.Max(0.05f, tApex);
                config.Air.FallGravityMultiplier = 1.0f; // Symmetric GoldSrc jump arc
                config.Air.AirAcceleration = Mathf.Max(0f, SvAirAccelerate * 1.5f);
            }
        }

        public static void ResetDefaults()
        {
            SvGravity = 800f;
            SvMaxSpeed = 320f;
            SvAccelerate = 5f;
            SvAirAccelerate = 10f;
            SvFriction = 4f;
            SvStopSpeed = 75f;
            SvStepSize = 18f;
            SvCheats = false;
            UseCs16Physics = true;
            MpRoundTime = 2.5f;
            MpFreezeTime = 5;
            MpBuyTime = 45;
            MpStartMoney = 800;
            MpFriendlyFire = false;
            MpTimeLimit = 30;
            MpWinLimit = 16;
            BotDifficulty = 1;
            BotQuota = 5;
        }
    }
}
