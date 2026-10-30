using UnityEngine;
using Wobblefolk.Ragdoll;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.TalkMouth.Samples
{
    /// <summary>
    /// Builds the sample at runtime: a primitive Wobblefolk Active Ragdoll with a physical jaw. The jaw is its own
    /// rigidbody on a ConfigurableJoint, opened by Joint Mouth Driver through the joint's target rotation only, so it
    /// wobbles with every step and fall. WASD walks, 1 / 2 switch synthetic voice and microphone, M mutes, K knocks the
    /// ragdoll out (the voice falls quiet and the jaw hangs open until it wakes). Requires the com.wobblefolk.active-ragdoll package.
    /// </summary>
    public sealed class RagdollTalkSampleSetup : MonoBehaviour
    {
        public Material bodyMaterial;
        public Material jawMaterial;
        public Material eyeWhiteMaterial;
        public Material pupilMaterial;
        public Material bubbleMaterial;
        public TalkMouthProfile profile;
        [Range(60f, 400f)] public float voicePitch = 170f;
        [Tooltip("Jaw opening (°) at full voice.")]
        [Range(5f, 45f)] public float jawAngle = 28f;
        [Tooltip("How fast the jaw follows its target (Hz). Lower = floppier.")]
        [Range(1f, 15f)] public float jawFrequency = 10f;
        [Tooltip("Damping ratio of the jaw (below 1 bounces).")]
        [Range(0.05f, 1f)] public float jawDamping = 0.35f;

        ActiveRagdoll ragdoll;
        Rigidbody jawBody;
        ConfigurableJoint jawJoint;
        JointDrive awakeDrive;
        TalkVoice voice;
        AudioSource voiceAudio;
        AudioSourceSignal clipSignal;
        MicrophoneSignal microphone;
        AudioClip clip;
        bool useMicrophone;

        void Awake()
        {
            new GameObject("Ragdoll Physics Settings").AddComponent<RagdollPhysicsSettings>();
            var profiles = new RagdollProfileSet
            {
                ragdoll = ScriptableObject.CreateInstance<RagdollProfile>(),
                balance = ScriptableObject.CreateInstance<BalanceProfile>(),
                locomotion = ScriptableObject.CreateInstance<LocomotionProfile>(),
                arms = ScriptableObject.CreateInstance<ArmProfile>(),
                carry = ScriptableObject.CreateInstance<CarryProfile>(),
                climb = ScriptableObject.CreateInstance<ClimbProfile>(),
            };
            ragdoll = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), profiles, transform.position, transform.rotation,
                bodyMaterial, "Talking Ragdoll");

            Transform head = ragdoll.Head.transform;
            var skull = ragdoll.Head.MainCollider as SphereCollider;
            Vector3 center = skull != null ? skull.center : new Vector3(0f, 0.18f, 0f);
            float radius = skull != null ? skull.radius : 0.12f;

            Eye(head, center + new Vector3(-0.4f * radius, 0.25f * radius, 0.8f * radius), radius);
            Eye(head, center + new Vector3(0.4f * radius, 0.25f * radius, 0.8f * radius), radius);
            Transform jaw = CreateJaw(head, center, radius);

            // The voice lives on the head, so listeners (and the sound) follow it.
            GameObject voiceObject = head.gameObject;
            voiceAudio = voiceObject.AddComponent<AudioSource>();
            voiceAudio.spatialBlend = 0.5f;
            voiceAudio.loop = true;
            clipSignal = voiceObject.AddComponent<AudioSourceSignal>();
            microphone = voiceObject.AddComponent<MicrophoneSignal>();
            microphone.enabled = false;
            voice = voiceObject.AddComponent<TalkVoice>();
            if (profile != null) voice.Profile = profile;
            voice.SetSource(clipSignal);

            var jawDriver = jaw.gameObject.AddComponent<JointMouthDriver>();
            jawDriver.Voice = voice;
            jawDriver.OpenAngle = jawAngle;

            var bubble = Visual(head, PrimitiveType.Sphere, center + new Vector3(0.9f * radius, 2.2f * radius, 0f),
                new Vector3(1.1f, 0.75f, 0.25f) * radius, bubbleMaterial);
            var indicator = voiceObject.AddComponent<SpeakingIndicator>();
            indicator.Voice = voice;
            indicator.Indicator = bubble.gameObject;

            ragdoll.StateChanged += (from, to) => SetConscious(to != RagdollState.Unconscious);

            clip = SyntheticSpeech.CreateClip("Ragdoll Voice", 30f, 44100, 13, voicePitch);
            voiceAudio.clip = clip;
            voiceAudio.Play();

            Camera cam = Camera.main;
            var follow = cam.GetComponent<RagdollFollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<RagdollFollowCamera>();
            follow.Target = ragdoll;
            var input = ragdoll.gameObject.AddComponent<RagdollPlayerInput>();
            input.FollowCamera = follow;
        }

        /// <summary>
        /// A chin on its own rigidbody, hinged at the back of the head's lower half: locked in place, free to swing
        /// about its X axis, and pulled to the target rotation by a slerp drive tuned from its inertia.
        /// </summary>
        Transform CreateJaw(Transform head, Vector3 center, float radius)
        {
            var jaw = new GameObject("Jaw");
            jaw.transform.SetParent(head, false);
            jaw.transform.localPosition = center + new Vector3(0f, -0.3f * radius, -0.25f * radius);
            jaw.transform.localRotation = Quaternion.identity;

            Vector3 chin = new Vector3(0f, -0.45f * radius, 0.85f * radius);
            Visual(jaw.transform, PrimitiveType.Capsule, chin, new Vector3(0.6f, 0.55f, 0.6f) * radius, jawMaterial, new Vector3(0f, 0f, 90f));
            var collider = jaw.AddComponent<CapsuleCollider>();
            collider.direction = 0;
            collider.center = chin;
            collider.radius = 0.3f * radius;
            collider.height = 1.1f * radius;
            foreach (Collider other in ragdoll.GetComponentsInChildren<Collider>())
            {
                if (other != collider) Physics.IgnoreCollision(collider, other);
            }

            Rigidbody headBody = ragdoll.Head.Body;
            var body = jawBody = jaw.AddComponent<Rigidbody>();
            // Awake, the jaw muscles hold it against gravity; see SetConscious.
            body.useGravity = false;
            body.mass = Mathf.Max(0.05f, headBody.mass * 0.12f);
            body.interpolation = headBody.interpolation;
            body.collisionDetectionMode = headBody.collisionDetectionMode;
            // Mostly the chin's distance from the hinge.
            float inertia = body.mass * chin.sqrMagnitude * 1.2f;
            body.inertiaTensor = Vector3.one * inertia;
            body.inertiaTensorRotation = Quaternion.identity;

            var joint = jawJoint = jaw.AddComponent<ConfigurableJoint>();
            joint.connectedBody = headBody;
            joint.axis = Vector3.right;
            joint.secondaryAxis = Vector3.up;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -50f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 50f };
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            float omega = 2f * Mathf.PI * jawFrequency;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = inertia * omega * omega,
                positionDamper = 2f * jawDamping * inertia * omega,
                maximumForce = float.MaxValue,
            };
            awakeDrive = joint.slerpDrive;
            return jaw.transform;
        }

        /// <summary>
        /// Knocked out, the voice goes quiet and the jaw muscles let go: it hangs open under gravity. Awake again, the
        /// jaw is held and the mouth follows the voice.
        /// </summary>
        void SetConscious(bool conscious)
        {
            voice.SetEnabled(conscious);
            jawBody.useGravity = !conscious;
            JointDrive drive = awakeDrive;
            if (!conscious)
            {
                drive.positionSpring *= 0.02f;
                drive.positionDamper *= 0.3f;
            }

            jawJoint.slerpDrive = drive;
        }

        void Eye(Transform head, Vector3 position, float radius)
        {
            Transform eye = Visual(head, PrimitiveType.Sphere, position, Vector3.one * 0.5f * radius, eyeWhiteMaterial);
            Visual(eye, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.4f), Vector3.one * 0.5f, pupilMaterial);
        }

        static Transform Visual(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, Vector3 euler = default)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            // Immediately: a collider left until the end of the frame would join the parent's rigidbody.
            DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            return g.transform;
        }

        void OnDestroy()
        {
            if (clip != null) Destroy(clip);
        }

        void Update()
        {
            if (Pressed(KeyCode.Alpha1)) SetMicrophone(false);
            if (Pressed(KeyCode.Alpha2)) SetMicrophone(true);
            if (Pressed(KeyCode.M)) voice.SetEnabled(!voice.IsVoiceEnabled);
            if (Pressed(KeyCode.K)) ragdoll.SetState(RagdollState.Unconscious);
        }

        void SetMicrophone(bool on)
        {
            useMicrophone = on;
            microphone.enabled = on;
            if (on) voiceAudio.Stop();
            else voiceAudio.Play();
            voice.SetSource(on ? (VoiceSignalSource)microphone : clipSignal);
        }

        void OnGUI()
        {
            string text =
                $"WASD: walk    1 / 2: {(useMicrophone ? "microphone" : "synthetic voice")}    M: mouth {(voice.IsVoiceEnabled ? "on" : "muted")}    K: knock out\n" +
                $"{ragdoll.State}, {(voice.IsSpeaking ? "speaking" : "quiet")}, open {voice.State.openness:0.00}";
            GUI.Box(new Rect(10f, 10f, 520f, 46f), text);
        }

        static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.Alpha1: return keyboard.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return keyboard.digit2Key.wasPressedThisFrame;
                case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
                case KeyCode.K: return keyboard.kKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
