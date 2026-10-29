using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum AudioReadMode
    {
        /// <summary>Clip samples when the clip allows it, otherwise the output data.</summary>
        Auto = 0,
        /// <summary>
        /// Reads the clip at the play position. Exact, and not affected by volume, distance attenuation, mute or
        /// effects. Needs a clip loaded as Decompress On Load (not Streaming).
        /// </summary>
        ClipSamples = 1,
        /// <summary>
        /// What the AudioSource last played (AudioSource.GetOutputData). Works for streamed and generated audio, but may
        /// include the source volume and effects.
        /// </summary>
        OutputData = 2,
    }

    /// <summary>
    /// Voice from an AudioSource playing dialogue clips. By default it reads the clip itself at the play position,
    /// so a character far from the listener (or with the audio muted) still moves its mouth exactly as loud.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Wobblefolk/Talk Mouth/Audio Source Signal")]
    public sealed class AudioSourceSignal : VoiceSignalSource
    {
        [Tooltip("The voice. Empty = the AudioSource on this object.")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioReadMode readMode = AudioReadMode.Auto;
        [Tooltip("Clip samples only: read this far ahead of the play position (s). Positive values make the mouth lead the sound slightly, which often looks more in sync.")]
        [Range(-0.3f, 0.3f)] [SerializeField] float lookAhead = 0f;
        [Tooltip("Output data only: divide by the AudioSource volume, so turning a voice down does not close its mouth.")]
        [SerializeField] bool compensateVolume = true;

        float[] interleaved = new float[0];
        float[] output = new float[0];
        AudioClip unreadableClip;

        public AudioSource AudioSource => audioSource;

        /// <summary>The mode used for the current clip (Auto resolved).</summary>
        public AudioReadMode ActiveMode
        {
            get
            {
                if (readMode != AudioReadMode.Auto) return readMode;
                return CanReadClip(audioSource != null ? audioSource.clip : null) ? AudioReadMode.ClipSamples : AudioReadMode.OutputData;
            }
        }

        void Reset() => audioSource = GetComponent<AudioSource>();

        void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        public override void Read(VoiceSignal signal)
        {
            if (audioSource == null || !audioSource.isPlaying) return;

            if (ActiveMode == AudioReadMode.ClipSamples && ReadClip(signal)) return;
            ReadOutput(signal);
        }

        bool CanReadClip(AudioClip clip) =>
            clip != null && clip != unreadableClip && clip.loadType != AudioClipLoadType.Streaming &&
            clip.loadState == AudioDataLoadState.Loaded;

        bool ReadClip(VoiceSignal signal)
        {
            AudioClip clip = audioSource.clip;
            if (!CanReadClip(clip)) return false;

            int frames = SampleMixer.WindowFrames(signal.Capacity, clip.samples);
            long playhead = audioSource.timeSamples + (long)(lookAhead * clip.frequency);
            if (!SampleMixer.TryGetWindow(playhead, frames, clip.samples, audioSource.loop, out int start))
            {
                return true; // past the end: silence
            }

            int channels = Mathf.Max(1, clip.channels);
            int length = frames * channels;
            if (interleaved.Length != length) interleaved = new float[length];
            if (!clip.GetData(interleaved, start))
            {
                unreadableClip = clip; // e.g. compressed in memory: use the output data from now on
                return false;
            }

            SampleMixer.Downmix(interleaved, channels, signal.BeginSamples(frames, clip.frequency));
            return true;
        }

        void ReadOutput(VoiceSignal signal)
        {
            // GetOutputData needs a power-of-two length; the capacity is one (the FFT size).
            if (output.Length != signal.Capacity) output = new float[signal.Capacity];
            audioSource.GetOutputData(output, 0);

            float gain = compensateVolume && audioSource.volume > 1e-3f ? 1f / audioSource.volume : 1f;
            var mono = signal.BeginSamples(output.Length, AudioSettings.outputSampleRate);
            for (int i = 0; i < mono.Length; i++) mono[i] = output[i] * gain;
        }
    }
}
