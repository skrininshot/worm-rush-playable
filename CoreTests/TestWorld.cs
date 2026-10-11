using System;
using System.Collections.Generic;

namespace WormRush.Core.Tests
{
    /// <summary>Small helpers so each test builds exactly the world it needs.</summary>
    static class TestWorld
    {
        public const float Dt = 1f / 60f;

        /// <summary>No random food, no bots: only the player.</summary>
        public static SimConfig Empty()
        {
            return new SimConfig { FoodTarget = 0, BotStartLengths = new int[0] };
        }

        /// <summary>No random food, one bot (length 20, colour 1) which is also the scripted victim.</summary>
        public static SimConfig OneBot()
        {
            return new SimConfig
            {
                FoodTarget = 0,
                BotStartLengths = new[] { 20 },
                BotColorIds = new[] { 1 },
            };
        }

        public static WormSim Frozen(SimConfig config, int seed = 1)
        {
            WormSim sim = new WormSim(config, seed);
            foreach (Worm worm in sim.Worms) worm.Speed = 0f;
            return sim;
        }

        public static void Run(WormSim sim, float seconds)
        {
            int steps = (int)MathF.Round(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Dt);
        }

        public static int CountEvents(WormSim sim, SimEventType type)
        {
            int count = 0;
            foreach (SimEvent e in sim.Events)
            {
                if (e.Type == type) count++;
            }
            return count;
        }

        public static SimEvent FirstEvent(WormSim sim, SimEventType type)
        {
            foreach (SimEvent e in sim.Events)
            {
                if (e.Type == type) return e;
            }
            throw new InvalidOperationException("No " + type + " event in the last step");
        }

        /// <summary>Flat dump of everything observable, for equality checks between runs.</summary>
        public static List<float> Snapshot(WormSim sim)
        {
            List<float> data = new List<float> { sim.Time, sim.FoodCount, sim.PlayerKills };
            foreach (Worm worm in sim.Worms)
            {
                data.Add(worm.Alive ? 1f : 0f);
                data.Add(worm.Length);
                data.Add(worm.Angle);
                data.Add(worm.SegmentCount);
                for (int i = 0; i < worm.SegmentCount; i++)
                {
                    data.Add(worm.Segments[i].X);
                    data.Add(worm.Segments[i].Y);
                }
            }
            for (int i = 0; i < sim.FoodCount; i++)
            {
                data.Add(sim.FoodItems[i].X);
                data.Add(sim.FoodItems[i].Y);
                data.Add(sim.FoodItems[i].Value);
                data.Add(sim.FoodItems[i].ColorId);
            }
            return data;
        }
    }
}
