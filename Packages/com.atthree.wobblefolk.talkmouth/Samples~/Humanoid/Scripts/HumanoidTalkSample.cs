using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.TalkMouth.Samples
{
    /// <summary>
    /// Sample controls: 1 = synthetic voice clip, 2 = microphone, 3 = typed dialogue (Space says the next line),
    /// M = mute the mouth. The voice is made up on the fly, so the sample ships no audio files.
    /// </summary>
    public sealed class HumanoidTalkSample : MonoBehaviour
    {
        public TalkVoice voice;
        public AudioSource voiceAudio;
        public AudioSourceSignal clipSignal;
        public MicrophoneSignal microphoneSignal;
        public TextDrivenSignal textSignal;
        [Range(60f, 400f)] public float voicePitch = 135f;
        [TextArea] public string[] lines =
        {
            "Hello there! Nice to meet you.",
            "Mmm, maybe. Or maybe not...",
            "Oh! Look over there, a big balloon!",
            "Wow, so many voices and only one mouth.",
        };

        int mode = -1;
        int line;
        AudioClip clip;

        void Start()
        {
            clip = SyntheticSpeech.CreateClip("Synthetic Voice", 30f, 44100, 11, voicePitch);
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
            if (Pressed(KeyCode.Alpha3)) SetMode(2);
            if (Pressed(KeyCode.Space) && mode == 2) SayNext();
            if (Pressed(KeyCode.M)) voice.SetEnabled(!voice.IsVoiceEnabled);
        }

        void SetMode(int next)
        {
            if (next == mode) return;
            mode = next;

            if (mode == 0) voiceAudio.Play();
            else voiceAudio.Stop();
            microphoneSignal.enabled = mode == 1;
            if (mode != 2) textSignal.Stop();

            switch (mode)
            {
                case 0: voice.SetSource(clipSignal); break;
                case 1: voice.SetSource(microphoneSignal); break;
                default:
                    voice.SetSource(textSignal);
                    SayNext();
                    break;
            }
        }

        void SayNext()
        {
            if (lines.Length == 0) return;
            textSignal.Say(lines[line % lines.Length]);
            line++;
        }

        void OnGUI()
        {
            string[] names = { "synthetic voice clip", "microphone", "typed dialogue" };
            string text =
                $"1 / 2 / 3: {names[Mathf.Max(0, mode)]}" + (mode == 2 ? "  (Space: next line)" : "") + "\n" +
                $"M: mouth {(voice.IsVoiceEnabled ? "on" : "muted")}\n" +
                $"{(voice.IsSpeaking ? "speaking" : "quiet")}, open {voice.State.openness:0.00}, shape {voice.State.Dominant}";
            GUI.Box(new Rect(10f, 10f, 360f, 64f), text);
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
                case KeyCode.Alpha3: return keyboard.digit3Key.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
