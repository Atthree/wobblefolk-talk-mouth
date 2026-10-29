using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Turns voice samples or levels into a <see cref="MouthState"/>: level → noise gate → attack/release envelope →
    /// curve and exaggeration → mouth shapes. Plain C# without scene objects, so every step can be tested.
    /// One analyzer per voice; several mouths can read its state.
    /// </summary>
    public sealed class TalkAnalyzer
    {
        readonly NoiseGate gate = new NoiseGate();
        readonly VisemeClassifier classifier = new VisemeClassifier();
        readonly SpectrumAnalyzer spectrum;
        float envelope;

        public MouthState State { get; private set; } = MouthState.Silent;
        public NoiseGate Gate => gate;
        public int FftSize => spectrum.Size;

        public TalkAnalyzer(int fftSize = 1024) => spectrum = new SpectrumAnalyzer(fftSize);

        public void Reset()
        {
            gate.Reset();
            classifier.Reset();
            envelope = 0f;
            State = MouthState.Silent;
        }

        /// <summary>
        /// Analyses the newest PCM samples (mono, -1…1). With <paramref name="allowVisemes"/> false, or a profile in
        /// amplitude mode, the FFT is skipped (cheapest, e.g. for far away characters).
        /// </summary>
        public MouthState Process(ReadOnlySpan<float> samples, int sampleRate, TalkMouthProfile profile, float dt, bool allowVisemes = true)
        {
            float rms = LevelMeter.Rms(samples);
            if (allowVisemes && profile.visemes.mode == AnalysisMode.Visemes)
            {
                BandEnergies bands = spectrum.Analyze(samples, sampleRate, profile.visemes);
                return Step(rms, bands, true, profile, dt);
            }

            return Step(rms, default, false, profile, dt);
        }

        /// <summary>Amplitude only, from a level computed elsewhere (e.g. a voice chat SDK). <paramref name="rms"/> is linear.</summary>
        public MouthState ProcessLevel(float rms, TalkMouthProfile profile, float dt) => Step(rms, default, false, profile, dt);

        /// <summary>From a level and band energies computed elsewhere.</summary>
        public MouthState ProcessBands(float rms, in BandEnergies bands, TalkMouthProfile profile, float dt) =>
            Step(rms, bands, profile.visemes.mode == AnalysisMode.Visemes, profile, dt);

        MouthState Step(float rms, in BandEnergies bands, bool useBands, TalkMouthProfile profile, float dt)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            dt = Mathf.Max(0f, dt);

            LevelSettings level = profile.level;
            EnvelopeSettings timing = profile.envelope;

            float levelDb = rms <= 0f ? LevelMeter.MinDecibels : LevelMeter.ToDecibels(rms) + level.inputGainDb;
            gate.Update(levelDb, level, dt);
            float target = gate.Normalize(levelDb);

            float time = target > envelope ? timing.attackTime : gate.IsSpeaking ? timing.releaseTime : timing.closeTime;
            envelope = LevelMeter.Smooth(envelope, target, time, dt);
            if (!gate.IsSpeaking && envelope < 1e-4f) envelope = 0f;

            float openness = LevelResponse.Apply(envelope, profile.response);
            if (useBands) classifier.Update(bands, openness, profile.visemes, dt);
            else classifier.UpdateAmplitude(openness, profile.visemes, dt);

            State = new MouthState(openness, target, levelDb, gate.IsOpen, gate.IsSpeaking, classifier.Weights);
            return State;
        }
    }
}
