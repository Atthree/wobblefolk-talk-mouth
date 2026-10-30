using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Editor
{
    /// <summary>Assets > Wobblefolk > Bake Talk Mouth: bakes the selected audio clips into <see cref="BakedVoice"/> assets.</summary>
    static class TalkMouthBaking
    {
        const string Menu = "Assets/Wobblefolk/Bake Talk Mouth";

        [MenuItem(Menu, true)]
        static bool CanBake()
        {
            foreach (Object o in Selection.objects) if (o is AudioClip) return true;
            return false;
        }

        [MenuItem(Menu, false, 1100)]
        static void BakeSelection()
        {
            var clips = new List<AudioClip>();
            foreach (Object o in Selection.objects) if (o is AudioClip clip) clips.Add(clip);

            var baked = new List<Object>();
            try
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Bake Talk Mouth", clips[i].name, (float)i / clips.Count);
                    BakedVoice result = BakeAsset(clips[i]);
                    if (result != null) baked.Add(result);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            if (baked.Count > 0) Selection.objects = baked.ToArray();
            Debug.Log($"Talk Mouth: baked {baked.Count} of {clips.Count} clip(s).");
        }

        /// <summary>
        /// Bakes <paramref name="clip"/> into "&lt;clip&gt;.talkmouth.asset" next to it, updating an existing bake in
        /// place so references to it keep working. Clips that cannot be read are briefly reimported as Decompress On Load.
        /// </summary>
        public static BakedVoice BakeAsset(AudioClip clip, float frameRate = VoiceBaker.DefaultFrameRate)
        {
            string clipPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(clipPath)) return null;

            BakedVoice fresh = BakeReadable(clip, clipPath, frameRate);
            if (fresh == null)
            {
                Debug.LogWarning($"Talk Mouth: could not read the samples of {clip.name}.", clip);
                return null;
            }

            string bakePath = Path.ChangeExtension(clipPath, null) + ".talkmouth.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BakedVoice>(bakePath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, bakePath);
                return fresh;
            }

            Undo.RecordObject(existing, "Bake Talk Mouth");
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = Path.GetFileNameWithoutExtension(bakePath);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static BakedVoice BakeReadable(AudioClip clip, string clipPath, float frameRate)
        {
            clip.LoadAudioData();
            BakedVoice baked = VoiceBaker.Bake(clip, frameRate);
            if (baked != null) return baked;

            // Compressed or streamed: decompress for a moment, read, then put the settings back.
            if (!(AssetImporter.GetAtPath(clipPath) is AudioImporter importer)) return null;
            AudioImporterSampleSettings original = importer.defaultSampleSettings;
            AudioImporterSampleSettings readable = original;
            readable.loadType = AudioClipLoadType.DecompressOnLoad;
            try
            {
                importer.defaultSampleSettings = readable;
                importer.SaveAndReimport();
                var reloaded = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                reloaded.LoadAudioData();
                return VoiceBaker.Bake(reloaded, frameRate);
            }
            finally
            {
                importer.defaultSampleSettings = original;
                importer.SaveAndReimport();
            }
        }
    }

    [CustomEditor(typeof(BakedVoice))]
    sealed class BakedVoiceEditor : UnityEditor.Editor
    {
        static readonly Color Background = new Color(0f, 0f, 0f, 0.25f);
        static readonly Color Bar = new Color(0.45f, 0.9f, 0.45f);

        public override void OnInspectorGUI()
        {
            var baked = (BakedVoice)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Clip", baked.Clip, typeof(AudioClip), false);
            }

            EditorGUILayout.LabelField("Length", $"{baked.Duration:0.00} s, {baked.FrameCount} frames at {baked.FrameRate:0} fps");
            Rect rect = GUILayoutUtility.GetRect(10f, 60f, GUILayout.ExpandWidth(true));
            DrawLevels(rect, baked);

            if (baked.Clip != null && GUILayout.Button("Bake Again"))
            {
                TalkMouthBaking.BakeAsset(baked.Clip, baked.FrameRate);
                AssetDatabase.SaveAssets();
            }
        }

        static void DrawLevels(Rect rect, BakedVoice baked)
        {
            EditorGUI.DrawRect(rect, Background);
            int frames = baked.FrameCount;
            if (frames == 0 || Event.current.type != EventType.Repaint) return;

            int columns = Mathf.Max(1, (int)rect.width);
            for (int x = 0; x < columns; x++)
            {
                int from = x * frames / columns;
                int to = Mathf.Max(from + 1, (x + 1) * frames / columns);
                float peak = 0f;
                for (int f = from; f < to && f < frames; f++) peak = Mathf.Max(peak, baked.LevelAt(f));
                // -60..0 dB onto the height.
                float h = Mathf.InverseLerp(-60f, 0f, LevelMeter.ToDecibels(Mathf.Max(peak, 1e-6f))) * rect.height;
                EditorGUI.DrawRect(new Rect(rect.x + x, rect.yMax - h, 1f, h), Bar);
            }
        }
    }
}
