using UnityEngine;
using Wobblefolk.LookAt;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// A Wobblefolk Look At target: whoever is speaking nearby. Listeners turn their eyes and head to the loudest
    /// voice in range, stay with it for a moment when people talk over each other, and fall back to another target
    /// (or straight ahead) when everyone is quiet. Needs the com.atthree.wobblefolk.lookat package.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Speaker Look Target")]
    public sealed class SpeakerLookTarget : LookTargetProvider
    {
        [Tooltip("The listener's own voice, never looked at. Empty = the Talk Voice on this object or its parents.")]
        [SerializeField] TalkVoice ownVoice;
        [Tooltip("Only voices this close (m) are heard.")]
        [Min(0f)] [SerializeField] float range = 12f;
        [Tooltip("Stay with a speaker at least this long (s) before turning to a louder one.")]
        [Min(0f)] [SerializeField] float minHold = 0.8f;
        [Tooltip("How much louder (0–1) another voice must be to take over.")]
        [Range(0f, 1f)] [SerializeField] float switchMargin = 0.25f;
        [Tooltip("Keep looking at a speaker this long (s) after they stop.")]
        [Min(0f)] [SerializeField] float linger = 1f;
        [Tooltip("Target used when nobody speaks. Empty = no target (the look relaxes).")]
        [SerializeField] LookTargetProvider fallback;

        readonly SpeakerTracker tracker = new SpeakerTracker();
        TalkVoice lastSpeaker;
        float quiet = float.MaxValue;

        public TalkVoice OwnVoice
        {
            get => ownVoice;
            set => ownVoice = value;
        }

        public LookTargetProvider Fallback
        {
            get => fallback;
            set => fallback = value;
        }

        /// <summary>The voice looked at now, or null.</summary>
        public TalkVoice Speaker => lastSpeaker != null && quiet <= linger ? lastSpeaker : null;

        void Awake()
        {
            if (ownVoice == null) ownVoice = GetComponentInParent<TalkVoice>();
        }

        void Update() => quiet += Time.deltaTime;

        protected override bool Resolve(in LookContext context, out Vector3 worldPoint)
        {
            tracker.range = range;
            tracker.minHold = minHold;
            tracker.switchMargin = switchMargin;
            TalkVoice speaker = tracker.Update(context.origin, Time.timeAsDouble, ownVoice);
            if (speaker != null)
            {
                lastSpeaker = speaker;
                quiet = 0f;
            }

            TalkVoice target = Speaker;
            if (target != null)
            {
                worldPoint = target.SpeakerPoint.position;
                return true;
            }

            if (fallback != null && fallback != this) return fallback.TryGetTarget(context, out worldPoint);
            worldPoint = default;
            return false;
        }
    }
}
