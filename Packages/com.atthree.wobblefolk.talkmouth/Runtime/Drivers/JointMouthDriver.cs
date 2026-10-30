using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Opens a physical jaw through its <see cref="ConfigurableJoint.targetRotation"/> only, for active ragdolls and
    /// other physics characters: the joint's angular drive pulls the jaw open, so it wobbles, collides and can be
    /// knocked shut. Works with any ConfigurableJoint (no ragdoll package needed). The drive is left as it is; give it
    /// a spring (Slerp or X drive) for the jaw to follow.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Joint Mouth Driver")]
    public sealed class JointMouthDriver : MouthDriver
    {
        [Header("Joint")]
        [Tooltip("The jaw's joint, connected to the head. Empty = the one on this object.")]
        [SerializeField] ConfigurableJoint joint;
        [Tooltip("Rotation axis in the jaw body's own space.")]
        [SerializeField] Vector3 axis = Vector3.right;
        [Tooltip("Target angle (°) when closed, relative to the pose the joint was created in.")]
        [Range(-45f, 45f)] [SerializeField] float closedAngle;
        [Tooltip("Target angle (°) at full opening.")]
        [Range(-90f, 90f)] [SerializeField] float openAngle = 25f;

        [Header("Shapes")]
        [Range(0f, 1f)] [SerializeField] float shapeInfluence = 0.5f;
        [SerializeField] ShapeOpenings shapeOpenings = ShapeOpenings.Default;

        ConfigurableJoint bound;
        Quaternion jointToLocal = Quaternion.identity;
        MouthSpringState spring;
        bool warned;

        public JointMouthDriver()
        {
            // The joint's drive already springs; a second spring would only add lag.
            SpringMode = MouthSpringMode.Off;
        }

        public ConfigurableJoint Joint
        {
            get => joint != null ? joint : GetComponent<ConfigurableJoint>();
            set
            {
                Restore();
                joint = value;
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

        public float ShapeInfluence
        {
            get => shapeInfluence;
            set => shapeInfluence = Mathf.Clamp01(value);
        }

        /// <summary>The target angle set last frame (°); the jaw follows it as its drive allows.</summary>
        public float CurrentAngle => Mathf.LerpUnclamped(closedAngle, openAngle, spring.position);

        protected override void Capture()
        {
            bound = Joint;
            spring.Reset();
            if (bound == null) return;
            jointToLocal = JointToLocal(bound.axis, bound.secondaryAxis);
            if (!warned && Application.isPlaying && !HasDrive(bound))
            {
                warned = true;
                Debug.LogWarning($"{name}: the joint has no angular drive spring, so the jaw will not follow. Set its Slerp or X drive.", this);
            }
        }

        protected override void Apply(float dt)
        {
            if (bound != Joint) Capture();
            if (bound == null) return;

            float target = ShapedOpenness(Sample(), shapeOpenings, shapeInfluence);
            float open = ApplySpring(ref spring, target, dt);
            bound.targetRotation = TargetRotation(Mathf.LerpUnclamped(closedAngle, openAngle, open), JawBoneMouthDriver.SafeAxis(axis), jointToLocal);
        }

        public override void Restore()
        {
            if (bound != null) bound.targetRotation = Quaternion.identity;
            spring.Reset();
        }

        /// <summary>
        /// Joint-space target that turns the body by <paramref name="angle"/> about its local <paramref name="localAxis"/>
        /// from the pose the joint was created in.
        /// </summary>
        public static Quaternion TargetRotation(float angle, Vector3 localAxis, Quaternion jointToLocal)
        {
            Quaternion local = Quaternion.AngleAxis(angle, localAxis);
            return Quaternion.Inverse(Quaternion.Inverse(jointToLocal) * local * jointToLocal);
        }

        /// <summary>Rotation from joint space (x = axis, y = secondary axis) into the body's local space.</summary>
        public static Quaternion JointToLocal(Vector3 jointAxis, Vector3 secondaryAxis)
        {
            Vector3 right = jointAxis.sqrMagnitude > 1e-8f ? jointAxis.normalized : Vector3.right;
            Vector3 forward = Vector3.Cross(right, secondaryAxis);
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.Cross(right, Vector3.up);
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.Cross(right, Vector3.forward);
            forward.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return Quaternion.LookRotation(forward, up);
        }

        static bool HasDrive(ConfigurableJoint j) =>
            j.rotationDriveMode == RotationDriveMode.Slerp ? j.slerpDrive.positionSpring > 0f : j.angularXDrive.positionSpring > 0f || j.angularYZDrive.positionSpring > 0f;
    }
}
