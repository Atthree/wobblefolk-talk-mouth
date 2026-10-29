using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>What a blend shape follows.</summary>
    public enum MouthChannel
    {
        /// <summary>The overall opening (a "jaw open" or "mouth open" shape).</summary>
        Open = 0,
        /// <summary>Lips together: grows as the mouth closes while speaking (m, b, p).</summary>
        Closed = 1,
        AA = 2,
        OH = 3,
        EE = 4,
        FF = 5,
    }

    /// <summary>One row of the table: a blend shape, what drives it and how far.</summary>
    [Serializable]
    public sealed class BlendShapeBinding
    {
        [Tooltip("Blend shape name on the mesh.")]
        public string blendShape = "";
        public MouthChannel channel = MouthChannel.Open;
        [Tooltip("Blend shape weight at full channel value (blend shapes usually go to 100).")]
        [Range(0f, 200f)] public float weight = 100f;

        public BlendShapeBinding() { }

        public BlendShapeBinding(string blendShape, MouthChannel channel, float weight = 100f)
        {
            this.blendShape = blendShape;
            this.channel = channel;
            this.weight = weight;
        }
    }

    /// <summary>
    /// Drives blend shapes on a skinned mesh from a table: the opening and each mouth shape to any blend shape, by any
    /// weight. Several rows may drive the same blend shape; they add up. Weights set by an animation are kept and the
    /// mouth is added on top.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Talk Mouth/Blend Shape Mouth Driver")]
    public sealed class BlendShapeMouthDriver : MouthDriver
    {
        [Header("Blend Shapes")]
        [Tooltip("The face mesh. Empty = the first skinned mesh on this object or its children.")]
        [SerializeField] SkinnedMeshRenderer target;
        [Tooltip("Which blend shape follows what. Right-click the component > Find Blend Shapes By Name to fill it.")]
        [SerializeField] List<BlendShapeBinding> bindings = new List<BlendShapeBinding>();

        SkinnedMeshRenderer bound;
        Mesh boundMesh;
        int[] indices = Array.Empty<int>();
        float[] values = Array.Empty<float>();
        float[] baseWeights = Array.Empty<float>();
        float[] lastWritten = Array.Empty<float>();
        bool[] touched = Array.Empty<bool>();
        bool written;
        MouthSpringState spring;

        public BlendShapeMouthDriver()
        {
            SpringMode = MouthSpringMode.Off;
        }

        public SkinnedMeshRenderer Target
        {
            get => target != null ? target : GetComponentInChildren<SkinnedMeshRenderer>();
            set
            {
                Restore();
                target = value;
                if (isActiveAndEnabled) Capture();
            }
        }

        /// <summary>The table. After changing it in code, call <see cref="Rebind"/>.</summary>
        public List<BlendShapeBinding> Bindings => bindings;

        /// <summary>Rows whose blend shape was not found on the mesh (checked on enable and rebind).</summary>
        public int MissingBindings { get; private set; }

        public void Rebind()
        {
            Restore();
            Capture();
        }

        protected override void Capture()
        {
            bound = Target;
            boundMesh = bound != null ? bound.sharedMesh : null;
            spring.Reset();
            written = false;
            MissingBindings = 0;

            int shapeCount = boundMesh != null ? boundMesh.blendShapeCount : 0;
            if (values.Length != shapeCount)
            {
                values = new float[shapeCount];
                baseWeights = new float[shapeCount];
                lastWritten = new float[shapeCount];
                touched = new bool[shapeCount];
            }

            Array.Clear(touched, 0, touched.Length);
            if (indices.Length != bindings.Count) indices = new int[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
            {
                BlendShapeBinding b = bindings[i];
                int index = boundMesh != null && b != null && !string.IsNullOrEmpty(b.blendShape)
                    ? boundMesh.GetBlendShapeIndex(b.blendShape)
                    : -1;
                indices[i] = index;
                if (index < 0)
                {
                    MissingBindings++;
                    continue;
                }

                touched[index] = true;
                baseWeights[index] = bound.GetBlendShapeWeight(index);
            }

            if (MissingBindings > 0 && bound != null && Application.isPlaying)
            {
                Debug.LogWarning($"{name}: {MissingBindings} blend shape(s) of the mouth table are not on {bound.name}.", this);
            }
        }

        protected override void Apply(float dt)
        {
            if (bound != Target || bound != null && bound.sharedMesh != boundMesh || indices.Length != bindings.Count) Capture();
            if (bound == null) return;

            MouthSample s = Sample();
            float open = ApplySpring(ref spring, s.openness, dt);
            // The spring moves the opening; the shapes follow it in proportion.
            float shapeScale = s.openness > 1e-4f ? open / s.openness : 0f;

            for (int i = 0; i < values.Length; i++)
            {
                if (!touched[i]) continue;
                float current = bound.GetBlendShapeWeight(i);
                if (!written || !Mathf.Approximately(current, lastWritten[i])) baseWeights[i] = current;
                values[i] = baseWeights[i];
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                int index = indices[i];
                if (index < 0) continue;
                BlendShapeBinding b = bindings[i];
                values[index] += b.weight * Channel(b.channel, s, open, shapeScale);
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!touched[i]) continue;
                bound.SetBlendShapeWeight(i, values[i]);
                lastWritten[i] = bound.GetBlendShapeWeight(i);
            }

            written = true;
        }

        static float Channel(MouthChannel channel, in MouthSample s, float open, float shapeScale)
        {
            VisemeWeights v = s.visemes;
            switch (channel)
            {
                case MouthChannel.Open: return open;
                // Lips pressed between sounds while speaking; at rest the mouth is relaxed, not pressed.
                case MouthChannel.Closed: return s.speaking ? v.closed : 0f;
                case MouthChannel.AA: return v.aa * shapeScale;
                case MouthChannel.OH: return v.oh * shapeScale;
                case MouthChannel.EE: return v.ee * shapeScale;
                case MouthChannel.FF: return v.ff * shapeScale;
                default: return 0f;
            }
        }

        public override void Restore()
        {
            if (bound != null && written)
            {
                for (int i = 0; i < touched.Length; i++)
                {
                    if (touched[i]) bound.SetBlendShapeWeight(i, baseWeights[i]);
                }
            }

            written = false;
            spring.Reset();
        }

        /// <summary>
        /// Fills the table from blend shape names that look like mouth shapes (jawOpen, viseme_aa, mouth_O, ...).
        /// Returns how many rows were added; rows already in the table are kept.
        /// </summary>
        public int FindBlendShapesByName()
        {
            SkinnedMeshRenderer smr = Target;
            if (smr == null || smr.sharedMesh == null) return 0;
            Mesh mesh = smr.sharedMesh;
            var taken = new bool[Enum.GetValues(typeof(MouthChannel)).Length];
            foreach (BlendShapeBinding b in bindings) if (b != null) taken[(int)b.channel] = true;

            int added = 0;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shape = mesh.GetBlendShapeName(i);
                if (!GuessChannel(shape, out MouthChannel channel) || taken[(int)channel]) continue;
                bindings.Add(new BlendShapeBinding(shape, channel));
                taken[(int)channel] = true;
                added++;
            }

            if (isActiveAndEnabled) Rebind();
            return added;
        }

        [ContextMenu("Find Blend Shapes By Name")]
        void FindBlendShapesMenu()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Find Blend Shapes");
