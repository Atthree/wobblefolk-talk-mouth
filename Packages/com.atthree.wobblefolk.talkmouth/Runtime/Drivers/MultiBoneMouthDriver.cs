using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>One bone of a many-part mouth: a jaw, a mandible, a beak half, a tongue, a feather.</summary>
    [Serializable]
    public sealed class MouthBone
    {
        public Transform bone;
        [Tooltip("What moves this bone: the opening, or one mouth shape's weight.")]
        public MouthChannel channel = MouthChannel.Open;
        [Tooltip("Rotation axis in the bone's own space.")]
        public Vector3 axis = Vector3.right;
        [Tooltip("Angle (°) at full value. Negative turns the other way (an upper jaw that lifts).")]
        [Range(-180f, 180f)] public float openAngle = 20f;
        [Tooltip("Local position offset at full value, in the bone's own axes (a tongue that slides out).")]
        public Vector3 openOffset;
        [Tooltip("Scale multiplier at full value (1 = unchanged).")]
        public Vector3 openScale = Vector3.one;
        [Tooltip("Value (0–1, x) to movement (0–1, y). Start late to move only when loud.")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Share of the movement this bone takes.")]
        [Range(0f, 3f)] public float multiplier = 1f;
        [Tooltip("Follow the voice this much later (s).")]
        [Range(0f, 0.3f)] public float delay;

        [NonSerialized] internal readonly LocalPose pose = new LocalPose();
        [NonSerialized] internal MouthSpringState spring;

        public MouthBone() { }

        public MouthBone(Transform bone, Vector3 axis, float openAngle, float delay = 0f)
        {
            this.bone = bone;
            this.axis = axis;
            this.openAngle = openAngle;
            this.delay = delay;
        }

        /// <summary>Movement applied last frame (0 = rest, 1 = full; can pass 1 while bouncing).</summary>
        public float CurrentValue => spring.position;

        public float CurrentAngle => openAngle * spring.position;
    }

    /// <summary>
    /// Many bones from one voice, each with its own axis, angle, offset, scale, curve and delay: monster jaws that
    /// open in several directions, a beak whose halves move apart, a tongue that slides out only when loud.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Multi Bone Mouth Driver")]
    public sealed class MultiBoneMouthDriver : MouthDriver
    {
        [SerializeField] List<MouthBone> bones = new List<MouthBone>();

        /// <summary>The bones. After changing the list in code while enabled, call <see cref="Recapture"/>.</summary>
        public List<MouthBone> Bones => bones;

        public MouthBone AddBone(Transform bone, Vector3 axis, float openAngle, float delay = 0f)
        {
            var b = new MouthBone(bone, axis, openAngle, delay);
            bones.Add(b);
            if (isActiveAndEnabled) Capture(b);
            return b;
        }

        public void Recapture()
        {
            Restore();
            Capture();
        }

        protected override void Capture()
        {
            foreach (MouthBone b in bones) if (b != null) Capture(b);
        }

        static void Capture(MouthBone b)
        {
            b.pose.Capture(b.bone);
            b.spring.Reset();
        }

        protected override void Apply(float dt)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                MouthBone b = bones[i];
                if (b == null || b.bone == null) continue;
                if (b.pose.Target != b.bone) Capture(b);
                b.pose.Refresh();

                float value = Mathf.Clamp01(ChannelValue(b.channel, Sample(b.delay)));
                if (b.curve != null && b.curve.length > 0) value = b.curve.Evaluate(value);
                float v = ApplySpring(ref b.spring, value * b.multiplier, dt);

                LocalPose pose = b.pose;
                Quaternion turn = Quaternion.AngleAxis(b.openAngle * v, JawBoneMouthDriver.SafeAxis(b.axis));
                pose.Write(
                    pose.Position + pose.Rotation * (b.openOffset * v),
                    pose.Rotation * turn,
                    Vector3.Scale(pose.Scale, Vector3.LerpUnclamped(Vector3.one, b.openScale, v)));
            }
        }

        public override void Restore()
        {
            foreach (MouthBone b in bones)
            {
                if (b == null) continue;
                b.pose.Restore();
                b.spring.Reset();
            }
        }
    }
}
