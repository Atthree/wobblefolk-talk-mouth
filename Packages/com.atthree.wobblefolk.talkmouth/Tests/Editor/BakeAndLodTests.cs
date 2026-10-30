using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class BakeAndLodTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        T Keep<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        static float[] Speech(float seconds, uint seed = 9)
        {
            var speech = new float[Mathf.RoundToInt(seconds * TestSignals.Rate)];
            SyntheticSpeech.Render(speech, TestSignals.Rate, seed);
            return speech;
        }

        [Test]
        public void Baked_MovesTheMouthLikeLiveAnalysis()
        {
            float[] speech = Speech(4f);
            BakedVoice baked = Keep(VoiceBaker.Bake(speech, TestSignals.Rate));
            TalkMouthProfile profile = Keep(TestSignals.Profile());

            var live = new TalkAnalyzer();
            var replay = new TalkAnalyzer();
            var signal = new VoiceSignal(16);
            var window = new float[TestSignals.Frame];
            float worst = 0f;
            int sameShape = 0, frames = 0;
            for (int frame = 1; frame < baked.FrameCount - 1; frame++)
            {
                int end = Mathf.RoundToInt(frame * Dt * TestSignals.Rate);
                for (int i = 0; i < window.Length; i++)
                {
                    int s = end - window.Length + i;
                    window[i] = s >= 0 && s < speech.Length ? speech[s] : 0f;
                }

                MouthState a = live.Process(window, TestSignals.Rate, profile, Dt);
                Assert.IsTrue(baked.Sample(frame * Dt, out float rms, out BandEnergies bands));
                signal.SetBands(rms, bands);
                MouthState b = replay.Process(signal, profile, Dt);

                worst = Mathf.Max(worst, Mathf.Abs(a.openness - b.openness));
                if (a.openness > 0.3f)
                {
                    frames++;
                    if (a.Dominant == b.Dominant) sameShape++;
                }
            }

            Assert.Less(worst, 0.02f, "same opening");
            Assert.Greater(frames, 30);
            Assert.GreaterOrEqual(sameShape, frames * 0.95f, "same shapes");
        }

        [Test]
        public void Baked_SamplesBetweenFramesAndIsSilentOutside()
        {
            var baked = Keep(ScriptableObject.CreateInstance<BakedVoice>());
            baked.Set(null, 10f, new[] { 0f, 1f, 0.5f }, new[] { Vector4.zero, new Vector4(1f, 0f, 0f, 0f), new Vector4(0f, 1f, 0f, 0f) });
            Assert.AreEqual(0.3f, baked.Duration, 1e-5f);

            Assert.IsTrue(baked.Sample(0.05, out float rms, out _));
            Assert.AreEqual(0.5f, rms, 1e-5f);
            Assert.IsTrue(baked.Sample(0.15, out rms, out BandEnergies bands));
            Assert.AreEqual(0.75f, rms, 1e-5f);
            Assert.AreEqual(0.5f, bands.mid, 1e-5f);
            Assert.IsFalse(baked.Sample(-0.01, out _, out _));
            Assert.IsFalse(baked.Sample(0.31, out _, out _));
        }

        [Test]
        public void Baked_FromAReadableClip()
        {
            AudioClip clip = Keep(SyntheticSpeech.CreateClip("Babble", 2f, 44100, 3));
            BakedVoice baked = Keep(VoiceBaker.Bake(clip));
            Assert.IsNotNull(baked);
            Assert.AreSame(clip, baked.Clip);
            Assert.AreEqual(VoiceBaker.FrameCount(clip.samples, 44100, 60f), baked.FrameCount);
            Assert.AreEqual(2f, baked.Duration, 0.05f);
        }

        [Test]
        public void BandSignal_IsAnalysedWithoutSamples()
        {
            TalkMouthProfile profile = Keep(TestSignals.Profile());
            TalkMouthProfile amplitude = Keep(TestSignals.Profile(TalkMouthPreset.Robot));
            var signal = new VoiceSignal(16);
            BandEnergies ee = TalkAnalyzer.BandsFor(Viseme.EE, profile.visemes);
            signal.SetBands(LevelMeter.FromDecibels(-16f), ee);
            Assert.AreEqual(VoiceSignalKind.Bands, signal.Kind);

            var analyzer = new TalkAnalyzer();
            for (int i = 0; i < 60; i++) analyzer.Process(signal, profile, Dt);
            Assert.AreEqual(Viseme.EE, analyzer.State.Dominant);

            analyzer = new TalkAnalyzer();
            for (int i = 0; i < 60; i++) analyzer.Process(signal, amplitude, Dt);
            Assert.AreEqual(0f, analyzer.State.visemes.ee, "amplitude profiles ignore the bands");
            Assert.Greater(analyzer.State.openness, 0.5f);
        }

        [Test]
        public void BakedSignal_PlaysOnItsOwnClockAndPicksBakesByClip()
        {
            float[] speech = Speech(1.5f);
            BakedVoice baked = Keep(VoiceBaker.Bake(speech, TestSignals.Rate));
            var signalSource = Keep(new GameObject("Voice")).AddComponent<BakedVisemeSignal>();
            var signal = new VoiceSignal(16);

            signalSource.Read(signal);
            Assert.AreEqual(VoiceSignalKind.Silent, signal.Kind, "nothing playing");

            signalSource.Play(baked);
            bool heard = false;
            for (float t = 0f; t < 1.4f; t += Dt)
            {
                signal.Clear();
                signalSource.Read(signal);
                heard |= signal.Kind == VoiceSignalKind.Bands && signal.Level > 0.01f;
                signalSource.Advance(Dt);
            }

            Assert.IsTrue(heard);
            signalSource.Advance(1f);
            Assert.IsFalse(signalSource.IsPlayingManually, "stops at the end");

            AudioClip a = Keep(AudioClip.Create("A", 100, 1, 44100, false));
            AudioClip b = Keep(AudioClip.Create("B", 100, 1, 44100, false));
            BakedVoice forA = Keep(ScriptableObject.CreateInstance<BakedVoice>());
            forA.Set(a, 60f, new float[1], new Vector4[1]);
            BakedVoice any = Keep(ScriptableObject.CreateInstance<BakedVoice>());
            signalSource.Bakes.Add(any);
            signalSource.Bakes.Add(forA);
            Assert.AreSame(forA, signalSource.Find(a));
            Assert.AreSame(any, signalSource.Find(b), "a bake without a clip fits any clip");
        }

        [TestCase(5f, true, TalkLod.Full)]
        [TestCase(20f, true, TalkLod.Amplitude)]
        [TestCase(60f, true, TalkLod.Culled)]
        [TestCase(5f, false, TalkLod.Culled)]
        public void Lod_ByDistanceAndVisibility(float distance, bool visible, TalkLod expected)
        {
            Assert.AreEqual(expected, TalkVoice.ComputeLod(distance, visible, 15f, 50f));
        }

        sealed class CountingSource : IVoiceSignalSource
        {
            public int reads;
            public Viseme shape = Viseme.EE;

            public void Read(VoiceSignal signal)
            {
                reads++;
                signal.SetNormalized(1f, shape);
            }
        }

        TalkVoice NewVoice(out CountingSource source)
        {
            TalkVoice voice = Keep(new GameObject("Voice")).AddComponent<TalkVoice>();
            voice.Profile = Keep(TestSignals.Profile());
            source = new CountingSource();
            voice.SetSource(source);
            return voice;
        }

        static void Run(TalkVoice voice, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) voice.Tick(Dt);
        }

        [Test]
        public void Lod_AmplitudeKeepsOneShapeAndCulledStopsReading()
        {
            TalkVoice voice = NewVoice(out CountingSource source);
            Run(voice, 0.5f);
            Assert.AreEqual(Viseme.EE, voice.State.Dominant);

            voice.LodOverride = TalkLod.Amplitude;
            Run(voice, 0.5f);
            Assert.AreEqual(TalkLod.Amplitude, voice.Lod);
            Assert.AreEqual(0f, voice.State.visemes.ee, 0.01f, "no shapes far away");
            Assert.Greater(voice.State.openness, 0.5f, "but the mouth still moves");

            int ended = 0;
            voice.OnSpeechEnded += () => ended++;
            voice.LodOverride = TalkLod.Culled;
            int reads = source.reads;
            Run(voice, 1f);
            Assert.AreEqual(reads, source.reads, "the source is not read");
            Assert.AreEqual(0f, voice.State.openness);
            Assert.AreEqual(1, ended, "speech ends for listeners");

            voice.LodOverride = null;
            Run(voice, 0.5f);
            Assert.AreEqual(Viseme.EE, voice.State.Dominant, "back to full detail");
        }

        [Test]
        public void Indicator_ShowsWhileSpeakingAndLingers()
        {
            TalkVoice voice = NewVoice(out CountingSource source);
            voice.SetEnabled(false);
            GameObject bubble = Keep(new GameObject("Bubble"));
            bubble.transform.localScale = Vector3.one * 0.2f;
            var indicator = voice.gameObject.AddComponent<SpeakingIndicator>();
            indicator.Voice = voice;
            indicator.FaceCamera = false;
            indicator.Linger = 0.3f;
            indicator.Indicator = bubble;
            Assert.IsFalse(bubble.activeSelf, "hidden at rest");

            voice.SetEnabled(true);
            for (int i = 0; i < 30; i++)
            {
                voice.Tick(Dt);
                indicator.Tick(Dt);
            }

            Assert.IsTrue(bubble.activeSelf);
            Assert.Greater(bubble.transform.localScale.x, 0.22f, "pulses with the voice");

            voice.SetEnabled(false);
            float shownFor = 0f;
            for (int i = 0; i < 120; i++)
            {
                voice.Tick(Dt);
                indicator.Tick(Dt);
                if (bubble.activeSelf) shownFor += Dt;
            }

            Assert.IsFalse(bubble.activeSelf);
            Assert.Greater(shownFor, 0.3f, "lingers after the voice");
            Assert.AreEqual(0.2f, bubble.transform.localScale.x, 1e-5f, "rest scale when hidden");
        }
    }
}