#endif
            int added = FindBlendShapesByName();
            Debug.Log($"{name}: added {added} blend shape row(s).", this);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Guesses what a blend shape is for from its name. Case, separators and common prefixes do not matter.</summary>
        public static bool GuessChannel(string blendShape, out MouthChannel channel)
        {
            channel = MouthChannel.Open;
            if (string.IsNullOrEmpty(blendShape)) return false;

            // "Face.viseme_AA", "mouth-O", "Jaw Open" -> "aa", "o", "jawopen"
            string n = blendShape.ToLowerInvariant();
            int dot = n.LastIndexOf('.');
            if (dot >= 0) n = n.Substring(dot + 1);
            n = n.Replace("_", "").Replace("-", "").Replace(" ", "");
            foreach (string prefix in Prefixes)
            {
                if (n.Length > prefix.Length && n.StartsWith(prefix, StringComparison.Ordinal))
                {
                    n = n.Substring(prefix.Length);
                    break;
                }
            }

            switch (n)
            {
                case "jawopen":
                case "open":
                case "mouthopen":
                    channel = MouthChannel.Open;
                    return true;
                case "pp":
                case "mbp":
                case "m":
                case "closed":
                case "close":
                    channel = MouthChannel.Closed;
                    return true;
                case "aa":
                case "a":
                case "ah":
                    channel = MouthChannel.AA;
                    return true;
                case "oh":
                case "o":
                case "ou":
                case "u":
                case "oo":
                    channel = MouthChannel.OH;
                    return true;
                case "ee":
                case "e":
                case "i":
                case "ih":
                    channel = MouthChannel.EE;
                    return true;
                case "ff":
                case "f":
                case "fv":
                case "ss":
                    channel = MouthChannel.FF;
                    return true;
                default:
                    return false;
            }
        }

        static readonly string[] Prefixes = { "viseme", "mouth", "v" };
    }
}
