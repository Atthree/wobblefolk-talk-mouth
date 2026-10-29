using UnityEngine;

namespace Wobblefolk.TalkMouth
{
    /// <summary>
    /// Anything a character's voice can come from: an AudioSource, the microphone, a voice chat SDK, typed text or
    /// a baked clip. Implement it in plain C# and hand it to <see cref="TalkVoice.SetSource"/>, or derive from
    /// <see cref="VoiceSignalSource"/> to assign it in the Inspector.
    /// </summary>
    public interface IVoiceSignalSource
    {
        /// <summary>
        /// Writes the newest signal into <paramref name="signal"/> (which is cleared to silence beforehand). Called once
        /// per frame on the main thread; it must not allocate.
        /// </summary>
        void Read(VoiceSignal signal);
    }

    /// <summary>A voice source component that can be assigned to <see cref="TalkVoice"/> in the Inspector.</summary>
    public abstract class VoiceSignalSource : MonoBehaviour, IVoiceSignalSource
    {
        public abstract void Read(VoiceSignal signal);
    }
}
