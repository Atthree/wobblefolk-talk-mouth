using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class LevelAndGateTests
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
        public void Rms_OfASine_IsTheAmplitudeOverRootTwo()
        {
            var buffer = new float[4800];
            float amplitude = 0.5f;
            for (int i = 0; i < buffer.Length; i++) buffer[i] = amplitude * Mathf.Sin(2f * Mathf.PI * 440f * i / 48000f);
            Assert.AreEqual(amplitude / Mathf.Sqrt(2f), LevelMeter.Rms(buffer), 1e-3f);
            Assert.AreEqual(0f, LevelMeter.Rms(new float[0]));
        }

        [Test]
        public void Decibels_RoundTrip_AndSilenceIsTheFloor()
        {
            Assert.AreEqual(-20f, LevelMeter.ToDecibels(0.1f), 1e-4f);
            Assert.AreEqual(0.1f, LevelMeter.FromDecibels(-20f), 1e-6f);
            Assert.AreEqual(LevelMeter.MinDecibels, LevelMeter.ToDecibels(0f));
            Assert.AreEqual(0f, LevelMeter.FromDecibels(LevelMeter.MinDecibels));
        }

        [Test]
        public void Gate_StaysClosedBelowTheThreshold()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            for (int i = 0; i < 120; i++) analyzer.ProcessLevel(Rms(p.level.gateThresholdDb - 3f), p, Dt);

            Assert.IsFalse(analyzer.State.gateOpen);
            Assert.IsFalse(analyzer.State.isSpeaking);
            Assert.AreEqual(0f, analyzer.State.openness);
        }

        [Test]
        public void Gate_ClosesOnlyBelowTheHysteresis()
        {
            TalkMouthProfile p = Profile();
            float threshold = p.level.gateThresholdDb;
            var gate = new NoiseGate();

            gate.Update(threshold + 2f, p.level, Dt);
            Assert.IsTrue(gate.IsOpen, "opens above the threshold");

            gate.Update(threshold - p.level.gateHysteresisDb + 1f, p.level, Dt);
            Assert.IsTrue(gate.IsOpen, "stays open inside the hysteresis");

            gate.Update(threshold - p.level.gateHysteresisDb - 1f, p.level, Dt);
            Assert.IsFalse(gate.IsOpen, "closes below the hysteresis");
        }

        [Test]
        public void LevelHoveringAtTheThreshold_DoesNotFlutter()
        {
            TalkMouthProfile p = Profile();
            float threshold = p.level.gateThresholdDb;
            var analyzer = new TalkAnalyzer();

            int closes = 0;
            bool wasOpen = false;
            for (int i = 0; i < 240; i++)
            {
                float db = threshold + (i % 2 == 0 ? 1f : -3f);
                bool open = analyzer.ProcessLevel(Rms(db), p, Dt).gateOpen;
                if (wasOpen && !open) closes++;
                wasOpen = open;
            }

            Assert.AreEqual(0, closes, "the gate never closes while the level hovers at the threshold");
        }

        [Test]
        public void Speaking_HoldsThroughShortPauses_AndEndsAfterTheHoldTime()
        {
            TalkMouthProfile p = Profile();
            var analyzer = new TalkAnalyzer();
            for (int i = 0; i < 30; i++) analyzer.ProcessLevel(Rms(-20f), p, Dt);

            float pause = p.level.holdTime * 0.5f;
            for (float t = 0f; t < pause; t += Dt) analyzer.ProcessLevel(0f, p, Dt);
            Assert.IsTrue(analyzer.State.isSpeaking, "a short pause is still speech");

            for (float t = 0f; t < p.level.holdTime; t += Dt) analyzer.ProcessLevel(0f, p, Dt);
            Assert.IsFalse(analyzer.State.isSpeaking, "speech ends after the hold time");
        }

        [Test]
        public void Adaptive_RaisesTheGateAboveSteadyNoise()
        {
            float noise = Rms(-35f);

            TalkMouthProfile fixedGate = Profile();
            var plain = new TalkAnalyzer();
            for (int i = 0; i < 300; i++) plain.ProcessLevel(noise, fixedGate, Dt);
            Assert.IsTrue(plain.State.gateOpen, "without adaptation the noise opens the mouth");

            TalkMouthProfile adaptive = Profile();
            adaptive.level.adaptive = true;
            var analyzer = new TalkAnalyzer();
            for (int i = 0; i < 300; i++) analyzer.ProcessLevel(noise, adaptive, Dt);
            Assert.IsFalse(analyzer.State.gateOpen, "the adaptive gate sits above the noise");

            for (int i = 0; i < 20; i++) analyzer.ProcessLevel(Rms(-12f), adaptive, Dt);
            Assert.IsTrue(analyzer.State.gateOpen, "speech over the noise still opens it");
        }

        [Test]
        public void Adaptive_LetsAQuietSpeakerOpenTheMouthWide()
        {
            float MaxOpenness(TalkMouthProfile p)
            {
                var analyzer = new TalkAnalyzer();
                for (int i = 0; i < 30; i++) analyzer.ProcessLevel(Rms(-70f), p, Dt);

                float max = 0f;
                for (int i = 0; i < 60 * 20; i++)
                {
                    bool word = i % 24 < 18; // 0.3 s words, 0.1 s gaps
                    MouthState s = analyzer.ProcessLevel(Rms(word ? -38f : -70f), p, Dt);
                    if (i > 60 * 19) max = Mathf.Max(max, s.openness);
                }

                return max;
            }

            TalkMouthProfile plain = Profile();
            TalkMouthProfile adaptive = Profile();
            adaptive.level.adaptive = true;

            float without = MaxOpenness(plain);
            float with = MaxOpenness(adaptive);
            Assert.Less(without, 0.5f, "a quiet voice barely opens a fixed gate");
            Assert.Greater(with, 0.85f, "the adaptive full level follows the quiet voice");
        }
    }
}
