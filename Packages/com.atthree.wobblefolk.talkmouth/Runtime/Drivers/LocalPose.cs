using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// The pose a driver moves a transform away from. If something else changed the transform since the driver last
    /// wrote it (an Animator playing a jaw clip, a script, the Inspector), that becomes the new base, so the mouth is
    /// added on top of the animation instead of fighting it. Otherwise the remembered base is kept, so the offset
    /// never accumulates.
    /// </summary>
    public sealed class LocalPose
    {
        Transform target;
        bool written;
        Vector3 lastPosition;
        Quaternion lastRotation;
        Vector3 lastScale;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public Vector3 Scale { get; private set; } = Vector3.one;
        public Transform Target => target;

        /// <summary>Remembers <paramref name="transform"/>'s current local pose as the base.</summary>
        public void Capture(Transform transform)
        {
            target = transform;
            written = false;
            if (target == null) return;
            Position = target.localPosition;
            Rotation = target.localRotation;
            Scale = target.localScale;
        }

        /// <summary>Takes the transform's pose as the base if anything but this driver moved it.</summary>
        public void Refresh()
        {
            if (target == null) return;
            if (written &&
                target.localPosition == lastPosition &&
                target.localRotation == lastRotation &&
                target.localScale == lastScale)
            {
                return;
            }

            Position = target.localPosition;
            Rotation = target.localRotation;
            Scale = target.localScale;
        }

        public void Write(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (target == null) return;
            target.localPosition = position;
            target.localRotation = rotation;
            target.localScale = scale;
            lastPosition = target.localPosition;
            lastRotation = target.localRotation;
            lastScale = target.localScale;
            written = true;
        }

        /// <summary>Puts the transform back to its base pose.</summary>
        public void Restore()
        {
            if (target == null || !written) return;
            target.localPosition = Position;
            target.localRotation = Rotation;
            target.localScale = Scale;
            written = false;
        }
    }
}
