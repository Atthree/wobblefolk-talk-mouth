using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class TextAndSynthTests
    {
        const float Dt = 1f / 240f;

        static TextRhythmSettings Steady() => new TextRhythmSettings { variation = 0f };

        static float Duration(string text, TextRhythmSettings settings)
        {
            var rhythm = new TextRhythm();
            rhythm.Start(text);
            float time = 0f;
            while (rhythm.IsPlaying && time < 60f)
            {
                rhythm.Advance(Dt, settings);
                time += Dt;
            }

            return time;
        }

        [TestCase('a', TextCharClass.Vowel, Viseme.AA)]
        [TestCase('O', TextCharClass.Vowel, Viseme.OH)]
        [TestCase('ü', TextCharClass.Vowel, Viseme.OH)]
        [TestCase('i', TextCharClass.Vowel, Viseme.EE)]
        [TestCase('ı', TextCharClass.Vowel, Viseme.EE)]
        [TestCase('İ', TextCharClass.Vowel, Viseme.EE)]
        [TestCase('ş', TextCharClass.Fricative, Viseme.Closed)]
        [TestCase('S', TextCharClass.Fricative, Viseme.Closed)]
        [TestCase('m', TextCharClass.Lips, Viseme.Closed)]
        [TestCase('t', TextCharClass.Consonant, Viseme.Closed)]
        [TestCase(' ', TextCharClass.Space, Viseme.Closed)]
        [TestCase(',', TextCharClass.Comma, Viseme.Closed)]
        [TestCase('?', TextCharClass.Sentence, Viseme.Closed)]
        [TestCase('"', TextCharClass.Silent, Viseme.Closed)]
        public void Characters_AreClassified(char c, TextCharClass type, Viseme viseme)
        {
            TextRhythm.Classify(c, out TextCharClass actualType, out Viseme actualViseme);
            Assert.AreEqual(type, actualType);
            Assert.AreEqual(viseme, actualViseme);
        }

        [Test]
        public void OtherScripts_CountAsSyllables()
        {
            TextRhythm.Classify('日', out TextCharClass type, out Viseme viseme);
            Assert.AreEqual(TextCharClass.Vowel, type);
            Assert.AreNotEqual(Viseme.Closed, viseme);
        }

        [Test]
        public void Letters_SetLevelAndShape()
        {
            TextRhythmSettings s = Steady();
            var rhythm = new TextRhythm();
            var levels = new List<float>();
            var shapes = new List<Viseme>();
            rhythm.Start("tamos");
            while (rhythm.IsPlaying)
            {
                rhythm.Advance(Dt, s, _ =>
                {
                    levels.Add(rhythm.Level);
                    shapes.Add(rhythm.Viseme);
                });
            }

            // t: consonant before any vowel (default AA), a: open, m: lips shut, o: rounded, s: hiss.
            Assert.AreEqual(new[] { s.consonantLevel, s.vowelLevel, 0f, s.vowelLevel, s.consonantLevel }, levels);
            Assert.AreEqual(new[] { Viseme.AA, Viseme.AA, Viseme.Closed, Viseme.OH, Viseme.FF }, shapes);
        }

        [Test]
        public void Punctuation_Pauses()
        {
            TextRhythmSettings s = Steady();
            float plain = Duration("aa aa", s);
            Assert.AreEqual(5f / s.charactersPerSecond, plain, 2 * Dt);
            Assert.AreEqual(plain + s.commaPause, Duration("aa,aa", s), 2 * Dt);
            Assert.AreEqual(plain + s.sentencePause, Duration("aa.aa", s), 2 * Dt);
            Assert.AreEqual(plain + s.lineBreakPause, Duration("aa\naa", s), 2 * Dt);
        }

        [Test]
        public void RepeatedMarks_PauseOnce()
        {
            TextRhythmSettings s = Steady();
            float step = 1f / s.charactersPerSecond;
            Assert.AreEqual(Duration("a.", s) + 2 * step, Duration("a...", s), 2 * Dt);
        }

        [Test]
        public void RichTextTags_TakeNoTime()
        {
            TextRhythmSettings s = Steady();
            var rhythm = new TextRhythm();
            var spoken = new List<int>();
            rhythm.Start("<b>a</b>");
            rhythm.Advance(10f, s, spoken.Add);

            Assert.AreEqual(new[] { 3 }, spoken);
            Assert.AreEqual(Duration("a", s), Duration("<b>a</b>", s), 2 * Dt);
        }

        [Test]
        public void ALongFrame_StillReportsEveryCharacter()
        {
            var rhythm = new TextRhythm();
            var spoken = new List<int>();
            rhythm.Start("hello");
            int started = rhythm.Advance(10f, Steady(), spoken.Add);

            Assert.AreEqual(5, started);
            Assert.AreEqual(new[] { 0, 1, 2, 3, 4 }, spoken);
            Assert.IsFalse(rhythm.IsPlaying);
            Assert.AreEqual(0f, rhythm.Level);
        }

        [Test]
        public void Variation_IsDeterministicAndBounded()
        {
            var s = new TextRhythmSettings { variation = 0.4f };
            float[] Levels(uint seed)
            {
                var rhythm = new TextRhythm();
                var result = new List<float>();
                rhythm.Start("aaaaaaaa", seed);
                rhythm.Advance(10f, s, _ => result.Add(rhythm.Level));
                return result.ToArray();
            }

            Assert.AreEqual(Levels(7), Levels(7));
            Assert.AreNotEqual(Levels(7), Levels(8));
            foreach (float l in Levels(7)) Assert.That(l, NUnit.Framework.Is.InRange(0.6f, 1f));
        }

        [TestCase(Viseme.AA)]
        [TestCase(Viseme.OH)]
        [TestCase(Viseme.EE)]
        [TestCase(Viseme.FF)]
        public void SynthesizedShapes_AreRecognised(Viseme shape)
        {
            var settings = new VisemeSettings();
            var sound = new float[4800];
            FormantSynth.Render(sound, TestSignals.Rate, shape, 140f, 0.5f);

            var analyzer = new SpectrumAnalyzer(1024);
            BandEnergies bands = analyzer.Analyze(new System.ReadOnlySpan<float>(sound, 1800, 1024), TestSignals.Rate, settings);
            VisemeWeights weights = VisemeClassifier.Classify(bands, settings, Viseme.Closed);
            Assert.AreEqual(shape, weights.Dominant, bands.Shares.ToString());
        }

        [Test]
        public void Synth_HitsThePeakAndFades()
        {
            var sound = new float[2400];
            FormantSynth.Render(sound, TestSignals.Rate, Viseme.AA, 220f, 0.8f);

            float peak = 0f;
            foreach (float x in sound) peak = Mathf.Max(peak, Mathf.Abs(x));
            Assert.AreEqual(0.8f, peak, 0.01f);
            Assert.AreEqual(0f, sound[0]);
            Assert.AreEqual(0f, sound[sound.Length - 1], 1e-6f);
        }

        [Test]
        public void TextAndSynth_DoNotAllocate()
        {
            var sound = new float[1024];
            var rhythm = new TextRhythm();
            TextRhythmSettings s = Steady();
            rhythm.Start("Merhaba, dünya!");
            rhythm.Advance(Dt, s);

            Assert.That(() =>
            {
                FormantSynth.Render(sound, TestSignals.Rate, Viseme.OH, 180f, 0.5f);
                FormantSynth.Render(sound, TestSignals.Rate, Viseme.FF, 180f, 0.5f);
                for (int i = 0; i < 20; i++) rhythm.Advance(Dt, s);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
