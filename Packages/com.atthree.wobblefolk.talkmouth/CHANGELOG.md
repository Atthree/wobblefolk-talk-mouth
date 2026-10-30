# Changelog

## [0.1.0] - 2026-10-30

First release.

### Analysis
- Level meter, noise gate with hysteresis and hold, optional adaptive gate (noise floor and quiet speakers).
- Frame-rate independent envelope, level curve, exaggeration, steps.
- Allocation-free FFT into four bands and five coarse mouth shapes (closed, AA, OH, EE, FF); amplitude-only mode.
- Exact damped spring for bouncy mouths. Presets: Human, Cartoon, Monster, Bird, Robot, Bouncy Split Head.

### Voice sources
- Audio Source, Microphone, External Level (voice chat SDKs, thread-safe, delay), Text Driven (babble voice),
  Baked Viseme Signal. Synthetic speech for samples and tests.

### Mouth drivers
- Jaw Bone, Blend Shape (table, find by name), Transform, Hinge Split (split heads), Multi Bone, Sprite,
  Animator Parameter, Joint (ConfigurableJoint target rotation). Added on top of animation; rest pose restored.
- Scene gizmos: hinge, axis, opening arc, current angle.

### Tools
- Wobblefolk > Setup Talk Mouth: detects jaws, blend shapes, joints, split head halves, sprites and Animator parameters.
- Assets > Wobblefolk > Bake Talk Mouth, BakedVoice asset with a level graph.
- Level of detail, Speaking Indicator, SpeakerTracker.
- Optional integrations: Speaker Look Target (Wobblefolk Look At); joint jaw sample for Wobblefolk Active Ragdoll.

### Samples
- Humanoid, Split Head, Monster, Beak, Crowd, Active Ragdoll Integration, Look At Integration.
