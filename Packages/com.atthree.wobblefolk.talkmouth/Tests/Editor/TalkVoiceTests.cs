using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class TalkVoiceTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();

        sealed class FakeSource : IVoiceSignalSource
        {
            public float rms;
            public void Read(VoiceSignal signal)
            {
                if (rms > 0f) signal.SetLevel(rms);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        TalkVoice NewVoice(out FakeSource source)
        {
            var go = new GameObject("Voice");
            created.Add(go);
            TalkVoice voice = go.AddComponent<TalkVoice>();
            source = new FakeSource();
            voice.SetSource(source);
            return voice;
        }

        static void Run(TalkVoice voice, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) voice.Tick(Dt);
        }

        [Test]
        public void Events_MarkTheStartAndEndOfSpeech()
        {
            TalkVoice voice = NewVoice(out FakeSource source);
            int started = 0, ended = 0, levels = 0, unityStarted = 0;
            voice.OnSpeechStarted += () => started++;
            voice.OnSpeechEnded += () => ended++;
            voice.OnSpeechLevel += _ => levels++;
            voice.SpeechStarted.AddListener(() => unityStarted++);

            Run(voice, 0.5f);
            Assert.AreEqual(0, started);

            source.rms = LevelMeter.FromDecibels(-16f);
            Run(voice, 0.5f);
            Assert.IsTrue(voice.IsSpeaking);
            Assert.Greater(voice.State.openness, 0.5f);

            source.rms = 0f;
            Run(voice, 1f);
            Assert.AreEqual(1, started);
            Assert.AreEqual(1, unityStarted);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(Mathf.CeilToInt(2f / Dt), levels, 3, "the level event fires every frame");
        }

        [Test]
        public void SetEnabledFalse_ClosesTheMouthSmoothly()
        {
            TalkVoice voice = NewVoice(out FakeSource source);
            source.rms = LevelMeter.FromDecibels(-16f);
            Run(voice, 0.5f);
            float open = voice.State.openness;

            voice.SetEnabled(false);
            voice.Tick(Dt);
            Assert.Greater(voice.State.openness, 0f, "not snapped shut");
            Assert.Less(voice.State.openness, open);

            Run(voice, 1f);
            Assert.AreEqual(0f, voice.State.openness);
            Assert.IsFalse(voice.IsSpeaking);

            voice.SetEnabled(true);
            Run(voice, 0.5f);
            Assert.IsTrue(voice.IsSpeaking);
        }

        [Test]
        public void MissingProfile_UsesTheHumanPreset()
        {
            TalkVoice voice = NewVoice(out _);
            Assert.IsNotNull(voice.Profile);
            Assert.AreEqual(new LevelSettings().gateThresholdDb, voice.Profile.level.gateThresholdDb);
        }

        [Test]
        public void TextSource_DrivesTheVoice()
        {
            TalkVoice voice = NewVoice(out _);
            TextDrivenSignal text = voice.gameObject.AddComponent<TextDrivenSignal>();
            voice.SetSource(text);
            int finished = 0;
            text.Finished += () => finished++;

            text.Say("aaaaaaaaaaaaaa");
            for (int i = 0; i < 30; i++)
            {
                text.Tick(Dt);
                voice.Tick(Dt);
            }

            Assert.IsTrue(voice.IsSpeaking);
            Assert.AreEqual(Viseme.AA, voice.State.Dominant);

            for (int i = 0; i < 180; i++)
            {
                text.Tick(Dt);
                voice.Tick(Dt);
            }

            Assert.AreEqual(1, finished);
            Assert.IsFalse(text.IsSaying);
            Assert.AreEqual(0f, voice.State.openness);
        }
    }
}
