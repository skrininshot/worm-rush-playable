using System;
using System.Collections.Generic;

namespace WormRush.Core.Tests
{
    /// <summary>
    /// Runs the full default game (5 bots, 380 pellets, scripted victim) with a stand-in for the human
    /// and reports when, and by whom, the victim died.
    /// </summary>
    public static class ClimaxHarness
    {
        public const float TapSeconds = 3f;      // end of the tutorial beat in the spec
        public const float GiveUpSeconds = 30f;

        public delegate float Pilot(WormSim sim);

        public struct Outcome
        {
            public int Seed;
            public bool VictimDied;
            public float DeathTime;      // sim time from the start of the run
            public int KillerId;         // -1 = arena edge
            public bool PlayerDied;
            public int PlayerKillerId;   // who killed the player (-1 = edge)
        }

        /// <summary>Keeps the heading it had at the tap.</summary>
        public static Pilot Straight()
        {
            bool set = false;
            float heading = 0f;
            return sim =>
            {
                if (!set) { heading = sim.Player.Angle; set = true; }
                return heading;
            };
        }

        /// <summary>Turns at a constant rate: a worm circling in place.</summary>
        public static Pilot Circling(float radiansPerSecond)
        {
            float heading = 0f;
            return sim =>
            {
                heading += radiansPerSecond * TestWorld.Dt;
                return heading;
            };
        }

        /// <summary>What a player does by default: heads for the nearest pellet.</summary>
        public static Pilot NearestFood()
        {
            return sim =>
            {
                Vec2 head = sim.Player.Head;
                float best = float.MaxValue;
                float angle = sim.Player.Angle;
                for (int i = 0; i < sim.FoodCount; i++)
                {
                    float dx = sim.FoodItems[i].X - head.X;
                    float dy = sim.FoodItems[i].Y - head.Y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < best)
                    {
                        best = d2;
                        angle = MathF.Atan2(dy, dx);
                    }
                }
                return angle;
            };
        }

        /// <summary>Worst case: always flees straight away from the victim.</summary>
        public static Pilot FleeVictim()
        {
            return sim =>
            {
                Vec2 head = sim.Player.Head;
                Vec2 v = sim.Worms[1].Head;
                return MathF.Atan2(head.Y - v.Y, head.X - v.X);
            };
        }

        /// <summary>
        /// Wraps a pilot with what any human does: look a short way ahead and turn away from another
        /// worm's body instead of driving into it.
        /// </summary>
        public static Pilot Careful(Pilot inner)
        {
            const float Lookahead = 90f;
            const float HalfCone = 1.2f;    // radians either side of the heading
            const float Evade = 1.3f;       // how far off the heading to swerve

            return sim =>
            {
                float wanted = inner(sim);
                Worm player = sim.Player;
                Vec2 head = player.Head;
                float best = Lookahead * Lookahead;
                float swerve = 0f;

                for (int w = 1; w < sim.Worms.Count; w++)
                {
                    Worm other = sim.Worms[w];
                    if (!other.Alive) continue;
                    for (int s = 0; s < other.SegmentCount; s++)
                    {
                        float dx = other.Segments[s].X - head.X;
                        float dy = other.Segments[s].Y - head.Y;
                        float d2 = dx * dx + dy * dy;
                        if (d2 >= best) continue;

                        float rel = MathF.Atan2(dy, dx) - player.Angle;
                        rel = MathF.Atan2(MathF.Sin(rel), MathF.Cos(rel));
                        if (MathF.Abs(rel) > HalfCone) continue;

                        best = d2;
                        swerve = rel > 0f ? -Evade : Evade;
                    }
                }

                return swerve != 0f ? player.Angle + swerve : wanted;
            };
        }

        /// <summary>What any human does since the edge kills: turn back toward the centre well before touching it.</summary>
        public static Pilot EdgeAware(Pilot inner, float margin = 220f)
        {
            return sim =>
            {
                Vec2 head = sim.Player.Head;
                float limit = sim.Config.ArenaRadius - margin;
                if (head.X * head.X + head.Y * head.Y > limit * limit)
                    return MathF.Atan2(-head.Y, -head.X);
                return inner(sim);
            };
        }

        /// <summary>A stand-in for a person: avoids the edge and other worms' bodies.</summary>
        public static Pilot Human(Pilot inner)
        {
            return Careful(EdgeAware(inner));
        }

        public static Outcome Run(int seed, Pilot pilot, float tapSeconds = TapSeconds, SimConfig config = null)
        {
            WormSim sim = new WormSim(config ?? new SimConfig(), seed);
            Worm victim = sim.Worms[1];
            Outcome outcome = new Outcome { Seed = seed, KillerId = -2 };

            int steps = (int)(GiveUpSeconds / TestWorld.Dt);
            for (int i = 0; i < steps; i++)
            {
                if (!sim.Started && sim.Time >= tapSeconds) sim.StartPlay();
                if (sim.Started) sim.SetPlayerHeading(pilot(sim));
                sim.Step(TestWorld.Dt);

                foreach (SimEvent e in sim.Events)
                {
                    if (e.Type == SimEventType.Killed && e.WormId == victim.Id)
                    {
                        outcome.VictimDied = true;
                        outcome.DeathTime = sim.Time;
                        outcome.KillerId = e.OtherId;
                    }
                    if (e.Type == SimEventType.PlayerDied)
                    {
                        outcome.PlayerDied = true;
                        outcome.PlayerKillerId = e.OtherId;
                    }
                }
                if (outcome.VictimDied || outcome.PlayerDied) break;
            }
            return outcome;
        }

        public static List<Outcome> RunMany(int seedCount, Func<Pilot> pilotFactory, float tapSeconds = TapSeconds, SimConfig config = null)
        {
            List<Outcome> outcomes = new List<Outcome>();
            for (int seed = 1; seed <= seedCount; seed++)
                outcomes.Add(Run(seed, pilotFactory(), tapSeconds, config));
            return outcomes;
        }

        /// <summary>Victim killed by the player, between minSeconds and maxSeconds after the start.</summary>
        public static bool InWindow(Outcome o, float minSeconds, float maxSeconds)
        {
            return o.VictimDied && o.KillerId == 0 && o.DeathTime >= minSeconds && o.DeathTime <= maxSeconds;
        }

        public static string Describe(List<Outcome> outcomes)
        {
            List<string> parts = new List<string>();
            foreach (Outcome o in outcomes)
            {
                string who = o.VictimDied ? (o.KillerId == 0 ? "player" : o.KillerId < 0 ? "EDGE" : "bot" + o.KillerId) : (o.PlayerDied ? "PLAYER-DIED(by" + o.PlayerKillerId + ")" : "never");
                parts.Add(o.Seed + ":" + (o.VictimDied ? o.DeathTime.ToString("0.0") : "-") + "s/" + who);
            }
            return string.Join("  ", parts);
        }
    }
}
