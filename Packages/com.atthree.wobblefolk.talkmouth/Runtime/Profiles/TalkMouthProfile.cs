using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum AnalysisMode
    {
        /// <summary>Only how loud: the mouth opens and closes. Cheapest; split heads and robots use this.</summary>
        Amplitude = 0,
        /// <summary>Loudness plus coarse mouth shapes from the spectrum.</summary>
        Visemes = 1,
    }

    [Serializable]
    public sealed class LevelSettings
    {
        [Tooltip("Boost (or cut) applied to the voice before analysis, in dB. Raise for quiet sources.")]
        [Range(-24f, 36f)] public float inputGainDb = 0f;
        [Tooltip("Level that opens the gate (dB full scale). Lower = more sensitive.")]
        [Range(-80f, 0f)] public float gateThresholdDb = -42f;
        [Tooltip("How far below the threshold the level must fall to close the gate again. Stops flutter at the threshold.")]
        [Range(0f, 24f)] public float gateHysteresisDb = 6f;
        [Tooltip("Level that opens the mouth fully (dB full scale). Lower = the mouth opens wider for the same voice.")]
        [Range(-60f, 0f)] public float fullLevelDb = -14f;
        [Tooltip("Speaking ends only after this much silence (s), so pauses between words keep the 'speaking' state.")]
        [Min(0f)] public float holdTime = 0.2f;

        [Header("Adaptive")]
        [Tooltip("Follow the background noise and quiet speakers automatically.")]
        public bool adaptive;
        [Tooltip("Lowest gate the adaptation may use (dB).")]
        [Range(-90f, 0f)] public float adaptiveMinGateDb = -60f;
        [Tooltip("Gate sits this far above the tracked background noise (dB).")]
        [Range(0f, 30f)] public float noiseFloorMarginDb = 10f;
        [Tooltip("How fast the tracked noise floor rises (dB per second). Slow, so speech does not lift it.")]
        [Min(0f)] public float noiseFloorRiseRate = 2f;
        [Tooltip("How fast the tracked noise floor falls (dB per second).")]
        [Min(0f)] public float noiseFloorFallRate = 30f;
        [Tooltip("How fast the remembered loudest speech fades (dB per second), letting quiet speakers open the mouth fully.")]
        [Min(0f)] public float peakDecayRate = 2f;
        [Tooltip("Smallest distance between the adaptive gate and full level (dB).")]
        [Range(3f, 40f)] public float minRangeDb = 12f;
    }

    [Serializable]
    public sealed class EnvelopeSettings
    {
        [Tooltip("Time constant of opening (s). Small = snappy.")]
        [Min(0f)] public float attackTime = 0.03f;
        [Tooltip("Time constant of closing while still speaking (s).")]
        [Min(0f)] public float releaseTime = 0.1f;
        [Tooltip("Time constant of closing once speaking has ended (s).")]
        [Min(0f)] public float closeTime = 0.08f;
    }

    [Serializable]
    public sealed class ResponseSettings
    {
        [Tooltip("Level (0–1, x) to opening (0–1, y). Bend it up to make quiet speech visible.")]
        public AnimationCurve levelCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Multiplies the opening after the curve. Above 1 exaggerates.")]
        [Range(0f, 4f)] public float exaggeration = 1f;
        [Tooltip("Largest opening the drivers receive (0–1).")]
        [Range(0f, 1f)] public float maxOpenness = 1f;
        [Tooltip("Quantise the opening into this many steps (0 = smooth, 1 = open or closed).")]
        [Range(0, 8)] public int steps;
    }

    [Serializable]
    public sealed class VisemeSettings
    {
        public AnalysisMode mode = AnalysisMode.Visemes;
        [Tooltip("Time constant of shape changes (s).")]
        [Min(0f)] public float transitionTime = 0.06f;
        [Tooltip("How much the current shape is preferred (0 = none). Stops flicker between two similar shapes.")]
        [Range(0f, 2f)] public float stickiness = 0.25f;
        [Tooltip("How far another shape must lead before it becomes the dominant shape.")]
        [Range(0f, 0.5f)] public float switchMargin = 0.08f;
        [Tooltip("How strictly band profiles are matched. Higher = cleaner shapes, lower = more blending.")]
        [Range(1f, 200f)] public float sharpness = 30f;
        [Tooltip("Pre-emphasis filter (0 = off). Flattens the voice's downward tilt so the formants decide.")]
        [Range(0f, 0.99f)] public float preEmphasis = 0.95f;

        [Header("Bands (Hz)")]
        [Min(0f)] public float lowStartHz = 80f;
        [Min(0f)] public float lowMidHz = 700f;
        [Min(0f)] public float midHighHz = 1800f;
        [Min(0f)] public float highFricativeHz = 4000f;
        [Min(0f)] public float fricativeEndHz = 9000f;

        [Header("Band profiles (low, mid, high, fricative shares)")]
        public Vector4 aaProfile = new Vector4(0.35f, 0.45f, 0.15f, 0.05f);
        public Vector4 ohProfile = new Vector4(0.7f, 0.2f, 0.07f, 0.03f);
        public Vector4 eeProfile = new Vector4(0.35f, 0.15f, 0.4f, 0.1f);
        public Vector4 ffProfile = new Vector4(0.1f, 0.1f, 0.3f, 0.5f);
    }

    [Serializable]
    public sealed class SpringSettings
    {
        [Tooltip("Stiffness as a natural frequency (Hz). Higher = faster, tighter bounce.")]
        [Range(0.5f, 30f)] public float frequency = 6f;
        [Tooltip("How far a sudden opening overshoots (0.2 = 20 %). 0 = no bounce.")]
        [Range(0f, 0.8f)] public float overshoot = 0.05f;

        public float Damping => MouthSpring.DampingFromOvershoot(overshoot);
    }

    /// <summary>How a character's mouth listens and moves. Right-click the asset to apply a preset.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Talk Mouth Profile", fileName = "TalkMouthProfile")]
    public sealed class TalkMouthProfile : ScriptableObject
    {
        public LevelSettings level = new LevelSettings();
        public EnvelopeSettings envelope = new EnvelopeSettings();
        public ResponseSettings response = new ResponseSettings();
        public VisemeSettings visemes = new VisemeSettings();
        [Tooltip("Bounce used by the drivers that support it (hinge split, jaw, joints).")]
        public SpringSettings spring = new SpringSettings();

        [ContextMenu("Preset/Human")] void PresetHuman() => ApplyPreset(TalkMouthPreset.Human);
        [ContextMenu("Preset/Cartoon")] void PresetCartoon() => ApplyPreset(TalkMouthPreset.Cartoon);
        [ContextMenu("Preset/Monster")] void PresetMonster() => ApplyPreset(TalkMouthPreset.Monster);
        [ContextMenu("Preset/Bird")] void PresetBird() => ApplyPreset(TalkMouthPreset.Bird);
        [ContextMenu("Preset/Robot")] void PresetRobot() => ApplyPreset(TalkMouthPreset.Robot);
        [ContextMenu("Preset/Bouncy Split Head")] void PresetBouncy() => ApplyPreset(TalkMouthPreset.BouncySplitHead);

        void ApplyPreset(TalkMouthPreset preset)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Talk Mouth Preset");
#endif
            TalkMouthPresets.Apply(this, preset);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
