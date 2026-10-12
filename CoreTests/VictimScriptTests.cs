using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class VictimScriptTests
    {
        [Test]
        public void ScriptStaysOffWithoutAFirstTap()
        {
            WormSim sim = new WormSim(TestWorld.OneVictim(), 1);
            sim.Player.Speed = 0f;

            TestWorld.Run(sim, 6f);

            Worm victim = sim.Worms[1];
            Assert.That(victim.VictimScriptActive, Is.False);
            Assert.That(victim.Speed, Is.EqualTo(sim.Config.BotSpeed));
        }

        [Test]
        public void ScriptWaitsForTheDelayAfterTheTap()
        {
            SimConfig config = TestWorld.OneVictim();
            WormSim sim = new WormSim(config, 1);
            sim.Player.Speed = 0f;
            sim.Worms[1].Place(-55f, 100f, MathF.PI / 2f);
            TestWorld.Run(sim, 1f);   // time passes before the tap and must not count
            sim.StartPlay();

            TestWorld.Run(sim, config.VictimDelaySeconds - 0.5f);
            Assert.That(sim.Worms[1].VictimScriptActive, Is.False);

            TestWorld.Run(sim, 1f);
            Assert.That(sim.Worms[1].VictimScriptActive, Is.True);
        }

        [Test]
        public void ScriptAimsAtThePlayersBodyAndSpeedsTheVictimUp()
        {
            SimConfig config = TestWorld.OneVictim();
            WormSim sim = new WormSim(config, 1);
            sim.Player.Speed = 0f;
            sim.Worms[1].Place(-55f, 100f, MathF.PI / 2f);
            sim.StartPlay();

            TestWorld.Run(sim, config.VictimDelaySeconds + 0.5f);

            Worm victim = sim.Worms[1];
            Assert.That(victim.HasTarget, Is.True);
            Assert.That(victim.Speed, Is.GreaterThanOrEqualTo(config.VictimSpeed));
            Assert.That(victim.Speed, Is.LessThan(config.VictimSpeed + 1f * config.VictimSpeedRamp));

            // Target is one of the player's segments (the body moves at most a few units per step).
            float nearest = float.MaxValue;
            for (int i = 0; i < sim.Player.SegmentCount; i++)
                nearest = Math.Min(nearest, Vec2.SqrDistance(victim.Target, sim.Player.Segments[i]));
            Assert.That(nearest, Is.LessThan(1f));
        }

        [Test]
        public void VictimDiesAgainstThePlayersBodyWithinTheWindow()
        {
            SimConfig config = TestWorld.OneVictim();
            WormSim sim = new WormSim(config, 1);
            Worm victim = sim.Worms[1];
            sim.Player.Speed = 0f;
            sim.Player.Place(0f, 0f, 0f);
            victim.Place(-55f, 100f, MathF.PI / 2f);   // flies off, then comes back at the script's call
            sim.StartPlay();

            float tapTime = sim.Time;
            float deathTime = -1f;
            for (int i = 0; i < 15 * 60 && deathTime < 0f; i++)
            {
                sim.Step(TestWorld.Dt);
                if (!victim.Alive) deathTime = sim.Time - tapTime;
            }

            Assert.That(deathTime, Is.GreaterThan(config.VictimDelaySeconds), "victim died before the script started");
            Assert.That(deathTime, Is.LessThan(15f), "victim never reached the player");
            Assert.That(victim.VictimScriptActive, Is.True);
            Assert.That(sim.Player.Alive, Is.True);
            Assert.That(sim.PlayerKills, Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheVictimIsScripted()
        {
            SimConfig config = new SimConfig { FoodTarget = 0 };
            WormSim sim = new WormSim(config, 5);
            sim.Player.Speed = 0f;
            sim.StartPlay();

            TestWorld.Run(sim, config.VictimDelaySeconds + 0.5f);

            for (int i = 1; i < sim.Worms.Count; i++)
                Assert.That(sim.Worms[i].VictimScriptActive, Is.EqualTo(sim.Worms[i].IsVictim));
        }
    }
}
