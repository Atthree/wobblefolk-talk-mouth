using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum VoiceSignalKind
    {
        /// <summary>Nothing this frame: the mouth closes.</summary>
        Silent = 0,
        /// <summary>Mono PCM samples (-1…1): level and, if wanted, mouth shapes from the spectrum.</summary>
        Samples = 1,
        /// <summary>A linear RMS level computed elsewhere (e.g. a voice chat SDK).</summary>
        Level = 2,
        /// <summary>A clean 0–1 level (e.g. typed text): 0 = closed, 1 = fully open by the profile's levels.</summary>
        Normalized = 3,
    }

    /// <summary>
    /// What a voice source hands the analyzer for one frame. The sample buffer is allocated once and reused, so
    /// reading a source never allocates.
    /// </summary>
    public sealed class VoiceSignal
    {
        readonly float[] samples;

        public VoiceSignal(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            samples = new float[capacity];
        }

        /// <summary>Largest number of samples a source may write.</summary>
        public int Capacity => samples.Length;
        public VoiceSignalKind Kind { get; private set; }
        public int SampleCount { get; private set; }
        public int SampleRate { get; private set; }
        /// <summary>Linear RMS for <see cref="VoiceSignalKind.Level"/>, 0–1 for <see cref="VoiceSignalKind.Normalized"/>.</summary>
        public float Level { get; private set; }
        /// <summary>The source already knows the mouth shape (e.g. from the letter being typed).</summary>
        public bool HasViseme { get; private set; }
        public Viseme Viseme { get; private set; }

        public ReadOnlySpan<float> Samples => new ReadOnlySpan<float>(samples, 0, SampleCount);

        public void Clear()
        {
            Kind = VoiceSignalKind.Silent;
            SampleCount = 0;
            SampleRate = 0;
            Level = 0f;
            HasViseme = false;
            Viseme = Viseme.Closed;
        }

        /// <summary>Starts a sample frame and returns the buffer to fill (mono). <paramref name="count"/> is clamped to the capacity.</summary>
        public Span<float> BeginSamples(int count, int sampleRate)
        {
            Clear();
            Kind = VoiceSignalKind.Samples;
            SampleCount = Mathf.Clamp(count, 0, samples.Length);
            SampleRate = sampleRate;
            return new Span<float>(samples, 0, SampleCount);
        }

        public void SetLevel(float rms)
        {
            Clear();
            Kind = VoiceSignalKind.Level;
            Level = Mathf.Max(0f, rms);
        }

        public void SetLevel(float rms, Viseme viseme)
        {
            SetLevel(rms);
            SetViseme(viseme);
        }

        public void SetNormalized(float level)
        {
            Clear();
            Kind = VoiceSignalKind.Normalized;
            Level = Mathf.Clamp01(level);
        }

        public void SetNormalized(float level, Viseme viseme)
        {
            SetNormalized(level);
            SetViseme(viseme);
        }

        void SetViseme(Viseme viseme)
        {
            HasViseme = viseme != Viseme.Closed;
            Viseme = viseme;
        }
    }
}
