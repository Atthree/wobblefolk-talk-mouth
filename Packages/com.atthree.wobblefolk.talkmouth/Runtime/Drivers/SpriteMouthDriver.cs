using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.TalkMouth
{
    public enum SpriteMouthMode
    {
        /// <summary>Frames from closed to wide open, picked by the opening.</summary>
        Opening = 0,
        /// <summary>One sprite per mouth shape (closed, aa, oh, ee, ff).</summary>
        Shapes = 1,
    }

    /// <summary>
    /// Swaps mouth sprites with the voice, for 2D characters and dialogue portraits. Drives a SpriteRenderer, and
    /// reports every change through an event, so a UI Image (or anything else) can show the same sprite.
    /// Each sprite is held for a minimum time, so the mouth does not flicker between two frames.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Sprite Mouth Driver")]
    public sealed class SpriteMouthDriver : MouthDriver
    {
        [Header("Sprites")]
        [Tooltip("The renderer to change. Empty = the one on this object; may stay empty if only the event is used.")]
        [SerializeField] SpriteRenderer target;
        [SerializeField] SpriteMouthMode mode = SpriteMouthMode.Opening;
        [Tooltip("Opening mode: frames from closed (first) to wide open (last).")]
        [SerializeField] Sprite[] openingFrames = new Sprite[0];
        [Tooltip("Shapes mode: the closed mouth.")]
        [SerializeField] Sprite closed;
        [SerializeField] Sprite aa;
        [SerializeField] Sprite oh;
        [SerializeField] Sprite ee;
        [SerializeField] Sprite ff;
        [Tooltip("Shapes mode: below this opening the closed sprite shows.")]
        [Range(0f, 1f)] [SerializeField] float openThreshold = 0.15f;
        [Tooltip("Shortest time (s) a sprite stays before the next may show.")]
        [Range(0f, 0.3f)] [SerializeField] float minHoldTime = 0.05f;
        [Tooltip("Every time the sprite changes, e.g. to set a UI Image's sprite.")]
        [SerializeField] UnityEvent<Sprite> onSpriteChanged = new UnityEvent<Sprite>();

        SpriteRenderer bound;
        Sprite restSprite;
        bool written;
        float held = float.MaxValue;
        MouthSpringState spring;

        public SpriteMouthDriver()
        {
            SpringMode = MouthSpringMode.Off;
        }

        public SpriteRenderer Target
        {
            get => target != null ? target : GetComponent<SpriteRenderer>();
            set
            {
                Restore();
                target = value;
                if (isActiveAndEnabled) Capture();
            }
        }

        public SpriteMouthMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public Sprite[] OpeningFrames
        {
            get => openingFrames;
            set => openingFrames = value ?? new Sprite[0];
        }

        public void SetShapeSprites(Sprite closedSprite, Sprite aaSprite, Sprite ohSprite, Sprite eeSprite, Sprite ffSprite)
        {
            closed = closedSprite;
            aa = aaSprite;
            oh = ohSprite;
            ee = eeSprite;
            ff = ffSprite;
        }

        public float OpenThreshold
        {
            get => openThreshold;
            set => openThreshold = Mathf.Clamp01(value);
        }

        public float MinHoldTime
        {
            get => minHoldTime;
            set => minHoldTime = Mathf.Max(0f, value);
        }

        public UnityEvent<Sprite> SpriteChanged => onSpriteChanged;

        /// <summary>The sprite shown now (also when no renderer is set).</summary>
        public Sprite Current { get; private set; }

        protected override void Capture()
        {
            bound = Target;
            restSprite = bound != null ? bound.sprite : null;
            Current = restSprite;
            written = false;
            held = float.MaxValue;
            spring.Reset();
        }

        protected override void Apply(float dt)
        {
            if (bound != Target) Capture();
            held += dt;

            MouthSample s = Sample();
            float open = ApplySpring(ref spring, s.openness, dt);
            Sprite next = Pick(s, open);
            if (next == null || next == Current || held < minHoldTime) return;

            Current = next;
            held = 0f;
            if (bound != null) bound.sprite = next;
            written = true;
            onSpriteChanged.Invoke(next);
        }

        Sprite Pick(in MouthSample s, float open)
        {
            if (mode == SpriteMouthMode.Shapes)
            {
                if (open < openThreshold) return closed != null ? closed : Frame(0f);
                Sprite shape = Shape(s.visemes);
                return shape != null ? shape : Frame(open);
            }

            return Frame(open);
        }

        Sprite Frame(float open)
        {
            if (openingFrames == null || openingFrames.Length == 0) return null;
            int index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(open) * (openingFrames.Length - 1)), 0, openingFrames.Length - 1);
            return openingFrames[index];
        }

        /// <summary>Sprite of the strongest open shape that has one (aa when only aa is set).</summary>
        Sprite Shape(in VisemeWeights v)
        {
            Sprite best = aa;
            float weight = v.aa;
            if (v.oh > weight && oh != null) { best = oh; weight = v.oh; }
            if (v.ee > weight && ee != null) { best = ee; weight = v.ee; }
            if (v.ff > weight && ff != null) best = ff;
            return best;
        }

        public override void Restore()
        {
            if (written && bound != null) bound.sprite = restSprite;
            if (written) onSpriteChanged.Invoke(restSprite);
            Current = restSprite;
            written = false;
            spring.Reset();
        }
    }
}
