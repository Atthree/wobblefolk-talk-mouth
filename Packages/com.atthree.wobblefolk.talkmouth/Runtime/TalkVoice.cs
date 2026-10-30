using System;
using System.Collections.Generic;
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

    /// <summary>How much work a voice does, by distance and visibility.</summary>
    public enum TalkLod
    {
        /// <summary>Level and mouth shapes.</summary>
        Full = 0,
        /// <summary>Level only: no FFT. The mouth opens and closes but keeps one shape.</summary>
        Amplitude = 1,
        /// <summary>Not read or analysed: the mouth closes and stays shut. Events still report the end of speech.</summary>
        Culled = 2,
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
        [Tooltip("Where listeners look when this voice speaks (the head or mouth). Empty = this object.")]
        [SerializeField] Transform speakerPoint;

        [Header("Level of Detail")]
        [Tooltip("Do less work far from the camera: shapes up close, level only further away, nothing beyond.")]
        [SerializeField] bool useLod;
        [Tooltip("Distances are measured from this. Empty = the main camera.")]
        [SerializeField] Transform lodViewer;
        [Tooltip("Up to this distance (m): level and mouth shapes.")]
        [Min(0f)] [SerializeField] float fullDetailDistance = 15f;
        [Tooltip("Beyond this distance (m): not analysed, the mouth closes.")]
        [Min(0f)] [SerializeField] float cullDistance = 50f;
        [Tooltip("Optional: when this renderer is off screen, the voice is not analysed either.")]
        [SerializeField] Renderer visibilityRenderer;

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
        TalkLod automaticLod = TalkLod.Full;

        static readonly List<TalkVoice> enabledVoices = new List<TalkVoice>();

        /// <summary>Every enabled voice, e.g. to find who is speaking nearby. Do not keep the list.</summary>
        public static IReadOnlyList<TalkVoice> All => enabledVoices;

        public MouthState State { get; private set; } = MouthState.Silent;

        public Transform SpeakerPoint
        {
            get => speakerPoint != null ? speakerPoint : transform;
            set => speakerPoint = value;
        }

        /// <summary>Detail used last frame (always Full without LOD, unless overridden).</summary>
        public TalkLod Lod { get; private set; } = TalkLod.Full;

        /// <summary>Forces a detail level (e.g. a game's own LOD or networking relevance); null goes back to automatic.</summary>
        public TalkLod? LodOverride { get; set; }

        public bool UseLod
        {
            get => useLod;
            set => useLod = value;
        }

        public void SetLodDistances(float fullDetail, float cull)
        {
            fullDetailDistance = Mathf.Max(0f, fullDetail);
            cullDistance = Mathf.Max(fullDetailDistance, cull);
        }

        /// <summary>The detail for a distance and visibility.</summary>
        public static TalkLod ComputeLod(float distance, bool visible, float fullDetailDistance, float cullDistance)
        {
            if (!visible || distance > cullDistance) return TalkLod.Culled;
            return distance > fullDetailDistance ? TalkLod.Amplitude : TalkLod.Full;
        }
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

        void OnEnable()
        {
            if (!enabledVoices.Contains(this)) enabledVoices.Add(this);
        }

        void OnDisable()
        {
            enabledVoices.Remove(this);
            bool wasSpeaking = State.isSpeaking;
            ResetAnalysis();
            if (wasSpeaking) RaiseEnded();
        }

        void OnDestroy()
        {
            if (fallbackProfile != null) DestroyImmediate(fallbackProfile);
        }

        void Update()
        {
            automaticLod = AutomaticLod();
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        TalkLod AutomaticLod()
        {
            if (!useLod) return TalkLod.Full;
            Transform viewer = lodViewer;
            if (viewer == null)
            {
                Camera main = Camera.main;
                viewer = main != null ? main.transform : null;
            }

            float distance = viewer != null ? Vector3.Distance(viewer.position, transform.position) : 0f;
            bool visible = visibilityRenderer == null || visibilityRenderer.isVisible;
            return ComputeLod(distance, visible, fullDetailDistance, cullDistance);
        }

        /// <summary>Reads, analyses and publishes one frame. Public so tests and custom loops can drive it.</summary>
        public void Tick(float dt)
        {
            EnsureAnalyzer();
            signal.Clear();
            TalkLod lod = LodOverride ?? automaticLod;
            Lod = lod;
            IVoiceSignalSource current = Source;
            if (voiceEnabled && current != null && lod != TalkLod.Culled) current.Read(signal);

            bool wasSpeaking = State.isSpeaking;
            State = analyzer.Process(signal, Profile, dt, lod == TalkLod.Full);

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
