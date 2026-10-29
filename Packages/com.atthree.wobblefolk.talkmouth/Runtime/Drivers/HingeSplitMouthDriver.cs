using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>One piece that swings on the hinge: the top of the head, a lower jaw, a hat that follows late.</summary>
    [Serializable]
    public sealed class HingePart
    {
        [Tooltip("The piece to swing. It must not be the hinge or one of its parents.")]
        public Transform part;
        [Tooltip("Share of the opening angle this piece takes.")]
        [Range(0f, 3f)] public float multiplier = 1f;
        [Tooltip("Swing the other way, e.g. a lower half that drops while the top lifts.")]
        public bool reverse;
        [Tooltip("Follow the voice this much later (s), so pieces move one after another.")]
        [Range(0f, 0.3f)] public float delay;

        [NonSerialized] internal readonly LocalPose pose = new LocalPose();
        [NonSerialized] internal MouthSpringState spring;
        [NonSerialized] internal float envelope;
        [NonSerialized] internal float angle;

        public HingePart() { }

        public HingePart(Transform part, float multiplier = 1f, bool reverse = false, float delay = 0f)
        {
            this.part = part;
            this.multiplier = multiplier;
            this.reverse = reverse;
            this.delay = delay;
        }

        /// <summary>Angle (°) applied last frame, signed (negative when reversed).</summary>
        public float CurrentAngle => angle;
    }

    /// <summary>
    /// A head that splits open as the character talks: the top half (and anything else in the list) swings about a
    /// hinge, by loudness only. Snappy by default with an underdamped spring, so it pops open, overshoots and wobbles
    /// shut. Needs separate meshes for the moving pieces; the hinge must not be a child of one of them.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Hinge Split Mouth Driver")]
    public sealed class HingeSplitMouthDriver : MouthDriver
    {
        [Header("Hinge")]
        [Tooltip("The hinge: its position is the pivot, the axis is in its space. Empty = this object.")]
        [SerializeField] Transform hinge;
        [Tooltip("Hinge axis in the hinge's space. With the hinge at the back of the head, Left lifts the front of the top half.")]
        [SerializeField] Vector3 axis = Vector3.left;
        [Tooltip("Angle (°) when closed, relative to the rest pose.")]
        [Range(-30f, 30f)] [SerializeField] float closedAngle;
        [Tooltip("Angle (°) at full opening.")]
        [Range(0f, 180f)] [SerializeField] float maxOpenAngle = 45f;
        [SerializeField] List<HingePart> parts = new List<HingePart>();

        [Header("Response")]
        [Tooltip("Time constant of opening (s), on top of the voice. Very short = it pops open on every syllable.")]
        [Min(0f)] [SerializeField] float attackTime = 0.01f;
        [Tooltip("Time constant of closing (s), on top of the voice.")]
        [Min(0f)] [SerializeField] float releaseTime = 0.05f;
        [Tooltip("Opening (0–1, x) to swing (0–1, y).")]
        [SerializeField] AnimationCurve levelCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Multiplies the swing after the curve, capped at the full angle (the spring may still overshoot).")]
        [Range(0f, 4f)] [SerializeField] float exaggeration = 1f;

        [Header("Jitter")]
        [Tooltip("Rattle when loud, like a lid on a boiling pot.")]
        [SerializeField] bool jitter;
        [Tooltip("Opening above which the rattle starts.")]
        [Range(0f, 1f)] [SerializeField] float jitterAbove = 0.7f;
        [Tooltip("Rattle size at full opening (°).")]
        [Range(0f, 15f)] [SerializeField] float jitterAngle = 3f;
        [Tooltip("Rattle speed (Hz).")]
        [Range(1f, 40f)] [SerializeField] float jitterFrequency = 14f;

        bool warnedHinge;

        public Transform Hinge
        {
            get => hinge != null ? hinge : transform;
            set => hinge = value;
        }

        public Vector3 Axis
        {
            get => axis;
            set => axis = value;
        }

        public float ClosedAngle
        {
            get => closedAngle;
            set => closedAngle = value;
        }

        public float MaxOpenAngle
        {
            get => maxOpenAngle;
            set => maxOpenAngle = value;
        }

        /// <summary>The swinging pieces. After changing the list in code while enabled, call <see cref="Recapture"/>.</summary>
        public List<HingePart> Parts => parts;

        public float AttackTime
        {
            get => attackTime;
            set => attackTime = Mathf.Max(0f, value);
        }

        public float ReleaseTime
        {
            get => releaseTime;
            set => releaseTime = Mathf.Max(0f, value);
        }

        public AnimationCurve LevelCurve
        {
            get => levelCurve;
            set => levelCurve = value;
        }

        public float Exaggeration
        {
            get => exaggeration;
            set => exaggeration = Mathf.Max(0f, value);
        }

        public bool Jitter
        {
            get => jitter;
            set => jitter = value;
        }

        public void SetJitter(bool on, float above, float angle, float frequency)
        {
            jitter = on;
            jitterAbove = Mathf.Clamp01(above);
            jitterAngle = Mathf.Max(0f, angle);
            jitterFrequency = Mathf.Max(0.01f, frequency);
        }

        /// <summary>Adds a piece (and captures its rest pose when enabled).</summary>
        public HingePart AddPart(Transform part, float multiplier = 1f, bool reverse = false, float delay = 0f)
        {
            var p = new HingePart(part, multiplier, reverse, delay);
            parts.Add(p);
            if (isActiveAndEnabled) Capture(p);
            return p;
        }

        public void Recapture()
        {
            Restore();
            Capture();
        }

        /// <summary>The pivot in world space.</summary>
        public Vector3 HingePoint => Hinge.position;

        /// <summary>The axis in world space.</summary>
        public Vector3 HingeAxis => Hinge.TransformDirection(JawBoneMouthDriver.SafeAxis(axis)).normalized;

        protected override void Capture()
        {
            foreach (HingePart p in parts) if (p != null) Capture(p);
            warnedHinge = false;
        }

        static void Capture(HingePart p)
        {
            p.pose.Capture(p.part);
            p.spring.Reset();
            p.envelope = 0f;
            p.angle = 0f;
        }

        protected override void Apply(float dt)
        {
            Transform pivot = Hinge;
            Vector3 point = pivot.position;
            Vector3 worldAxis = HingeAxis;

            for (int i = 0; i < parts.Count; i++)
            {
                HingePart p = parts[i];
                if (p == null || p.part == null) continue;
                if (p.pose.Target != p.part) Capture(p);
                if (pivot == p.part || pivot.IsChildOf(p.part))
                {
                    WarnHinge(p.part);
                    continue;
                }

                p.pose.Refresh();
                float open = Opening(p, dt);
                float angle = closedAngle + (maxOpenAngle - closedAngle) * open * p.multiplier + Rattle(open, i);
                if (p.reverse) angle = -angle;
                p.angle = angle;
                Swing(p, point, worldAxis, angle);
            }
        }

        /// <summary>Voice → attack/release → curve and exaggeration → spring, for one piece.</summary>
        float Opening(HingePart p, float dt)
        {
            float level = Mathf.Max(0f, Sample(p.delay).openness);
            p.envelope = LevelMeter.Smooth(p.envelope, level, level > p.envelope ? attackTime : releaseTime, dt);
            float shaped = levelCurve != null && levelCurve.length > 0 ? levelCurve.Evaluate(Mathf.Clamp01(p.envelope)) : p.envelope;
            shaped = Mathf.Clamp01(shaped * exaggeration);
            return ApplySpring(ref p.spring, shaped, dt);
        }

        float Rattle(float open, int index)
        {
            if (!jitter || jitterAngle <= 0f || open <= jitterAbove) return 0f;
            float strength = Mathf.Clamp01((open - jitterAbove) / Mathf.Max(1e-3f, 1f - jitterAbove));
            float t = (float)(Clock * jitterFrequency);
            float noise = Mathf.PerlinNoise(t, 17.3f + index * 7.1f) * 2f - 1f;
            return noise * jitterAngle * strength;
        }

        static void Swing(HingePart p, Vector3 worldPoint, Vector3 worldAxis, float angle)
        {
            Transform parent = p.part.parent;
            Vector3 point = parent != null ? parent.InverseTransformPoint(worldPoint) : worldPoint;
            Vector3 localAxis = parent != null ? parent.InverseTransformDirection(worldAxis) : worldAxis;
            Quaternion turn = Quaternion.AngleAxis(angle, localAxis);
            p.pose.Write(point + turn * (p.pose.Position - point), turn * p.pose.Rotation, p.pose.Scale);
        }

        void WarnHinge(Transform part)
        {
            if (warnedHinge) return;
            warnedHinge = true;
            Debug.LogWarning($"{name}: the hinge must not be {part.name} or inside it (it would swing with it). Move the hinge next to the moving pieces.", this);
        }

        public override void Restore()
        {
            foreach (HingePart p in parts)
            {
                if (p == null) continue;
                p.pose.Restore();
                p.spring.Reset();
                p.envelope = 0f;
                p.angle = 0f;
            }
        }
    }
}
