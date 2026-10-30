using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// A voice clip analysed ahead of time: its level and spectrum bands at a fixed frame rate. Playing it back costs
    /// no FFT and works with streamed or compressed clips that cannot be read at runtime. The character's profile
    /// (gate, envelope, curve, shapes, spring) still applies when it plays, so one bake suits every character.
    /// Made with Assets > Wobblefolk > Bake Talk Mouth, or <see cref="VoiceBaker"/> at runtime.
    /// </summary>
    public sealed class BakedVoice : ScriptableObject
    {
        [Tooltip("The clip this was baked from. Baked Viseme Signal picks the bake whose clip is playing.")]
        [SerializeField] AudioClip clip;
        [SerializeField] float frameRate = 60f;
        [SerializeField] float[] levels = new float[0];
        [SerializeField] Vector4[] bands = new Vector4[0];

        public AudioClip Clip => clip;
        public float FrameRate => frameRate;
        public int FrameCount => levels != null ? levels.Length : 0;
        public float Duration => FrameCount / Mathf.Max(1e-3f, frameRate);

        /// <summary>Replaces the data. <paramref name="bandShares"/> must be as long as <paramref name="rmsLevels"/>.</summary>
        public void Set(AudioClip source, float rate, float[] rmsLevels, Vector4[] bandShares)
        {
            if (rmsLevels == null || bandShares == null || rmsLevels.Length != bandShares.Length)
            {
                throw new System.ArgumentException("Levels and bands must have the same length.");
            }

            clip = source;
            frameRate = Mathf.Max(1f, rate);
            levels = rmsLevels;
            bands = bandShares;
        }

        /// <summary>Linear RMS level of a frame (for meters and tests).</summary>
        public float LevelAt(int frame) => levels[frame];

        /// <summary>
        /// Level and bands at <paramref name="time"/> seconds, interpolated between frames. False (and silence) before
        /// the start or after the end.
        /// </summary>
        public bool Sample(double time, out float rms, out BandEnergies energies)
        {
            rms = 0f;
            energies = default;
            int count = FrameCount;
            if (count == 0 || time < 0.0) return false;

            double position = time * frameRate;
            int frame = (int)position;
            if (frame >= count) return false;

            int next = Mathf.Min(frame + 1, count - 1);
            float t = (float)(position - frame);
            rms = Mathf.Lerp(levels[frame], levels[next], t);
            Vector4 b = Vector4.Lerp(bands[frame], bands[next], t);
            energies = new BandEnergies(b.x, b.y, b.z, b.w);
            return true;
        }
    }
}
