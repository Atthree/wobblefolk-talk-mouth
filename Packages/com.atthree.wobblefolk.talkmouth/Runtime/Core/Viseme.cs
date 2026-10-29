using System;
using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>Coarse, language-independent mouth shapes.</summary>
    public enum Viseme
    {
        /// <summary>Silence, or lips pressed together (m, b, p).</summary>
        Closed = 0,
        /// <summary>Wide open (a).</summary>
        AA = 1,
        /// <summary>Rounded (o, u).</summary>
        OH = 2,
        /// <summary>Spread wide (e, i).</summary>
        EE = 3,
        /// <summary>Hissing and friction sounds (f, v, s, sh): teeth almost together.</summary>
        FF = 4,
    }

    /// <summary>One weight per <see cref="Viseme"/>. A value type, so passing it around never allocates.</summary>
    [Serializable]
    public struct VisemeWeights
    {
        public const int Count = 5;

        public float closed;
        public float aa;
        public float oh;
        public float ee;
        public float ff;

        public VisemeWeights(float closed, float aa, float oh, float ee, float ff)
        {
            this.closed = closed;
            this.aa = aa;
            this.oh = oh;
            this.ee = ee;
            this.ff = ff;
        }

        /// <summary>All weight on one shape.</summary>
        public static VisemeWeights Only(Viseme viseme)
        {
            var w = new VisemeWeights();
            w[viseme] = 1f;
            return w;
        }

        public static VisemeWeights Rest => Only(Viseme.Closed);

        public float this[Viseme viseme]
        {
            get => this[(int)viseme];
            set => this[(int)viseme] = value;
        }

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return closed;
                    case 1: return aa;
                    case 2: return oh;
                    case 3: return ee;
                    case 4: return ff;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
            set
            {
                switch (index)
                {
                    case 0: closed = value; break;
                    case 1: aa = value; break;
                    case 2: oh = value; break;
                    case 3: ee = value; break;
                    case 4: ff = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public float Sum => closed + aa + oh + ee + ff;

        /// <summary>The shape with the largest weight.</summary>
        public Viseme Dominant
        {
            get
            {
                int best = 0;
                for (int i = 1; i < Count; i++)
                {
                    if (this[i] > this[best]) best = i;
                }

                return (Viseme)best;
            }
        }

        public static VisemeWeights Lerp(in VisemeWeights a, in VisemeWeights b, float t)
        {
            t = Mathf.Clamp01(t);
            return new VisemeWeights(
                a.closed + (b.closed - a.closed) * t,
                a.aa + (b.aa - a.aa) * t,
                a.oh + (b.oh - a.oh) * t,
                a.ee + (b.ee - a.ee) * t,
                a.ff + (b.ff - a.ff) * t);
        }

        public override string ToString() =>
            $"(closed {closed:0.00}, aa {aa:0.00}, oh {oh:0.00}, ee {ee:0.00}, ff {ff:0.00})";
    }
}
