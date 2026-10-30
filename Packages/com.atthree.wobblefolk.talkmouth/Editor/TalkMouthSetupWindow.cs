using UnityEditor;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Editor
{
    /// <summary>Wobblefolk > Setup Talk Mouth: makes a character talk in a few clicks.</summary>
    sealed class TalkMouthSetupWindow : EditorWindow
    {
        GameObject target;
        TalkMouthSetupPlan plan;
        bool createProfile = true;
        Vector2 scroll;

        [MenuItem("Wobblefolk/Setup Talk Mouth", false, 30)]
        static void Open()
        {
            var window = GetWindow<TalkMouthSetupWindow>("Talk Mouth Setup");
            window.minSize = new Vector2(360f, 320f);
            window.Pick(Selection.activeGameObject);
        }

        void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject != target)
            {
                Pick(Selection.activeGameObject);
                Repaint();
            }
        }

        void Pick(GameObject go)
        {
            target = go;
            plan = go != null ? TalkMouthSetup.Analyze(go) : null;
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var picked = (GameObject)EditorGUILayout.ObjectField("Root", target, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) Pick(picked);

            if (plan == null)
            {
                EditorGUILayout.HelpBox("Select the character's root object in the scene.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (EditorUtility.IsPersistent(target))
            {
                EditorGUILayout.HelpBox("Open the prefab (or use an instance in the scene) to set it up.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Found", EditorStyles.boldLabel);
            foreach (string finding in plan.findings) EditorGUILayout.LabelField("• " + finding, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Look Again", GUILayout.Width(100f))) Pick(target);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Set Up", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            plan.driver = (SetupDriver)EditorGUILayout.EnumPopup("Mouth", plan.driver);
            if (EditorGUI.EndChangeCheck()) plan.preset = TalkMouthSetup.DefaultPreset(plan.driver);
            DrawDriverTargets();
            plan.source = (SetupSource)EditorGUILayout.EnumPopup("Voice from", plan.source);
            plan.preset = (TalkMouthPreset)EditorGUILayout.EnumPopup("Preset", plan.preset);
            createProfile = EditorGUILayout.ToggleLeft("Save the preset as a profile asset (to tune it)", createProfile);
            if (!createProfile && plan.preset != TalkMouthPreset.Human)
            {
                EditorGUILayout.HelpBox("Without a profile asset the voice uses the Human preset.", MessageType.Info);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!plan.CanApply || EditorUtility.IsPersistent(target)))
            {
                if (GUILayout.Button("Set Up Talk Mouth", GUILayout.Height(28f))) Apply();
            }

            if (!plan.CanApply) EditorGUILayout.HelpBox(MissingHint(), MessageType.Warning);
            EditorGUILayout.HelpBox(SourceHint(plan.source), MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void DrawDriverTargets()
        {
            EditorGUI.indentLevel++;
            switch (plan.driver)
            {
                case SetupDriver.JawBone:
                    plan.jaw = (Transform)EditorGUILayout.ObjectField("Jaw", plan.jaw, typeof(Transform), true);
                    break;
                case SetupDriver.Joint:
                    plan.jawJoint = (ConfigurableJoint)EditorGUILayout.ObjectField("Jaw Joint", plan.jawJoint, typeof(ConfigurableJoint), true);
                    break;
                case SetupDriver.BlendShapes:
                    plan.face = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Face Mesh", plan.face, typeof(SkinnedMeshRenderer), true);
                    break;
                case SetupDriver.Sprite:
                    plan.mouthSprite = (SpriteRenderer)EditorGUILayout.ObjectField("Mouth Sprite", plan.mouthSprite, typeof(SpriteRenderer), true);
                    break;
                case SetupDriver.Animator:
                    plan.animator = (Animator)EditorGUILayout.ObjectField("Animator", plan.animator, typeof(Animator), true);
                    break;
                case SetupDriver.HingeSplit:
                    plan.upperHalf = (Transform)EditorGUILayout.ObjectField("Top Half", plan.upperHalf, typeof(Transform), true);
                    plan.lowerHalf = (Transform)EditorGUILayout.ObjectField("Bottom Half", plan.lowerHalf, typeof(Transform), true);
                    break;
                default:
                    plan.mouthObject = (Transform)EditorGUILayout.ObjectField("Moving Object", plan.mouthObject, typeof(Transform), true);
                    break;
            }

            EditorGUI.indentLevel--;
        }

        string MissingHint()
        {
            switch (plan.driver)
            {
                case SetupDriver.HingeSplit: return "A split head needs separate top and bottom meshes. Assign both halves.";
                case SetupDriver.Joint: return "Assign the jaw's ConfigurableJoint (connected to the head).";
                default: return "Assign what the mouth should move.";
            }
        }

        static string SourceHint(SetupSource source)
        {
            switch (source)
            {
                case SetupSource.Baked: return "Bake clips with Assets > Wobblefolk > Bake Talk Mouth and add them to Baked Viseme Signal.";
                case SetupSource.Microphone: return "The microphone is analysed only, never played back.";
                case SetupSource.External: return "Call ExternalLevelSignal.PushSamples or PushLevel from your voice chat SDK.";
                case SetupSource.Text: return "Call TextDrivenSignal.Say(\"...\"), or fill Say On Start.";
                default: return "Play dialogue on the AudioSource. Clips need Decompress On Load (or bake them).";
            }
        }

        void Apply()
        {
            if (plan.driver == SetupDriver.HingeSplit && (plan.upperHalf == null || plan.lowerHalf == null)) return;

            TalkMouthProfile profile = null;
            if (createProfile)
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Talk Mouth Profile", target.name + " Talk Mouth", "asset",
                    "Where to save the profile.");
                if (string.IsNullOrEmpty(path)) return;
                profile = TalkMouthPresets.Create(plan.preset);
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
            }

            TalkVoice voice = TalkMouthSetup.Apply(plan, profile);
            if (voice == null) return;
            Selection.activeGameObject = voice.gameObject;
            Debug.Log($"Talk Mouth set up on {target.name}: {plan.driver} driver, voice from {plan.source}.", voice);
            Pick(target);
        }
    }
}
