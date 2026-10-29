using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class ExternalBufferTests
    {
        const int Rate = TestSignals.Rate;
        const int Chunk = 960; // 20 ms, as voice codecs deliver
        const float Timeout = 0.12f;
        static readonly float LoudRms = LevelMeter.FromDecibels(-16f);

        /// <summary>A sine from 0 to <paramref name="speechEnd"/> s, then silence until <paramref name="end"/> s.</summary>
        static void PushSpeech(ExternalVoiceBuffer buffer, double speechEnd, double end)
        {
            var chunk = new float[Chunk];
            int position = 0;
            for (double t = 0; t < end - 1e-9; t += (double)Chunk / Rate)
            {
                if (t < speechEnd - 1e-9) TestSignals.Sine(chunk, 1000f, -16f, position);
                else TestSignals.Silence(chunk);
                position += Chunk;
                buffer.PushSamples(chunk, 1, Rate, t + (double)Chunk / Rate);
            }
        }

        static float ReadRms(ExternalVoiceBuffer buffer, double now, float delay, out VoiceSignalKind kind)
        {
            var signal = new VoiceSignal(1024);
            buffer.Read(signal, now, delay, Timeout);
            kind = signal.Kind;
            return kind == VoiceSignalKind.Samples ? LevelMeter.Rms(signal.Samples) : signal.Level;
        }

        [Test]
        public void Samples_AreHeardDelayLater()
        {
            var buffer = new ExternalVoiceBuffer();
            PushSpeech(buffer, 0.5, 1.0);

            Assert.AreEqual(0f, ReadRms(buffer, 1.0, 0f, out _), 1e-6f, "without delay: the newest audio is silence");
            Assert.AreEqual(LoudRms, ReadRms(buffer, 1.0, 0.7f, out _), LoudRms * 0.05f, "0.7 s ago there was speech");
        }

        [Test]
        public void ShortGaps_AreBridged()
        {
            var buffer = new ExternalVoiceBuffer();
            PushSpeech(buffer, 1.0, 1.0);
            Assert.AreEqual(LoudRms, ReadRms(buffer, 1.0 + Timeout * 0.5, 0f, out VoiceSignalKind kind), LoudRms * 0.05f);
            Assert.AreEqual(VoiceSignalKind.Samples, kind);
        }

        [Test]
        public void StoppedSpeaker_FallsSilent()
        {
            var buffer = new ExternalVoiceBuffer();
            PushSpeech(buffer, 1.0, 1.0);
            ReadRms(buffer, 1.0 + Timeout + 0.1, 0f, out VoiceSignalKind kind);
            Assert.AreEqual(VoiceSignalKind.Silent, kind);
        }

        [Test]
        public void StoppedSpeaker_PlaysTheDelayedTailFirst()
        {
            var buffer = new ExternalVoiceBuffer();
            PushSpeech(buffer, 1.0, 1.0);
            // Nothing arrives after 1.0 s, but with a 0.3 s delay, 1.2 s still shows speech from 0.9 s.
            Assert.AreEqual(LoudRms, ReadRms(buffer, 1.2, 0.3f, out _), LoudRms * 0.05f);
            ReadRms(buffer, 1.0 + 0.3 + Timeout + 0.1, 0.3f, out VoiceSignalKind kind);
            Assert.AreEqual(VoiceSignalKind.Silent, kind);
        }

        [Test]
        public void Levels_AreHeardDelayLater()
        {
            var buffer = new ExternalVoiceBuffer();
            for (int i = 0; i <= 50; i++) buffer.PushLevel(i < 25 ? 0.3f : 0f, i * 0.02);

            Assert.AreEqual(0f, ReadRms(buffer, 1.0, 0f, out _));
            Assert.AreEqual(0.3f, ReadRms(buffer, 1.0, 0.6f, out _), "0.6 s ago the level was 0.3");
            ReadRms(buffer, 1.0, 1.5f, out VoiceSignalKind kind);
            Assert.AreEqual(VoiceSignalKind.Silent, kind, "before the first report");
        }

        [Test]
        public void OldLevels_TimeOut()
        {
            var buffer = new ExternalVoiceBuffer();
            buffer.PushLevel(0.3f, 0.0);
            Assert.AreEqual(0.3f, ReadRms(buffer, Timeout * 0.5, 0f, out _));
            ReadRms(buffer, Timeout * 2, 0f, out VoiceSignalKind kind);
            Assert.AreEqual(VoiceSignalKind.Silent, kind);
        }

        [Test]
        public void NewSampleRate_StartsOver()
        {
            var buffer = new ExternalVoiceBuffer();
            PushSpeech(buffer, 1.0, 1.0);
            buffer.PushSamples(new float[Chunk], 1, 16000, 1.02);
            Assert.AreEqual(16000, buffer.SampleRate);
            Assert.AreEqual(0f, ReadRms(buffer, 1.02, 0f, out _), 1e-6f, "the old 48 kHz speech is gone");
        }

        [Test]
        public void Stereo_IsMixedToMono()
        {
            var buffer = new ExternalVoiceBuffer();
            var stereo = new float[2048];
            for (int i = 0; i < stereo.Length; i += 2) stereo[i] = 0.5f; // left only
            buffer.PushSamples(stereo, 2, Rate, 0.0);
            Assert.AreEqual(0.25f, ReadRms(buffer, 0.0, 0f, out _), 1e-4f);
        }

        [Test]
        public void PushingFromAnotherThread_IsSafe()
        {
            var buffer = new ExternalVoiceBuffer();
            var signal = new VoiceSignal(1024);
            using (var stop = new CancellationTokenSource())
            using (var firstPush = new ManualResetEventSlim())
            {
                Task writer = Task.Run(() =>
                {
                    var chunk = new float[Chunk];
                    for (int i = 0; i < chunk.Length; i++) chunk[i] = 0.1f;
                    double t = 0;
                    while (!stop.IsCancellationRequested)
                    {
                        buffer.PushSamples(chunk, 1, Rate, t);
                        buffer.PushLevel(0.1f, t);
                        firstPush.Set();
                        t += 0.02;
                    }
                });

                Assert.IsTrue(firstPush.Wait(5000));
                for (int i = 0; i < 2000; i++) buffer.Read(signal, 1e9, 0f, 1e12f);
                stop.Cancel();
                writer.Wait();
            }

            Assert.AreEqual(VoiceSignalKind.Samples, signal.Kind);
        }

        [Test]
        public void Reading_DoesNotAllocate()
        {
            var buffer = new ExternalVoiceBuffer();
            PushSpeech(buffer, 1.0, 1.0);
            var signal = new VoiceSignal(1024);
            buffer.Read(signal, 1.0, 0.2f, Timeout);

            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++) buffer.Read(signal, 1.0, 0.2f, Timeout);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
