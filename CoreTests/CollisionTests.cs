using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>
    /// Geometry used below: the player (length 24, radius 8.2) lies along -X from the origin,
    /// segment i at (-5i, 0). The bot has length 20 (radius 8). Head hit reach is
    /// 0.5 * headRadius + bodyRadius, about 12 units.
    /// </summary>
    public class CollisionTests
    {
        static WormSim PlayerAlongMinusX()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.OneBot());
            sim.Player.Place(0f, 0f, 0f);
            return sim;
        }

        [Test]
        public void HeadTouchingABodyKillsItAndCreditsTheBodyOwner()
        {
            WormSim sim = PlayerAlongMinusX();
            sim.StartPlay();
            Worm bot = sim.Worms[1];
            bot.Place(-50f, 2f, 0f);   // head sits on player segment 10

            sim.Step(TestWorld.Dt);

            Assert.That(bot.Alive, Is.False);
            Assert.That(sim.Player.Alive, Is.True);
            Assert.That(sim.PlayerKills, Is.EqualTo(1));

            SimEvent killed = TestWorld.FirstEvent(sim, SimEventType.Killed);
            Assert.That(killed.WormId, Is.EqualTo(bot.Id));
            Assert.That(killed.OtherId, Is.EqualTo(sim.Player.Id));
        }

        [Test]
        public void DeadWormLeavesFoodOfItsOwnColour()
        {
            WormSim sim = PlayerAlongMinusX();
            sim.StartPlay();
            sim.Worms[1].Place(-50f, 2f, 0f);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.FoodCount, Is.EqualTo(10));   // every 2nd of 20 segments
            for (int i = 0; i < sim.FoodCount; i++)
            {
                Assert.That(sim.FoodItems[i].Value, Is.EqualTo(sim.Config.FoodValueRemains));
                Assert.That(sim.FoodItems[i].ColorId, Is.EqualTo(1));
            }
        }

        [Test]
        public void TheFirstTwoSegmentsOfABodyAreSafeToTouch()
        {
            WormSim sim = PlayerAlongMinusX();
            sim.StartPlay();
            Worm bot = sim.Worms[1];
            // Head within reach of the player's head and segment 1, out of reach of segment 2 and beyond.
            // Heading -Y, so the bot's own body trails away from the player.
            bot.Place(5f, 6f, -MathF.PI / 2f);

            sim.Step(TestWorld.Dt);

            Assert.That(bot.Alive, Is.True);
            Assert.That(sim.Player.Alive, Is.True);
            Assert.That(TestWorld.CountEvents(sim, SimEventType.Killed), Is.EqualTo(0));
        }

        [Test]
        public void PlayerCannotDieBeforeTheFirstTap()
        {
            WormSim sim = PlayerAlongMinusX();
            sim.Worms[1].Place(30f, 0f, 0f);   // its body runs through the player's head

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Alive, Is.True);
            Assert.That(TestWorld.CountEvents(sim, SimEventType.PlayerDied), Is.EqualTo(0));
        }

        [Test]
        public void PlayerDiesAfterTheFirstTapAndEventsReportIt()
        {
            WormSim sim = PlayerAlongMinusX();
            sim.Worms[1].Place(30f, 0f, 0f);
            sim.Step(TestWorld.Dt);
            Assert.That(sim.Player.Alive, Is.True);

            sim.StartPlay();
            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Alive, Is.False);
            Assert.That(sim.PlayerKills, Is.EqualTo(0));
            SimEvent died = TestWorld.FirstEvent(sim, SimEventType.PlayerDied);
            Assert.That(died.WormId, Is.EqualTo(0));
            Assert.That(died.OtherId, Is.EqualTo(1));
        }

        [Test]
        public void ADeadWormsBodyIsNoLongerDangerous()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.OneBot());
            Worm bot = sim.Worms[1];
            bot.Place(sim.Config.ArenaRadius - 1f, 0f, 0f);   // dies on the edge
            sim.Player.Place(949f, 0f, 0f);                   // head on the bot's old body

            sim.Step(TestWorld.Dt);
            Assert.That(bot.Alive, Is.False);

            sim.StartPlay();
            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Alive, Is.True);
        }
    }
}
