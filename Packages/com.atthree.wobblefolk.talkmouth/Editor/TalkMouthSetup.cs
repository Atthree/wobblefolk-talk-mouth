using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Editor
{
    public enum SetupSource
    {
        [InspectorName("Audio Source (dialogue clips)")] AudioSource = 0,
        [InspectorName("Baked clips")] Baked = 1,
        Microphone = 2,
        [InspectorName("External (voice chat SDK)")] External = 3,
        [InspectorName("Typed text")] Text = 4,
    }

    public enum SetupDriver
    {
        [InspectorName("Jaw bone")] JawBone = 0,
        [InspectorName("Blend shapes")] BlendShapes = 1,
        [InspectorName("Physical jaw (joint)")] Joint = 2,
        [InspectorName("Split head (hinge)")] HingeSplit = 3,
        [InspectorName("Sprites")] Sprite = 4,
        [InspectorName("Animator parameters")] Animator = 5,
        [InspectorName("Transform")] Transform = 6,
    }

    /// <summary>What the setup tool found on a character and what it will add.</summary>
    public sealed class TalkMouthSetupPlan
    {
        public GameObject root;
        public SetupSource source;
        public SetupDriver driver;
        public TalkMouthPreset preset;
        public Transform jaw;
        public ConfigurableJoint jawJoint;
        public SkinnedMeshRenderer face;
        public SpriteRenderer mouthSprite;
        public Animator animator;
        public Transform upperHalf;
        public Transform lowerHalf;
        public Transform mouthObject;
        public readonly List<string> findings = new List<string>();

        /// <summary>The object the driver goes on.</summary>
        public Transform DriverTarget
        {
            get
            {
                switch (driver)
                {
                    case SetupDriver.JawBone: return jaw;
                    case SetupDriver.Joint: return jawJoint != null ? jawJoint.transform : null;
                    case SetupDriver.BlendShapes: return face != null ? face.transform : null;
                    case SetupDriver.Sprite: return mouthSprite != null ? mouthSprite.transform : null;
                    case SetupDriver.Animator: return animator != null ? animator.transform : null;
                    case SetupDriver.HingeSplit: return upperHalf != null ? root.transform : null;
                    default: return mouthObject;
                }
            }
        }

        public bool CanApply => root != null && DriverTarget != null;
    }

    /// <summary>
    /// Finds what a character can talk with (a humanoid jaw bone, mouth blend shapes, a jaw joint, split head halves,
    /// a mouth sprite, Animator talk parameters) and adds a voice, a source and a fitting driver in one undo step.
    /// </summary>
    public static class TalkMouthSetup
    {
        static readonly string[] JawNames = { "jaw", "mandible", "chin" };
        // Whole names (spaces, dashes and underscores ignored), so "UpperArm" or "LowerLeg" never count as halves.
        static readonly string[] UpperNames = { "top", "upper", "tophalf", "upperhalf", "headtop", "lid" };
        static readonly string[] LowerNames = { "bottom", "lower", "bottomhalf", "lowerhalf", "headbottom" };
        static readonly string[] MouthNames = { "mouth", "lips" };

        public static TalkMouthSetupPlan Analyze(GameObject root)
        {
            var plan = new TalkMouthSetupPlan { root = root };
            if (root == null) return plan;
            Transform t = root.transform;

            plan.animator = root.GetComponentInChildren<Animator>();
            if (plan.animator != null && plan.animator.isHuman)
            {
                plan.jaw = plan.animator.GetBoneTransform(HumanBodyBones.Jaw);
                if (plan.jaw != null) plan.findings.Add($"Humanoid jaw bone: {plan.jaw.name}");
            }

            if (plan.jaw == null) plan.jaw = FindByName(t, JawNames);
            if (plan.jaw != null)
            {
                plan.jawJoint = plan.jaw.GetComponent<ConfigurableJoint>();
                if (plan.jawJoint != null) plan.findings.Add($"Jaw joint: {plan.jaw.name}");
                else if (!plan.findings.Exists(f => f.StartsWith("Humanoid"))) plan.findings.Add($"Jaw: {plan.jaw.name}");
            }

            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int mouthShapes = CountMouthShapes(smr);
                if (mouthShapes == 0) continue;
                if (plan.face == null || mouthShapes > CountMouthShapes(plan.face)) plan.face = smr;
            }

            if (plan.face != null) plan.findings.Add($"Mouth blend shapes: {CountMouthShapes(plan.face)} on {plan.face.name}");

            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (NameMatches(sr.name, MouthNames))
                {
                    plan.mouthSprite = sr;
                    plan.findings.Add($"Mouth sprite: {sr.name}");
                    break;
                }
            }

            bool talkParameters = plan.animator != null && HasFloatParameter(plan.animator, "MouthOpen");
            if (talkParameters) plan.findings.Add("Animator parameter: MouthOpen");

            plan.upperHalf = FindByWholeName(t, UpperNames);
            plan.lowerHalf = FindByWholeName(t, LowerNames);
            if (plan.upperHalf != null && plan.lowerHalf != null)
            {
                plan.findings.Add($"Split head halves: {plan.upperHalf.name} / {plan.lowerHalf.name}");
            }
            else
            {
                plan.upperHalf = null;
                plan.lowerHalf = null;
            }

            plan.mouthObject = FindByName(t, MouthNames) ?? t;

            // The most specific rig wins.
            if (plan.face != null) plan.driver = SetupDriver.BlendShapes;
            else if (plan.jawJoint != null) plan.driver = SetupDriver.Joint;
            else if (plan.jaw != null) plan.driver = SetupDriver.JawBone;
            else if (talkParameters) plan.driver = SetupDriver.Animator;
            else if (plan.upperHalf != null) plan.driver = SetupDriver.HingeSplit;
            else if (plan.mouthSprite != null) plan.driver = SetupDriver.Sprite;
            else plan.driver = SetupDriver.Transform;

            plan.preset = DefaultPreset(plan.driver);
            plan.source = SetupSource.AudioSource;
            if (plan.findings.Count == 0) plan.findings.Add("No jaw, blend shapes, halves or sprite found: a Transform driver will move the whole object.");
            return plan;
        }

        public static TalkMouthPreset DefaultPreset(SetupDriver driver)
        {
            switch (driver)
            {
                case SetupDriver.HingeSplit: return TalkMouthPreset.BouncySplitHead;
                case SetupDriver.Joint: return TalkMouthPreset.Cartoon;
                case SetupDriver.Sprite: return TalkMouthPreset.Cartoon;
                default: return TalkMouthPreset.Human;
            }
        }

        /// <summary>
        /// Adds (or reuses) the voice, its source and the driver, as one undo step. A null <paramref name="profile"/>
        /// leaves the voice's profile as it is (empty = Human at runtime).
        /// </summary>
        public static TalkVoice Apply(TalkMouthSetupPlan plan, TalkMouthProfile profile)
        {
            if (!plan.CanApply) return null;
            Undo.SetCurrentGroupName("Set Up Talk Mouth");
            int group = Undo.GetCurrentGroup();
            GameObject root = plan.root;

            VoiceSignalSource source = AddSource(root, plan.source);
            TalkVoice voice = Ensure<TalkVoice>(root);
            Undo.RecordObject(voice, "Set Up Talk Mouth");
            if (profile != null) voice.Profile = profile;
            voice.SetSource(source);

            MouthDriver driver = AddDriver(plan, voice);
            if (driver != null)
            {
                Undo.RecordObject(driver, "Set Up Talk Mouth");
                driver.Voice = voice;
                EditorUtility.SetDirty(driver);
            }

            EditorUtility.SetDirty(voice);
            Undo.CollapseUndoOperations(group);
            return voice;
        }

        static VoiceSignalSource AddSource(GameObject root, SetupSource kind)
        {
            switch (kind)
            {
                case SetupSource.Baked:
                    Ensure<AudioSource>(root);
                    return Ensure<BakedVisemeSignal>(root);
                case SetupSource.Microphone:
                    return Ensure<MicrophoneSignal>(root);
                case SetupSource.External:
                    return Ensure<ExternalLevelSignal>(root);
                case SetupSource.Text:
                    return Ensure<TextDrivenSignal>(root);
                default:
                    Ensure<AudioSource>(root);
                    return Ensure<AudioSourceSignal>(root);
            }
        }

        static MouthDriver AddDriver(TalkMouthSetupPlan plan, TalkVoice voice)
        {
            GameObject target = plan.DriverTarget.gameObject;
            switch (plan.driver)
            {
                case SetupDriver.JawBone:
                    return Ensure<JawBoneMouthDriver>(target);
                case SetupDriver.Joint:
                    return Ensure<JointMouthDriver>(target);
                case SetupDriver.BlendShapes:
                {
                    var blend = Ensure<BlendShapeMouthDriver>(target);
                    Undo.RecordObject(blend, "Set Up Talk Mouth");
                    blend.FindBlendShapesByName();
                    return blend;
                }
                case SetupDriver.Sprite:
                    return Ensure<SpriteMouthDriver>(target);
                case SetupDriver.Animator:
                    return Ensure<AnimatorParameterDriver>(target);
                case SetupDriver.HingeSplit:
                {
                    var split = Ensure<HingeSplitMouthDriver>(target);
                    Undo.RecordObject(split, "Set Up Talk Mouth");
                    split.Hinge = CreateHinge(plan);
                    split.Axis = Vector3.left;
                    if (!split.Parts.Exists(p => p != null && p.part == plan.upperHalf)) split.Parts.Add(new HingePart(plan.upperHalf));
                    if (!split.Parts.Exists(p => p != null && p.part == plan.lowerHalf)) split.Parts.Add(new HingePart(plan.lowerHalf, 0.15f, true));
                    return split;
                }
                default:
                    return Ensure<TransformMouthDriver>(target);
            }
        }

        /// <summary>A hinge at the back of the halves, where they meet, outside both of them.</summary>
        static Transform CreateHinge(TalkMouthSetupPlan plan)
        {
            Transform root = plan.root.transform;
            Transform existing = root.Find("Talk Hinge");
            if (existing != null) return existing;

            Bounds upper = WorldBounds(plan.upperHalf);
            Bounds lower = WorldBounds(plan.lowerHalf);
            Bounds both = upper;
            both.Encapsulate(lower);
            float seam = (upper.min.y + lower.max.y) * 0.5f;
            Vector3 back = both.center - root.forward * Vector3.Dot(both.extents, Abs(root.forward));

            var hinge = new GameObject("Talk Hinge");
            Undo.RegisterCreatedObjectUndo(hinge, "Set Up Talk Mouth");
            hinge.transform.SetParent(root, false);
            hinge.transform.position = new Vector3(back.x, seam, back.z);
            hinge.transform.rotation = root.rotation;
            return hinge.transform;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Bounds WorldBounds(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        // Not "??": in the editor a missing component is a fake null object.
        static T Ensure<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        static int CountMouthShapes(SkinnedMeshRenderer smr)
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) return 0;
            int count = 0;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (BlendShapeMouthDriver.GuessChannel(mesh.GetBlendShapeName(i), out _)) count++;
            }

            return count;
        }

        static bool HasFloatParameter(Animator animator, string parameter)
        {
            if (animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller)
            {
                foreach (AnimatorControllerParameter p in controller.parameters)
                {
                    if (p.name == parameter && p.type == AnimatorControllerParameterType.Float) return true;
                }
            }

            return false;
        }

        static Transform FindByName(Transform root, string[] names)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && NameMatches(child.name, names)) return child;
            }

            return null;
        }

        /// <summary>A child whose whole name is one of <paramref name="names"/> and that has something to show.</summary>
        static Transform FindByWholeName(Transform root, string[] names)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root || child.GetComponentInChildren<Renderer>() == null) continue;
                string n = child.name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
                if (System.Array.IndexOf(names, n) >= 0) return child;
            }

            return null;
        }

        static bool NameMatches(string name, string[] words)
        {
            string n = name.ToLowerInvariant();
            foreach (string w in words) if (n.Contains(w)) return true;
            return false;
        }
    }
}
