using System;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.TalkMouth
{
    public enum FftSize
    {
        [InspectorName("256 (cheapest)")] Size256 = 256,
        [InspectorName("512")] Size512 = 512,
        [InspectorName("1024 (default)")] Size1024 = 1024,
        [InspectorName("2048 (finest)")] Size2048 = 2048,
    }

    /// <summary>
    /// One character's voice: reads a voice source every frame, analyses it and publishes the result. Any number of
    /// mouths can read the same voice. Analysis runs in Update, so mouth drivers can apply it in LateUpdate on top of
    /// the animation.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Wobblefolk/Talk Mouth/Talk Voice")]
    public sealed class TalkVoice : MonoBehaviour
    {
        [Tooltip("How this voice is heard and how the mouth moves. Empty = the Human preset.")]
        [SerializeField] TalkMouthProfile profile;
        [Tooltip("Where the voice comes from. Empty = the first voice source on this object.")]
        [SerializeField] VoiceSignalSource source;
        [Tooltip("Samples analysed per frame. Larger = finer mouth shapes, more CPU.")]
        [SerializeField] FftSize fftSize = FftSize.Size1024;
        [Tooltip("Keep talking while the game is paused (Time.timeScale = 0), as audio does.")]
        [SerializeField] bool useUnscaledTime = true;

        [Header("Events")]
        [SerializeField] UnityEvent onSpeechStarted = new UnityEvent();
        [SerializeField] UnityEvent onSpeechEnded = new UnityEvent();
        [Tooltip("Mouth opening (0–1), every frame.")]
        [SerializeField] UnityEvent<float> onLevel = new UnityEvent<float>();

        /// <summary>Every frame, after analysis. A plain C# event: subscribe from any system without a reference to this package's types beyond MouthState.</summary>
        public event Action<MouthState> OnSpeechLevel;
        public event Action OnSpeechStarted;
        public event Action OnSpeechEnded;

        TalkAnalyzer analyzer;
        VoiceSignal signal;
        IVoiceSignalSource codeSource;
        TalkMouthProfile fallbackProfile;
        bool voiceEnabled = true;

        public MouthState State { get; private set; } = MouthState.Silent;
        public bool IsSpeaking => State.isSpeaking;
        /// <summary>False after <see cref="SetEnabled"/>(false): the voice is ignored and the mouth stays shut.</summary>
        public bool IsVoiceEnabled => voiceEnabled;
        public TalkAnalyzer Analyzer => EnsureAnalyzer();
        /// <summary>The signal read this frame (for debugging and custom meters).</summary>
        public VoiceSignal Signal => signal;
        public IVoiceSignalSource Source => codeSource ?? source;

        public TalkMouthProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                if (fallbackProfile == null)
                {
                    fallbackProfile = TalkMouthPresets.Create(TalkMouthPreset.Human);
                    fallbackProfile.hideFlags = HideFlags.DontSave;
                }

                return fallbackProfile;
            }
            set => profile = value;
        }

        public UnityEvent SpeechStarted => onSpeechStarted;
        public UnityEvent SpeechEnded => onSpeechEnded;
        public UnityEvent<float> Level => onLevel;

        /// <summary>Uses a plain C# source (e.g. a voice chat bridge) instead of the Inspector one; null goes back to it.</summary>
        public void SetSource(IVoiceSignalSource voiceSource) => codeSource = voiceSource;

        /// <summary>Uses a source component.</summary>
        public void SetSource(VoiceSignalSource voiceSource)
        {
            codeSource = null;
            source = voiceSource;
        }

        /// <summary>
        /// Mutes this character's mouth (death, fainting, a muted player) without touching the audio. The mouth closes
        /// smoothly by the profile's close time; enabling resumes on the next sound.
        /// </summary>
        public void SetEnabled(bool on) => voiceEnabled = on;

        /// <summary>Forgets the noise floor and closes the mouth at once (e.g. after a teleport or a new speaker).</summary>
        public void ResetAnalysis()
        {
            EnsureAnalyzer().Reset();
            State = MouthState.Silent;
        }

        void Reset()
        {
            source = GetComponent<VoiceSignalSource>();
        }

        void Awake()
        {
            if (source == null) source = GetComponent<VoiceSignalSource>();
            EnsureAnalyzer();
        }

        void OnDisable()
        {
            bool wasSpeaking = State.isSpeaking;
            ResetAnalysis();
            if (wasSpeaking) RaiseEnded();
        }

        void OnDestroy()
        {
            if (fallbackProfile != null) DestroyImmediate(fallbackProfile);
        }

        void Update() => Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        /// <summary>Reads, analyses and publishes one frame. Public so tests and custom loops can drive it.</summary>
        public void Tick(float dt)
        {
            EnsureAnalyzer();
            signal.Clear();
            IVoiceSignalSource current = Source;
            if (voiceEnabled && current != null) current.Read(signal);

            bool wasSpeaking = State.isSpeaking;
            State = analyzer.Process(signal, Profile, dt);

            if (State.isSpeaking && !wasSpeaking)
            {
                onSpeechStarted.Invoke();
                OnSpeechStarted?.Invoke();
            }
            else if (!State.isSpeaking && wasSpeaking)
            {
                RaiseEnded();
            }

            onLevel.Invoke(State.openness);
            OnSpeechLevel?.Invoke(State);
        }

        void RaiseEnded()
        {
            onSpeechEnded.Invoke();
            OnSpeechEnded?.Invoke();
        }

        TalkAnalyzer EnsureAnalyzer()
        {
            int size = (int)fftSize;
            if (analyzer == null || analyzer.FftSize != size)
            {
                analyzer = new TalkAnalyzer(size);
                signal = new VoiceSignal(size);
            }

            return analyzer;
        }
    }
}
