using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Shows an object while the voice speaks (a bubble or icon above the head, a "talking" UI badge), pulses it with
    /// the voice and keeps it a moment after the last word, so it does not blink between sentences.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Speaking Indicator")]
    public sealed class SpeakingIndicator : MonoBehaviour
    {
        [Tooltip("The voice to follow. Empty = the first Talk Voice on this object or its parents.")]
        [SerializeField] TalkVoice voice;
        [Tooltip("Shown while speaking. It should not be this object or a parent of it.")]
        [SerializeField] GameObject indicator;
        [Tooltip("Stays this long (s) after speaking ends.")]
        [Min(0f)] [SerializeField] float linger = 0.3f;
        [Tooltip("Scale growth at full mouth opening (0 = no pulse).")]
        [Range(0f, 1f)] [SerializeField] float pulse = 0.25f;
        [Tooltip("Turn the indicator to face the camera (for world space icons).")]
        [SerializeField] bool faceCamera = true;

        GameObject captured;
        Vector3 restScale = Vector3.one;
        float quiet = float.MaxValue;
        bool shown;

        public TalkVoice Voice
        {
            get => voice;
            set => voice = value;
        }

        public GameObject Indicator
        {
            get => indicator;
            set
            {
                indicator = value;
                Capture();
            }
        }

        public float Linger
        {
            get => linger;
            set => linger = Mathf.Max(0f, value);
        }

        public float Pulse
        {
            get => pulse;
            set => pulse = Mathf.Clamp01(value);
        }

        public bool FaceCamera
        {
            get => faceCamera;
            set => faceCamera = value;
        }

        public bool IsShown => shown;

        void Reset() => voice = GetComponentInParent<TalkVoice>();

        void Awake()
        {
            if (voice == null) voice = GetComponentInParent<TalkVoice>();
            Capture();
        }

        void Capture()
        {
            captured = indicator;
            if (indicator == null) return;
            restScale = indicator.transform.localScale;
            shown = indicator.activeSelf;
            Show(false);
        }

        void LateUpdate() => Tick(Time.unscaledDeltaTime);

        /// <summary>Updates one frame. Public so tests can drive it.</summary>
        public void Tick(float dt)
        {
            if (indicator == null) return;
            if (captured != indicator) Capture();
            bool speaking = voice != null && voice.IsSpeaking;
            quiet = speaking ? 0f : quiet + dt;
            Show(speaking || quiet <= linger);
            if (!shown) return;

            float open = voice != null ? voice.State.openness : 0f;
            indicator.transform.localScale = restScale * (1f + pulse * open);
            if (faceCamera)
            {
                Camera cam = Camera.main;
                if (cam != null) indicator.transform.rotation = cam.transform.rotation;
            }
        }

        void Show(bool on)
        {
            if (shown == on) return;
            shown = on;
            indicator.SetActive(on);
            if (!on) indicator.transform.localScale = restScale;
        }
    }
}
