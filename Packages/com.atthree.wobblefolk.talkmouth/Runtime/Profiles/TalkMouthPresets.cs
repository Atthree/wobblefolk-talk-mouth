using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum TalkMouthPreset
    {
        /// <summary>Natural: moderate opening, soft shape changes, almost no bounce.</summary>
        Human = 0,
        /// <summary>Exaggerated: opens wide, snappy, a bouncy close.</summary>
        Cartoon = 1,
        /// <summary>Heavy and slow: late to open, slow to close.</summary>
        Monster = 2,
        /// <summary>Beak: fast and sharp, amplitude only.</summary>
        Bird = 3,
        /// <summary>Open or closed, nothing in between.</summary>
        Robot = 4,
        /// <summary>Split head: reacts to whispers, opens instantly, wobbles shut.</summary>
        BouncySplitHead = 5,
    }

    public static class TalkMouthPresets
    {
        public static TalkMouthProfile Create(TalkMouthPreset preset)
        {
            var profile = ScriptableObject.CreateInstance<TalkMouthProfile>();
            profile.name = preset.ToString();
            Apply(profile, preset);
            return profile;
        }

        public static void Apply(TalkMouthProfile p, TalkMouthPreset preset)
        {
            // Start from the defaults so a preset never inherits values from the previous one.
            p.level = new LevelSettings();
            p.envelope = new EnvelopeSettings();
            p.response = new ResponseSettings();
            p.visemes = new VisemeSettings();
            p.spring = new SpringSettings();

            switch (preset)
            {
                case TalkMouthPreset.Human:
                    break;

                case TalkMouthPreset.Cartoon:
                    Level(p, -45f, 6f, -18f, 0.15f);
                    Envelope(p, 0.02f, 0.08f, 0.06f);
                    Response(p, EaseOut(), 1.4f, 0);
                    p.visemes.transitionTime = 0.04f;
                    Spring(p, 5f, 0.25f);
                    break;

                case TalkMouthPreset.Monster:
                    Level(p, -40f, 6f, -12f, 0.3f);
                    Envelope(p, 0.08f, 0.25f, 0.3f);
                    Response(p, null, 1.1f, 0);
                    p.visemes.transitionTime = 0.12f;
                    Spring(p, 2.5f, 0.08f);
                    break;

                case TalkMouthPreset.Bird:
                    Level(p, -40f, 5f, -16f, 0.1f);
                    Envelope(p, 0.01f, 0.05f, 0.04f);
                    Response(p, null, 1.2f, 0);
                    p.visemes.mode = AnalysisMode.Amplitude;
                    Spring(p, 10f, 0.02f);
                    break;

                case TalkMouthPreset.Robot:
                    Level(p, -40f, 4f, -20f, 0.1f);
                    Envelope(p, 0f, 0.05f, 0.05f);
                    Response(p, null, 2f, 1);
                    p.visemes.mode = AnalysisMode.Amplitude;
                    Spring(p, 12f, 0f);
                    break;

                case TalkMouthPreset.BouncySplitHead:
                    Level(p, -55f, 6f, -24f, 0.15f);
                    p.level.adaptive = true;
                    Envelope(p, 0.01f, 0.06f, 0.1f);
                    Response(p, EaseOut(), 1.8f, 0);
                    p.visemes.mode = AnalysisMode.Amplitude;
                    Spring(p, 5f, 0.3f);
                    break;
            }
        }

        /// <summary>Rises steeply, then flattens: quiet speech already opens the mouth noticeably.</summary>
        public static AnimationCurve EaseOut() => new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        static void Level(TalkMouthProfile p, float gate, float hysteresis, float full, float hold)
        {
            p.level.gateThresholdDb = gate;
            p.level.gateHysteresisDb = hysteresis;
            p.level.fullLevelDb = full;
            p.level.holdTime = hold;
        }

        static void Envelope(TalkMouthProfile p, float attack, float release, float close)
        {
            p.envelope.attackTime = attack;
            p.envelope.releaseTime = release;
            p.envelope.closeTime = close;
        }

        static void Response(TalkMouthProfile p, AnimationCurve curve, float exaggeration, int steps)
        {
            if (curve != null) p.response.levelCurve = curve;
            p.response.exaggeration = exaggeration;
            p.response.steps = steps;
        }

        static void Spring(TalkMouthProfile p, float frequency, float overshoot)
        {
            p.spring.frequency = frequency;
            p.spring.overshoot = overshoot;
        }
    }
}
