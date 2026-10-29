using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// A tiny vowel synthesizer: a buzzing source shaped by two resonances (formants), as a voice is shaped by the
    /// mouth. Good enough for babble voices and test audio, not for real speech. Writes into a caller's buffer and
    /// never allocates.
    /// </summary>
    public static class FormantSynth
    {
        const float Bandwidth = 100f;

        /// <summary>First three formants (Hz) of each shape.</summary>
        public static Vector3 FormantsFor(Viseme viseme)
        {
            switch (viseme)
            {
                case Viseme.AA: return new Vector3(650f, 1100f, 2500f);
                case Viseme.OH: return new Vector3(450f, 800f, 2500f);
                case Viseme.EE: return new Vector3(300f, 2300f, 3000f);
                default: return new Vector3(250f, 1000f, 2500f); // a hum
            }
        }

        /// <summary>Relative strength of those formants.</summary>
        public static Vector3 FormantGainsFor(Viseme viseme)
        {
            switch (viseme)
            {
                case Viseme.AA: return new Vector3(1f, 1f, 0.5f);
                case Viseme.OH: return new Vector3(1f, 0.5f, 0.25f);
                case Viseme.EE: return new Vector3(1f, 1f, 0.5f);
                default: return new Vector3(1f, 0.1f, 0.05f);
            }
        }

        /// <summary>
        /// One syllable in a shape: a vowel, or a hiss for <see cref="Viseme.FF"/>. Peak = <paramref name="amplitude"/>,
        /// with a short fade in and out so it does not click.
        /// </summary>
        public static void Render(Span<float> output, int sampleRate, Viseme viseme, float pitchHz, float amplitude, uint seed = 1)
        {
            if (viseme == Viseme.FF) RenderNoise(output, sampleRate, 6000f, amplitude, seed);
            else RenderVowel(output, sampleRate, pitchHz, FormantsFor(viseme), FormantGainsFor(viseme), amplitude);
        }

        /// <summary>A pulse train (the vocal folds) through three parallel formants of the given strengths.</summary>
        public static void RenderVowel(Span<float> output, int sampleRate, float pitchHz, Vector3 formants, Vector3 gains, float amplitude)
        {
            var f1 = new Resonator(formants.x, Bandwidth, sampleRate);
            var f2 = new Resonator(formants.y, Bandwidth, sampleRate);
            var f3 = new Resonator(formants.z, Bandwidth, sampleRate);
            float phase = 1f;
            float step = pitchHz / sampleRate;
            float tilt = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                float pulse = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    pulse = 1f;
                }

                phase += step;
                // Voices lose highs (-6 dB per octave here); the analyzer's pre-emphasis undoes exactly this.
                tilt = pulse + 0.95f * tilt;
                output[i] = gains.x * f1.Process(tilt) + gains.y * f2.Process(tilt) + gains.z * f3.Process(tilt);
            }

            Finish(output, sampleRate, amplitude);
        }

        public static void RenderNoise(Span<float> output, int sampleRate, float centerHz, float amplitude, uint seed = 1)
        {
            uint state = seed == 0 ? 1u : seed;
            var band = new Resonator(Mathf.Min(centerHz, sampleRate * 0.45f), 3000f, sampleRate);
            float previous = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float white = (state & 0xFFFFFF) / (float)0x800000 - 1f;
                float hiss = white - previous; // tilt towards highs
                previous = white;
                output[i] = band.Process(hiss);
            }

            Finish(output, sampleRate, amplitude);
        }

        /// <summary>Normalises to the peak and fades the ends (5 ms in, 20 ms out).</summary>
        static void Finish(Span<float> output, int sampleRate, float amplitude)
        {
            float peak = 0f;
            for (int i = 0; i < output.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(output[i]));
            float gain = peak > 0f ? amplitude / peak : 0f;

            int fadeIn = Mathf.Min(output.Length / 2, Mathf.CeilToInt(0.005f * sampleRate));
            int fadeOut = Mathf.Min(output.Length / 2, Mathf.CeilToInt(0.02f * sampleRate));
            for (int i = 0; i < output.Length; i++)
            {
                float fade = 1f;
                if (i < fadeIn) fade = (float)i / fadeIn;
                int fromEnd = output.Length - 1 - i;
                if (fromEnd < fadeOut) fade = Mathf.Min(fade, (float)fromEnd / fadeOut);
                output[i] *= gain * fade;
            }
        }

        /// <summary>Two-pole resonance at a frequency with a bandwidth; about 1 at its peak, whatever the frequency.</summary>
        struct Resonator
        {
            readonly float a1;
            readonly float a2;
            readonly float gain;
            float y1;
            float y2;

            public Resonator(float frequency, float bandwidth, int sampleRate)
            {
                float r = Mathf.Exp(-Mathf.PI * bandwidth / sampleRate);
                float theta = 2f * Mathf.PI * frequency / sampleRate;
                a1 = 2f * r * Mathf.Cos(theta);
                a2 = -r * r;
                gain = (1f - r) * 2f * Mathf.Max(1e-3f, Mathf.Sin(theta));
                y1 = 0f;
                y2 = 0f;
            }

            public float Process(float x)
            {
                float y = gain * x + a1 * y1 + a2 * y2;
                y2 = y1;
                y1 = y;
                return y;
            }
        }
    }
}
