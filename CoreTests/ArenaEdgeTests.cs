using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class ArenaEdgeTests
    {
        [Test]
        public void BotTouchingTheEdgeDiesWithoutAKiller()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.OneBot());
            Worm bot = sim.Worms[1];
            bot.Place(sim.Config.ArenaRadius - 1f, 0f, 0f);

            sim.Step(TestWorld.Dt);

            Assert.That(bot.Alive, Is.False);
            Assert.That(sim.PlayerKills, Is.EqualTo(0));
            SimEvent killed = TestWorld.FirstEvent(sim, SimEventType.Killed);
            Assert.That(killed.WormId, Is.EqualTo(bot.Id));
            Assert.That(killed.OtherId, Is.EqualTo(-1));
            Assert.That(sim.FoodCount, Is.EqualTo(10));
        }

        [Test]
        public void BotWellInsideTheArenaLives()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.OneBot());
            Worm bot = sim.Worms[1];
            bot.Place(sim.Config.ArenaRadius - 100f, 0f, 0f);

            TestWorld.Run(sim, 1f);

            Assert.That(bot.Alive, Is.True);
        }

        [Test]
        public void PlayerIsPushedBackFromTheEdgeAndSurvives()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            sim.StartPlay();
            sim.Player.Place(sim.Config.ArenaRadius + 50f, 0f, 0f);

            sim.Step(TestWorld.Dt);

            Vec2 head = sim.Player.Head;
            float dist = MathF.Sqrt(head.X * head.X + head.Y * head.Y);
            Assert.That(sim.Player.Alive, Is.True);
            Assert.That(dist, Is.EqualTo(sim.Config.ArenaRadius - sim.Player.Radius).Within(0.01));
            Assert.That(TestWorld.CountEvents(sim, SimEventType.PlayerDied), Is.EqualTo(0));
        }

        [Test]
        public void PlayerDrivingIntoTheEdgeNeverLeavesTheArena()
        {
            WormSim sim = new WormSim(TestWorld.Empty(), 1);
            sim.StartPlay();
            sim.SetPlayerHeading(0f);

            TestWorld.Run(sim, 12f);   // 150 u/s, 1000 units to the edge

            Vec2 head = sim.Player.Head;
            float dist = MathF.Sqrt(head.X * head.X + head.Y * head.Y);
            Assert.That(sim.Player.Alive, Is.True);
            Assert.That(dist, Is.LessThanOrEqualTo(sim.Config.ArenaRadius));
        }
    }
}
