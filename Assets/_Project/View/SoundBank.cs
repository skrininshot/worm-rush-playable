using System;
using System.Collections.Generic;
using UnityEngine;
using WormRush.Config;
using WormRush.Core;
using Object = UnityEngine.Object;

namespace WormRush.View
{
    /// <summary>
    /// The playable's sounds, synthesized at start-up from the cues in JuiceConfig (no audio files in the build,
    /// like the reference's WebAudio oscillators) and played from a small pool of AudioSources. The eat blip has
    /// one clip per combo step, so its pitch climbs exactly as the reference does and each blip keeps its length.
    /// Delayed cues (the second note of a chord of two) are timed here rather than with PlayDelayed, which
    /// keeps the Web build on the simplest audio path.
    /// </summary>
    public sealed class SoundBank : IDisposable
    {
        const int VoiceCount = 8;

        struct Pending
        {
            public float At;
            public AudioClip Clip;
            public float Volume;
        }

        readonly JuiceConfig _config;
        readonly UiJuiceConfig _ui;
        readonly AudioClip[] _eat;
        readonly AudioClip[] _kill;
        readonly AudioClip[] _death;
        readonly AudioClip[] _endCard;
        readonly AudioClip[] _tap;
        readonly AudioClip[] _pop;
        readonly List<Object> _owned = new List<Object>();
        readonly List<Pending> _pending = new List<Pending>(4);
        readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        readonly GameObject _root;
        int _nextVoice;

        public SoundBank(PlayableConfig config, Camera camera)
        {
            _config = config.Juice;
            _ui = config.UiJuice;

            _eat = new AudioClip[_config.ComboMaxSteps + 1];
            for (int step = 1; step < _eat.Length; step++)
                _eat[step] = MakeClip("Eat" + step, _config.EatCue, _config.EatCue.Frequency + step * _config.EatPitchStep);

            _kill = MakeClips("Kill", _config.KillCues);
            _death = MakeClips("Death", _config.DeathCues);
            _endCard = MakeClips("EndCard", _config.EndCardCues);
            _tap = MakeClips("UiTap", _ui.TapCues);
            _pop = MakeClips("UiPop", _ui.PopCues);

            _root = new GameObject("WormRush Audio");
            for (int i = 0; i < VoiceCount; i++)
            {
                AudioSource voice = _root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 0f;
                _voices[i] = voice;
            }

            // A scene without a listener is silent; the template's camera has one, a camera made by Bootstrap does not.
            if (Object.FindFirstObjectByType<AudioListener>() == null && camera != null)
                camera.gameObject.AddComponent<AudioListener>();
        }

        AudioClip[] MakeClips(string name, SoundCue[] cues)
        {
            AudioClip[] clips = new AudioClip[cues != null ? cues.Length : 0];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = MakeClip(name + i, cues[i], cues[i].Frequency);
            return clips;
        }

        AudioClip MakeClip(string name, SoundCue cue, float frequency)
        {
            float[] samples = ToneSynth.Render(cue, frequency, _config.SampleRate);
            AudioClip clip = AudioClip.Create("WormRush." + name, samples.Length, 1, _config.SampleRate, false);
            clip.SetData(samples, 0);
            _owned.Add(clip);
            return clip;
        }

        /// <summary>Starts the sound a JuiceState asked for.</summary>
        public void Play(in SoundRequest request, float time)
        {
            switch (request.Id)
            {
                case SoundId.Eat:
                    int step = Mathf.Clamp(request.Step, 1, _eat.Length - 1);
                    PlayClip(_eat[step], _config.EatCue.Volume, 0f, time);
                    break;
                case SoundId.Kill:
                    PlayCues(_kill, _config.KillCues, time);
                    break;
                case SoundId.Death:
                    PlayCues(_death, _config.DeathCues, time);
                    break;
                case SoundId.EndCard:
                    PlayCues(_endCard, _config.EndCardCues, time);
                    break;
                case SoundId.UiTap:
                    PlayCues(_tap, _ui.TapCues, time);
                    break;
                case SoundId.UiPop:
                    PlayCues(_pop, _ui.PopCues, time);
                    break;
            }
        }

        void PlayCues(AudioClip[] clips, SoundCue[] cues, float time)
        {
            for (int i = 0; i < clips.Length; i++)
                PlayClip(clips[i], cues[i].Volume, cues[i].DelaySeconds, time);
        }

        void PlayClip(AudioClip clip, float volume, float delay, float time)
        {
            if (clip == null) return;
            float level = volume * _config.MasterVolume;
            if (delay > 0f)
                _pending.Add(new Pending { At = time + delay, Clip = clip, Volume = level });
            else
                StartVoice(clip, level);
        }

        /// <summary>Starts delayed notes that have come due. Call once per frame.</summary>
        public void Update(float time)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending p = _pending[i];
                if (p.At > time) continue;
                _pending.RemoveAt(i);
                StartVoice(p.Clip, p.Volume);
            }
        }

        void StartVoice(AudioClip clip, float volume)
        {
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % VoiceCount;
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.Play();
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) Object.Destroy(_owned[i]);
            _owned.Clear();
        }
    }
}
