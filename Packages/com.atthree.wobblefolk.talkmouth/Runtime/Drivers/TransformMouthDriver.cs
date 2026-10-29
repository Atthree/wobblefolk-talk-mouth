using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Moves, turns and scales any object with the voice: a cartoon mouth disc that stretches, a visor that slides,
    /// a speaker cone that pumps. Optionally reshapes it per mouth shape (wide for "ee", round for "oh").
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Transform Mouth Driver")]
    public sealed class TransformMouthDriver : MouthDriver
    {
        [Header("Target")]
        [Tooltip("The object to move. Empty = this object.")]
        [SerializeField] Transform target;
        [Tooltip("Local position offset at full opening (in the target's own axes).")]
        [SerializeField] Vector3 openPosition;
        [Tooltip("Rotation (Euler °) added at full opening.")]
        [SerializeField] Vector3 openRotation;
        [Tooltip("Scale multiplier at full opening (1 = unchanged).")]
        [SerializeField] Vector3 openScale = new Vector3(1f, 2f, 1f);

        [Header("Shapes")]
        [Tooltip("Reshape per mouth shape: each shape's scale multiplier blends in by its weight.")]
        [SerializeField] bool useShapes;
        [SerializeField] Vector3 aaScale = Vector3.one;
        [SerializeField] Vector3 ohScale = new Vector3(0.65f, 1.1f, 1f);
        [SerializeField] Vector3 eeScale = new Vector3(1.35f, 0.6f, 1f);
        [SerializeField] Vector3 ffScale = new Vector3(1.1f, 0.35f, 1f);

        readonly LocalPose pose = new LocalPose();
        MouthSpringState spring;

        public Transform Target
        {
            get => target != null ? target : transform;
            set
            {
                Restore();
                target = value;
                if (isActiveAndEnabled) Capture();
            }
        }

        public Vector3 OpenPosition
        {
            get => openPosition;
            set => openPosition = value;
        }

        public Vector3 OpenRotation
        {
            get => openRotation;
            set => openRotation = value;
        }

        public Vector3 OpenScale
        {
            get => openScale;
            set => openScale = value;
        }

        public bool UseShapes
        {
            get => useShapes;
            set => useShapes = value;
        }

        /// <summary>Scale multiplier of each mouth shape at full weight.</summary>
        public void SetShapeScales(Vector3 aa, Vector3 oh, Vector3 ee, Vector3 ff)
        {
            aaScale = aa;
            ohScale = oh;
            eeScale = ee;
            ffScale = ff;
        }

        public float CurrentOpening => spring.position;

        protected override void Capture()
        {
            pose.Capture(Target);
            spring.Reset();
        }

        protected override void Apply(float dt)
        {
            if (pose.Target != Target) Capture();
            pose.Refresh();

            MouthSample s = Sample();
            float open = ApplySpring(ref spring, s.openness, dt);

            Vector3 scale = Vector3.LerpUnclamped(Vector3.one, openScale, open);
            if (useShapes) scale = Vector3.Scale(scale, ShapeScale(s.visemes));

            pose.Write(
                pose.Position + pose.Rotation * (openPosition * open),
                pose.Rotation * Quaternion.Euler(openRotation * open),
                Vector3.Scale(pose.Scale, scale));
        }

        /// <summary>Blend of the shape scales by their weights; the closed weight keeps the rest scale.</summary>
        Vector3 ShapeScale(in VisemeWeights v)
        {
            float shapes = v.aa + v.oh + v.ee + v.ff;
            if (shapes < 1e-5f) return Vector3.one;
            Vector3 blend = (aaScale * v.aa + ohScale * v.oh + eeScale * v.ee + ffScale * v.ff) / shapes;
            return Vector3.Lerp(Vector3.one, blend, shapes);
        }

        public override void Restore()
        {
            pose.Restore();
            spring.Reset();
        }
    }
}
