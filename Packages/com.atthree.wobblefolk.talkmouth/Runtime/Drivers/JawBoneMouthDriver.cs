using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Opens a jaw bone by rotating it about a local axis, optionally sliding it as well. Works on rigged characters
    /// (on top of their animation) and on a plain jaw object. The shape changes the opening a little: "aa" opens
    /// widest, "oh" and "ee" less, "ff" barely.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Jaw Bone Mouth Driver")]
    public sealed class JawBoneMouthDriver : MouthDriver
    {
        [Header("Jaw")]
        [Tooltip("The jaw bone or object. Its pivot is the hinge. Empty = this object.")]
        [SerializeField] Transform jaw;
        [Tooltip("Rotation axis in the jaw's own space. Positive angles turn the front down on a Z-forward, X-right jaw.")]
        [SerializeField] Vector3 axis = Vector3.right;
        [Tooltip("Angle (°) when the mouth is closed, relative to the rest pose.")]
        [Range(-45f, 45f)] [SerializeField] float closedAngle;
        [Tooltip("Angle (°) when the mouth is fully open.")]
        [Range(-90f, 90f)] [SerializeField] float openAngle = 20f;
        [Tooltip("Local position offset at full opening, e.g. a jaw that drops and slides forward.")]
        [SerializeField] Vector3 openOffset;

        [Header("Shapes")]
        [Tooltip("How much the mouth shape changes the opening (0 = only loudness).")]
        [Range(0f, 1f)] [SerializeField] float shapeInfluence = 0.5f;
        [SerializeField] ShapeOpenings shapeOpenings = ShapeOpenings.Default;

        readonly LocalPose pose = new LocalPose();
        MouthSpringState spring;

        public Transform Jaw
        {
            get => jaw != null ? jaw : transform;
            set
            {
                Restore();
                jaw = value;
                if (isActiveAndEnabled) Capture();
            }
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

        public float OpenAngle
        {
            get => openAngle;
            set => openAngle = value;
        }

        public Vector3 OpenOffset
        {
            get => openOffset;
            set => openOffset = value;
        }

        public float ShapeInfluence
        {
            get => shapeInfluence;
            set => shapeInfluence = Mathf.Clamp01(value);
        }

        public ShapeOpenings ShapeOpenings
        {
            get => shapeOpenings;
            set => shapeOpenings = value;
        }

        /// <summary>The opening applied last frame, after the spring (can pass 1 while bouncing).</summary>
        public float CurrentOpening => spring.position;

        /// <summary>The angle applied last frame (°).</summary>
        public float CurrentAngle => Mathf.LerpUnclamped(closedAngle, openAngle, spring.position);

        internal LocalPose Pose => pose;

        protected override void Capture()
        {
            pose.Capture(Jaw);
            spring.Reset();
        }

        protected override void Apply(float dt)
        {
            if (pose.Target != Jaw) Capture();
            pose.Refresh();

            float target = ShapedOpenness(Sample(), shapeOpenings, shapeInfluence);
            float open = ApplySpring(ref spring, target, dt);
            Quaternion bend = Quaternion.AngleAxis(Mathf.LerpUnclamped(closedAngle, openAngle, open), SafeAxis(axis));
            pose.Write(pose.Position + pose.Rotation * (openOffset * open), pose.Rotation * bend, pose.Scale);
        }

        public override void Restore()
        {
            pose.Restore();
            spring.Reset();
        }

        internal static Vector3 SafeAxis(Vector3 axis) => axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.right;
    }
}
