# Wobblefolk Talk Mouth

![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)
![License](https://img.shields.io/badge/License-MIT-green)

Voice-driven mouths for any character in Unity 6: human jaws and blend shapes, monster and beak bones, and stylised
heads that split open on a hinge as the character talks. Driven by AudioSources, the microphone, voice chat SDKs,
typed dialogue or baked clips.

> **Work in progress.** Stages 1–2 of 6 are done: the analyzer and the voice sources. Mouth drivers, samples and the
> full documentation follow.

**Install:** *Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-talk-mouth.git?path=/Packages/com.atthree.wobblefolk.talkmouth
```

## Try it now

1. Add **Talk Voice** and one voice source (below) to a GameObject.
2. Enter Play Mode and select it: the Inspector shows the voice level against the gate, the mouth opening and the
   five mouth shapes live.

## Voice sources

| Source | Use it for |
|---|---|
| **Audio Source Signal** | Dialogue clips. Reads the clip at the play position, so distance, volume and mute do not change the mouth. Needs *Decompress On Load*; streamed clips fall back to the AudioSource output. *Look Ahead* lets the mouth lead the sound slightly. |
| **Microphone Signal** | The local player. Analysed only, never played back (no echo). Asks for permission on mobile. Not on WebGL. |
| **External Level Signal** | Any voice chat SDK, without depending on one. Call `PushSamples` or `PushLevel` from any thread; *Delay* holds the mouth back to meet the played audio. |
| **Text Driven Signal** | Written dialogue without voice audio: `Say("Hello!")`. Letters set the shape, m/b/p close the lips, punctuation pauses. Optional babble voice and per-character events for typewriter text. |

Your own source: implement `IVoiceSignalSource` and call `TalkVoice.SetSource`.

```csharp
voice.OnSpeechStarted += () => indicator.SetActive(true);
voice.OnSpeechLevel += state => Debug.Log(state.openness);
voice.SetEnabled(false); // dead, fainted or muted: the mouth closes smoothly
```

## The analyzer (stage 1)

| Step | What it does |
|---|---|
| Level | RMS of the newest samples in dB, plus an input gain. |
| Noise gate | Opens at the threshold, closes a hysteresis below it, so the mouth never flutters at the threshold. A hold time keeps "speaking" through pauses between words. |
| Adaptive (optional) | The gate follows the background noise, and the full level follows quiet speakers. |
| Envelope | Frame-rate independent attack, release and close times. |
| Response | Level curve, exaggeration, optional steps (robot open/close), maximum opening. |
| Visemes | Allocation-free FFT into four bands, then the nearest of five coarse shapes (closed, AA, OH, EE, FF). The current shape is sticky, so it does not flicker. Amplitude-only mode skips the FFT. |
| Spring | Exact damped spring (stable at any fps), tuned by stiffness and overshoot. |

Presets: Human, Cartoon, Monster, Bird, Robot, Bouncy Split Head.

## License

MIT. See [LICENSE](LICENSE).
