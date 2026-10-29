using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>What a driver applies: the mouth opening and shape at one moment.</summary>
    public readonly struct MouthSample
    {
        public readonly float openness;
        public readonly VisemeWeights visemes;
        /// <summary>Speaking, including short pauses between words.</summary>
        public readonly bool speaking;

        public MouthSample(float openness, in VisemeWeights visemes, bool speaking)
        {
            this.openness = openness;
            this.visemes = visemes;
            this.speaking = speaking;
        }

        public static MouthSample Closed => new MouthSample(0f, VisemeWeights.Rest, false);
    }

    /// <summary>
    /// The last second or so of mouth states, so a driver (or one part of it) can lag behind the voice: a hat that
    /// follows the head a moment later, a voice chat mouth that waits for the audio. Fixed size, never allocates.
    /// </summary>
    public sealed class MouthHistory
    {
        struct Entry
        {
            public double time;
            public MouthSample sample;
        }

        readonly Entry[] entries;
        int head;
        int count;

        public MouthHistory(int capacity = 256) => entries = new Entry[Mathf.Max(2, capacity)];

        public int Count => count;

        public void Clear()
        {
            head = 0;
            count = 0;
        }

        public void Push(double time, in MouthSample sample)
        {
            entries[head] = new Entry { time = time, sample = sample };
            head = (head + 1) % entries.Length;
            count = Mathf.Min(count + 1, entries.Length);
        }

        /// <summary>The state <paramref name="delay"/> seconds before the newest one, interpolated between frames.</summary>
        public MouthSample Sample(float delay)
        {
            if (count == 0) return MouthSample.Closed;
            Entry newest = At(0);
            if (delay <= 0f || count == 1) return newest.sample;

            double time = newest.time - delay;
            for (int n = 1; n < count; n++)
            {
                Entry older = At(n);
                if (older.time > time) continue;

                Entry newer = At(n - 1);
                double span = newer.time - older.time;
                float t = span > 1e-9 ? (float)((time - older.time) / span) : 1f;
                return new MouthSample(
                    Mathf.Lerp(older.sample.openness, newer.sample.openness, t),
                    VisemeWeights.Lerp(older.sample.visemes, newer.sample.visemes, t),
                    t < 0.5f ? older.sample.speaking : newer.sample.speaking);
            }

            // Before the first entry the mouth was at rest; once the history is full, the oldest is the best guess.
            return count < entries.Length ? MouthSample.Closed : At(count - 1).sample;
        }

        Entry At(int age) => entries[(head - 1 - age + entries.Length * 2) % entries.Length];
    }
}
