using System;
using System.Diagnostics;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum ExternalLevelUnit
    {
        /// <summary>Linear amplitude or RMS, 0–1 (most SDKs: "amplitude", "energy", "level").</summary>
        Linear = 0,
        /// <summary>Decibels full scale (0 = loudest, e.g. -30).</summary>
        Decibels = 1,
    }

    /// <summary>
    /// Voice from any voice chat SDK, without depending on one. Call <see cref="PushSamples"/> from the SDK's audio
    /// callback, or <see cref="PushLevel"/> with its speaking level, from any thread. The delay holds the mouth back
    /// to meet the audio, which usually reaches the speakers a little after the SDK reports it.
    /// Ready-made bridges are in the package samples.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/External Level Signal")]
    public sealed class ExternalLevelSignal : VoiceSignalSource
    {
        [Tooltip("Hold the mouth back this long (s) so it matches the played audio. Tune by eye; voice chat is often 0.05–0.3 s.")]
        [Range(0f, 1f)] [SerializeField] float delay = 0f;
        [Tooltip("Gaps in the reports shorter than this (s) are bridged (network jitter); longer means the speaker stopped or left, and the mouth closes.")]
        [Min(0f)] [SerializeField] float timeout = 0.12f;
        [Tooltip("What PushLevel values mean.")]
        [SerializeField] ExternalLevelUnit levelUnit = ExternalLevelUnit.Linear;
        [Tooltip("Multiplier for linear PushLevel values (SDK levels are often peak amplitude, about 1.4× RMS).")]
        [Min(0f)] [SerializeField] float levelScale = 1f;

        static readonly Stopwatch clock = Stopwatch.StartNew();
        readonly ExternalVoiceBuffer buffer = new ExternalVoiceBuffer();

        public float Delay
        {
            get => delay;
            set => delay = Mathf.Max(0f, value);
        }

        /// <summary>Thread-safe time base (s) used for the delay.</summary>
        public static double Now => clock.Elapsed.TotalSeconds;

        /// <summary>Voice PCM (interleaved if <paramref name="channels"/> &gt; 1). Any thread; does not allocate.</summary>
        public void PushSamples(ReadOnlySpan<float> pcm, int channels, int sampleRate) =>
            buffer.PushSamples(pcm, channels, sampleRate, Now);

        public void PushSamples(float[] pcm, int channels, int sampleRate) =>
            buffer.PushSamples(pcm, channels, sampleRate, Now);

        /// <summary>A speaking level in <see cref="ExternalLevelUnit"/>. Any thread.</summary>
        public void PushLevel(float value)
        {
            float rms = levelUnit == ExternalLevelUnit.Decibels ? LevelMeter.FromDecibels(value) : value * levelScale;
            buffer.PushLevel(rms, Now);
        }

        /// <summary>Forget everything pushed so far (e.g. the speaker changed).</summary>
        public void Clear() => buffer.Clear();

        public override void Read(VoiceSignal signal) => buffer.Read(signal, Now, delay, timeout);
    }
}
