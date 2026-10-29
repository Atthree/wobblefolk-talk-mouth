using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public static class LevelMeter
    {
        /// <summary>The level reported for silence.</summary>
        public const float MinDecibels = -100f;

        /// <summary>Root mean square of the samples (a sine of amplitude A gives A / √2).</summary>
        public static float Rms(ReadOnlySpan<float> samples)
        {
            if (samples.Length == 0) return 0f;
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return (float)Math.Sqrt(sum / samples.Length);
        }

        public static float ToDecibels(float linear)
        {
            if (linear <= 0f) return MinDecibels;
            return Mathf.Max(MinDecibels, 20f * Mathf.Log10(linear));
        }

        public static float FromDecibels(float decibels) =>
            decibels <= MinDecibels ? 0f : Mathf.Pow(10f, decibels / 20f);

        /// <summary>Frame-rate independent exponential approach; <paramref name="time"/> is the time constant.</summary>
        public static float Smooth(float current, float target, float time, float dt)
        {
            if (time <= 0f) return target;
            return current + (target - current) * (1f - Mathf.Exp(-dt / time));
        }
    }
}
