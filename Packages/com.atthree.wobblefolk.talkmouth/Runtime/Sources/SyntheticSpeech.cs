using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Made-up speech for samples and tests: syllables of different vowels and loudness, short gaps between words
    /// and longer pauses between phrases, with a wandering pitch. No recorded audio needed. Deterministic per seed.
    /// </summary>
    public static class SyntheticSpeech
    {
        /// <summary>Fills <paramref name="output"/> (mono) with babble. Peak loudness is about <paramref name="amplitude"/>.</summary>
        public static void Render(Span<float> output, int sampleRate, uint seed = 1, float pitchHz = 140f, float amplitude = 0.5f)
        {
            output.Clear();
            uint state = seed == 0 ? 1u : seed;
            int position = Seconds(0.25f, sampleRate);
            int syllablesLeft = 0;
            int wordsLeft = 0;
            float pitch = pitchHz;

            while (position < output.Length)
            {
                if (wordsLeft == 0)
                {
                    wordsLeft = 2 + (int)(Next(ref state) * 5f); // words in a phrase
                    pitch = pitchHz * Mathf.Lerp(0.95f, 1.15f, Next(ref state));
                }

                if (syllablesLeft == 0) syllablesLeft = 1 + (int)(Next(ref state) * 3f);

                int length = Seconds(Mathf.Lerp(0.1f, 0.22f, Next(ref state)), sampleRate);
                length = Mathf.Min(length, output.Length - position);
                if (length <= 0) break;

                float r = Next(ref state);
                Viseme shape = r < 0.4f ? Viseme.AA : r < 0.65f ? Viseme.OH : r < 0.9f ? Viseme.EE : Viseme.FF;
                float loudness = amplitude * Mathf.Lerp(0.45f, 1f, Next(ref state));
                FormantSynth.Render(output.Slice(position, length), sampleRate, shape, pitch, loudness, state);
                position += length;
                pitch *= Mathf.Lerp(0.97f, 1.01f, Next(ref state)); // phrases drift down

                if (--syllablesLeft > 0) continue;
                // A word ended: a short gap, or a long pause after the phrase.
                float gap = --wordsLeft > 0 ? Mathf.Lerp(0.04f, 0.12f, Next(ref state)) : Mathf.Lerp(0.45f, 0.8f, Next(ref state));
                position += Seconds(gap, sampleRate);
            }
        }

        /// <summary>A clip of babble. Readable, so <see cref="AudioSourceSignal"/> analyses it at the play position.</summary>
        public static AudioClip CreateClip(string name, float seconds, int sampleRate = 44100, uint seed = 1, float pitchHz = 140f, float amplitude = 0.5f)
        {
            int length = Mathf.Max(1, Seconds(seconds, sampleRate));
            var data = new float[length];
            Render(data, sampleRate, seed, pitchHz, amplitude);
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static int Seconds(float seconds, int sampleRate) => Mathf.RoundToInt(seconds * sampleRate);

        /// <summary>xorshift32 in [0, 1).</summary>
        static float Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
