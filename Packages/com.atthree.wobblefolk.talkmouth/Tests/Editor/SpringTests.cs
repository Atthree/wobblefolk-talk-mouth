using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class SpringTests
    {
        static float Run(float target, float frequency, float overshoot, float duration, float dt, ref float x, ref float v, out float min, out float max)
        {
            float damping = MouthSpring.DampingFromOvershoot(overshoot);
            min = x;
            max = x;
            int steps = Mathf.RoundToInt(duration / dt);
            for (int i = 0; i < steps; i++)
            {
                MouthSpring.Step(ref x, ref v, target, frequency, damping, dt);
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }

            return x;
        }

        [TestCase(0.1f)]
        [TestCase(0.3f)]
        [TestCase(0.5f)]
        public void Overshoot_MatchesTheSetting(float overshoot)
        {
            float x = 0f, v = 0f;
            Run(1f, 5f, overshoot, 2f, 1f / 1000f, ref x, ref v, out _, out float max);
            Assert.AreEqual(1f + overshoot, max, 0.01f);
        }

        [Test]
        public void ZeroOvershoot_NeverPassesTheTarget()
        {
            float x = 0f, v = 0f;
            Run(1f, 5f, 0f, 2f, 1f / 240f, ref x, ref v, out _, out float max);
            Assert.LessOrEqual(max, 1f + 1e-5f);
            Assert.AreEqual(1f, x, 1e-3f);
        }

        [Test]
        public void ClosingWithOvershoot_BouncesBackAndSettles()
        {
            float x = 1f, v = 0f;
            Run(0f, 5f, 0.3f, 3f, 1f / 60f, ref x, ref v, out float min, out _);
            Assert.Less(min, -0.1f, "the mouth bounces past closed");
            Assert.AreEqual(0f, x, 1e-3f, "and settles shut");
            Assert.AreEqual(0f, v, 1e-2f);
        }

        [Test]
        public void IsFrameRateIndependent()
        {
            float x1 = 0f, v1 = 0f, x2 = 0f, v2 = 0f;
            Run(1f, 6f, 0.25f, 0.5f, 1f / 30f, ref x1, ref v1, out _, out _);
            Run(1f, 6f, 0.25f, 0.5f, 1f / 240f, ref x2, ref v2, out _, out _);
            Assert.AreEqual(x2, x1, 1e-4f);
            Assert.AreEqual(v2, v1, 1e-3f);
        }

        [Test]
        public void StaysStableAtVeryLowFrameRates()
        {
            float x = 0f, v = 0f;
            Run(1f, 20f, 0.6f, 10f, 0.5f, ref x, ref v, out float min, out float max);
            Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x));
            Assert.LessOrEqual(max, 1.6f + 1e-3f, "never more than the configured overshoot");
            Assert.GreaterOrEqual(min, 0f - 1e-3f);
            Assert.AreEqual(1f, x, 1e-2f);
        }

        [Test]
        public void ZeroFrequency_SnapsToTheTarget()
        {
            float x = 0f, v = 3f;
            MouthSpring.Step(ref x, ref v, 0.7f, 0f, 1f, 1f / 60f);
            Assert.AreEqual(0.7f, x);
            Assert.AreEqual(0f, v);
        }
    }
}
