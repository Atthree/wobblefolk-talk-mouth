using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.TalkMouth.Samples
{
    /// <summary>
    /// Sample controls: 1 = synthetic voice clip, 2 = microphone, M = mute the mouth. Both beak halves and the crest feathers are one Multi Bone driver; the feathers follow one after another.
    /// </summary>
    public sealed class BeakTalkSample : MonoBehaviour
    {
        public TalkVoice voice;
        public AudioSource voiceAudio;
        public AudioSourceSignal clipSignal;
        public MicrophoneSignal microphoneSignal;
        [Range(60f, 600f)] public float voicePitch = 320f;

        int mode = -1;
        AudioClip clip;

        void Start()
        {
            clip = SyntheticSpeech.CreateClip("Synthetic Voice", 30f, 44100, 41, voicePitch);
            voiceAudio.clip = clip;
            voiceAudio.loop = true;
            SetMode(0);
        }

        void OnDestroy()
        {
            if (clip != null) Destroy(clip);
        }

        void Update()
        {
            if (Pressed(KeyCode.Alpha1)) SetMode(0);
            if (Pressed(KeyCode.Alpha2)) SetMode(1);
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

        void OnGUI()
        {
            string text =
                $"1 / 2: {(mode == 1 ? "microphone" : "synthetic voice clip")}    M: mouth {(voice.IsVoiceEnabled ? "on" : "muted")}\n" +
                $"{(voice.IsSpeaking ? "speaking" : "quiet")}, open {voice.State.openness:0.00}";
            GUI.Box(new Rect(10f, 10f, 360f, 46f), text);
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
                case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
