using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Decides whether there is speech. It opens above the threshold and closes only once the level falls a
    /// hysteresis below it, so a level hovering at the threshold does not make the mouth flutter. Speaking ends only
    /// after the gate stayed closed for the hold time, so short pauses between words do not count as silence.
    /// With adaptive levels the threshold follows the background noise and the full level follows quiet speakers.
    /// </summary>
    public sealed class NoiseGate
    {
        bool tracking;
        float closedTime;
        float peakDb;

        public bool IsOpen { get; private set; }
        public bool IsSpeaking { get; private set; }

        /// <summary>Level that opens the gate this frame.</summary>
        public float ThresholdDb { get; private set; }
        /// <summary>Level that closes it again.</summary>
        public float CloseThresholdDb { get; private set; }
        /// <summary>Level that opens the mouth fully this frame.</summary>
        public float FullLevelDb { get; private set; }
        /// <summary>Tracked background noise (adaptive mode only).</summary>
        public float NoiseFloorDb { get; private set; }

        public NoiseGate() => Reset();

        public void Reset()
        {
            tracking = false;
            closedTime = 0f;
            peakDb = LevelMeter.MinDecibels;
            IsOpen = false;
            IsSpeaking = false;
            ThresholdDb = 0f;
            CloseThresholdDb = 0f;
            FullLevelDb = 0f;
            NoiseFloorDb = LevelMeter.MinDecibels;
        }

        /// <param name="adapt">
        /// False for clean levels that already sit on the profile's scale (typed dialogue, normalized SDK levels):
        /// they have no background noise to follow, and a steady level would otherwise be taken for noise.
        /// </param>
        public void Update(float levelDb, LevelSettings settings, float dt, bool adapt = true)
        {
            float threshold = settings.gateThresholdDb;
            float full = settings.fullLevelDb;

            if (settings.adaptive && adapt)
            {
                if (!tracking)
                {
                    NoiseFloorDb = levelDb;
                    peakDb = full;
                    tracking = true;
                }

                // The floor follows dips quickly and rises slowly, so it sits on the quiet gaps between words.
                NoiseFloorDb = levelDb < NoiseFloorDb
                    ? Mathf.Max(levelDb, NoiseFloorDb - settings.noiseFloorFallRate * dt)
                    : Mathf.Min(levelDb, NoiseFloorDb + settings.noiseFloorRiseRate * dt);
                threshold = Mathf.Max(settings.adaptiveMinGateDb, NoiseFloorDb + settings.noiseFloorMarginDb);

                // The loudest recent speech becomes the full level, so quiet voices still open the mouth.
                peakDb = IsOpen && levelDb > peakDb ? levelDb : peakDb - settings.peakDecayRate * dt;
                full = Mathf.Min(settings.fullLevelDb, Mathf.Max(peakDb, threshold + settings.minRangeDb));
            }

            ThresholdDb = threshold;
            CloseThresholdDb = threshold - Mathf.Max(0f, settings.gateHysteresisDb);
            FullLevelDb = Mathf.Max(full, threshold + 1f);

            if (!IsOpen && levelDb >= ThresholdDb) IsOpen = true;
            else if (IsOpen && levelDb < CloseThresholdDb) IsOpen = false;

            if (IsOpen)
            {
                IsSpeaking = true;
                closedTime = 0f;
            }
            else if (IsSpeaking)
            {
                closedTime += dt;
                if (closedTime >= settings.holdTime) IsSpeaking = false;
            }
        }

        /// <summary>0–1 position of the level between the close threshold and the full level; 0 while closed.</summary>
        public float Normalize(float levelDb)
        {
            if (!IsOpen) return 0f;
            return Mathf.InverseLerp(CloseThresholdDb, FullLevelDb, levelDb);
        }
    }
}
