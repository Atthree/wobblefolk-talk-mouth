using System.Collections;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// The local player's microphone. The recording is only analysed, never played back, so there is no echo.
    /// On mobile it asks for microphone permission first. Not available on WebGL.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Microphone Signal")]
    public sealed class MicrophoneSignal : VoiceSignalSource
    {
        [Tooltip("Device name from Microphone.devices. Empty = the default microphone.")]
        [SerializeField] string deviceName = "";
        [Tooltip("Requested sample rate (Hz); clamped to what the device supports.")]
        [SerializeField] int sampleRate = 48000;
        [Tooltip("Length of the recording ring buffer (s).")]
        [Range(1, 10)] [SerializeField] int bufferSeconds = 1;
        [Tooltip("Start recording when enabled, stop when disabled.")]
        [SerializeField] bool recordOnEnable = true;
        [Tooltip("Ask for microphone permission (mobile, web) before recording.")]
        [SerializeField] bool requestPermission = true;

        AudioClip clip;
        float[] interleaved = new float[0];
        string activeDevice;

        public bool IsRecording => clip != null && IsDeviceRecording(activeDevice);
        public string DeviceName
        {
            get => deviceName;
            set => deviceName = value;
        }

        /// <summary>Names of the available microphones.</summary>
        public static string[] Devices
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                return Microphone.devices;
#else
                return new string[0];
#endif
            }
        }

        void OnEnable()
        {
            if (recordOnEnable) StartRecording();
        }

        void OnDisable() => StopRecording();

        /// <summary>Starts (or restarts) recording from <see cref="DeviceName"/>.</summary>
        public void StartRecording()
        {
            StopRecording();
            if (requestPermission && !Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                if (isActiveAndEnabled) StartCoroutine(AskThenRecord());
                return;
            }

            Begin();
        }

        public void StopRecording()
        {
            StopAllCoroutines();
#if !UNITY_WEBGL || UNITY_EDITOR
            if (clip != null && IsDeviceRecording(activeDevice)) Microphone.End(activeDevice);
#endif
            if (clip != null) Destroy(clip);
            clip = null;
        }

        IEnumerator AskThenRecord()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (Application.HasUserAuthorization(UserAuthorization.Microphone)) Begin();
            else Debug.LogWarning("Talk Mouth: microphone permission was denied.", this);
        }

        void Begin()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("Talk Mouth: no microphone found.", this);
                return;
            }

            activeDevice = string.IsNullOrEmpty(deviceName) ? null : deviceName;
            Microphone.GetDeviceCaps(activeDevice, out int min, out int max);
            int rate = min == 0 && max == 0 ? sampleRate : Mathf.Clamp(sampleRate, min, max);
            clip = Microphone.Start(activeDevice, true, bufferSeconds, rate);
#else
            Debug.LogWarning("Talk Mouth: the microphone is not supported on WebGL.", this);
#endif
        }

        static bool IsDeviceRecording(string device)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Microphone.IsRecording(device);
#else
            return false;
#endif
        }

        public override void Read(VoiceSignal signal)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (clip == null || !Microphone.IsRecording(activeDevice)) return;

            int position = Microphone.GetPosition(activeDevice);
            int frames = SampleMixer.WindowFrames(signal.Capacity, clip.samples);
            if (!SampleMixer.TryGetWindow(position, frames, clip.samples, true, out int start)) return;

            int channels = Mathf.Max(1, clip.channels);
            int length = frames * channels;
            if (interleaved.Length != length) interleaved = new float[length];
            if (!clip.GetData(interleaved, start)) return; // the ring buffer wraps around by itself

            SampleMixer.Downmix(interleaved, channels, signal.BeginSamples(frames, clip.frequency));
#endif
        }
    }
}
