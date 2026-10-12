using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>
    /// Stage 3 acceptance: in the full default game (5 bots, 380 pellets, scripted victim) the victim dies
    /// on the player's body 8-15 s into the clip in at least 9 of 10 runs. "Runs" are fixed seeds with a
    /// stand-in for the human; the window is measured from the clip start with the first tap at the end
    /// of the 3 s tutorial beat, so it is the same as 5-12 s after the tap for an earlier tap.
    /// </summary>
    public class ClimaxTests
    {
        const int Runs = 50;

        static IEnumerable<TestCaseData> CarefulPlayers()
        {
            yield return new TestCaseData("straight", (Func<ClimaxHarness.Pilot>)(() => ClimaxHarness.Careful(ClimaxHarness.Straight())));
            yield return new TestCaseData("circling", (Func<ClimaxHarness.Pilot>)(() => ClimaxHarness.Careful(ClimaxHarness.Circling(1.5f))));
            yield return new TestCaseData("nearest food", (Func<ClimaxHarness.Pilot>)(() => ClimaxHarness.Careful(ClimaxHarness.NearestFood())));
            yield return new TestCaseData("flees the victim", (Func<ClimaxHarness.Pilot>)(() => ClimaxHarness.Careful(ClimaxHarness.FleeVictim())));
        }

        [TestCaseSource(nameof(CarefulPlayers))]
        public void VictimDiesOnThePlayerInTheWindowInNineOfTenRuns(string name, Func<ClimaxHarness.Pilot> pilot)
        {
            List<ClimaxHarness.Outcome> outcomes = ClimaxHarness.RunMany(Runs, pilot);

            int inWindow = 0;
            foreach (ClimaxHarness.Outcome o in outcomes)
            {
                if (ClimaxHarness.InWindow(o, 8f, 15f)) inWindow++;
            }

            Assert.That(inWindow, Is.GreaterThanOrEqualTo(Runs * 9 / 10),
                name + ": " + inWindow + "/" + Runs + " in window. " + ClimaxHarness.Describe(outcomes));
        }

        [TestCaseSource(nameof(CarefulPlayers))]
        public void AnEarlyTapShiftsTheWholeWindowWithIt(string name, Func<ClimaxHarness.Pilot> pilot)
        {
            const float tap = 1f;
            List<ClimaxHarness.Outcome> outcomes = ClimaxHarness.RunMany(Runs, pilot, tap);

            int inWindow = 0;
            foreach (ClimaxHarness.Outcome o in outcomes)
            {
                if (ClimaxHarness.InWindow(o, tap + 5f, tap + 12f)) inWindow++;
            }

            Assert.That(inWindow, Is.GreaterThanOrEqualTo(Runs * 9 / 10),
                name + ": " + inWindow + "/" + Runs + " in window. " + ClimaxHarness.Describe(outcomes));
        }

        [Test]
        public void ABlindPlayerEitherDiesOrTheVictimIsKilledByThemAndNobodyElse()
        {
            // No avoidance at all: the player may well drive into a bot. But whenever the victim dies it is
            // the player's doing, and it never simply survives the 30 s.
            (string, Func<ClimaxHarness.Pilot>)[] blind =
            {
                ("straight", () => ClimaxHarness.Straight()),
                ("circling", () => ClimaxHarness.Circling(1.5f)),
                ("nearest food", () => ClimaxHarness.NearestFood()),
                ("flees the victim", () => ClimaxHarness.FleeVictim()),
            };

            foreach ((string name, Func<ClimaxHarness.Pilot> pilot) in blind)
            {
                foreach (ClimaxHarness.Outcome o in ClimaxHarness.RunMany(Runs, pilot))
                {
                    if (o.VictimDied)
                        Assert.That(o.KillerId, Is.EqualTo(0), name + " seed " + o.Seed + ": victim killed by " + o.KillerId);
                    else
                        Assert.That(o.PlayerDied, Is.True, name + " seed " + o.Seed + ": victim survived 30 s with the player alive");
                }
            }
        }

        [Test]
        public void TheClimaxNeverComesBeforeTheScriptStarts()
        {
            foreach (ClimaxHarness.Outcome o in ClimaxHarness.RunMany(Runs, () => ClimaxHarness.Careful(ClimaxHarness.NearestFood())))
            {
                if (o.VictimDied)
                    Assert.That(o.DeathTime, Is.GreaterThan(ClimaxHarness.TapSeconds + new SimConfig().VictimDelaySeconds), "seed " + o.Seed);
            }
        }
    }
}
