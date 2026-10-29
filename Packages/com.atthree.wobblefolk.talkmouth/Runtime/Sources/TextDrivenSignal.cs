using System;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Talks from written dialogue, without voice audio: call <see cref="Say"/> and the mouth follows the letters with
    /// pauses at punctuation. Optionally plays a babble voice (a short synthesized blip per letter) and reports each
    /// character, so a typewriter text box can stay in step.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Text Driven Signal")]
    public sealed class TextDrivenSignal : VoiceSignalSource
    {
        [SerializeField] TextRhythmSettings rhythm = new TextRhythmSettings();
        [Tooltip("Said when the game starts (handy for testing).")]
        [TextArea(2, 5)] [SerializeField] string sayOnStart = "";
        [Tooltip("Repeat the line forever.")]
        [SerializeField] bool loop;
        [Tooltip("Pause before repeating (s).")]
        [Min(0f)] [SerializeField] float loopPause = 1f;
        [Tooltip("Keep talking while the game is paused (Time.timeScale = 0).")]
        [SerializeField] bool useUnscaledTime = true;

        [Header("Babble voice (optional)")]
        [Tooltip("Plays a blip per letter here. Empty = silent.")]
        [SerializeField] AudioSource babbleOutput;
        [Tooltip("Voice pitch (Hz). Low = big creature, high = small one.")]
        [Range(50f, 800f)] [SerializeField] float babblePitch = 220f;
        [Tooltip("Random pitch change per blip (0.1 = ±10 %).")]
        [Range(0f, 0.5f)] [SerializeField] float pitchVariation = 0.08f;
        [Tooltip("Length of one blip (s).")]
        [Range(0.02f, 0.3f)] [SerializeField] float blipLength = 0.07f;
        [Range(0f, 1f)] [SerializeField] float babbleVolume = 0.5f;
        [Tooltip("Blip on every Nth spoken letter (1 = every letter).")]
        [Min(1)] [SerializeField] int everyNthLetter = 1;

        [Header("Events")]
        [Tooltip("A character starts; the argument is its index in the text (for typewriter text boxes).")]
        [SerializeField] UnityEvent<int> onCharacter = new UnityEvent<int>();
        [SerializeField] UnityEvent onFinished = new UnityEvent();

        public event Action<int> CharacterSpoken;
        public event Action Finished;

        readonly TextRhythm line = new TextRhythm();
        readonly AudioClip[] blips = new AudioClip[VisemeWeights.Count];
        Action<int> characterCallback;
        float builtPitch;
        float builtLength;
        float loopWait = -1f;
        int letters;
        uint seed = 1;
        uint pitchRandom = 0x9E3779B9;

        public bool IsSaying => line.IsPlaying;
        public string Text => line.Text;
        public TextRhythm Rhythm => line;
        public TextRhythmSettings Settings => rhythm;
        public UnityEvent<int> CharacterEvent => onCharacter;
        public UnityEvent FinishedEvent => onFinished;

        /// <summary>Starts saying <paramref name="text"/>, replacing any line in progress.</summary>
        public void Say(string text)
        {
            line.Start(text, seed++);
            letters = 0;
            loopWait = -1f;
        }

        /// <summary>Stops talking at once. <see cref="Finished"/> is not raised.</summary>
        public void Stop()
        {
            line.Stop();
            loopWait = -1f;
        }

        void Awake() => characterCallback = OnCharacter;

        void Start()
        {
            if (!string.IsNullOrEmpty(sayOnStart)) Say(sayOnStart);
        }

        void OnDestroy()
        {
            foreach (AudioClip clip in blips)
            {
                if (clip != null) Destroy(clip);
            }
        }

        void Update() => Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        /// <summary>Moves the line on by <paramref name="dt"/>. Public so tests and custom loops can drive it.</summary>
        public void Tick(float dt)
        {
            if (characterCallback == null) characterCallback = OnCharacter;

            if (line.IsPlaying)
            {
                line.Advance(dt, rhythm, characterCallback);
                if (!line.IsPlaying)
                {
                    onFinished.Invoke();
                    Finished?.Invoke();
                    if (loop) loopWait = loopPause;
                }
            }
            else if (loopWait >= 0f)
            {
                loopWait -= dt;
                if (loopWait < 0f) Say(line.Text);
            }
        }

        public override void Read(VoiceSignal signal)
        {
            if (line.IsPlaying) signal.SetNormalized(line.Level, line.Viseme);
        }

        void OnCharacter(int index)
        {
            onCharacter.Invoke(index);
            CharacterSpoken?.Invoke(index);
            if (babbleOutput != null && line.Level > 0f && line.Viseme != Viseme.Closed && ++letters % everyNthLetter == 0) Blip(line.Viseme);
        }

        void Blip(Viseme shape)
        {
            AudioClip clip = BlipFor(shape);
            if (clip == null) return;

            pitchRandom ^= pitchRandom << 13;
            pitchRandom ^= pitchRandom >> 17;
            pitchRandom ^= pitchRandom << 5;
            float r = (pitchRandom & 0xFFFF) / 65535f * 2f - 1f;
            babbleOutput.pitch = 1f + r * pitchVariation;
            babbleOutput.PlayOneShot(clip, babbleVolume);
        }

        AudioClip BlipFor(Viseme shape)
        {
            if (!Mathf.Approximately(builtPitch, babblePitch) || !Mathf.Approximately(builtLength, blipLength))
            {
                for (int i = 0; i < blips.Length; i++)
                {
                    if (blips[i] != null) Destroy(blips[i]);
                    blips[i] = null;
                }

                builtPitch = babblePitch;
                builtLength = blipLength;
            }

            int index = (int)shape;
            if (blips[index] != null) return blips[index];

            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            var data = new float[Mathf.Max(1, Mathf.RoundToInt(blipLength * rate))];
            FormantSynth.Render(data, rate, shape, babblePitch, 0.8f, (uint)(index + 1));
            AudioClip clip = AudioClip.Create($"Babble {shape}", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            blips[index] = clip;
            return clip;
        }
    }
}
