using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class VisemeTests
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

        static MouthState Speak(TalkAnalyzer analyzer, TalkMouthProfile p, float hz, float db, float seconds)
        {
            var buffer = new float[TestSignals.Frame];
            int samplesPerFrame = Mathf.RoundToInt(TestSignals.Rate * Dt);
            int position = 0;
            for (float t = 0f; t < seconds; t += Dt)
            {
                TestSignals.Sine(buffer, hz, db, position);
                position += samplesPerFrame;
                analyzer.Process(buffer, TestSignals.Rate, p, Dt);
            }

            return analyzer.State;
        }

        [TestCase(300f, Viseme.OH)]
        [TestCase(1200f, Viseme.AA)]
        [TestCase(2600f, Viseme.EE)]
        [TestCase(6000f, Viseme.FF)]
        public void Tone_IsClassifiedByItsBand(float hz, Viseme expected)
        {
            TalkMouthProfile p = Profile();
            MouthState state = Speak(new TalkAnalyzer(), p, hz, -16f, 0.5f);
            Assert.AreEqual(expected, state.Dominant, state.ToString());
            Assert.AreEqual(1f, state.visemes.Sum, 1e-3f, "weights sum to 1");
        }

        [Test]
        public void Spectrum_PutsAToneInItsBand()
        {
            var settings = new VisemeSettings();
            var analyzer = new SpectrumAnalyzer(1024);
            var buffer = new float[1024];
            TestSignals.Sine(buffer, 1200f, -20f, 0);

            Vector4 shares = analyzer.Analyze(buffer, TestSignals.Rate, settings).Shares;
            Assert.Greater(shares.y, 0.95f, shares.ToString());
        }

        [Test]
        public void ShortBuffers_AreZeroPadded()
        {
            var settings = new VisemeSettings();
            var analyzer = new SpectrumAnalyzer(1024);
            var buffer = new float[300];
            TestSignals.Sine(buffer, 2600f, -20f, 0);

            Vector4 shares = analyzer.Analyze(buffer, TestSignals.Rate, settings).Shares;
            Assert.Greater(shares.z, 0.8f, shares.ToString());
        }

        [Test]
        public void Silence_IsClosed()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            var buffer = new float[TestSignals.Frame];
            for (int i = 0; i < 30; i++) analyzer.Process(buffer, TestSignals.Rate, p, Dt);

            Assert.AreEqual(Viseme.Closed, analyzer.State.Dominant);
            Assert.AreEqual(1f, analyzer.State.visemes.closed, 1e-4f);
        }

        [Test]
        public void AmplitudeMode_OnlyOpensAndCloses()
        {
            TalkMouthProfile p = Profile(TalkMouthPreset.Bird);
            MouthState state = Speak(new TalkAnalyzer(), p, 2600f, -16f, 0.5f);

            Assert.Greater(state.visemes.aa, 0.5f);
            Assert.AreEqual(0f, state.visemes.oh);
            Assert.AreEqual(0f, state.visemes.ee);
            Assert.AreEqual(0f, state.visemes.ff);
        }

        [Test]
        public void AllowVisemesFalse_SkipsTheSpectrum()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            var buffer = new float[TestSignals.Frame];
            for (int i = 0; i < 30; i++)
            {
                TestSignals.Sine(buffer, 2600f, -16f, i * 800);
                analyzer.Process(buffer, TestSignals.Rate, p, Dt, allowVisemes: false);
            }

            Assert.AreEqual(0f, analyzer.State.visemes.ee, "no shapes without the spectrum (e.g. far away)");
            Assert.Greater(analyzer.State.openness, 0.5f, "the mouth still moves");
        }

        [Test]
        public void CurrentShape_WinsATie()
        {
            var settings = new VisemeSettings();
            Vector4 between = (settings.aaProfile + settings.ohProfile) * 0.5f;
            var bands = new BandEnergies(between.x, between.y, between.z, between.w);

            VisemeWeights asAa = VisemeClassifier.Classify(bands, settings, Viseme.AA);
            VisemeWeights asOh = VisemeClassifier.Classify(bands, settings, Viseme.OH);
            Assert.Greater(asAa.aa, asAa.oh);
            Assert.Greater(asOh.oh, asOh.aa);
        }

        [Test]
        public void SoundBetweenTwoShapes_DoesNotFlicker()
        {
            var settings = new VisemeSettings();
            var classifier = new VisemeClassifier();
            Vector4 between = (settings.aaProfile + settings.ohProfile) * 0.5f;
            Vector4 nudge = (settings.aaProfile - settings.ohProfile) * 0.04f;

            int switches = 0;
            Viseme last = Viseme.Closed;
            for (int i = 0; i < 240; i++)
            {
                Vector4 s = i % 2 == 0 ? between + nudge : between - nudge;
                classifier.Update(new BandEnergies(s.x, s.y, s.z, s.w), 0.8f, settings, Dt);
                if (i > 30 && classifier.Dominant != last) switches++;
                last = classifier.Dominant;
            }

            Assert.LessOrEqual(switches, 1);
        }

        [Test]
        public void ShapeChanges_AreSmoothed()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            Speak(analyzer, p, 300f, -16f, 0.5f);
            float ohBefore = analyzer.State.visemes.oh;

            var buffer = new float[TestSignals.Frame];
            TestSignals.Sine(buffer, 2600f, -16f, 0);
            analyzer.Process(buffer, TestSignals.Rate, p, Dt);

            Assert.Greater(analyzer.State.visemes.oh, ohBefore * 0.5f, "one frame does not swap the shape");
            Assert.Less(analyzer.State.visemes.ee, 0.5f);
        }

        [Test]
        public void Analysis_DoesNotAllocate()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            var buffer = new float[TestSignals.Frame];
            TestSignals.Sine(buffer, 1200f, -16f, 0);
            analyzer.Process(buffer, TestSignals.Rate, p, Dt);

            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++) analyzer.Process(buffer, TestSignals.Rate, p, Dt);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
