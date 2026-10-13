using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>Under the end card the player's worm is steered by BotBrain like any other bot.</summary>
    public class AutopilotTests
    {
        [Test]
        public void ThePointerIsIgnoredWhileTheAutopilotIsOn()
        {
            WormSim sim = new WormSim(TestWorld.Empty(), 1);
            sim.StartPlay();
            sim.SetPlayerHeading(1f);
            Assert.That(sim.Player.WantAngle, Is.EqualTo(1f));

            sim.Autopilot = true;
            sim.SetPlayerHeading(-2f);

            Assert.That(sim.Player.WantAngle, Is.EqualTo(1f));
        }

        [Test]
        public void TheAutopilotHeadsForPellets()
        {
            WormSim sim = new WormSim(TestWorld.Empty(), 1);
            sim.StartPlay();
            sim.Player.Place(0f, 0f, 0f);
            sim.SpawnFood(0f, 400f, 1, 0);
            sim.Autopilot = true;

            TestWorld.Run(sim, 0.5f);

            Assert.That(sim.Player.WantAngle, Is.EqualTo(MathF.PI / 2f).Within(0.2f));
            Assert.That(sim.Player.Head.Y, Is.GreaterThan(20f));
        }

        [Test]
        public void TheAutopilotTurnsBackFromTheEdgeInsteadOfDrivingIntoIt()
        {
            WormSim sim = new WormSim(TestWorld.Empty(), 1);
            sim.StartPlay();
            sim.Player.Place(sim.Config.ArenaRadius - sim.Config.BotEdgeMargin + 10f, 0f, 0f);
            sim.Autopilot = true;

            TestWorld.Run(sim, 6f);

            Assert.That(sim.Player.Alive, Is.True);
        }

        [Test]
        public void WithoutTheAutopilotTheSameWormHitsTheEdge()
        {
            WormSim sim = new WormSim(TestWorld.Empty(), 1);
            sim.StartPlay();
            sim.Player.Place(sim.Config.ArenaRadius - sim.Config.BotEdgeMargin + 10f, 0f, 0f);

            TestWorld.Run(sim, 6f);

            Assert.That(sim.Player.Alive, Is.False);
        }

        [Test]
        public void ADeadPlayerOnAutopilotDoesNotBreakTheStep()
        {
            WormSim sim = new WormSim(new SimConfig(), 1);
            sim.StartPlay();
            sim.Player.Place(sim.Config.ArenaRadius - 1f, 0f, 0f);
            sim.Step(TestWorld.Dt);
            Assert.That(sim.Player.Alive, Is.False);

            sim.Autopilot = true;
            Assert.DoesNotThrow(() => TestWorld.Run(sim, 2f));
        }
    }
}
