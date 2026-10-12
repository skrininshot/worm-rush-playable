using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class BotAITests
    {
        [Test]
        public void BotTargetsTheNearestOfTheSampledPellets()
        {
            // BotFoodSamples random draws from two pellets: with 12 draws both are seen, so the near one wins.
            WormSim sim = new WormSim(TestWorld.OneBot(), 3);
            sim.Player.Speed = 0f;
            Worm bot = sim.Worms[1];
            bot.Place(0f, 400f, 0f);
            sim.SpawnFood(300f, 400f, 1, 0);    // 300 away
            sim.SpawnFood(-700f, 400f, 1, 0);   // 700 away

            sim.Step(TestWorld.Dt);

            Assert.That(bot.HasTarget, Is.True);
            Assert.That(bot.Target.X, Is.EqualTo(300f).Within(1f));
            Assert.That(bot.Target.Y, Is.EqualTo(400f).Within(1f));
        }

        [Test]
        public void BotTurnsTowardItsTarget()
        {
            WormSim sim = new WormSim(TestWorld.OneBot(), 3);
            sim.Player.Speed = 0f;
            Worm bot = sim.Worms[1];
            bot.Place(0f, 400f, 0f);
            sim.SpawnFood(0f, 700f, 1, 0);   // straight "up" from a bot that faces +X

            TestWorld.Run(sim, 0.2f);

            Assert.That(bot.WantAngle, Is.EqualTo(MathF.PI / 2f).Within(0.1f));
            Assert.That(bot.Angle, Is.GreaterThan(0.2f));
        }

        [Test]
        public void RetargetIntervalIsDrawnWithinTheConfiguredRange()
        {
            SimConfig config = TestWorld.OneBot();
            WormSim sim = new WormSim(config, 5);
            sim.Player.Speed = 0f;
            sim.SpawnFood(100f, 0f, 1, 0);
            Worm bot = sim.Worms[1];
            bot.Speed = 0f;

            float min = float.MaxValue;
            float max = 0f;
            for (int i = 0; i < 600; i++)
            {
                float before = bot.ThinkTimer;
                sim.Step(TestWorld.Dt);
                if (bot.ThinkTimer > before)
                {
                    min = Math.Min(min, bot.ThinkTimer);
                    max = Math.Max(max, bot.ThinkTimer);
                }
            }

            Assert.That(max, Is.GreaterThan(0f), "bot never retargeted");
            Assert.That(min, Is.GreaterThanOrEqualTo(config.BotRetargetMin - TestWorld.Dt));
            Assert.That(max, Is.LessThanOrEqualTo(config.BotRetargetMax));
        }

        [Test]
        public void BotNearTheEdgeHeadsBackToTheCentreAndSurvives()
        {
            SimConfig config = TestWorld.OneBot();
            WormSim sim = new WormSim(config, 1);
            sim.Player.Speed = 0f;
            sim.Player.Place(-900f, 0f, 0f);      // out of the way: the centre is where the bot is heading
            Worm bot = sim.Worms[1];
            bot.Place(0f, 500f, MathF.PI / 2f);   // outward, no food on the map

            float farthest = 0f;
            for (int i = 0; i < 20 * 60; i++)
            {
                sim.Step(TestWorld.Dt);
                Vec2 h = bot.Head;
                farthest = Math.Max(farthest, MathF.Sqrt(h.X * h.X + h.Y * h.Y));
            }

            Assert.That(bot.Alive, Is.True);
            Assert.That(farthest, Is.LessThan(config.ArenaRadius - bot.Radius));
        }

        [Test]
        public void WithoutEdgeAvoidanceAStraightBotDiesOnTheEdge()
        {
            SimConfig config = TestWorld.OneBot();
            config.BotEdgeMargin = -config.ArenaRadius;   // threshold beyond the arena: never triggers
            WormSim sim = new WormSim(config, 1);
            sim.Player.Speed = 0f;
            sim.Worms[1].Place(0f, 500f, MathF.PI / 2f);

            TestWorld.Run(sim, 3f);
            Assert.That(sim.Worms[1].Alive, Is.True);

            TestWorld.Run(sim, 3f);   // 500 / 115 is about 4.3 s
            Assert.That(sim.Worms[1].Alive, Is.False);
        }

        [Test]
        public void BotsFindAndEatFood()
        {
            WormSim sim = new WormSim(new SimConfig(), 11);
            sim.Player.Speed = 0f;

            int botMeals = 0;
            for (int i = 0; i < 20 * 60; i++)
            {
                sim.Step(TestWorld.Dt);
                foreach (SimEvent e in sim.Events)
                    if (e.Type == SimEventType.Ate && e.WormId != sim.Player.Id) botMeals++;
            }

            // Five bots seeking the nearest pellet for 20 s: well over a few dozen meals.
            Assert.That(botMeals, Is.GreaterThan(30));
        }

        [Test]
        public void BotsKeepTheArenaPopulatedForTheWholeClip()
        {
            // 25 s idle (player never taps): at least 3 of the 5 bots are still around, thanks to edge avoidance.
            int survivorsTotal = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                WormSim sim = new WormSim(new SimConfig(), seed);
                TestWorld.Run(sim, 25f);
                for (int i = 1; i < sim.Worms.Count; i++)
                    if (sim.Worms[i].Alive) survivorsTotal++;
            }

            Assert.That(survivorsTotal / 10f, Is.GreaterThanOrEqualTo(3f));
        }

        [Test]
        public void SameSeedGivesTheSameBotRun()
        {
            WormSim a = new WormSim(new SimConfig(), 42);
            WormSim b = new WormSim(new SimConfig(), 42);
            TestWorld.Run(a, 10f);
            TestWorld.Run(b, 10f);

            Assert.That(TestWorld.Snapshot(a), Is.EqualTo(TestWorld.Snapshot(b)));
        }
    }
}
