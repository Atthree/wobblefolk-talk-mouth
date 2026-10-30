using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class SpeakerTrackerTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();
        readonly List<TalkVoice> voices = new List<TalkVoice>();
        readonly List<Source> sources = new List<Source>();

        sealed class Source : IVoiceSignalSource
        {
            public float level;
            public void Read(VoiceSignal signal)
            {
                if (level > 0f) signal.SetNormalized(level);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            voices.Clear();
            sources.Clear();
        }

        void AddVoice(Vector3 position)
        {
            var go = new GameObject("Voice " + voices.Count);
            created.Add(go);
            go.transform.position = position;
            var voice = go.AddComponent<TalkVoice>();
            var source = new Source();
            voice.SetSource(source);
            voices.Add(voice);
            sources.Add(source);
        }

        void Run(SpeakerTracker tracker, ref double now, float seconds, TalkVoice exclude = null)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                now += Dt;
                foreach (TalkVoice v in voices) v.Tick(Dt);
                tracker.Update(Vector3.zero, now, voices, exclude);
            }
        }

        [Test]
        public void FollowsTheLoudestAndHoldsThroughOverlaps()
        {
            AddVoice(new Vector3(1f, 0f, 0f));
            AddVoice(new Vector3(-1f, 0f, 0f));
            AddVoice(new Vector3(0f, 0f, 30f)); // out of range
            var tracker = new SpeakerTracker { range = 10f, minHold = 0.8f, switchMargin = 0.25f };
            double now = 0;

            Run(tracker, ref now, 0.5f);
            Assert.IsNull(tracker.Current, "nobody speaks");

            sources[2].level = 1f;
            Run(tracker, ref now, 0.5f);
            Assert.IsNull(tracker.Current, "too far away");

            sources[0].level = 0.4f;
            Run(tracker, ref now, 0.3f);
            Assert.AreSame(voices[0], tracker.Current);

            sources[1].level = 1f;
            Run(tracker, ref now, 0.2f);
            Assert.AreSame(voices[0], tracker.Current, "held: no flicking");

            Run(tracker, ref now, 0.6f);
            Assert.AreSame(voices[1], tracker.Current, "a much louder voice takes over after the hold");

            sources[1].level = 0f;
            Run(tracker, ref now, 1f);
            Assert.AreSame(voices[0], tracker.Current, "back to the one still talking");

            sources[0].level = 0f;
            Run(tracker, ref now, 1f);
            Assert.IsNull(tracker.Current);
        }

        [Test]
        public void NeverPicksTheListenersOwnVoice()
        {
            AddVoice(Vector3.zero);
            var tracker = new SpeakerTracker();
            double now = 0;
            sources[0].level = 1f;
            Run(tracker, ref now, 0.5f, voices[0]);
            Assert.IsNull(tracker.Current);
        }

        [Test]
        public void VoiceSpeakerPointDefaultsToItself()
        {
            AddVoice(Vector3.one);
            Assert.AreSame(voices[0].transform, voices[0].SpeakerPoint);
            var head = new GameObject("Head").transform;
            created.Add(head.gameObject);
            voices[0].SpeakerPoint = head;
            Assert.AreSame(head, voices[0].SpeakerPoint);
        }
    }
}
