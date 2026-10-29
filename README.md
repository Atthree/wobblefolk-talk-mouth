# Wobblefolk Talk Mouth

![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)
![License](https://img.shields.io/badge/License-MIT-green)

Voice-driven mouths for any character in Unity 6: human jaws and blend shapes, monster and beak bones, and stylised
heads that split open on a hinge as the character talks. Driven by AudioSources, the microphone, voice chat SDKs,
typed dialogue or baked clips.

> **Work in progress.** Stage 1 of 6 is done: the analyzer (pure logic) and its tests. Sources, drivers, samples and
> the full documentation follow.

**Install:** *Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-talk-mouth.git?path=/Packages/com.atthree.wobblefolk.talkmouth
```

## Stage 1: the analyzer

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
