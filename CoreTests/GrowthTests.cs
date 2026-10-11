using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class GrowthTests
    {
        [Test]
        public void EatingRemainsAddsLengthAndSegments()
        {
            SimConfig config = TestWorld.Empty();
            WormSim sim = new WormSim(config, 1);
            sim.SpawnFood(0f, 0f, config.FoodValueRemains, 0);

            sim.Step(TestWorld.Dt);

            Worm player = sim.Player;
            Assert.That(player.Length, Is.EqualTo(24f + 3 * 0.6f).Within(1e-4));
            Assert.That(player.SegmentCount, Is.EqualTo(25));
            Assert.That(player.Radius, Is.EqualTo(config.WormRadius(player.Length)).Within(1e-4));
            Assert.That(sim.FoodCount, Is.EqualTo(0));

            SimEvent ate = TestWorld.FirstEvent(sim, SimEventType.Ate);
            Assert.That(ate.WormId, Is.EqualTo(0));
            Assert.That(ate.Value, Is.EqualTo(config.FoodValueRemains));
        }

        [Test]
        public void NormalFoodGrowsLessThanRemains()
        {
            SimConfig config = TestWorld.Empty();
            WormSim sim = new WormSim(config, 1);
            sim.SpawnFood(0f, 0f, config.FoodValueNormal, 0);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Length, Is.EqualTo(24f + 0.6f).Within(1e-4));
            Assert.That(sim.Player.SegmentCount, Is.EqualTo(24));
        }

        [Test]
        public void SegmentsAppearOnceLengthCrossesAWholeNumber()
        {
            SimConfig config = TestWorld.Empty();
            WormSim sim = new WormSim(config, 1);
            for (int i = 0; i < 4; i++) sim.SpawnFood(0f, 0f, config.FoodValueNormal, 0);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.Player.Length, Is.EqualTo(26.4f).Within(1e-4));
            Assert.That(sim.Player.SegmentCount, Is.EqualTo(26));
        }

        [Test]
        public void RadiusGrowsWithLengthThenCaps()
        {
            SimConfig config = new SimConfig();
            Assert.That(config.WormRadius(24f), Is.EqualTo(8.2f).Within(1e-4));
            Assert.That(config.WormRadius(100f), Is.EqualTo(12f).Within(1e-4));
            Assert.That(config.WormRadius(160f), Is.EqualTo(15f).Within(1e-4));
            Assert.That(config.WormRadius(1000f), Is.EqualTo(15f).Within(1e-4));
        }

        [Test]
        public void StartingWorldHasTheConfiguredFoodAndWorms()
        {
            WormSim sim = new WormSim(new SimConfig(), 7);

            Assert.That(sim.FoodCount, Is.EqualTo(380));
            Assert.That(sim.Worms.Count, Is.EqualTo(6));
            Assert.That(sim.Player.IsPlayer, Is.True);
            Assert.That(sim.Worms[1].IsVictim, Is.True);
            Assert.That(sim.Worms[2].IsVictim, Is.False);
        }

        [Test]
        public void EatenFoodIsReplacedSoTheCountStaysAtTarget()
        {
            SimConfig config = new SimConfig { BotStartLengths = new int[0], FoodTarget = 50 };
            WormSim sim = new WormSim(config, 3);
            sim.SpawnFood(0f, 0f, 1, 0);
            Assert.That(sim.FoodCount, Is.EqualTo(51));

            sim.Step(TestWorld.Dt);

            Assert.That(TestWorld.CountEvents(sim, SimEventType.Ate), Is.GreaterThan(0));
            Assert.That(sim.FoodCount, Is.EqualTo(50));
        }

        [Test]
        public void FoodFarFromAnyHeadIsLeftAlone()
        {
            SimConfig config = TestWorld.OneBot();
            WormSim sim = TestWorld.Frozen(config);
            sim.Worms[1].Place(500f, 0f, 0f);
            sim.SpawnFood(-400f, 300f, 1, 0);

            sim.Step(TestWorld.Dt);

            Assert.That(sim.FoodCount, Is.EqualTo(1));
            Assert.That(TestWorld.CountEvents(sim, SimEventType.Ate), Is.EqualTo(0));
        }

        [Test]
        public void PlayerMagnetPullsNearbyFoodButNotBeyondRange()
        {
            SimConfig config = TestWorld.Empty();
            WormSim sim = TestWorld.Frozen(config);
            sim.Player.Place(0f, 0f, 0f);
            sim.SpawnFood(30f, 0f, 1, 0);     // inside radius + MagnetRange (8.2 + 44)
            sim.SpawnFood(0f, 200f, 1, 0);    // far outside

            sim.Step(TestWorld.Dt);

            float pull = config.MagnetSpeed * TestWorld.Dt;
            Assert.That(sim.FoodItems[0].X, Is.EqualTo(30f - pull).Within(1e-3));
            Assert.That(sim.FoodItems[1].Y, Is.EqualTo(200f).Within(1e-3));
        }

        [Test]
        public void BotsDoNotAttractFood()
        {
            SimConfig config = TestWorld.OneBot();
            WormSim sim = TestWorld.Frozen(config);
            sim.Worms[1].Place(500f, 0f, 0f);
            sim.SpawnFood(520f, 0f, 1, 0);   // 20 away: outside eat reach, inside a hypothetical magnet

            sim.Step(TestWorld.Dt);

            Assert.That(sim.FoodItems[0].X, Is.EqualTo(520f).Within(1e-3));
            Assert.That(sim.FoodCount, Is.EqualTo(1));
        }
    }
}
