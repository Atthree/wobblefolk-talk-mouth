using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    public enum TextCharClass
    {
        /// <summary>Opens the mouth fully in its own shape (a, o, e…).</summary>
        Vowel,
        /// <summary>Hiss: half open, teeth together (f, v, s, z, ş, ç…).</summary>
        Fricative,
        /// <summary>Lips together: the mouth closes (m, b, p).</summary>
        Lips,
        /// <summary>Other consonants: half open in the last vowel's shape.</summary>
        Consonant,
        /// <summary>Between words: nearly closed.</summary>
        Space,
        /// <summary>Short pause (, ; :).</summary>
        Comma,
        /// <summary>Long pause (. ! ?).</summary>
        Sentence,
        LineBreak,
        /// <summary>Takes no time (quotes, brackets, symbols).</summary>
        Silent,
    }

    [Serializable]
    public sealed class TextRhythmSettings
    {
        [Tooltip("Typing speed. 12–16 feels like talking.")]
        [Range(1f, 60f)] public float charactersPerSecond = 14f;
        [Tooltip("Extra pause after , ; : (s).")]
        [Min(0f)] public float commaPause = 0.15f;
        [Tooltip("Extra pause after . ! ? (s). Repeated marks (...) pause once.")]
        [Min(0f)] public float sentencePause = 0.35f;
        [Tooltip("Extra pause at a line break (s).")]
        [Min(0f)] public float lineBreakPause = 0.3f;

        [Header("Opening (0–1)")]
        [Range(0f, 1f)] public float vowelLevel = 1f;
        [Range(0f, 1f)] public float consonantLevel = 0.55f;
        [Range(0f, 1f)] public float spaceLevel = 0.1f;
        [Tooltip("Random variation per letter, so the mouth does not look mechanical.")]
        [Range(0f, 1f)] public float variation = 0.15f;

        [Tooltip("Skip <tags> (TextMeshPro rich text) without pausing.")]
        public bool skipRichTextTags = true;
    }

    /// <summary>
    /// Turns written dialogue into a talking rhythm, letter by letter like a typewriter: vowels open the mouth in their
    /// shape, m/b/p close it, punctuation pauses. Language-independent; letters outside Latin scripts count as
    /// syllables. Plain C#, driven by <see cref="Advance"/>.
    /// </summary>
    public sealed class TextRhythm
    {
        string text = "";
        int index = -1;
        float timeLeft;
        uint random = 1;
        Viseme lastVowel = Viseme.AA;
        TextCharClass lastClass = TextCharClass.Space;

        public string Text => text;
        public bool IsPlaying { get; private set; }
        /// <summary>Index in <see cref="Text"/> of the character being spoken; -1 before the first.</summary>
        public int CharacterIndex => index;
        /// <summary>Mouth opening for the current character (0–1).</summary>
        public float Level { get; private set; }
        public Viseme Viseme { get; private set; } = Viseme.Closed;

        public void Start(string line, uint seed = 1)
        {
            text = line ?? "";
            index = -1;
            timeLeft = 0f;
            random = seed == 0 ? 1u : seed;
            lastVowel = Viseme.AA;
            lastClass = TextCharClass.Space;
            Level = 0f;
            Viseme = Viseme.Closed;
            IsPlaying = text.Length > 0;
        }

        public void Stop()
        {
            IsPlaying = false;
            Level = 0f;
            Viseme = Viseme.Closed;
        }

        /// <summary>
        /// Moves time on. <paramref name="onCharacter"/> is called for every character that starts, in order, even
        /// when several start in one frame. Returns how many started.
        /// </summary>
        public int Advance(float dt, TextRhythmSettings settings, Action<int> onCharacter = null)
        {
            if (!IsPlaying) return 0;
            timeLeft -= Mathf.Max(0f, dt);
            int started = 0;
            while (timeLeft <= 0f)
            {
                if (!Next(settings))
                {
                    Stop();
                    break;
                }

                started++;
                onCharacter?.Invoke(index);
            }

            return started;
        }

        bool Next(TextRhythmSettings s)
        {
            index++;
            if (s.skipRichTextTags)
            {
                while (index < text.Length && text[index] == '<')
                {
                    int close = text.IndexOf('>', index);
                    if (close < 0) break;
                    index = close + 1;
                }
            }

            if (index >= text.Length) return false;

            Classify(text[index], out TextCharClass type, out Viseme shape);
            float step = 1f / Mathf.Max(1f, s.charactersPerSecond);
            bool repeatedPause = IsPause(type) && IsPause(lastClass);

            switch (type)
            {
                case TextCharClass.Vowel:
                    Speak(s.vowelLevel, shape, step, s);
                    lastVowel = shape;
                    break;
                case TextCharClass.Fricative:
                    Speak(s.consonantLevel, Viseme.FF, step, s);
                    break;
                case TextCharClass.Consonant:
                    Speak(s.consonantLevel, lastVowel, step, s);
                    break;
                case TextCharClass.Space:
                    Level = s.spaceLevel;
                    Viseme = lastVowel;
                    timeLeft += step;
                    break;
                case TextCharClass.Lips:
                    Pause(step);
                    break;
                case TextCharClass.Comma:
                    Pause(repeatedPause ? step : step + s.commaPause);
                    break;
                case TextCharClass.Sentence:
                    Pause(repeatedPause ? step : step + s.sentencePause);
                    break;
                case TextCharClass.LineBreak:
                    Pause(repeatedPause ? step : step + s.lineBreakPause);
                    break;
                // Silent characters take no time and keep the mouth as it is.
            }

            if (type != TextCharClass.Silent) lastClass = type;
            return true;
        }

        void Speak(float level, Viseme shape, float step, TextRhythmSettings s)
        {
            Level = level * (1f - s.variation * NextRandom());
            Viseme = shape;
            timeLeft += step;
        }

        void Pause(float time)
        {
            Level = 0f;
            Viseme = Viseme.Closed;
            timeLeft += time;
        }

        static bool IsPause(TextCharClass c) =>
            c == TextCharClass.Comma || c == TextCharClass.Sentence || c == TextCharClass.LineBreak;

        float NextRandom()
        {
            // xorshift32: deterministic and allocation-free.
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            return (random & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>How a character is spoken; <paramref name="viseme"/> is the shape for vowels.</summary>
        public static void Classify(char c, out TextCharClass type, out Viseme viseme)
        {
            viseme = Viseme.Closed;
            if (c == 'İ') c = 'i'; // Turkish dotted capital I
            c = char.ToLowerInvariant(c);

            switch (c)
            {
                case 'a': case 'à': case 'á': case 'â': case 'ã': case 'ä': case 'å': case 'æ':
                    type = TextCharClass.Vowel; viseme = Viseme.AA; return;
                case 'o': case 'ò': case 'ó': case 'ô': case 'õ': case 'ö': case 'ø': case 'œ':
                case 'u': case 'ù': case 'ú': case 'û': case 'ü':
                    type = TextCharClass.Vowel; viseme = Viseme.OH; return;
                case 'e': case 'è': case 'é': case 'ê': case 'ë':
                case 'i': case 'ì': case 'í': case 'î': case 'ï': case 'ı': case 'y':
                    type = TextCharClass.Vowel; viseme = Viseme.EE; return;
                case 'f': case 'v': case 's': case 'z': case 'ş': case 'ç': case 'c': case 'j': case 'x': case 'ß':
                    type = TextCharClass.Fricative; return;
                case 'm': case 'b': case 'p':
                    type = TextCharClass.Lips; return;
                case ' ': case '\t': case ' ': case '　':
                    type = TextCharClass.Space; return;
                case '\n':
                    type = TextCharClass.LineBreak; return;
                case ',': case ';': case ':': case '—': case '–': case '、': case '，':
                    type = TextCharClass.Comma; return;
                case '.': case '!': case '?': case '…': case '。': case '！': case '？':
                    type = TextCharClass.Sentence; return;
            }

            if (char.IsDigit(c))
            {
                type = TextCharClass.Consonant;
                return;
            }

            if (char.IsLetter(c))
            {
                if (c <= 'ɏ')
                {
                    type = TextCharClass.Consonant;
                    return;
                }

                // Other scripts (Cyrillic, Greek, CJK, Arabic…): each letter as a syllable, shapes varied by code.
                type = TextCharClass.Vowel;
                viseme = (Viseme)(1 + c % 3);
                return;
            }

            type = TextCharClass.Silent;
        }
    }
}
