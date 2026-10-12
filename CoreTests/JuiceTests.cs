using System;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    /// <summary>Shake, particles, popups, combo and the tone synth: the parts of stage 5 that need no Unity.</summary>
    public class JuiceTests
    {
        const float Dt = TestWorld.Dt;

        // ---- primitives ----

        [Test]
        public void ComboCountsUpAndStartsOverAfterThePause()
        {
            ComboMeter combo = new ComboMeter(1.5f);
            Assert.That(combo.Register(0f), Is.EqualTo(1));
            Assert.That(combo.Register(0.5f), Is.EqualTo(2));
            Assert.That(combo.Register(1.9f), Is.EqualTo(3));   // 1.4 s since the last one: still a streak
            Assert.That(combo.Register(3.5f), Is.EqualTo(1));   // 1.6 s: over
        }

        [Test]
        public void ComboWithoutAWindowNeverResets()
        {
            ComboMeter combo = new ComboMeter(0f);
            combo.Register(0f);
            Assert.That(combo.Register(1000f), Is.EqualTo(2));
        }

        [Test]
        public void ShakeDecaysToZeroAndOnlyExtendsNeverShortens()
        {
            ScreenShake shake = new ScreenShake(18f);
            SimRandom rng = new SimRandom(3);

            shake.Sample(rng, out float x, out float y);
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(y, Is.EqualTo(0f));

            shake.Trigger(0.5f);
            shake.Trigger(0.35f);
            Assert.That(shake.Remaining, Is.EqualTo(0.5f));

            for (int i = 0; i < 20; i++)
            {
                shake.Sample(rng, out x, out y);
                Assert.That(Math.Abs(x), Is.LessThanOrEqualTo(shake.Remaining * 18f));
                Assert.That(Math.Abs(y), Is.LessThanOrEqualTo(shake.Remaining * 18f));
            }

            for (int i = 0; i < 40; i++) shake.Step(Dt);
            Assert.That(shake.Remaining, Is.EqualTo(0f));
        }

        [Test]
        public void ABurstSpawnsTheConfiguredParticlesAndTheyAllDie()
        {
            JuiceConfig config = new JuiceConfig();
            ParticlePool pool = new ParticlePool(config);
            SimRandom rng = new SimRandom(5);

            pool.Burst(10f, 20f, 3, rng);

            Assert.That(pool.Count, Is.EqualTo(config.BurstCount));
            for (int i = 0; i < pool.Count; i++)
            {
                Assert.That(pool.Items[i].X, Is.EqualTo(10f));
                Assert.That(pool.Items[i].ColorId, Is.EqualTo(3));
                float speed = MathF.Sqrt(pool.Items[i].VX * pool.Items[i].VX + pool.Items[i].VY * pool.Items[i].VY);
                Assert.That(speed, Is.InRange(config.BurstSpeedMin - 0.01f, config.BurstSpeedMax + 0.01f));
            }

            float elapsed = 0f;
            while (pool.Count > 0 && elapsed < 5f)
            {
                pool.Step(Dt);
                elapsed += Dt;
            }

            Assert.That(pool.Count, Is.EqualTo(0));
            Assert.That(elapsed, Is.InRange(config.ParticleLifeMin - Dt, config.ParticleLifeMax + Dt));
        }

        [Test]
        public void ParticlesSlowDownAndSpreadOut()
        {
            JuiceConfig config = new JuiceConfig { BurstCount = 1, ParticleLifeMin = 5f, ParticleLifeMax = 5f };
            ParticlePool pool = new ParticlePool(config);
            pool.Burst(0f, 0f, 0, new SimRandom(9));
            float speed0 = MathF.Sqrt(pool.Items[0].VX * pool.Items[0].VX + pool.Items[0].VY * pool.Items[0].VY);

            for (int i = 0; i < 30; i++) pool.Step(Dt);   // half a second

            Particle p = pool.Items[0];
            float speed = MathF.Sqrt(p.VX * p.VX + p.VY * p.VY);
            Assert.That(speed, Is.EqualTo(speed0 * MathF.Exp(-config.ParticleDrag * 0.5f)).Within(0.5f));
            Assert.That(MathF.Sqrt(p.X * p.X + p.Y * p.Y), Is.GreaterThan(10f));
        }

        [Test]
        public void ThePoolDropsBurstsPastItsCapacityInsteadOfGrowing()
        {
            JuiceConfig config = new JuiceConfig { ParticleCapacity = 40, BurstCount = 26 };
            ParticlePool pool = new ParticlePool(config);
            SimRandom rng = new SimRandom(1);

            pool.Burst(0f, 0f, 0, rng);
            pool.Burst(0f, 0f, 0, rng);

            Assert.That(pool.Count, Is.EqualTo(40));
        }

        [Test]
        public void PopupsRiseFadeAndExpire()
        {
            JuiceConfig config = new JuiceConfig();
            PopupPool pool = new PopupPool(config);
            pool.Spawn(5f, 100f, PopupKind.Kill, 0, 1.6f);

            Assert.That(pool.Alpha(pool.Items[0]), Is.EqualTo(1f));

            for (int i = 0; i < 30; i++) pool.Step(Dt);   // 0.5 s
            Assert.That(pool.Items[0].Y, Is.EqualTo(100f + config.PopupRiseSpeed * 0.5f).Within(0.5f));
            Assert.That(pool.Alpha(pool.Items[0]), Is.EqualTo(1f));

            for (int i = 0; i < 36; i++) pool.Step(Dt);   // 1.1 s: inside the fade
            Assert.That(pool.Alpha(pool.Items[0]), Is.InRange(0.01f, 0.99f));

            for (int i = 0; i < 20; i++) pool.Step(Dt);
            Assert.That(pool.Count, Is.EqualTo(0));
        }

        [Test]
        public void AFullPopupPoolDropsTheOldest()
        {
            JuiceConfig config = new JuiceConfig { PopupCapacity = 3 };
            PopupPool pool = new PopupPool(config);
            for (int i = 1; i <= 4; i++) pool.Spawn(0f, 0f, PopupKind.Value, i, 1f);

            Assert.That(pool.Count, Is.EqualTo(3));
            Assert.That(pool.Items[0].Value, Is.EqualTo(2));
            Assert.That(pool.Items[2].Value, Is.EqualTo(4));
        }

        [Test]
        public void TheSineHasTheRequestedPitchAndTheEnvelopeDiesAway()
        {
            SoundCue cue = new SoundCue(Waveform.Sine, 440f, 0.1f, 1f);
            float[] samples = ToneSynth.Render(cue, cue.Frequency, 44100);

            Assert.That(samples.Length, Is.EqualTo(4410));

            // Upward zero crossings = cycles. 0.1 s of 440 Hz is 44 cycles.
            int cycles = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i - 1] < 0f && samples[i] >= 0f) cycles++;
            }
            Assert.That(cycles, Is.InRange(43, 45));

            float peakStart = 0f;
            float peakEnd = 0f;
            for (int i = 100; i < 300; i++) peakStart = Math.Max(peakStart, Math.Abs(samples[i]));
            for (int i = samples.Length - 100; i < samples.Length; i++) peakEnd = Math.Max(peakEnd, Math.Abs(samples[i]));
            Assert.That(peakStart, Is.GreaterThan(0.5f));
            Assert.That(peakEnd, Is.LessThan(0.01f));
        }

        [Test]
        public void EveryWaveformStaysInRange()
        {
            foreach (Waveform wave in Enum.GetValues(typeof(Waveform)))
            {
                float[] samples = ToneSynth.Render(new SoundCue(wave, 660f, 0.05f, 1f), 660f, 44100);
                foreach (float s in samples) Assert.That(Math.Abs(s), Is.LessThanOrEqualTo(1f));
            }
        }

        // ---- the rules ----

        static WormSim World(out PlayableFlow flow)
        {
            WormSim sim = TestWorld.Frozen(TestWorld.OneBot());
            flow = new PlayableFlow(sim);
            return sim;
        }

        static void Push(WormSim sim, SimEventType type, int wormId, int otherId, int value, float x = 0f, float y = 0f)
        {
            sim.Events.Clear();
            sim.Events.Add(new SimEvent(type, wormId, otherId, value, x, y));
        }

        [Test]
        public void EatingRisesInPitchWithTheStreakAndCapsOut()
        {
            WormSim sim = World(out PlayableFlow flow);
            JuiceConfig config = new JuiceConfig { EatMinIntervalSeconds = 0f };
            JuiceState juice = new JuiceState(config, 1);

            int last = 0;
            for (int i = 1; i <= config.ComboMaxSteps + 5; i++)
            {
                Push(sim, SimEventType.Ate, 0, -1, 1);
                juice.Update(sim, flow, Dt);
                Assert.That(juice.Sounds.Count, Is.EqualTo(1));
                Assert.That(juice.Sounds[0].Id, Is.EqualTo(SoundId.Eat));
                Assert.That(juice.Sounds[0].Step, Is.EqualTo(Math.Min(i, config.ComboMaxSteps)));
                last = juice.Sounds[0].Step;
            }
            Assert.That(last, Is.EqualTo(config.ComboMaxSteps));
        }

        [Test]
        public void TheEatSoundIsThrottled()
        {
            WormSim sim = World(out PlayableFlow flow);
            JuiceState juice = new JuiceState(new JuiceConfig(), 1);

            Push(sim, SimEventType.Ate, 0, -1, 1);
            juice.Update(sim, flow, Dt);
            Assert.That(juice.Sounds.Count, Is.EqualTo(1));

            Push(sim, SimEventType.Ate, 0, -1, 1);
            juice.Update(sim, flow, Dt);   // sim time has not moved: inside the minimum interval
            Assert.That(juice.Sounds.Count, Is.EqualTo(0));
            Assert.That(juice.ComboStreak, Is.EqualTo(2));   // but it still counts
        }

        [Test]
        public void OnlyThePlayersPelletsSoundAndOnlyBigOnesPopUp()
        {
            WormSim sim = World(out PlayableFlow flow);
            JuiceState juice = new JuiceState(new JuiceConfig(), 1);

            Push(sim, SimEventType.Ate, 1, -1, 3);   // a bot's pellet
            juice.Update(sim, flow, Dt);
            Assert.That(juice.Sounds.Count, Is.EqualTo(0));
            Assert.That(juice.Popups.Count, Is.EqualTo(0));

            Push(sim, SimEventType.Ate, 0, -1, 1);   // the player's, worth 1
            juice.Update(sim, flow, Dt);
            Assert.That(juice.Popups.Count, Is.EqualTo(0));

            Push(sim, SimEventType.Ate, 0, -1, 3, 40f, 50f);   // worth 3
            juice.Update(sim, flow, Dt);
            Assert.That(juice.Popups.Count, Is.EqualTo(1));
            Assert.That(juice.Popups.Items[0].Kind, Is.EqualTo(PopupKind.Value));
            Assert.That(juice.Popups.Items[0].Value, Is.EqualTo(3));
            Assert.That(juice.Popups.Items[0].X, Is.EqualTo(40f));
        }

        [Test]
        public void ThePlayersKillShakesPopsAndBlipsAndBurstsInTheVictimsColour()
        {
            WormSim sim = World(out PlayableFlow flow);
            JuiceConfig config = new JuiceConfig();
            JuiceState juice = new JuiceState(config, 1);

            Push(sim, SimEventType.Killed, 1, 0, 0, 100f, 200f);   // bot 1 died on the player's body
            juice.Update(sim, flow, Dt);

            Assert.That(juice.ShakeRemaining, Is.EqualTo(config.ShakeKillSeconds - Dt).Within(1e-4f));
            Assert.That(juice.Popups.Count, Is.EqualTo(1));
            Assert.That(juice.Popups.Items[0].Kind, Is.EqualTo(PopupKind.Kill));
            Assert.That(juice.Popups.Items[0].Y, Is.GreaterThan(200f));   // above the head
            Assert.That(juice.Sounds.Count, Is.EqualTo(1));
            Assert.That(juice.Sounds[0].Id, Is.EqualTo(SoundId.Kill));
            Assert.That(juice.Particles.Count, Is.EqualTo(config.BurstCount));
            Assert.That(juice.Particles.Items[0].ColorId, Is.EqualTo(sim.Worms[1].ColorId));
        }

        [Test]
        public void ThePlayersDeathShakesHarderAndBuzzes()
        {
            WormSim sim = World(out PlayableFlow flow);
            JuiceConfig config = new JuiceConfig();
            JuiceState juice = new JuiceState(config, 1);

            Push(sim, SimEventType.Killed, 0, 1, 0);
            juice.Update(sim, flow, Dt);

            Assert.That(juice.ShakeRemaining, Is.EqualTo(config.ShakeDeathSeconds - Dt).Within(1e-4f));
            Assert.That(juice.Popups.Count, Is.EqualTo(0));
            Assert.That(juice.Sounds.Count, Is.EqualTo(1));
            Assert.That(juice.Sounds[0].Id, Is.EqualTo(SoundId.Death));
            Assert.That(juice.Particles.Count, Is.EqualTo(config.BurstCount));
        }

        [Test]
        public void ABotDyingOnABotOnlyBursts()
        {
            WormSim sim = TestWorld.Frozen(new SimConfig { FoodTarget = 0, BotStartLengths = new[] { 20, 20 }, BotColorIds = new[] { 1, 2 }, VictimBotIndex = -1 });
            PlayableFlow flow = new PlayableFlow(sim);
            JuiceState juice = new JuiceState(new JuiceConfig(), 1);

            Push(sim, SimEventType.Killed, 1, 2, 0);
            juice.Update(sim, flow, Dt);

            Assert.That(juice.Particles.Count, Is.GreaterThan(0));
            Assert.That(juice.ShakeRemaining, Is.EqualTo(0f));
            Assert.That(juice.Popups.Count, Is.EqualTo(0));
            Assert.That(juice.Sounds.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheEndCardSoundsOnceAndThenOnlyBurstsReact()
        {
            WormSim sim = World(out PlayableFlow flow);
            JuiceConfig config = new JuiceConfig();
            JuiceState juice = new JuiceState(config, 1);

            // The player kills: the card comes EndAfterKillSeconds later.
            sim.StartPlay();
            Push(sim, SimEventType.Killed, 1, 0, 0);
            flow.Update();
            juice.Update(sim, flow, Dt);
            Assert.That(flow.State, Is.Not.EqualTo(FlowState.EndCard));

            int soundsOfTheEndCard = 0;
            for (int i = 0; i < 4 * 60 && soundsOfTheEndCard == 0; i++)
            {
                sim.Step(Dt);
                flow.Update();
                juice.Update(sim, flow, Dt);
                foreach (SoundRequest request in juice.Sounds)
                {
                    if (request.Id == SoundId.EndCard) soundsOfTheEndCard++;
                }
            }
            Assert.That(flow.State, Is.EqualTo(FlowState.EndCard));
            Assert.That(soundsOfTheEndCard, Is.EqualTo(1));

            // Under the card: a later kill still bursts, but nothing else.
            Push(sim, SimEventType.Killed, 1, 0, 0);
            juice.Update(sim, flow, Dt);
            Assert.That(juice.Sounds.Count, Is.EqualTo(0));

            Push(sim, SimEventType.Ate, 0, -1, 3);
            juice.Update(sim, flow, Dt);
            Assert.That(juice.Sounds.Count, Is.EqualTo(0));
            Assert.That(juice.Popups.Count, Is.EqualTo(0));
        }
    }
}
