using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.TalkMouth.Samples
{
    /// <summary>
    /// Sample controls: 1 = synthetic voice clip, 2 = microphone, B = bounce (none / bouncy / very bouncy),
    /// J = rattle when loud, M = mute. The profile is copied at start, so changes here never touch the asset.
    /// </summary>
    public sealed class SplitHeadTalkSample : MonoBehaviour
    {
        public TalkVoice voice;
        public HingeSplitMouthDriver splitHead;
        public AudioSource voiceAudio;
        public AudioSourceSignal clipSignal;
        public MicrophoneSignal microphoneSignal;
        [Range(60f, 400f)] public float voicePitch = 190f;

        static readonly float[] Bounces = { 0f, 0.3f, 0.6f };
        static readonly string[] BounceNames = { "none", "bouncy", "very bouncy" };

        int mode = -1;
        int bounce = 1;
        AudioClip clip;
        TalkMouthProfile profile;

        void Start()
        {
            profile = Instantiate(voice.Profile);
            voice.Profile = profile;
            SetBounce(1);

            clip = SyntheticSpeech.CreateClip("Synthetic Voice", 30f, 44100, 5, voicePitch);
            voiceAudio.clip = clip;
            voiceAudio.loop = true;
            SetMode(0);
        }

        void OnDestroy()
        {
            if (clip != null) Destroy(clip);
            if (profile != null) Destroy(profile);
        }

        void Update()
        {
            if (Pressed(KeyCode.Alpha1)) SetMode(0);
            if (Pressed(KeyCode.Alpha2)) SetMode(1);
            if (Pressed(KeyCode.B)) SetBounce((bounce + 1) % Bounces.Length);
            if (Pressed(KeyCode.J)) splitHead.Jitter = !splitHead.Jitter;
            if (Pressed(KeyCode.M)) voice.SetEnabled(!voice.IsVoiceEnabled);
        }

        void SetMode(int next)
        {
            if (next == mode) return;
            mode = next;
            if (mode == 0) voiceAudio.Play();
            else voiceAudio.Stop();
            microphoneSignal.enabled = mode == 1;
            voice.SetSource(mode == 0 ? (VoiceSignalSource)clipSignal : microphoneSignal);
        }

        void SetBounce(int index)
        {
            bounce = index;
            profile.spring.overshoot = Bounces[index];
        }

        void OnGUI()
        {
            string text =
                $"1 / 2: {(mode == 1 ? "microphone" : "synthetic voice clip")}\n" +
                $"B: bounce {BounceNames[bounce]}    J: rattle {(splitHead.Jitter ? "on" : "off")}    M: mouth {(voice.IsVoiceEnabled ? "on" : "muted")}\n" +
                $"{(voice.IsSpeaking ? "speaking" : "quiet")}, open {voice.State.openness:0.00}";
            GUI.Box(new Rect(10f, 10f, 400f, 64f), text);
        }

        static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.Alpha1: return keyboard.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return keyboard.digit2Key.wasPressedThisFrame;
                case KeyCode.B: return keyboard.bKey.wasPressedThisFrame;
                case KeyCode.J: return keyboard.jKey.wasPressedThisFrame;
                case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
