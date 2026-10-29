using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class SignalTests
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

        [Test]
        public void Downmix_AveragesChannels()
        {
            float[] stereo = { 1f, 0f, 0.5f, 0.5f, -1f, 1f };
            var mono = new float[3];
            SampleMixer.Downmix(stereo, 2, mono);
            Assert.AreEqual(new[] { 0.5f, 0.5f, 0f }, mono);

            SampleMixer.Downmix(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, 1, mono);
            Assert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, mono);
        }

        [Test]
        public void Window_EndsAtThePlayhead()
        {
            Assert.IsTrue(SampleMixer.TryGetWindow(5000, 1024, 48000, false, out int start));
            Assert.AreEqual(5000 - 1024, start);

            Assert.IsTrue(SampleMixer.TryGetWindow(100, 1024, 48000, false, out start));
            Assert.AreEqual(0, start, "a clip that just started reads from its beginning");

            Assert.IsTrue(SampleMixer.TryGetWindow(100, 1024, 48000, true, out start));
            Assert.AreEqual(48000 - 924, start, "a looping clip wraps around");

            Assert.IsFalse(SampleMixer.TryGetWindow(48001, 1024, 48000, false, out _), "past the end is silence");
            Assert.IsFalse(SampleMixer.TryGetWindow(0, 1024, 512, false, out _), "longer than the clip");
            Assert.AreEqual(512, SampleMixer.WindowFrames(1024, 512));
        }

        [Test]
        public void VoiceSignal_ClampsAndClears()
        {
            var signal = new VoiceSignal(256);
            Assert.AreEqual(256, signal.BeginSamples(1000, 48000).Length);
            Assert.AreEqual(VoiceSignalKind.Samples, signal.Kind);

            signal.SetNormalized(2f, Viseme.EE);
            Assert.AreEqual(1f, signal.Level);
            Assert.IsTrue(signal.HasViseme);
            Assert.AreEqual(0, signal.SampleCount);

            signal.SetNormalized(0.5f, Viseme.Closed);
            Assert.IsFalse(signal.HasViseme, "closed is no shape hint");

            signal.Clear();
            Assert.AreEqual(VoiceSignalKind.Silent, signal.Kind);
        }

        static MouthState Settle(TalkAnalyzer analyzer, VoiceSignal signal, TalkMouthProfile p, float seconds = 1f)
        {
            for (float t = 0f; t < seconds; t += Dt) analyzer.Process(signal, p, Dt);
            return analyzer.State;
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        public void NormalizedLevel_OpensTheMouthByThatMuch(float level, float expected)
        {
            TalkMouthProfile p = Profile();
            var signal = new VoiceSignal(16);
            var analyzer = new TalkAnalyzer();

            signal.SetNormalized(1f);
            Settle(analyzer, signal, p, 0.2f); // open the gate first, as speech would
            signal.SetNormalized(level);
            Assert.AreEqual(expected, Settle(analyzer, signal, p).openness, 0.02f);
        }

        [Test]
        public void NormalizedLevel_IgnoresTheInputGain()
        {
            TalkMouthProfile p = Profile();
            p.level.inputGainDb = 20f;
            var signal = new VoiceSignal(16);
            signal.SetNormalized(0.5f);
            var analyzer = new TalkAnalyzer();
            signal.SetNormalized(1f);
            Settle(analyzer, signal, p, 0.2f);
            signal.SetNormalized(0.5f);
            Assert.AreEqual(0.5f, Settle(analyzer, signal, p).openness, 0.02f);
        }

        [TestCase(Viseme.AA)]
        [TestCase(Viseme.OH)]
        [TestCase(Viseme.EE)]
        [TestCase(Viseme.FF)]
        public void ShapeHint_BecomesTheMouthShape(Viseme shape)
        {
            TalkMouthProfile p = Profile();
            var signal = new VoiceSignal(16);
            signal.SetNormalized(1f, shape);
            Assert.AreEqual(shape, Settle(new TalkAnalyzer(), signal, p).Dominant);
        }

        [Test]
        public void ShapeHint_IsIgnoredInAmplitudeMode()
        {
            TalkMouthProfile p = Profile(TalkMouthPreset.Robot);
            var signal = new VoiceSignal(16);
            signal.SetNormalized(1f, Viseme.EE);
            MouthState state = Settle(new TalkAnalyzer(), signal, p);
            Assert.AreEqual(0f, state.visemes.ee);
            Assert.Greater(state.openness, 0.5f);
        }

        [Test]
        public void SilentSignal_ClosesTheMouth()
        {
            TalkMouthProfile p = Profile();
            var signal = new VoiceSignal(16);
            var analyzer = new TalkAnalyzer();
            signal.SetLevel(LevelMeter.FromDecibels(-16f));
            Assert.Greater(Settle(analyzer, signal, p, 0.5f).openness, 0.5f);

            signal.Clear();
            MouthState state = Settle(analyzer, signal, p);
            Assert.AreEqual(0f, state.openness);
            Assert.IsFalse(state.isSpeaking);
        }

        [Test]
        public void SampleSignal_IsAnalysedLikeSamples()
        {
            TalkMouthProfile p = Profile();
            var signal = new VoiceSignal(TestSignals.Frame);
            var buffer = new float[TestSignals.Frame];
            TestSignals.Sine(buffer, 2600f, -16f, 0);
            new System.Span<float>(buffer).CopyTo(signal.BeginSamples(buffer.Length, TestSignals.Rate));

            MouthState state = Settle(new TalkAnalyzer(), signal, p, 0.5f);
            Assert.AreEqual(Viseme.EE, state.Dominant);
        }
    }
}
