using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Follows who is speaking near a listener: the loudest speaking voice in range, held for a minimum time so the
    /// attention does not flick between people talking over each other. For listeners that turn to the speaker
    /// (look-at, camera framing, subtitles). Plain C#, so it is cheap and testable.
    /// </summary>
    public sealed class SpeakerTracker
    {
        /// <summary>Only voices this close (m) count.</summary>
        public float range = 12f;
        /// <summary>Keep a speaker at least this long (s) before switching to a louder one.</summary>
        public float minHold = 0.8f;
        /// <summary>How much louder (0–1 level) another voice must be to take over after the hold.</summary>
        public float switchMargin = 0.25f;

        double since;

        /// <summary>The speaker followed now, or null.</summary>
        public TalkVoice Current { get; private set; }

        public void Reset() => Current = null;

        /// <summary>Picks the speaker for a listener at <paramref name="position"/> from all enabled voices.</summary>
        public TalkVoice Update(Vector3 position, double now, TalkVoice exclude = null) =>
            Update(position, now, TalkVoice.All, exclude);

        public TalkVoice Update(Vector3 position, double now, IReadOnlyList<TalkVoice> voices, TalkVoice exclude = null)
        {
            TalkVoice loudest = Loudest(position, voices, exclude, range);
            bool currentValid = Current != null && Current != exclude && Current.enabled && Current.gameObject.activeInHierarchy && Current.IsSpeaking &&
                                (Current.SpeakerPoint.position - position).sqrMagnitude <= range * range;

            if (!currentValid)
            {
                Switch(loudest, now);
            }
            else if (loudest != null && loudest != Current && now - since >= minHold &&
                     loudest.State.level > Current.State.level + switchMargin)
            {
                Switch(loudest, now);
            }

            return Current;
        }

        void Switch(TalkVoice next, double now)
        {
            if (next != Current) since = now;
            Current = next;
        }

        /// <summary>The loudest speaking voice within <paramref name="maxDistance"/>, or null.</summary>
        public static TalkVoice Loudest(Vector3 position, IReadOnlyList<TalkVoice> voices, TalkVoice exclude, float maxDistance)
        {
            TalkVoice best = null;
            float bestLevel = -1f;
            float maxSqr = maxDistance * maxDistance;
            for (int i = 0; i < voices.Count; i++)
            {
                TalkVoice v = voices[i];
                if (v == null || v == exclude || !v.IsSpeaking) continue;
                if ((v.SpeakerPoint.position - position).sqrMagnitude > maxSqr) continue;
                if (v.State.level > bestLevel)
                {
                    best = v;
                    bestLevel = v.State.level;
                }
            }

            return best;
        }
    }
}
