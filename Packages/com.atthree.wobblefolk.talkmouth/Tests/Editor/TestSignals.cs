using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    /// <summary>Synthetic test audio.</summary>
    static class TestSignals
    {
        public const int Rate = 48000;
        public const int Frame = 1024;

        /// <summary>Peak amplitude of a sine whose RMS level is <paramref name="db"/>.</summary>
        public static float SineAmplitude(float db) => LevelMeter.FromDecibels(db) * Mathf.Sqrt(2f);

        public static void Sine(float[] buffer, float hz, float rmsDb, int startSample, int rate = Rate)
        {
            float amplitude = SineAmplitude(rmsDb);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = amplitude * Mathf.Sin(2f * Mathf.PI * hz * (startSample + i) / rate);
            }
        }

        public static void Silence(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++) buffer[i] = 0f;
        }

        public static TalkMouthProfile Profile(TalkMouthPreset preset = TalkMouthPreset.Human) => TalkMouthPresets.Create(preset);
    }
}
