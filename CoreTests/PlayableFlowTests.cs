using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>The beat sheet: Intro, Tutorial, Play, Climax, EndCard and the three ways the run ends.</summary>
    public class PlayableFlowTests
    {
        const float Dt = TestWorld.Dt;

        static void Tick(WormSim sim, PlayableFlow flow, float seconds)
        {
            int steps = (int)MathF.Round(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Dt);
                flow.Update();
            }
        }

        /// <summary>Like Tick, but the victim is put back far from the player every step, so it cannot interfere.</summary>
        static void TickParked(WormSim sim, PlayableFlow flow, float seconds)
        {
            int steps = (int)MathF.Round(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Worms[1].Place(850f, 0f, 0f);
                sim.Step(Dt);
                flow.Update();
            }
        }

        /// <summary>Puts the player's head onto the ordinary bot's body.</summary>
        static void PlayerRunsIntoTheBot(WormSim sim)
        {
            Vec2 seg = sim.Worms[2].Segments[10];
            sim.Player.Place(seg.X, seg.Y + 2f, 0f);
        }

        /// <summary>Frozen world with the scripted victim far away and one ordinary bot far away on the other side.</summary>
        static WormSim VictimWorld(out PlayableFlow flow)
        {
            SimConfig config = TestWorld.OneVictim();
            config.BotStartLengths = new[] { 20, 20 };
            config.BotColorIds = new[] { 1, 2 };
            WormSim sim = TestWorld.Frozen(config);
            sim.Player.Place(0f, 0f, 0f);
            sim.Worms[1].Place(850f, 0f, 0f);     // the victim: too far to reach the player within the tests
            sim.Worms[2].Place(-300f, 300f, 0f);  // an ordinary bot, body along -X from (-300, 300)
            flow = new PlayableFlow(sim);
            return sim;
        }

        [Test]
        public void StartsInIntroThenWaitsOnTheTutorial()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            PlayableFlow flow = new PlayableFlow(sim);
            Assert.That(flow.State, Is.EqualTo(FlowState.Intro));
            Assert.That(flow.CtaVisible, Is.True);

            Tick(sim, flow, sim.Config.IntroSeconds / 2f);
            Assert.That(flow.State, Is.EqualTo(FlowState.Intro));
            Assert.That(flow.TutorialAlpha, Is.InRange(0.3f, 0.7f));

            Tick(sim, flow, sim.Config.IntroSeconds);
            Assert.That(flow.State, Is.EqualTo(FlowState.Tutorial));
            Assert.That(flow.TutorialAlpha, Is.EqualTo(1f));
            Assert.That(flow.TutorialVisible, Is.True);
        }

        [Test]
        public void AnIdleAdNeverEnds()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            PlayableFlow flow = new PlayableFlow(sim);

            Tick(sim, flow, 90f);

            Assert.That(flow.State, Is.EqualTo(FlowState.Tutorial));
            Assert.That(flow.CtaVisible, Is.True);
        }

        [Test]
        public void TapMovesToPlayAndHidesTheTutorial()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            PlayableFlow flow = new PlayableFlow(sim);
            Tick(sim, flow, 1f);

            sim.StartPlay();
            Tick(sim, flow, Dt);

            Assert.That(flow.State, Is.EqualTo(FlowState.Play));
            Assert.That(flow.TutorialVisible, Is.False);
            Assert.That(flow.TutorialAlpha, Is.EqualTo(0f));
        }

        [Test]
        public void TapDuringIntroGoesStraightToPlay()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            PlayableFlow flow = new PlayableFlow(sim);
            Tick(sim, flow, 0.1f);

            sim.StartPlay();
            Tick(sim, flow, Dt);

            Assert.That(flow.State, Is.EqualTo(FlowState.Play));
        }

        [Test]
        public void BlockHintShowsOnlyInsideItsWindow()
        {
            WormSim sim = VictimWorld(out PlayableFlow flow);
            sim.StartPlay();

            float first = -1f;
            float last = -1f;
            for (int i = 0; i < 9 * 60; i++)
            {
                sim.Worms[1].Place(850f, 0f, 0f);
                sim.Step(Dt);
                flow.Update();
                if (!flow.BlockHintVisible) continue;
                if (first < 0f) first = flow.PlayTime;
                last = flow.PlayTime;
            }

            Assert.That(first, Is.EqualTo(sim.Config.BlockHintStartSeconds).Within(2 * Dt));
            Assert.That(last, Is.EqualTo(sim.Config.BlockHintEndSeconds).Within(2 * Dt));
        }

        [Test]
        public void ClimaxStartsWhenTheVictimScriptActivates()
        {
            WormSim sim = VictimWorld(out PlayableFlow flow);
            sim.StartPlay();

            TickParked(sim, flow, sim.Config.VictimDelaySeconds - 0.5f);
            Assert.That(flow.State, Is.EqualTo(FlowState.Play));

            TickParked(sim, flow, 1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.Climax));
        }

        [Test]
        public void KillEndsTheRunAfterTheDelayWithAwesome()
        {
            WormSim sim = VictimWorld(out PlayableFlow flow);
            sim.StartPlay();
            TickParked(sim, flow, sim.Config.VictimDelaySeconds + 0.2f);
            sim.Worms[1].Place(-50f, 2f, 0f);   // victim's head on the player's body

            Tick(sim, flow, Dt);
            Assert.That(sim.PlayerKills, Is.EqualTo(1));
            Assert.That(flow.State, Is.EqualTo(FlowState.Climax));
            Assert.That(flow.BlockHintVisible, Is.False);

            Tick(sim, flow, sim.Config.EndAfterKillSeconds - 0.1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.Climax));

            Tick(sim, flow, 0.2f);
            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));
            Assert.That(flow.EndKind, Is.EqualTo(EndKind.Awesome));
            Assert.That(flow.EndReason, Is.EqualTo(EndReason.Kill));
            Assert.That(flow.CtaVisible, Is.False);
        }

        [Test]
        public void PlayerDeathEndsTheRunWithSoClose()
        {
            WormSim sim = VictimWorld(out PlayableFlow flow);
            sim.StartPlay();
            TickParked(sim, flow, 1f);
            PlayerRunsIntoTheBot(sim);

            Tick(sim, flow, Dt);
            Assert.That(sim.Player.Alive, Is.False);
            Assert.That(flow.State, Is.Not.EqualTo(FlowState.EndCard));

            Tick(sim, flow, sim.Config.EndAfterDeathSeconds + 0.1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));
            Assert.That(flow.EndKind, Is.EqualTo(EndKind.SoClose));
            Assert.That(flow.EndReason, Is.EqualTo(EndReason.PlayerDied));
        }

        [Test]
        public void DeathAfterAKillOverridesThePendingEnd()
        {
            WormSim sim = VictimWorld(out PlayableFlow flow);
            sim.StartPlay();
            TickParked(sim, flow, sim.Config.VictimDelaySeconds + 0.2f);
            sim.Worms[1].Place(-50f, 2f, 0f);
            Tick(sim, flow, Dt);
            Assert.That(sim.PlayerKills, Is.EqualTo(1));

            PlayerRunsIntoTheBot(sim);   // now run into the other bot before the kill card shows
            Tick(sim, flow, Dt);
            Assert.That(sim.Player.Alive, Is.False);

            Tick(sim, flow, sim.Config.EndAfterDeathSeconds + 0.1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));
            Assert.That(flow.EndKind, Is.EqualTo(EndKind.SoClose));
            Assert.That(flow.EndReason, Is.EqualTo(EndReason.PlayerDied));
        }

        [Test]
        public void TimeLimitEndsTheRunWithNiceMovesWhenNobodyDied()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            PlayableFlow flow = new PlayableFlow(sim);
            sim.StartPlay();

            Tick(sim, flow, sim.Config.MaxPlaySeconds - 0.5f);
            Assert.That(flow.State, Is.EqualTo(FlowState.Play));

            Tick(sim, flow, 1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));
            Assert.That(flow.EndReason, Is.EqualTo(EndReason.TimeUp));
            Assert.That(flow.EndKind, Is.EqualTo(EndKind.NiceMoves));
        }

        [Test]
        public void EndCardIsFinal()
        {
            WormSim sim = TestWorld.Frozen(TestWorld.Empty());
            PlayableFlow flow = new PlayableFlow(sim);
            sim.StartPlay();
            Tick(sim, flow, sim.Config.MaxPlaySeconds + 1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));

            Tick(sim, flow, 10f);

            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));
            Assert.That(flow.EndReason, Is.EqualTo(EndReason.TimeUp));
        }
    }
}
