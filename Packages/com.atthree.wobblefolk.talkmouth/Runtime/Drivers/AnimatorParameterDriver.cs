using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Feeds the voice into an Animator's parameters, so a blend tree or talking layer makes the mouth: a float for
    /// the opening, floats per shape, a bool while speaking, an int for the strongest shape. Leave a name empty to
    /// skip it. Runs in Update, before the Animator, so the pose matches the voice in the same frame.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Animator Parameter Driver")]
    public sealed class AnimatorParameterDriver : MouthDriver
    {
        [Header("Animator")]
        [Tooltip("Empty = the first Animator on this object or its children.")]
        [SerializeField] Animator animator;
        [Tooltip("Float, 0–1: the mouth opening.")]
        [SerializeField] string openParameter = "MouthOpen";
        [Tooltip("Bool: true while speaking (including short pauses).")]
        [SerializeField] string speakingParameter = "Talking";
        [Tooltip("Floats, 0–1: each shape's weight.")]
        [SerializeField] string aaParameter = "";
        [SerializeField] string ohParameter = "";
        [SerializeField] string eeParameter = "";
        [SerializeField] string ffParameter = "";
        [Tooltip("Int: the strongest shape (0 closed, 1 aa, 2 oh, 3 ee, 4 ff).")]
        [SerializeField] string shapeParameter = "";

        const int Open = 0, Speaking = 1, AA = 2, OH = 3, EE = 4, FF = 5, Shape = 6, Count = 7;

        readonly int[] hashes = new int[Count];
        readonly bool[] valid = new bool[Count];
        Animator bound;
        RuntimeAnimatorController boundController;
        bool written;
        bool warned;
        MouthSpringState spring;

        public AnimatorParameterDriver()
        {
            SpringMode = MouthSpringMode.Off;
        }

        protected override bool RunsBeforeAnimation => true;

        public Animator Animator
        {
            get => animator != null ? animator : GetComponentInChildren<Animator>();
            set
            {
                Restore();
                animator = value;
                if (isActiveAndEnabled) Capture();
            }
        }

        /// <summary>Sets the parameter names (null or empty skips one) and looks them up again.</summary>
        public void SetParameters(string open, string speaking, string aaName = "", string ohName = "", string eeName = "", string ffName = "", string shape = "")
        {
            openParameter = open;
            speakingParameter = speaking;
            aaParameter = aaName;
            ohParameter = ohName;
            eeParameter = eeName;
            ffParameter = ffName;
            shapeParameter = shape;
            Restore();
            Capture();
        }

        /// <summary>Named parameters the Animator does not have (or has with another type). Checked on enable.</summary>
        public int MissingParameters { get; private set; }

        protected override void Capture()
        {
            bound = Animator;
            boundController = bound != null ? bound.runtimeAnimatorController : null;
            written = false;
            spring.Reset();
            MissingParameters = 0;

            Resolve(Open, openParameter, AnimatorControllerParameterType.Float);
            Resolve(Speaking, speakingParameter, AnimatorControllerParameterType.Bool);
            Resolve(AA, aaParameter, AnimatorControllerParameterType.Float);
            Resolve(OH, ohParameter, AnimatorControllerParameterType.Float);
            Resolve(EE, eeParameter, AnimatorControllerParameterType.Float);
            Resolve(FF, ffParameter, AnimatorControllerParameterType.Float);
            Resolve(Shape, shapeParameter, AnimatorControllerParameterType.Int);

            if (MissingParameters > 0 && !warned && Application.isPlaying)
            {
                warned = true;
                Debug.LogWarning($"{name}: {MissingParameters} talk parameter(s) are not on the Animator {(bound != null ? bound.name : "(none)")}.", this);
            }
        }

        void Resolve(int slot, string parameter, AnimatorControllerParameterType type)
        {
            valid[slot] = false;
            if (string.IsNullOrEmpty(parameter)) return;
            hashes[slot] = Animator.StringToHash(parameter);
            if (bound != null && boundController != null)
            {
                // Only on enable: Animator.parameters allocates.
                foreach (AnimatorControllerParameter p in bound.parameters)
                {
                    if (p.nameHash == hashes[slot] && p.type == type)
                    {
                        valid[slot] = true;
                        return;
                    }
                }
            }

            MissingParameters++;
        }

        protected override void Apply(float dt)
        {
            if (bound != Animator || bound != null && bound.runtimeAnimatorController != boundController) Capture();
            if (bound == null || !bound.isActiveAndEnabled) return;

            MouthSample s = Sample();
            float open = ApplySpring(ref spring, s.openness, dt);
            VisemeWeights v = s.visemes;
            SetFloat(Open, open);
            if (valid[Speaking]) bound.SetBool(hashes[Speaking], s.speaking);
            SetFloat(AA, v.aa);
            SetFloat(OH, v.oh);
            SetFloat(EE, v.ee);
            SetFloat(FF, v.ff);
            if (valid[Shape]) bound.SetInteger(hashes[Shape], (int)v.Dominant);
            written = true;
        }

        void SetFloat(int slot, float value)
        {
            if (valid[slot]) bound.SetFloat(hashes[slot], value);
        }

        public override void Restore()
        {
            if (written && bound != null && bound.isActiveAndEnabled)
            {
                SetFloat(Open, 0f);
                if (valid[Speaking]) bound.SetBool(hashes[Speaking], false);
                SetFloat(AA, 0f);
                SetFloat(OH, 0f);
                SetFloat(EE, 0f);
                SetFloat(FF, 0f);
                if (valid[Shape]) bound.SetInteger(hashes[Shape], 0);
            }

            written = false;
            spring.Reset();
        }
    }
}
