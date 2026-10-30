using UnityEditor;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Editor
{
    /// <summary>Live meter in Play Mode: level against the gate, mouth opening and shapes.</summary>
    [CustomEditor(typeof(TalkVoice))]
    sealed class TalkVoiceEditor : UnityEditor.Editor
    {
        static readonly Color Background = new Color(0f, 0f, 0f, 0.25f);
        static readonly Color LevelColor = new Color(0.35f, 0.75f, 1f);
        static readonly Color OpenColor = new Color(0.45f, 0.9f, 0.45f);
        static readonly Color ShapeColor = new Color(1f, 0.75f, 0.3f);
        static readonly Color GateColor = new Color(1f, 0.35f, 0.3f);
        const float MeterMinDb = -80f;

        static readonly string[] ShapeNames = { "Closed", "AA", "OH", "EE", "FF" };

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var voice = (TalkVoice)target;
            if (voice.Source == null && voice.GetComponent<VoiceSignalSource>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Add a voice source: Audio Source Signal, Microphone Signal, External Level Signal or Text Driven Signal.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see the voice level and mouth shapes here.", MessageType.None);
                return;
            }

            MouthState state = voice.State;
            NoiseGate gate = voice.Analyzer.Gate;

            Rect rect = Bar("Level", Normalize(state.levelDb), LevelColor, $"{state.levelDb:0} dB");
            Marker(rect, Normalize(gate.ThresholdDb), GateColor);
            Marker(rect, Normalize(gate.FullLevelDb), OpenColor);
            Bar("Opening", state.openness, OpenColor, state.openness.ToString("0.00"));

            EditorGUILayout.LabelField("Speaking", state.isSpeaking ? "yes" : "no");
            EditorGUILayout.LabelField("Shape", state.Dominant.ToString());
            EditorGUILayout.LabelField("Detail", voice.Lod.ToString());
            for (int i = 0; i < VisemeWeights.Count; i++)
            {
                float w = state.visemes[i];
                Bar(ShapeNames[i], w, ShapeColor, w.ToString("0.00"));
            }

            EditorGUILayout.HelpBox("Red mark: gate threshold. Green mark: full opening level.", MessageType.None);
        }

        static float Normalize(float db) => Mathf.InverseLerp(MeterMinDb, 0f, db);

        static Rect Bar(string label, float value, Color color, string text)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
            EditorGUI.DrawRect(rect, Background);
            Rect fill = rect;
            fill.width *= Mathf.Clamp01(value);
            EditorGUI.DrawRect(fill, color);
            EditorGUI.LabelField(rect, text, EditorStyles.miniLabel);
            return rect;
        }

        static void Marker(Rect bar, float at, Color color)
        {
            var mark = new Rect(bar.x + bar.width * Mathf.Clamp01(at) - 1f, bar.y, 2f, bar.height);
            EditorGUI.DrawRect(mark, color);
        }
    }
}
