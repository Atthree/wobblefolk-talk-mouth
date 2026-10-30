using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>Analyses a whole clip ahead of time into a <see cref="BakedVoice"/>.</summary>
    public static class VoiceBaker
    {
        public const float DefaultFrameRate = 60f;

        /// <summary>Frames needed to cover <paramref name="sampleCount"/> samples.</summary>
        public static int FrameCount(int sampleCount, int sampleRate, float frameRate) =>
            sampleRate <= 0 ? 0 : Mathf.CeilToInt(sampleCount / (float)sampleRate * frameRate) + 1;

        /// <summary>
        /// Fills <paramref name="levels"/> (linear RMS) and <paramref name="bandShares"/> for each frame of a mono
        /// signal. Each frame looks at the <paramref name="fftSize"/> samples just before its time, as a live voice does,
        /// so a baked and a live mouth move alike.
        /// </summary>
        public static void Bake(ReadOnlySpan<float> mono, int sampleRate, float frameRate, int fftSize, VisemeSettings settings,
            Span<float> levels, Span<Vector4> bandShares)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (levels.Length != bandShares.Length) throw new ArgumentException("Levels and bands must have the same length.");
            settings ??= new VisemeSettings();

            var spectrum = new SpectrumAnalyzer(fftSize);
            int size = spectrum.Size;
            var window = new float[size];
            for (int frame = 0; frame < levels.Length; frame++)
            {
                int end = Mathf.RoundToInt(frame / frameRate * sampleRate);
                int start = end - size;
                for (int i = 0; i < size; i++)
                {
                    int s = start + i;
                    window[i] = s >= 0 && s < mono.Length ? mono[s] : 0f;
                }

                levels[frame] = LevelMeter.Rms(window);
                bandShares[frame] = levels[frame] > 0f ? spectrum.Analyze(window, sampleRate, settings).Shares : Vector4.zero;
            }
        }

        /// <summary>Bakes a mono signal into a new asset (not saved; the editor tool saves it).</summary>
        public static BakedVoice Bake(ReadOnlySpan<float> mono, int sampleRate, AudioClip source = null,
            float frameRate = DefaultFrameRate, int fftSize = 1024, VisemeSettings settings = null)
        {
            int frames = FrameCount(mono.Length, sampleRate, frameRate);
            var levels = new float[frames];
            var bands = new Vector4[frames];
            Bake(mono, sampleRate, frameRate, fftSize, settings, levels, bands);

            var baked = ScriptableObject.CreateInstance<BakedVoice>();
            baked.name = source != null ? source.name + " (Talk Mouth)" : "Baked Voice";
            baked.Set(source, frameRate, levels, bands);
            return baked;
        }

        /// <summary>
        /// Bakes a clip. Its samples must be readable: in the editor any clip loaded as Decompress On Load, at runtime
        /// clips made in code (e.g. <see cref="SyntheticSpeech"/>). Returns null if they cannot be read.
        /// </summary>
        public static BakedVoice Bake(AudioClip clip, float frameRate = DefaultFrameRate, int fftSize = 1024, VisemeSettings settings = null)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            int channels = Mathf.Max(1, clip.channels);
            var interleaved = new float[clip.samples * channels];
            if (!clip.GetData(interleaved, 0)) return null;

            float[] mono = interleaved;
            if (channels > 1)
            {
                mono = new float[clip.samples];
                SampleMixer.Downmix(interleaved, channels, mono);
            }

            return Bake(mono, clip.frequency, clip, frameRate, fftSize, settings);
        }
    }
}
