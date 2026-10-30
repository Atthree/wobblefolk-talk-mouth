# Wobblefolk Talk Mouth

![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)
![License](https://img.shields.io/badge/License-MIT-green)

Voice-driven mouths for any character in Unity 6: human jaws and blend shapes, monster and beak bones, and stylised
heads that split open on a hinge as the character talks. Driven by AudioSources, the microphone, voice chat SDKs,
typed dialogue or baked clips.

> **Work in progress.** Stages 1–4 of 6 are done: the analyzer, the voice sources, all main mouth drivers and four
> samples. Baking, LOD, the setup tool and the full documentation follow.

**Install:** *Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-talk-mouth.git?path=/Packages/com.atthree.wobblefolk.talkmouth
```

## Try it now

Import a sample (*Package Manager > Wobblefolk Talk Mouth > Samples*) and press Play:

| Sample | Shows |
|---|---|
| **Humanoid** | Sphere head with a capsule jaw (Jaw Bone driver) and a mouth that stretches per shape (Transform driver), both on one voice. 1 / 2 / 3: synthetic voice, microphone, typed dialogue. M mutes. |
| **Split Head** | The top half of the head swings open on a hinge (Hinge Split driver, Bouncy Split Head profile); a hat follows a moment later, the lower half drops a little. 1 / 2: synthetic voice, microphone. B: bounce, J: rattle, M: mute. |
| **Monster** | Lower and upper jaw, two mandibles that spread sideways and a tongue that slides out only when loud: one Multi Bone driver, Monster profile. 1 / 2: synthetic voice, microphone. M: mute. |
| **Beak** | Beak halves, crest feathers that follow one after another and wings that flap on loud syllables: one Multi Bone driver, Bird profile. 1 / 2: synthetic voice, microphone. M: mute. |

The samples ship no audio: their voice is synthesised at start. Or set up your own character:

1. Add **Talk Voice** and one voice source (below) to the character.
2. Add a mouth driver (below) to the jaw, face or head.
3. Enter Play Mode and select the voice: the Inspector shows the level against the gate, the opening and the five
   mouth shapes live. Select a jaw or hinge driver to see its axis and opening arc in the Scene view.

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

## Mouth drivers

| Driver | Moves |
|---|---|
| **Jaw Bone** | A jaw bone or object, rotated about a local axis (and optionally slid). "aa" opens widest, "oh" and "ee" less, "ff" barely. |
| **Blend Shape** | Blend shapes from a table: the opening and each mouth shape to any blend shape and weight. *Find Blend Shapes By Name* (component menu) fills it from names like `jawOpen`, `viseme_aa`, `mouth_O`. |
| **Transform** | Any object: position, rotation and scale at full opening, optionally reshaped per mouth shape. |
| **Multi Bone** | Any number of bones from one voice, each with its own axis, angle, offset, scale, curve, delay and channel (the opening or one shape's weight): multi-part jaws, mandibles, beaks, a tongue that only comes out when loud. |
| **Sprite** | 2D mouths: frames from closed to open, or one sprite per shape. Holds each sprite a minimum time, so it does not flicker. An event reports every change, e.g. for a UI Image in a dialogue portrait. |
| **Animator Parameter** | Animator parameters (opening, speaking, per-shape weights, strongest shape) for a blend tree or a talking layer. Runs in Update, before the Animator; missing names are skipped. |
| **Hinge Split** | Pieces that swing about a hinge: the top of a split head, a lower half (reversed), a hat that follows later. Each piece has its own share, direction and delay. Snappy attack and release, curve, exaggeration, a spring that overshoots and wobbles shut, and an optional rattle when loud. |

Pose drivers apply in LateUpdate (the Animator Parameter driver in Update), can lag behind the voice (*Delay*) and scale it (*Amount*), and bounce with the
profile's spring, their own or none. A bouncing mouth never swings through the face: it bounces off the closed pose.
Any number of drivers can follow one voice. Disabling a driver puts everything back to its rest pose.

**Animated characters.** Drivers add the mouth on top of the animation: whenever something else (an Animator, a
script) moved the jaw or set a blend shape since the driver last did, that becomes the new base. If an animation
should own the jaw completely (a scream), disable the driver for that moment.

**Split heads need separate meshes** for each moving piece (top and bottom half), and the hinge must not be inside
one of them. The Split Head sample uses two half-sphere meshes, each with a lid for the inside of the mouth.

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
