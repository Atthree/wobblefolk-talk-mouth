using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Plays baked voices (<see cref="BakedVoice"/>) in step with an AudioSource: whichever bake belongs to the clip
    /// it plays is read at the play position, so pitch, pauses and seeking stay in sync. No FFT at runtime, and it works
    /// with streamed and compressed clips. Without audio, <see cref="Play"/> runs a bake on its own clock.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Baked Viseme Signal")]
    public sealed class BakedVisemeSignal : VoiceSignalSource
    {
        [Tooltip("Bakes to choose from by the AudioSource's clip. A single bake without a clip is used for any clip.")]
        [SerializeField] List<BakedVoice> bakes = new List<BakedVoice>();
        [Tooltip("The AudioSource to follow. Empty = the one on this object.")]
        [SerializeField] AudioSource audioSource;
        [Tooltip("Read ahead (+) or behind (-) the play position (s).")]
        [Range(-0.3f, 0.3f)] [SerializeField] float lookAhead;
        [Tooltip("Advance Play() on unscaled time.")]
        [SerializeField] bool useUnscaledTime = true;

        BakedVoice manual;
        double manualTime;

        public List<BakedVoice> Bakes => bakes;

        public AudioSource AudioSource
        {
            get => audioSource != null ? audioSource : audioSource = GetComponent<AudioSource>();
            set => audioSource = value;
        }

        public float LookAhead
        {
            get => lookAhead;
            set => lookAhead = value;
        }

        /// <summary>The bake read this frame, if any.</summary>
        public BakedVoice Current { get; private set; }

        /// <summary>True while <see cref="Play"/> runs a bake without audio.</summary>
        public bool IsPlayingManually => manual != null;

        /// <summary>Plays a bake on its own clock (no AudioSource needed), e.g. for a voice streamed from elsewhere.</summary>
        public void Play(BakedVoice baked, double startTime = 0.0)
        {
            manual = baked;
            manualTime = startTime;
        }

        public void Stop() => manual = null;

        void Update() => Advance(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        /// <summary>Moves the <see cref="Play"/> clock on. Public so tests can drive it.</summary>
        public void Advance(float dt)
        {
            if (manual == null) return;
            manualTime += dt;
            if (manualTime > manual.Duration) manual = null;
        }

        /// <summary>The bake for <paramref name="clip"/>: the one baked from it, or a lone bake without a clip.</summary>
        public BakedVoice Find(AudioClip clip)
        {
            BakedVoice found = null;
            foreach (BakedVoice b in bakes)
            {
                if (b == null) continue;
                if (b.Clip == clip)
                {
                    found = b;
                    break;
                }

                if (b.Clip == null && found == null) found = b;
            }

            return found;
        }

        public override void Read(VoiceSignal signal)
        {
            Current = null;
            double time;
            BakedVoice baked;
            if (manual != null)
            {
                baked = manual;
                time = manualTime;
            }
            else
            {
                AudioSource source = AudioSource;
                if (source == null || !source.isPlaying || source.clip == null) return;
                baked = Find(source.clip);
                if (baked == null) return;
                time = source.timeSamples / (double)source.clip.frequency;
            }

            if (baked.Sample(time + lookAhead, out float rms, out BandEnergies energies))
            {
                Current = baked;
                signal.SetBands(rms, energies);
            }
        }
    }
}
