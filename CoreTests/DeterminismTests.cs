using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class DeterminismTests
    {
        static WormSim PlayScripted(int seed)
        {
            WormSim sim = new WormSim(new SimConfig(), seed);
            for (int i = 0; i < 20 * 60; i++)
            {
                if (i == 30) sim.StartPlay();
                if (i % 90 == 0) sim.SetPlayerHeading(i * 0.01f);
                sim.Step(TestWorld.Dt);
            }
            return sim;
        }

        [Test]
        public void SameSeedAndInputsGiveTheSameRun()
        {
            WormSim a = PlayScripted(42);
            WormSim b = PlayScripted(42);

            CollectionAssert.AreEqual(TestWorld.Snapshot(a), TestWorld.Snapshot(b));
        }

        [Test]
        public void DifferentSeedsGiveDifferentWorlds()
        {
            WormSim a = new WormSim(new SimConfig(), 1);
            WormSim b = new WormSim(new SimConfig(), 2);

            Assert.That(a.FoodItems[0].X, Is.Not.EqualTo(b.FoodItems[0].X));
        }

        [Test]
        public void ALongFrameIsClampedToTheMaximumStep()
        {
            SimConfig config = TestWorld.Empty();
            WormSim sim = new WormSim(config, 1);

            sim.Step(5f);

            Assert.That(sim.Time, Is.EqualTo(config.MaxStepSeconds).Within(1e-6));
        }

        [Test]
        public void ZeroOrNegativeStepChangesNothing()
        {
            WormSim sim = new WormSim(new SimConfig(), 1);
            var before = TestWorld.Snapshot(sim);

            sim.Step(0f);
            sim.Step(-1f);

            CollectionAssert.AreEqual(before, TestWorld.Snapshot(sim));
        }

        [Test]
        public void RandomGeneratorRepeatsPerSeedAndStaysInRange()
        {
            SimRandom a = new SimRandom(9);
            SimRandom b = new SimRandom(9);
            for (int i = 0; i < 1000; i++)
            {
                float v = a.NextFloat();
                Assert.That(v, Is.EqualTo(b.NextFloat()));
                Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }
    }
}
