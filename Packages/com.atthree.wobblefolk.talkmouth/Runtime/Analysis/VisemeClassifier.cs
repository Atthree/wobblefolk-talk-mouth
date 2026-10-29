using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Guesses a coarse mouth shape from band energies by comparing them with one typical band profile per shape.
    /// No language model and no calibration: the aim is believable talking, not exact lip reading. The current
    /// shape gets a small bonus and must be beaten by a margin, so the mouth does not flicker between two shapes.
    /// </summary>
    public sealed class VisemeClassifier
    {
        VisemeWeights weights = VisemeWeights.Rest;

        public VisemeWeights Weights => weights;
        public Viseme Dominant { get; private set; } = Viseme.Closed;

        public void Reset()
        {
            weights = VisemeWeights.Rest;
            Dominant = Viseme.Closed;
        }

        public void Update(in BandEnergies bands, float openness, VisemeSettings settings, float dt)
        {
            VisemeWeights shape = Classify(bands, settings, Dominant);
            Blend(Combine(shape, openness), settings, dt);
        }

        /// <summary>Amplitude only: the mouth just opens wide (AA) by the openness.</summary>
        public void UpdateAmplitude(float openness, VisemeSettings settings, float dt)
        {
            Blend(Combine(VisemeWeights.Only(Viseme.AA), openness), settings, dt);
        }

        /// <summary>Shape weights for an open mouth (the closed weight is 0); they sum to 1.</summary>
        public static VisemeWeights Classify(in BandEnergies bands, VisemeSettings settings, Viseme current)
        {
            Vector4 shares = bands.Shares;
            if (shares == Vector4.zero) return VisemeWeights.Only(Viseme.AA);

            float aa = (shares - settings.aaProfile).sqrMagnitude;
            float oh = (shares - settings.ohProfile).sqrMagnitude;
            float ee = (shares - settings.eeProfile).sqrMagnitude;
            float ff = (shares - settings.ffProfile).sqrMagnitude;
            float nearest = Mathf.Min(Mathf.Min(aa, oh), Mathf.Min(ee, ff));

            // Distances relative to the nearest profile, so the best score is 1 and nothing underflows.
            var w = new VisemeWeights(
                0f,
                Score(aa - nearest, Viseme.AA, current, settings),
                Score(oh - nearest, Viseme.OH, current, settings),
                Score(ee - nearest, Viseme.EE, current, settings),
                Score(ff - nearest, Viseme.FF, current, settings));

            float sum = w.Sum;
            return new VisemeWeights(0f, w.aa / sum, w.oh / sum, w.ee / sum, w.ff / sum);
        }

        static float Score(float distance, Viseme viseme, Viseme current, VisemeSettings settings)
        {
            float score = Mathf.Exp(-distance * settings.sharpness);
            return viseme == current ? score * (1f + settings.stickiness) : score;
        }

        static VisemeWeights Combine(in VisemeWeights shape, float openness)
        {
            openness = Mathf.Clamp01(openness);
            return new VisemeWeights(1f - openness, shape.aa * openness, shape.oh * openness, shape.ee * openness, shape.ff * openness);
        }

        void Blend(in VisemeWeights target, VisemeSettings settings, float dt)
        {
            float t = settings.transitionTime <= 0f ? 1f : 1f - Mathf.Exp(-dt / settings.transitionTime);
            weights = VisemeWeights.Lerp(weights, target, t);

            // Switch the reported shape only when another one clearly leads.
            Viseme best = weights.Dominant;
            if (best != Dominant && weights[best] > weights[Dominant] + settings.switchMargin) Dominant = best;
        }
    }
}
