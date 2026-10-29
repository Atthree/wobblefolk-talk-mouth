namespace Wobblefolk.TalkMouth
{
    /// <summary>What the analyzer hears this frame: how open the mouth should be and in which shape.</summary>
    public readonly struct MouthState
    {
        /// <summary>Final mouth opening, 0 = closed, 1 = fully open, after smoothing, curve and exaggeration.</summary>
        public readonly float openness;
        /// <summary>Raw voice level mapped to 0–1 between the gate and the full level, before smoothing.</summary>
        public readonly float level;
        /// <summary>Voice level in decibels (full scale), after the input gain.</summary>
        public readonly float levelDb;
        /// <summary>The noise gate lets the sound through.</summary>
        public readonly bool gateOpen;
        /// <summary>Speaking, including short pauses between words (see the hold time).</summary>
        public readonly bool isSpeaking;
        /// <summary>Mouth shape weights; they sum to 1, and <see cref="VisemeWeights.closed"/> grows as the mouth closes.</summary>
        public readonly VisemeWeights visemes;

        public MouthState(float openness, float level, float levelDb, bool gateOpen, bool isSpeaking, in VisemeWeights visemes)
        {
            this.openness = openness;
            this.level = level;
            this.levelDb = levelDb;
            this.gateOpen = gateOpen;
            this.isSpeaking = isSpeaking;
            this.visemes = visemes;
        }

        public static MouthState Silent => new MouthState(0f, 0f, LevelMeter.MinDecibels, false, false, VisemeWeights.Rest);

        public Viseme Dominant => visemes.Dominant;

        public override string ToString() =>
            $"open {openness:0.00}, level {level:0.00} ({levelDb:0.0} dB), speaking {isSpeaking}, {visemes}";
    }
}
