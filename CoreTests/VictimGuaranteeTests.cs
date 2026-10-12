using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>
    /// The climax guarantee: the scripted victim's body never kills the player, and its own head only
    /// dies on the player's body once the script is active. Geometry as in CollisionTests: the player
    /// lies along -X from the origin, segment i at (-5i, 0).
    /// </summary>
    public class VictimGuaranteeTests
    {
        static WormSim Setup(SimConfig config)
        {
            WormSim sim = TestWorld.Frozen(config);
            sim.Player.Place(0f, 0f, 0f);
            return sim;
        }

        static void ActivateScript(WormSim sim)
        {
            sim.StartPlay();
            TestWorld.Run(sim, sim.Config.VictimDelaySeconds + 0.2f);
            Assert.That(sim.Worms[1].VictimScriptActive, Is.True);
        }

        [Test]
        public void VictimIsImmuneToThePlayersBodyBeforeTheScript()
        {
            WormSim sim = Setup(TestWorld.OneVictim());
            sim.StartPlay();
            sim.Worms[1].Place(-50f, 2f, 0f);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Worms[1].Alive, Is.True);
            Assert.That(sim.Worms[1].VictimScriptActive, Is.False);
        }

        [Test]
        public void ScriptedVictimDiesOnThePlayersBody()
        {
            WormSim sim = Setup(TestWorld.OneVictim());
            ActivateScript(sim);
            sim.Worms[1].Place(-50f, 2f, 0f);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Worms[1].Alive, Is.False);
            Assert.That(sim.PlayerKills, Is.EqualTo(1));
            Assert.That(TestWorld.FirstEvent(sim, SimEventType.Killed).OtherId, Is.EqualTo(sim.Player.Id));
        }

        [Test]
        public void VictimsBodyNeverKillsThePlayer()
        {
            WormSim sim = Setup(TestWorld.OneVictim());
            sim.StartPlay();
            sim.Worms[1].Place(-100f, 0f, MathF.PI / 2f);   // body hangs below the head, along x = -100
            sim.Player.Place(-100f, -60f, MathF.PI / 2f);   // player head runs into the middle of it

            sim.Player.Speed = 0f;
            sim.Step(TestWorld.Dt);
            sim.Player.Place(-100f, -20f, MathF.PI / 2f);
            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Alive, Is.True);
        }

        [Test]
        public void WithoutTheGuaranteeTheVictimsBodyKillsThePlayer()
        {
            SimConfig config = TestWorld.OneVictim();
            config.VictimGuarantee = false;
            WormSim sim = Setup(config);
            sim.StartPlay();
            sim.Worms[1].Place(-100f, 100f, MathF.PI / 2f);   // body along x = -100, y from 100 downward
            sim.Player.Place(-100f, 80f, MathF.PI / 2f);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Alive, Is.False);
        }

        [Test]
        public void OtherBotsBodiesDoNotKillTheVictim()
        {
            SimConfig config = TestWorld.OneVictim();
            config.BotStartLengths = new[] { 20, 20 };
            config.BotColorIds = new[] { 1, 2 };
            WormSim sim = Setup(config);
            ActivateScript(sim);
            sim.Worms[2].Place(300f, 300f, 0f);
            sim.Worms[1].Place(250f, 302f, 0f);   // head on the other bot's body

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Worms[1].Alive, Is.True);
        }

        [Test]
        public void VictimIsPushedBackFromTheEdgeInsteadOfDying()
        {
            WormSim sim = Setup(TestWorld.OneVictim());
            sim.Worms[1].Place(sim.Config.ArenaRadius + 30f, 0f, 0f);

            sim.Step(TestWorld.Dt);

            Worm victim = sim.Worms[1];
            Assert.That(victim.Alive, Is.True);
            Assert.That(victim.Head.X, Is.LessThan(sim.Config.ArenaRadius));
        }

        [Test]
        public void ScriptedVictimSpeedsUpTheLongerTheChaseAndStopsAtTheCap()
        {
            SimConfig config = TestWorld.OneVictim();
            WormSim sim = Setup(config);
            sim.StartPlay();
            sim.Worms[1].Place(500f, 500f, 0f);
            TestWorld.Run(sim, config.VictimDelaySeconds + 0.1f);
            float early = sim.Worms[1].Speed;

            // Keep the victim away from the player (the test is about speed, not the kill).
            for (int i = 0; i < 8 * 60; i++)
            {
                sim.Worms[1].Place(500f, 500f, 0f);
                sim.Step(TestWorld.Dt);
            }
            float late = sim.Worms[1].Speed;

            Assert.That(early, Is.GreaterThanOrEqualTo(config.VictimSpeed));
            Assert.That(late, Is.GreaterThan(early));
            Assert.That(late, Is.EqualTo(config.VictimMaxSpeed));
        }

        [Test]
        public void VictimReturnsToBotSpeedWhenThePlayerIsGone()
        {
            SimConfig config = TestWorld.OneVictim();
            config.VictimGuarantee = false;   // so a bot body can actually kill the player in this setup
            config.BotStartLengths = new[] { 20, 20 };
            config.BotColorIds = new[] { 1, 2 };
            WormSim sim = Setup(config);
            ActivateScript(sim);
            Assert.That(sim.Worms[1].Speed, Is.GreaterThan(config.BotSpeed));

            sim.Worms[2].Place(0f, 100f, MathF.PI / 2f);        // body hangs below its head
            sim.Player.Place(0f, 60f, MathF.PI / 2f);            // player head lands on it
            sim.Step(TestWorld.Dt);
            Assert.That(sim.Player.Alive, Is.False);

            sim.Step(TestWorld.Dt);
            Assert.That(sim.Worms[1].VictimScriptActive, Is.False);
            Assert.That(sim.Worms[1].Speed, Is.EqualTo(config.BotSpeed));
        }

        [Test]
        public void VictimLoitersAtTheConfiguredDistanceFromThePlayerBeforeTheScript()
        {
            // Pellet A is next to the victim, B hugs the player, C sits at the loiter distance from the player.
            WormSim sim = new WormSim(TestWorld.OneVictim(), 2);
            sim.Player.Speed = 0f;
            sim.Player.Place(0f, 0f, 0f);
            Worm victim = sim.Worms[1];
            victim.Speed = 0f;
            victim.Place(600f, 0f, 0f);
            sim.SpawnFood(650f, 0f, 1, 0);                                // 50 from the victim
            sim.SpawnFood(-40f, 30f, 1, 0);                               // 50 from the player
            sim.SpawnFood(0f, sim.Config.VictimLoiterDistance, 1, 0);     // right on the ring

            sim.Step(TestWorld.Dt);

            Assert.That(victim.Target.Y, Is.EqualTo(sim.Config.VictimLoiterDistance).Within(1f));
        }

        [Test]
        public void ZeroLoiterDistanceMakesTheVictimAnOrdinaryFoodSeekerUntilTheScript()
        {
            SimConfig config = TestWorld.OneVictim();
            config.VictimLoiterDistance = 0f;
            WormSim sim = new WormSim(config, 2);
            sim.Player.Speed = 0f;
            sim.Player.Place(0f, 0f, 0f);
            Worm victim = sim.Worms[1];
            victim.Speed = 0f;
            victim.Place(600f, 0f, 0f);
            sim.SpawnFood(650f, 0f, 1, 0);
            sim.SpawnFood(0f, 350f, 1, 0);

            sim.Step(TestWorld.Dt);

            Assert.That(victim.Target.X, Is.EqualTo(650f).Within(1f));
        }
    }
}
