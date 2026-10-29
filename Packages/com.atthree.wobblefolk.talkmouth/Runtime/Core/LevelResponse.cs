using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>Shapes a 0–1 level into a mouth opening: curve, exaggeration, optional steps, then the maximum.</summary>
    public static class LevelResponse
    {
        public static float Apply(float level, AnimationCurve curve, float exaggeration, int steps, float maxOpenness)
        {
            float y = Mathf.Clamp01(level);
            if (curve != null && curve.length > 0) y = curve.Evaluate(y);
            y = Mathf.Clamp01(y * exaggeration);
            if (steps > 0) y = Mathf.Round(y * steps) / steps;
            return y * Mathf.Clamp01(maxOpenness);
        }

        public static float Apply(float level, ResponseSettings settings) =>
            Apply(level, settings.levelCurve, settings.exaggeration, settings.steps, settings.maxOpenness);
    }
}
