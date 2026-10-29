using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Holds what a voice chat SDK reports (samples or levels) and plays it back a fixed delay later, so the mouth
    /// can wait for the audio it belongs to. Thread-safe: SDKs often deliver voice on their own audio thread.
    /// Plain C#, with the clock passed in, so it can be tested without waiting.
    /// </summary>
    public sealed class ExternalVoiceBuffer
    {
        struct LevelEntry
        {
            public double time;
            public float rms;
        }

        readonly object gate = new object();
        readonly float[] ring;
        readonly LevelEntry[] levels;
        long written;
        int levelCount;
        int levelHead;
        int sampleRate;
        double lastSampleTime = double.NegativeInfinity;
        double lastLevelTime = double.NegativeInfinity;

        /// <param name="sampleCapacity">Samples kept (bounds the delay: 131072 holds 2.7 s at 48 kHz).</param>
        /// <param name="levelCapacity">Level reports kept.</param>
        public ExternalVoiceBuffer(int sampleCapacity = 131072, int levelCapacity = 512)
        {
            ring = new float[Mathf.Max(16, sampleCapacity)];
            levels = new LevelEntry[Mathf.Max(2, levelCapacity)];
        }

        public int SampleRate
        {
            get { lock (gate) return sampleRate; }
        }

        public void Clear()
        {
            lock (gate)
            {
                written = 0;
                levelCount = 0;
                levelHead = 0;
                sampleRate = 0;
                lastSampleTime = double.NegativeInfinity;
                lastLevelTime = double.NegativeInfinity;
            }
        }

        /// <summary>Adds PCM (interleaved if <paramref name="channels"/> &gt; 1; mixed to mono). A new sample rate starts over.</summary>
        public void PushSamples(ReadOnlySpan<float> pcm, int channels, int rate, double time)
        {
            if (rate <= 0) return;
            channels = Mathf.Max(1, channels);
            lock (gate)
            {
                if (rate != sampleRate)
                {
                    sampleRate = rate;
                    written = 0;
                }

                int frames = pcm.Length / channels;
                for (int f = 0, i = 0; f < frames; f++)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++, i++) sum += pcm[i];
                    ring[(int)(written % ring.Length)] = sum / channels;
                    written++;
                }

                lastSampleTime = time;
            }
        }

        /// <summary>Adds a linear RMS level measured at <paramref name="time"/>.</summary>
        public void PushLevel(float rms, double time)
        {
            lock (gate)
            {
                levels[levelHead] = new LevelEntry { time = time, rms = Mathf.Max(0f, rms) };
                levelHead = (levelHead + 1) % levels.Length;
                levelCount = Mathf.Min(levelCount + 1, levels.Length);
                lastLevelTime = time;
            }
        }

        /// <summary>
        /// Writes what was heard <paramref name="delay"/> seconds before <paramref name="now"/>. Samples play back in
        /// real time behind the newest one; a gap up to <paramref name="timeout"/> holds the newest window (network
        /// jitter), after that the window slides into silence. A level older than the timeout is silence too.
        /// Samples win over levels when both arrive.
        /// </summary>
        public void Read(VoiceSignal signal, double now, float delay, float timeout)
        {
            delay = Mathf.Max(0f, delay);
            timeout = Mathf.Max(0f, timeout);
            lock (gate)
            {
                if (written > 0 && sampleRate > 0 && ReadSamples(signal, now, delay, timeout)) return;
                if (levelCount == 0 || lastLevelTime < lastSampleTime && written > 0) return;

                // Newest report not newer than the moment being shown.
                double heardAt = now - delay;
                for (int n = 1; n <= levelCount; n++)
                {
                    LevelEntry e = levels[(levelHead - n + levels.Length) % levels.Length];
                    if (e.time > heardAt + 1e-9) continue;
                    if (heardAt - e.time <= timeout) signal.SetLevel(e.rms);
                    return;
                }
            }
        }

        bool ReadSamples(VoiceSignal signal, double now, float delay, float timeout)
        {
            // How far the moment being shown is past the newest sample.
            double late = now - lastSampleTime - delay;
            double offset = late <= 0 ? late : late <= timeout ? 0 : late - timeout;
            long end = written + (long)Math.Round(offset * sampleRate);
            int count = signal.Capacity;
            long first = end - count;
            long oldest = Math.Max(0, written - ring.Length);
            if (first >= written) return false; // the speaker stopped: silence
            if (end <= oldest) return false;

            var mono = signal.BeginSamples(count, sampleRate);
            for (int i = 0; i < count; i++)
            {
                long s = first + i;
                mono[i] = s < oldest || s >= written ? 0f : ring[(int)(s % ring.Length)];
            }

            return true;
        }
    }
}
