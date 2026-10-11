using System;

namespace WormRush.Core
{
    /// <summary>
    /// Every simulation tunable. Plain C# so Core stays free of UnityEngine;
    /// the PlayableConfig ScriptableObject (View/Config side) will fill one of these.
    /// Defaults match reference/worm-rush-playable.html.
    /// </summary>
    public sealed class SimConfig
    {
        // ---- Arena ----
        public float ArenaRadius = 1000f;

        // ---- Worm movement ----
        public float SegmentSpacing = 5f;
        public float TurnRate = 4.2f;          // rad/s
        public float PlayerSpeed = 150f;
        public float BotSpeed = 115f;
        public float VictimSpeed = 140f;       // bot speed once the victim script kicks in
        public float IdleTurnRate = 0.7f;      // player spins on the spot before the first tap
        public float MaxStepSeconds = 0.033f;  // a longer frame is clamped, as in the reference

        // ---- Worm size ----
        public int PlayerStartLength = 24;
        public float PlayerStartAngle = 1.5707964f;   // up (Unity: +Y)
        public float RadiusBase = 7f;
        public float RadiusPerLength = 0.05f;
        public float RadiusLengthCap = 160f;   // radius stops growing past this length
        public float LengthPerFoodValue = 0.6f;

        // ---- Food ----
        public int FoodTarget = 380;
        public float FoodSpawnMargin = 30f;    // keep spawns this far inside the arena edge
        public int FoodValueNormal = 1;
        public int FoodValueRemains = 3;
        public float FoodRadiusBase = 2.5f;
        public float FoodRadiusPerValue = 1.6f;
        public int ColorCount = 6;             // palette size the View maps ColorId onto
        public int RemainsStride = 2;          // a dead worm drops food from every Nth segment
        public float RemainsJitter = 6f;
        public float MagnetRange = 44f;        // added to the player's radius
        public float MagnetSpeed = 240f;

        // ---- Collisions ----
        public int BodyHitSkipSegments = 2;    // a head cannot hit the first N segments of a body
        public float HeadHitRadiusFactor = 0.5f;
        public float HashCellSize = 48f;

        // ---- Bots ----
        public int[] BotStartLengths = { 18, 30, 26, 40, 20 };
        public int[] BotColorIds = { 0, 1, 2, 4, 5 };
        public int PlayerColorId = 3;
        public float BotSpawnAngleOffset = 0.6f;
        public float BotSpawnDistanceMin = 420f;
        public float BotSpawnDistanceMax = 820f;

        // ---- Scripted victim ----
        public int VictimBotIndex = 0;
        public float VictimSpawnDistance = 280f;
        public float VictimDelaySeconds = 4f;           // after the first tap
        public float VictimTargetBodyFraction = 0.45f;  // aims this far along the player's body

        public int BotCount
        {
            get { return BotStartLengths.Length; }
        }

        public float WormRadius(float length)
        {
            return RadiusBase + Math.Min(length, RadiusLengthCap) * RadiusPerLength;
        }

        public float FoodRadius(int value)
        {
            return FoodRadiusBase + value * FoodRadiusPerValue;
        }

        public float MaxWormRadius
        {
            get { return WormRadius(RadiusLengthCap); }
        }

        public float MaxFoodRadius
        {
            get { return FoodRadius(Math.Max(FoodValueNormal, FoodValueRemains)); }
        }
    }
}
