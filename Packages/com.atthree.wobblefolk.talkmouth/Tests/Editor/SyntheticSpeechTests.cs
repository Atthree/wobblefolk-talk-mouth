using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class SyntheticSpeechTests
    {
        [Test]
        public void Babble_SoundsLikeSpeechToTheAnalyzer()
        {
            const int rate = TestSignals.Rate;
            var speech = new float[rate * 6];
            SyntheticSpeech.Render(speech, rate, 7, 140f, 0.5f);

            float peak = 0f;
            foreach (float s in speech) peak = Mathf.Max(peak, Mathf.Abs(s));
            Assert.LessOrEqual(peak, 0.5f + 1e-4f);
            Assert.Greater(peak, 0.3f);

            TalkMouthProfile profile = TestSignals.Profile();
            try
            {
                var analyzer = new TalkAnalyzer(TestSignals.Frame);
                int hop = rate / 60;
                int starts = 0;
                bool speaking = false;
                var shapes = new HashSet<Viseme>();
                float maxOpen = 0f;
                for (int end = TestSignals.Frame; end <= speech.Length; end += hop)
                {
                    var window = new System.ReadOnlySpan<float>(speech, end - TestSignals.Frame, TestSignals.Frame);
                    MouthState state = analyzer.Process(window, rate, profile, 1f / 60f);
                    if (state.isSpeaking && !speaking) starts++;
                    speaking = state.isSpeaking;
                    if (state.openness > 0.3f) shapes.Add(state.Dominant);
                    maxOpen = Mathf.Max(maxOpen, state.openness);
                }

                Assert.GreaterOrEqual(starts, 2, "phrases with pauses between them");
                Assert.GreaterOrEqual(shapes.Count, 2, "more than one mouth shape");
                Assert.Greater(maxOpen, 0.7f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Babble_IsTheSameForTheSameSeed()
        {
            var a = new float[48000];
            var b = new float[48000];
            var c = new float[48000];
            SyntheticSpeech.Render(a, 48000, 3);
            SyntheticSpeech.Render(b, 48000, 3);
            SyntheticSpeech.Render(c, 48000, 4);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void Clip_IsReadable()
        {
            AudioClip clip = SyntheticSpeech.CreateClip("Babble", 1f, 44100, 2);
            try
            {
                Assert.AreEqual(44100, clip.samples);
                var data = new float[clip.samples];
                Assert.IsTrue(clip.GetData(data, 0));
                float energy = 0f;
                foreach (float s in data) energy += s * s;
                Assert.Greater(energy, 0f);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
