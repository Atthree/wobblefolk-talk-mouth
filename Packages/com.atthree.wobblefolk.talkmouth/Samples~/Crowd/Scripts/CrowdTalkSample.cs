using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.TalkMouth.Samples
{
    /// <summary>
    /// A crowd of talkers sharing one synthetic voice clip, baked once at start, so no talker runs an FFT. Each plays
    /// it at its own pitch and position. W / S move the camera, B switches baked and live analysis, L toggles level of
    /// detail. Speech bubbles are tinted by detail: green = full, yellow = level only.
    /// </summary>
    public sealed class CrowdTalkSample : MonoBehaviour
    {
        public GameObject talkerTemplate;
        [Min(1)] public int rows = 4;
        [Min(1)] public int columns = 5;
        [Min(0.5f)] public float spacing = 1.6f;
        [Min(1f)] public float cameraSpeed = 6f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");
        static readonly UnityEngine.Color FullColor = new UnityEngine.Color(0.45f, 0.95f, 0.45f);
        static readonly UnityEngine.Color AmplitudeColor = new UnityEngine.Color(1f, 0.85f, 0.3f);

        readonly List<TalkVoice> voices = new List<TalkVoice>();
        readonly List<Renderer> bubbles = new List<Renderer>();
        MaterialPropertyBlock block;
        AudioClip clip;
        BakedVoice baked;
        bool useBaked = true;
        bool useLod = true;
        float frameMs;

        void Start()
        {
            block = new MaterialPropertyBlock();
            clip = SyntheticSpeech.CreateClip("Crowd Voice", 20f, 44100, 77, 150f);
            baked = VoiceBaker.Bake(clip);
            talkerTemplate.SetActive(false);

            var random = new System.Random(5);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var position = new Vector3((c - (columns - 1) * 0.5f) * spacing, 0f, r * spacing * 1.4f);
                    Spawn(position, random);
                }
            }

            ApplyModes();
        }

        void Spawn(Vector3 position, System.Random random)
        {
            GameObject talker = Instantiate(talkerTemplate, position, Quaternion.Euler(0f, 180f + Range(random, -20f, 20f), 0f));
            talker.name = "Talker " + voices.Count;
            talker.SetActive(true);

            var body = talker.transform.Find("Body").GetComponent<Renderer>();
            Tint(body, UnityEngine.Color.HSVToRGB(Range(random, 0f, 1f), 0.55f, 0.9f));

            var audio = talker.GetComponent<AudioSource>();
            audio.clip = clip;
            audio.pitch = Range(random, 0.8f, 1.3f);
            audio.timeSamples = random.Next(clip.samples);
            audio.Play();

            talker.GetComponent<BakedVisemeSignal>().Bakes.Add(baked);
            voices.Add(talker.GetComponent<TalkVoice>());
            bubbles.Add(talker.GetComponent<SpeakingIndicator>().Indicator.GetComponent<Renderer>());
        }

        void OnDestroy()
        {
            if (clip != null) Destroy(clip);
            if (baked != null) Destroy(baked);
        }

        void Update()
        {
            frameMs = Mathf.Lerp(frameMs, Time.unscaledDeltaTime * 1000f, 0.05f);
            if (Pressed(KeyCode.B))
            {
                useBaked = !useBaked;
                ApplyModes();
            }

            if (Pressed(KeyCode.L))
            {
                useLod = !useLod;
                ApplyModes();
            }

            float move = (Held(KeyCode.W) ? 1f : 0f) - (Held(KeyCode.S) ? 1f : 0f);
            Camera cam = Camera.main;
            if (cam != null && move != 0f) cam.transform.position += cam.transform.forward * (move * cameraSpeed * Time.deltaTime);

            for (int i = 0; i < voices.Count; i++)
            {
                Tint(bubbles[i], voices[i].Lod == TalkLod.Full ? FullColor : AmplitudeColor);
            }
        }

        void ApplyModes()
        {
            foreach (TalkVoice voice in voices)
            {
                voice.UseLod = useLod;
                voice.SetSource(useBaked ? (VoiceSignalSource)voice.GetComponent<BakedVisemeSignal>() : voice.GetComponent<AudioSourceSignal>());
            }
        }

        void Tint(Renderer target, UnityEngine.Color tint)
        {
            target.GetPropertyBlock(block);
            block.SetColor(BaseColor, tint);
            block.SetColor(Color, tint);
            target.SetPropertyBlock(block);
        }

        void OnGUI()
        {
            int full = 0, amplitude = 0, culled = 0;
            foreach (TalkVoice v in voices)
            {
                if (v.Lod == TalkLod.Full) full++;
                else if (v.Lod == TalkLod.Amplitude) amplitude++;
                else culled++;
            }

            string text =
                $"W / S: move    B: {(useBaked ? "baked (no FFT)" : "live analysis")}    L: detail {(useLod ? "by distance" : "always full")}\n" +
                $"{voices.Count} talkers: {full} full, {amplitude} level only, {culled} culled    {frameMs:0.0} ms";
            GUI.Box(new Rect(10f, 10f, 470f, 46f), text);
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.B: return keyboard.bKey.wasPressedThisFrame;
                case KeyCode.L: return keyboard.lKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }

        static bool Held(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.W: return keyboard.wKey.isPressed;
                case KeyCode.S: return keyboard.sKey.isPressed;
                default: return false;
            }
#else
            return Input.GetKey(key);
#endif
        }
    }
}
