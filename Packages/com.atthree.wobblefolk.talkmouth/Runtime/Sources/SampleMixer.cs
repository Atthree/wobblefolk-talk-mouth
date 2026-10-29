using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>Helpers for reading the newest window of a clip.</summary>
    public static class SampleMixer
    {
        /// <summary>Averages interleaved channels into mono; <paramref name="mono"/>.Length frames are written.</summary>
        public static void Downmix(ReadOnlySpan<float> interleaved, int channels, Span<float> mono)
        {
            if (channels <= 1)
            {
                interleaved.Slice(0, mono.Length).CopyTo(mono);
                return;
            }

            float scale = 1f / channels;
            for (int f = 0, i = 0; f < mono.Length; f++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++, i++) sum += interleaved[i];
                mono[f] = sum * scale;
            }
        }

        /// <summary>
        /// First frame of the <paramref name="frames"/> long window that ends at <paramref name="playhead"/>.
        /// Looping clips wrap; others are clamped to the clip, and a playhead past the end has nothing to read.
        /// </summary>
        public static bool TryGetWindow(long playhead, int frames, int clipFrames, bool loop, out int start)
        {
            start = 0;
            if (clipFrames <= 0 || frames <= 0 || frames > clipFrames) return false;

            if (loop)
            {
                long s = (playhead - frames) % clipFrames;
                start = (int)(s < 0 ? s + clipFrames : s);
                return true;
            }

            if (playhead > clipFrames || playhead < 0) return false;
            start = (int)Math.Max(0, Math.Min(playhead - frames, clipFrames - frames));
            return true;
        }

        /// <summary>Frames to analyse: the buffer size, but no more than the clip has.</summary>
        public static int WindowFrames(int capacity, int clipFrames) => Mathf.Max(0, Mathf.Min(capacity, clipFrames));
    }
}
