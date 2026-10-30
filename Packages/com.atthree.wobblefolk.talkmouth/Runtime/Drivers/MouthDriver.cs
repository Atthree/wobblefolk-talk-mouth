using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum MouthSpringMode
    {
        /// <summary>Follow the voice exactly.</summary>
        Off = 0,
        /// <summary>Use the spring of the voice's profile.</summary>
        Profile = 1,
        /// <summary>Use this driver's own spring.</summary>
        Custom = 2,
    }

    /// <summary>Position and velocity of one sprung value (an opening or an angle).</summary>
    public struct MouthSpringState
    {
        public float position;
        public float velocity;

        public void Reset(float value = 0f)
        {
            position = value;
            velocity = 0f;
        }
    }

    /// <summary>
    /// Moves something with a <see cref="TalkVoice"/>: bones, blend shapes, sprites, parameters. Any number of drivers
    /// can read one voice (jaw plus tongue plus lips). Applies in LateUpdate, after the Animator, so it is added on
    /// top of animation; returns everything to its rest pose when disabled.
    /// </summary>
    public abstract class MouthDriver : MonoBehaviour
    {
        [Tooltip("The voice to follow. Empty = the first Talk Voice on this object or its parents.")]
        [SerializeField] TalkVoice voice;
        [Tooltip("Scales the opening this driver receives. Above 1 exaggerates.")]
        [Range(0f, 3f)] [SerializeField] float amount = 1f;
        [Tooltip("Lag behind the voice (s), e.g. to meet delayed voice chat audio.")]
        [Range(0f, 0.5f)] [SerializeField] float delay;
        [Tooltip("Keep moving while the game is paused (Time.timeScale = 0), as the voice does.")]
        [SerializeField] bool useUnscaledTime = true;

        [Header("Spring")]
        [Tooltip("Bounce on top of the voice: the profile's spring, this driver's own, or none.")]
        [SerializeField] MouthSpringMode springMode = MouthSpringMode.Profile;
        [SerializeField] SpringSettings customSpring = new SpringSettings();
        [Tooltip("When a bouncing mouth slams shut, how much of the speed bounces back (0 = it stops dead).")]
        [Range(0f, 1f)] [SerializeField] float closedBounce = 0.3f;

        readonly MouthHistory history = new MouthHistory();
        double clock;

        public TalkVoice Voice
        {
            get => voice;
            set => voice = value;
        }

        public float Amount
        {
            get => amount;
            set => amount = Mathf.Max(0f, value);
        }

        public float Delay
        {
            get => delay;
            set => delay = Mathf.Max(0f, value);
        }

        public MouthSpringMode SpringMode
        {
            get => springMode;
            set => springMode = value;
        }

        public SpringSettings CustomSpring => customSpring;

        /// <summary>Seconds this driver has run since it was enabled (on its own time scale).</summary>
        protected double Clock => clock;

        public float ClosedBounce
        {
            get => closedBounce;
            set => closedBounce = Mathf.Clamp01(value);
        }

        /// <summary>The spring in use, or null when off.</summary>
        public SpringSettings Spring
        {
            get
            {
                switch (springMode)
                {
                    case MouthSpringMode.Profile: return voice != null ? voice.Profile.spring : null;
                    case MouthSpringMode.Custom: return customSpring;
                    default: return null;
                }
            }
        }

        protected virtual void Reset()
        {
            voice = GetComponentInParent<TalkVoice>();
        }

        protected virtual void Awake()
        {
            if (voice == null) voice = GetComponentInParent<TalkVoice>();
        }

        protected virtual void OnEnable()
        {
            history.Clear();
            clock = 0;
            Capture();
        }

        protected virtual void OnDisable() => Restore();

        /// <summary>
        /// True for drivers that feed the animation (Animator parameters) and so must run in Update, before it is
        /// evaluated; false (default) for drivers that pose things after it, in LateUpdate.
        /// </summary>
        protected virtual bool RunsBeforeAnimation => false;

        float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        void Update()
        {
            if (RunsBeforeAnimation) Tick(DeltaTime);
        }

        void LateUpdate()
        {
            if (!RunsBeforeAnimation) Tick(DeltaTime);
        }

        /// <summary>Reads the voice and applies one frame. Public so tests and custom loops can drive it.</summary>
        public void Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);
            clock += dt;
            MouthState state = voice != null ? voice.State : MouthState.Silent;
            history.Push(clock, new MouthSample(state.openness, state.visemes, state.isSpeaking));
            Apply(dt);
        }

        /// <summary>
        /// The voice <see cref="Delay"/> + <paramref name="extraDelay"/> seconds ago, scaled by <see cref="Amount"/>
        /// (the shape weights scale with it, and the closed weight takes the rest).
        /// </summary>
        protected MouthSample Sample(float extraDelay = 0f)
        {
            MouthSample s = history.Sample(delay + Mathf.Max(0f, extraDelay));
            if (Mathf.Approximately(amount, 1f)) return s;

            float open = s.openness * amount;
            VisemeWeights v = s.visemes;
            float shapes = 1f - v.closed;
            float scale = shapes > 1e-5f ? Mathf.Min(amount, 1f / shapes) : 0f;
            v = new VisemeWeights(0f, v.aa * scale, v.oh * scale, v.ee * scale, v.ff * scale);
            v.closed = Mathf.Max(0f, 1f - v.Sum);
            return new MouthSample(open, v, s.speaking);
        }

        /// <summary>
        /// Springs <paramref name="state"/> towards <paramref name="target"/> (an opening, 0 = closed). Overshoot above
        /// is kept; below closed it bounces back by <see cref="ClosedBounce"/> instead of passing through the face.
        /// </summary>
        protected float ApplySpring(ref MouthSpringState state, float target, float dt)
        {
            SpringSettings spring = Spring;
            if (spring == null)
            {
                state.Reset(target);
                return target;
            }

            MouthSpring.Step(ref state.position, ref state.velocity, target, spring.frequency, spring.Damping, dt);
            if (state.position < 0f)
            {
                state.position = 0f;
                if (state.velocity < 0f) state.velocity = -state.velocity * closedBounce;
            }

            return state.position;
        }

        /// <summary>Remembers the rest pose (on enable).</summary>
        protected abstract void Capture();

        /// <summary>Applies the mouth for one frame.</summary>
        protected abstract void Apply(float dt);

        /// <summary>Puts everything back to the rest pose (on disable).</summary>
        public abstract void Restore();

        /// <summary>
        /// A channel's value in <paramref name="s"/>: the opening, or one shape's weight. Closed counts only between
        /// sounds while speaking (lips pressed), not at rest.
        /// </summary>
        public static float ChannelValue(MouthChannel channel, in MouthSample s)
        {
            VisemeWeights v = s.visemes;
            switch (channel)
            {
                case MouthChannel.Open: return s.openness;
                case MouthChannel.Closed: return s.speaking ? v.closed : 0f;
                case MouthChannel.AA: return v.aa;
                case MouthChannel.OH: return v.oh;
                case MouthChannel.EE: return v.ee;
                case MouthChannel.FF: return v.ff;
                default: return 0f;
            }
        }

        /// <summary>
        /// Opening adjusted by shape: an "oh" or "ee" opens the jaw less than an "aa". <paramref name="influence"/> 0
        /// ignores the shape.
        /// </summary>
        protected static float ShapedOpenness(in MouthSample s, in ShapeOpenings openings, float influence)
        {
            if (influence <= 0f) return s.openness;
            VisemeWeights v = s.visemes;
            float shapes = v.aa + v.oh + v.ee + v.ff;
            if (shapes < 1e-5f) return s.openness;
            float factor = (v.aa * openings.aa + v.oh * openings.oh + v.ee * openings.ee + v.ff * openings.ff) / shapes;
            return s.openness * Mathf.Lerp(1f, factor, influence);
        }
    }

    /// <summary>How far the jaw opens for each shape, relative to the voice opening.</summary>
    [Serializable]
    public struct ShapeOpenings
    {
        [Range(0f, 1.5f)] public float aa;
        [Range(0f, 1.5f)] public float oh;
        [Range(0f, 1.5f)] public float ee;
        [Range(0f, 1.5f)] public float ff;

        public static ShapeOpenings Default => new ShapeOpenings { aa = 1f, oh = 0.75f, ee = 0.5f, ff = 0.2f };
    }
}
