using System.Collections.Generic;
using UnityEngine;
using Wobblefolk.LookAt;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.TalkMouth.Samples
{
    /// <summary>
    /// Builds the sample at runtime: three primitive characters chat in turns with typed lines and babble voices, and
    /// everyone listening turns eyes and head to whoever speaks (Speaker Look Target). Press 2 to talk into the
    /// microphone: they turn to you, the camera. Space pauses the chat. Requires the com.atthree.wobblefolk.lookat
    /// package.
    /// </summary>
    public sealed class LookAtTalkSampleSetup : MonoBehaviour
    {
        public Material[] shirtMaterials = new Material[0];
        public Material skinMaterial;
        public Material chinMaterial;
        public Material mouthMaterial;
        public Material eyeWhiteMaterial;
        public Material pupilMaterial;
        public Material bubbleMaterial;
        public TalkMouthProfile profile;
        [Min(0.5f)] public float circleRadius = 1.1f;
        [Min(0f)] public float pauseBetweenLines = 0.5f;

        [TextArea]
        public string[] conversation =
        {
            "So, did anyone see where the balloon went?",
            "It floated over the hill. I chased it for an hour!",
            "Mm-hmm. And then?",
            "Then a bird popped it. Very rude bird.",
            "Ha! Serves you right for chasing balloons.",
            "Hey, you over there! Say something!",
        };

        static readonly float[] Pitches = { 210f, 140f, 300f };

        readonly List<TextDrivenSignal> speakers = new List<TextDrivenSignal>();
        MicrophoneSignal microphone;
        int line;
        float wait;
        bool paused;
        bool micOn;

        void Awake()
        {
            for (int i = 0; i < 3; i++)
            {
                // Spread over the far half of a circle, all facing its middle; the camera stands at the near side.
                float angle = Mathf.Lerp(-70f, 70f, i / 2f) * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * circleRadius;
                speakers.Add(BuildCharacter("Talker " + (i + 1), position, i));
            }

            // The player is a voice too: the camera, heard through the microphone.
            Camera cam = Camera.main;
            microphone = cam.gameObject.AddComponent<MicrophoneSignal>();
            microphone.enabled = false;
            var playerVoice = cam.gameObject.AddComponent<TalkVoice>();
            playerVoice.SetSource(microphone);
        }

        TextDrivenSignal BuildCharacter(string name, Vector3 position, int index)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = position;
            root.localRotation = Quaternion.LookRotation(-position.normalized, Vector3.up);

            Material shirt = shirtMaterials.Length > 0 ? shirtMaterials[index % shirtMaterials.Length] : skinMaterial;
            Visual(root, PrimitiveType.Capsule, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.55f, 0.4f), shirt);
            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, 1.55f, 0f);
            Visual(head, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.42f, skinMaterial);
            Transform leftEye = Eye(head, -0.075f);
            Transform rightEye = Eye(head, 0.075f);
            Visual(head, PrimitiveType.Sphere, new Vector3(0f, -0.01f, 0.21f), new Vector3(0.05f, 0.06f, 0.06f), chinMaterial);
            Transform lips = Visual(head, PrimitiveType.Sphere, new Vector3(0f, -0.085f, 0.18f), new Vector3(0.1f, 0.022f, 0.05f), mouthMaterial);
            var jaw = new GameObject("Jaw Pivot").transform;
            jaw.SetParent(head, false);
            jaw.localPosition = new Vector3(0f, -0.05f, -0.03f);
            Visual(jaw, PrimitiveType.Capsule, new Vector3(0f, -0.09f, 0.16f), new Vector3(0.085f, 0.12f, 0.085f), chinMaterial, new Vector3(0f, 0f, 90f));
            Transform bubble = Visual(root, PrimitiveType.Sphere, new Vector3(0.22f, 1.95f, 0f), new Vector3(0.18f, 0.12f, 0.04f), bubbleMaterial);

            // Voice: typed lines with a babble voice of its own pitch.
            var babble = head.gameObject.AddComponent<AudioSource>();
            babble.spatialBlend = 0.6f;
            var text = head.gameObject.AddComponent<TextDrivenSignal>();
            text.BabbleOutput = babble;
            text.BabblePitch = Pitches[index % Pitches.Length];
            var voice = head.gameObject.AddComponent<TalkVoice>();
            if (profile != null) voice.Profile = profile;
            voice.SetSource(text);

            var jawDriver = jaw.gameObject.AddComponent<JawBoneMouthDriver>();
            jawDriver.Voice = voice;
            jawDriver.OpenAngle = 16f;
            var lipDriver = lips.gameObject.AddComponent<TransformMouthDriver>();
            lipDriver.Voice = voice;
            lipDriver.OpenScale = new Vector3(1f, 3f, 1f);
            lipDriver.UseShapes = true;
            var indicator = head.gameObject.AddComponent<SpeakingIndicator>();
            indicator.Voice = voice;
            indicator.Indicator = bubble.gameObject;

            // Listening: eyes and head turn to whoever speaks.
            var lookDriver = root.gameObject.AddComponent<TransformLookDriver>();
            lookDriver.head = head;
            lookDriver.leftEye = leftEye;
            lookDriver.rightEye = rightEye;
            var speakerTarget = root.gameObject.AddComponent<SpeakerLookTarget>();
            speakerTarget.OwnVoice = voice;
            var lookProfile = ScriptableObject.CreateInstance<LookProfile>();
            LookProfilePresets.Apply(lookProfile, LookPreset.Realistic);
            var look = root.gameObject.AddComponent<LookAtController>();
            look.Profile = lookProfile;
            look.TargetProvider = speakerTarget;
            look.RefreshDrivers();
            return text;
        }

        Transform Eye(Transform head, float x)
        {
            var eye = new GameObject(x < 0f ? "Left Eye" : "Right Eye").transform;
            eye.SetParent(head, false);
            eye.localPosition = new Vector3(x, 0.04f, 0.17f);
            Visual(eye, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.085f, eyeWhiteMaterial);
            Visual(eye, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.035f), Vector3.one * 0.04f, pupilMaterial);
            return eye;
        }

        static Transform Visual(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, Vector3 euler = default)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            return g.transform;
        }

        void Update()
        {
            if (Pressed(KeyCode.Space)) paused = !paused;
            if (Pressed(KeyCode.Alpha2))
            {
                micOn = !micOn;
                microphone.enabled = micOn;
            }

            if (paused || conversation.Length == 0 || speakers.Exists(s => s.IsSaying)) return;
            wait += Time.deltaTime;
            if (wait < pauseBetweenLines) return;

            wait = 0f;
            speakers[line % speakers.Count].Say(conversation[line % conversation.Length]);
            line++;
        }

        void OnGUI()
        {
            string text =
                $"Space: chat {(paused ? "paused" : "on")}    2: microphone {(micOn ? "on - talk to them!" : "off")}\n" +
                "Listeners turn to whoever speaks.";
            GUI.Box(new Rect(10f, 10f, 420f, 46f), text);
        }

        static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Alpha2: return keyboard.digit2Key.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
