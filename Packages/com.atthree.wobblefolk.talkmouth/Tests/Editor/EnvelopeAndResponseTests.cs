using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class EnvelopeAndResponseTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        TalkMouthProfile Profile(TalkMouthPreset preset = TalkMouthPreset.Human)
        {
            TalkMouthProfile p = TestSignals.Profile(preset);
            created.Add(p);
            return p;
        }

        static float Rms(float db) => LevelMeter.FromDecibels(db);

        [Test]
        public void Silence_KeepsTheMouthClosed()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            for (int i = 0; i < 120; i++) analyzer.ProcessLevel(0f, p, Dt);

            Assert.AreEqual(0f, analyzer.State.openness);
            Assert.AreEqual(Viseme.Closed, analyzer.State.Dominant);
        }

        [Test]
        public void Attack_IsFasterThanRelease()
        {
            TalkMouthProfile p = Profile();
            p.envelope.attackTime = 0.02f;
            p.envelope.releaseTime = 0.15f;
            p.envelope.closeTime = 0.15f;
            var analyzer = new TalkAnalyzer();

            int opening = 0;
            while (analyzer.ProcessLevel(Rms(p.level.fullLevelDb), p, Dt).openness < 0.9f) opening++;
            for (int i = 0; i < 30; i++) analyzer.ProcessLevel(Rms(p.level.fullLevelDb), p, Dt);

            int closing = 0;
            while (analyzer.ProcessLevel(0f, p, Dt).openness > 0.1f) closing++;

            Assert.Less(opening, closing);
        }

        [Test]
        public void Envelope_IsFrameRateIndependent()
        {
            float OpennessAt(float time, float dt)
            {
                TalkMouthProfile p = Profile();
                var analyzer = new TalkAnalyzer();
                int steps = Mathf.RoundToInt(time / dt);
                for (int i = 0; i < steps; i++)
                {
                    float t = i * dt;
                    analyzer.ProcessLevel(t < 0.5f - 1e-4f ? Rms(-20f) : 0f, p, dt);
                }

                return analyzer.State.openness;
            }

            Assert.AreEqual(OpennessAt(0.1f, 1f / 240f), OpennessAt(0.1f, 1f / 30f), 0.02f, "while opening");
            Assert.AreEqual(OpennessAt(0.6f, 1f / 240f), OpennessAt(0.6f, 1f / 30f), 0.02f, "while closing");
        }

        [Test]
        public void Mouth_ClosesFullyAfterSpeechEnds()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            for (int i = 0; i < 30; i++) analyzer.ProcessLevel(Rms(-16f), p, Dt);
            Assert.Greater(analyzer.State.openness, 0.5f);

            for (int i = 0; i < 120; i++) analyzer.ProcessLevel(0f, p, Dt);
            Assert.AreEqual(0f, analyzer.State.openness);
            Assert.IsFalse(analyzer.State.isSpeaking);
        }

        [Test]
        public void LouderSpeech_OpensWider()
        {
            TalkMouthProfile p = Profile();
            float Open(float db)
            {
                var analyzer = new TalkAnalyzer();
                for (int i = 0; i < 60; i++) analyzer.ProcessLevel(Rms(db), p, Dt);
                return analyzer.State.openness;
            }

            Assert.Less(Open(-36f), Open(-26f));
            Assert.Less(Open(-26f), Open(-16f));
            Assert.AreEqual(1f, Open(-6f), 1e-3f, "above the full level the mouth is fully open");
        }

        [Test]
        public void DefaultCurve_IsLinear()
        {
            var settings = new ResponseSettings();
            Assert.AreEqual(0.3f, LevelResponse.Apply(0.3f, settings), 1e-4f);
            Assert.AreEqual(0.3f, LevelResponse.Apply(0.3f, null, 1f, 0, 1f), 1e-4f, "no curve = linear");
        }

        [Test]
        public void Exaggeration_ScalesAndClamps()
        {
            Assert.AreEqual(0.6f, LevelResponse.Apply(0.3f, null, 2f, 0, 1f), 1e-4f);
            Assert.AreEqual(1f, LevelResponse.Apply(0.8f, null, 2f, 0, 1f), 1e-4f);
            Assert.AreEqual(0.5f, LevelResponse.Apply(0.8f, null, 2f, 0, 0.5f), 1e-4f, "max openness limits");
        }

        [Test]
        public void EaseOutCurve_MakesQuietSpeechVisible()
        {
            AnimationCurve curve = TalkMouthPresets.EaseOut();
            Assert.Greater(LevelResponse.Apply(0.2f, curve, 1f, 0, 1f), 0.3f);
            Assert.AreEqual(1f, LevelResponse.Apply(1f, curve, 1f, 0, 1f), 1e-4f);
        }

        [Test]
        public void OneStep_IsOpenOrClosed()
        {
            for (float x = 0f; x <= 1f; x += 0.05f)
            {
                float y = LevelResponse.Apply(x, null, 1f, 1, 1f);
                Assert.IsTrue(y == 0f || y == 1f, $"{x} → {y}");
            }
        }

        [Test]
        public void SplitHeadPreset_ReactsToWhispers()
        {
            float Open(TalkMouthPreset preset)
            {
                TalkMouthProfile p = Profile(preset);
                var analyzer = new TalkAnalyzer();
                for (int i = 0; i < 30; i++) analyzer.ProcessLevel(0f, p, Dt);
                for (int i = 0; i < 30; i++) analyzer.ProcessLevel(Rms(-48f), p, Dt);
                return analyzer.State.openness;
            }

            Assert.AreEqual(0f, Open(TalkMouthPreset.Human), "a human mouth ignores a whisper");
            Assert.Greater(Open(TalkMouthPreset.BouncySplitHead), 0.3f, "the split head opens visibly");
        }

        [Test]
        public void Presets_AreDistinct()
        {
            TalkMouthProfile robot = Profile(TalkMouthPreset.Robot);
            Assert.AreEqual(1, robot.response.steps);
            Assert.AreEqual(AnalysisMode.Amplitude, robot.visemes.mode);

            TalkMouthProfile split = Profile(TalkMouthPreset.BouncySplitHead);
            Assert.IsTrue(split.level.adaptive);
            Assert.AreEqual(AnalysisMode.Amplitude, split.visemes.mode);
            Assert.Greater(split.spring.overshoot, Profile(TalkMouthPreset.Human).spring.overshoot);

            TalkMouthProfile monster = Profile(TalkMouthPreset.Monster);
            Assert.Greater(monster.envelope.attackTime, Profile(TalkMouthPreset.Bird).envelope.attackTime);

            // Applying a preset resets everything else.
            robot.level.inputGainDb = 12f;
            TalkMouthPresets.Apply(robot, TalkMouthPreset.Human);
            Assert.AreEqual(0f, robot.level.inputGainDb);
            Assert.AreEqual(0, robot.response.steps);
        }
    }
}
