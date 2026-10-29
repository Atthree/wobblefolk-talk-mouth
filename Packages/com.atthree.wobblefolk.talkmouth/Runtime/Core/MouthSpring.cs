using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// A damped spring solved exactly for each step instead of integrated, so it behaves the same at any frame rate
    /// and cannot blow up, even at very low fps. Used to give mouths a little bounce.
    /// </summary>
    public static class MouthSpring
    {
        const float MinOvershoot = 1e-4f;
        const float MaxOvershoot = 0.95f;

        /// <summary>
        /// Damping ratio for which a step from rest overshoots by <paramref name="overshoot"/> (0.2 = 20 %).
        /// 0 gives a critically damped spring without overshoot.
        /// </summary>
        public static float DampingFromOvershoot(float overshoot)
        {
            if (overshoot < MinOvershoot) return 1f;
            float ln = Mathf.Log(Mathf.Min(overshoot, MaxOvershoot));
            return -ln / Mathf.Sqrt(Mathf.PI * Mathf.PI + ln * ln);
        }

        /// <summary>
        /// Moves <paramref name="position"/> and <paramref name="velocity"/> towards <paramref name="target"/>.
        /// <paramref name="frequency"/> is the natural frequency in Hz (stiffness); <paramref name="damping"/> the
        /// damping ratio, clamped to (0, 1].
        /// </summary>
        public static void Step(ref float position, ref float velocity, float target, float frequency, float damping, float dt)
        {
            if (dt <= 0f) return;
            if (frequency <= 0f)
            {
                position = target;
                velocity = 0f;
                return;
            }

            float omega = 2f * Mathf.PI * frequency;
            float zeta = Mathf.Clamp(damping, 1e-3f, 1f);
            float y = position - target;
            float v = velocity;

            if (zeta > 0.9999f)
            {
                // Critically damped: y(t) = (y0 + (v0 + w y0) t) e^(-w t).
                float decay = Mathf.Exp(-omega * dt);
                float c = v + omega * y;
                position = target + (y + c * dt) * decay;
                velocity = (v - omega * c * dt) * decay;
                return;
            }

            // Underdamped: y(t) = e^(-a t) (y0 cos(wd t) + (v0 + a y0) / wd sin(wd t)).
            float a = zeta * omega;
            float wd = omega * Mathf.Sqrt(1f - zeta * zeta);
            float e = Mathf.Exp(-a * dt);
            float cos = Mathf.Cos(wd * dt);
            float sin = Mathf.Sin(wd * dt);
            float b = (v + a * y) / wd;
            position = target + e * (y * cos + b * sin);
            velocity = e * (v * cos - (a * b + y * wd) * sin);
        }
    }
}
