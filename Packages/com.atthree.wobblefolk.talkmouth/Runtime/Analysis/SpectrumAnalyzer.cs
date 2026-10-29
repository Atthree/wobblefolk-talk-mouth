using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>Energy of the four frequency bands the viseme classifier looks at.</summary>
    public struct BandEnergies
    {
        /// <summary>Around the first formant of rounded vowels (o, u).</summary>
        public float low;
        /// <summary>First formant of open vowels (a).</summary>
        public float mid;
        /// <summary>Second formant of spread vowels (e, i).</summary>
        public float high;
        /// <summary>Hiss of friction sounds (s, f, sh).</summary>
        public float fricative;

        public BandEnergies(float low, float mid, float high, float fricative)
        {
            this.low = low;
            this.mid = mid;
            this.high = high;
            this.fricative = fricative;
        }

        public float Total => low + mid + high + fricative;

        /// <summary>Each band's share of the total, as (low, mid, high, fricative); zero for silence.</summary>
        public Vector4 Shares
        {
            get
            {
                float total = Total;
                if (total <= 0f) return Vector4.zero;
                return new Vector4(low / total, mid / total, high / total, fricative / total);
            }
        }
    }

    /// <summary>
    /// Allocation-free FFT of the newest samples into <see cref="BandEnergies"/>. Works on raw PCM, so every voice
    /// source (AudioSource, microphone, voice chat) is analysed the same way.
    /// </summary>
    public sealed class SpectrumAnalyzer
    {
        readonly float[] re;
        readonly float[] im;
        readonly float[] window;
        readonly float[] cos;
        readonly float[] sin;
        readonly int[] reversed;

        public int Size { get; }

        public SpectrumAnalyzer(int size = 1024)
        {
            if (size < 16 || !Mathf.IsPowerOfTwo(size)) throw new ArgumentException("The FFT size must be a power of two of at least 16.", nameof(size));
            Size = size;
            re = new float[size];
            im = new float[size];
            window = new float[size];
            cos = new float[size / 2];
            sin = new float[size / 2];
            reversed = new int[size];

            for (int i = 0; i < size; i++) window[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (size - 1));
            for (int i = 0; i < size / 2; i++)
            {
                cos[i] = Mathf.Cos(-2f * Mathf.PI * i / size);
                sin[i] = Mathf.Sin(-2f * Mathf.PI * i / size);
            }

            int bits = 0;
            while ((1 << bits) < size) bits++;
            for (int i = 0; i < size; i++)
            {
                int r = 0;
                for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b);
                reversed[i] = r;
            }
        }

        /// <summary>Band energies of the newest <see cref="Size"/> samples (fewer are zero padded).</summary>
        public BandEnergies Analyze(ReadOnlySpan<float> samples, int sampleRate, VisemeSettings settings)
        {
            if (sampleRate <= 0 || samples.Length == 0) return default;

            int n = Mathf.Min(samples.Length, Size);
            int start = samples.Length - n;
            float emphasis = Mathf.Clamp01(settings.preEmphasis);

            // Pre-emphasis flattens the natural downward tilt of the voice, so the formants decide, not the pitch.
            float previous = start > 0 ? samples[start - 1] : samples[start];
            for (int i = 0; i < Size; i++)
            {
                float value = 0f;
                if (i < n)
                {
                    float x = samples[start + i];
                    value = x - emphasis * previous;
                    previous = x;
                    value *= n == Size ? window[i] : 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / Mathf.Max(1, n - 1));
                }

                re[reversed[i]] = value;
                im[reversed[i]] = 0f;
            }

            Transform();

            float binHz = (float)sampleRate / Size;
            var bands = new BandEnergies();
            for (int k = 1; k < Size / 2; k++)
            {
                float f = k * binHz;
                if (f < settings.lowStartHz || f >= settings.fricativeEndHz) continue;
                float power = re[k] * re[k] + im[k] * im[k];
                if (f < settings.lowMidHz) bands.low += power;
                else if (f < settings.midHighHz) bands.mid += power;
                else if (f < settings.highFricativeHz) bands.high += power;
                else bands.fricative += power;
            }

            return bands;
        }

        /// <summary>In-place iterative radix-2 FFT of the bit-reversed input.</summary>
        void Transform()
        {
            for (int length = 2; length <= Size; length <<= 1)
            {
                int half = length >> 1;
                int step = Size / length;
                for (int i = 0; i < Size; i += length)
                {
                    for (int j = 0; j < half; j++)
                    {
                        float wr = cos[j * step];
                        float wi = sin[j * step];
                        int a = i + j;
                        int b = a + half;
                        float tr = re[b] * wr - im[b] * wi;
                        float ti = re[b] * wi + im[b] * wr;
                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                    }
                }
            }
        }
    }
}
