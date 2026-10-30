# Wobblefolk Talk Mouth

![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)
![License](https://img.shields.io/badge/License-MIT-green)
![Tests](https://img.shields.io/badge/EditMode%20tests-161-brightgreen)

**[English](#english)** · **[Türkçe](#türkçe)**

Voice-driven mouths for any character in Unity 6: human jaws and blend shapes, monster and beak bones, 2D sprites,
physical ragdoll jaws, and stylised heads that split open on a hinge as the character talks. Driven by AudioSources,
the microphone, voice chat SDKs, typed dialogue or baked clips.

---

# English

## Table of contents

- [Features](#features)
- [Install](#install)
- [Quick start](#quick-start)
- [Samples](#samples)
- [How it works](#how-it-works)
- [Voice sources](#voice-sources)
- [Mouth drivers](#mouth-drivers)
- [Profiles and presets](#profiles-and-presets)
- [Baking](#baking)
- [Level of detail](#level-of-detail)
- [Speaking indicator and listeners](#speaking-indicator-and-listeners)
- [Integrations](#integrations)
- [Multiplayer](#multiplayer)
- [Animated characters](#animated-characters)
- [Split heads](#split-heads)
- [Scripting](#scripting)
- [Tests](#tests)
- [Troubleshooting](#troubleshooting)
- [Credits and license](#credits-and-license)

## Features

- One analyzer, many mouths: any number of drivers follow one voice (jaw + lips + tongue + a hat that wobbles late).
- Noise gate with hysteresis and hold, optional adaptive gate for noisy microphones and quiet speakers.
- Five coarse, language-independent mouth shapes (closed, AA, OH, EE, FF) from an allocation-free FFT, or amplitude
  only for split heads and robots.
- Frame-rate independent envelope and an exact damped spring: mouths pop open, overshoot and bounce shut, but never
  swing through the face.
- Drivers: Jaw Bone, Blend Shape, Transform, Hinge Split, Multi Bone, Sprite, Animator Parameter, Joint (physics).
- Voice from AudioSources, the microphone, any voice chat SDK, typed text (with optional babble voice), or baked clips.
- Bake tool, level of detail, speaking indicator, a "who is speaking" tracker for listeners.
- Setup tool: *Wobblefolk > Setup Talk Mouth* detects jaws, blend shapes, joints, halves and sprites.
- Optional integrations with Wobblefolk Active Ragdoll and Wobblefolk Look At, without hard dependencies.
- Works with the Animator (added on top of animation), editor gizmos, live Inspector meter.

## Install

*Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-talk-mouth.git?path=/Packages/com.atthree.wobblefolk.talkmouth
```

Unity 6000.3 or newer. No dependencies. The integration samples need their packages (see [Integrations](#integrations)).

## Quick start

**With the setup tool:** select your character, open *Wobblefolk > Setup Talk Mouth*, check what it found, pick where
the voice comes from and press *Set Up Talk Mouth*. It adds a Talk Voice, a voice source and a fitting driver in one
undo step, and can save the preset as a profile asset to tune.

**By hand:**

1. Add **Talk Voice** and a voice source (e.g. **Audio Source Signal** next to an AudioSource) to the character.
2. Add a driver to what should move (e.g. **Jaw Bone Mouth Driver** on the jaw).
3. Press Play and select the voice: the Inspector shows the level against the gate, the opening and the shapes live.
   Select a jaw or hinge driver to see its axis and opening arc in the Scene view.

## Samples

Import them from *Package Manager > Wobblefolk Talk Mouth > Samples*. All are built from primitives, and their voices
are synthesised at start, so they ship no audio.

| Sample | Shows | Keys |
|---|---|---|
| **Humanoid** | Sphere head, capsule jaw (Jaw Bone) and a mouth that stretches per shape (Transform), on one voice. | 1 / 2 / 3: synthetic voice, microphone, typed dialogue (Space: next line). M: mute. |
| **Split Head** | Top half swings open on a hinge (Hinge Split, Bouncy Split Head profile); a hat follows late, the lower half drops a little. | 1 / 2: voice, microphone. B: bounce. J: rattle. M: mute. |
| **Monster** | Lower and upper jaw, mandibles that spread, a tongue that only comes out when loud (Multi Bone). | 1 / 2, M. |
| **Beak** | Beak halves, crest feathers that follow one after another, wings that flap on loud syllables (Multi Bone). | 1 / 2, M. |
| **Crowd** | Twenty talkers on one voice baked at start, level of detail, speech bubbles tinted by detail. | W / S: move. B: baked or live. L: LOD. |
| **Active Ragdoll Integration** | A physical jaw on a ConfigurableJoint (Joint driver) on a Wobblefolk Active Ragdoll. | WASD: walk. 1 / 2, M. K: knock out. |
| **Look At Integration** | Three characters chat; listeners turn eyes and head to whoever speaks, including you on the microphone. | 2: microphone. Space: pause. |

## How it works

```
Voice source ──► Talk Voice (analyzer) ──► MouthState ──► Mouth drivers (LateUpdate)
 AudioSource      level → noise gate        openness 0–1      Jaw Bone, Blend Shape, Transform,
 Microphone       → envelope → curve         shape weights     Hinge Split, Multi Bone, Sprite,
 External SDK     → shapes (FFT bands)       speaking          Animator Parameter, Joint
 Text, Baked
```

| Step | What it does |
|---|---|
| Level | RMS of the newest samples in dB, plus an input gain. |
| Noise gate | Opens at the threshold, closes a hysteresis below it; a hold time keeps "speaking" through pauses between words. |
| Adaptive (optional) | The gate follows the background noise, and the full level follows quiet speakers. |
| Envelope | Frame-rate independent attack, release and close times. |
| Response | Level curve, exaggeration, optional steps (robot open/close), maximum opening. |
| Shapes | Allocation-free FFT into four bands, then the nearest of five shapes. The current shape is sticky, so it does not flicker. |
| Spring | In the drivers: an exact damped spring tuned by stiffness and overshoot, stable at any frame rate. |

Analysis runs in Update (execution order -50); drivers apply in LateUpdate, after the Animator. The Animator
Parameter driver runs in Update, before it.

## Voice sources

| Source | Use it for |
|---|---|
| **Audio Source Signal** | Dialogue clips. Reads the clip at the play position, so distance, volume and mute do not change the mouth. Needs *Decompress On Load*; streamed clips fall back to the AudioSource output (or bake them). *Look Ahead* lets the mouth lead the sound. |
| **Baked Viseme Signal** | Baked clips: follows the AudioSource's play position (pitch, pauses, seeking stay in sync), no FFT at runtime, works with streamed and compressed clips. |
| **Microphone Signal** | The local player. Analysed only, never played back. Asks for permission on mobile. Not on WebGL. |
| **External Level Signal** | Any voice chat SDK, without depending on one: `PushSamples` or `PushLevel` from any thread. *Delay* holds the mouth back to meet the played audio; short network gaps are bridged. |
| **Text Driven Signal** | Written dialogue: `Say("Hello!")`. Letters set the shape, m/b/p close the lips, punctuation pauses. Optional babble voice and per-character events for typewriter text. |

Your own source: implement `IVoiceSignalSource` (write samples, a level or bands into the `VoiceSignal`) and call
`TalkVoice.SetSource`.

## Mouth drivers

| Driver | Moves |
|---|---|
| **Jaw Bone** | A jaw bone or object, rotated about a local axis and optionally slid. "aa" opens widest, "oh" and "ee" less, "ff" barely. |
| **Blend Shape** | Blend shapes from a table: the opening and each shape to any blend shape and weight. *Find Blend Shapes By Name* (component menu) fills it from names like `jawOpen`, `viseme_aa`, `mouth_O`. |
| **Transform** | Any object: position, rotation and scale at full opening, optionally reshaped per shape. |
| **Hinge Split** | Pieces swinging about a hinge: the top of a split head, a lower half (reversed), a hat that follows later. Own share, direction and delay per piece; snappy attack and release, curve, exaggeration, optional rattle when loud. |
| **Multi Bone** | Many bones, each with its own axis, angle, offset, scale, curve, delay and channel (the opening or one shape's weight). |
| **Sprite** | 2D: frames from closed to open, or one sprite per shape; a minimum hold time stops flicker; an event for UI Images. |
| **Animator Parameter** | Animator parameters (opening, speaking, shape weights, strongest shape) for blend trees or a talking layer. |
| **Joint** | A physical jaw through `ConfigurableJoint.targetRotation` only; its angular drive does the moving. No ragdoll package needed. |

Shared settings on every driver: *Amount* (scale the opening), *Delay* (lag behind the voice), *Spring* (profile's,
own or none) and *Closed Bounce* (a bouncing mouth bounces off the closed pose instead of passing it). Disabling a
driver restores the rest pose.

## Profiles and presets

*Create > Wobblefolk > Talk Mouth Profile*. Apply a preset from the Inspector's ⋮ menu (or right-click the header):

| Preset | Character |
|---|---|
| Human | Natural: moderate opening, soft shape changes, almost no bounce. |
| Cartoon | Opens wide, snappy, a bouncy close. |
| Monster | Heavy: late to open, slow to close. |
| Bird | Fast and sharp, amplitude only. |
| Robot | Open or closed, nothing in between. |
| Bouncy Split Head | Reacts to whispers (adaptive gate), opens instantly, wobbles shut. |

A Talk Voice without a profile uses Human. Tune it with the live meter: lower *Gate Threshold* for quiet voices, lower
*Full Level* for a wider mouth.

## Baking

Select audio clips, then *Assets > Wobblefolk > Bake Talk Mouth*: each gets a `<clip>.talkmouth.asset` next to it with
its level and spectrum at 60 fps. Compressed or streamed clips are decompressed for a moment while baking and their
import settings are put back. Re-baking updates the asset in place, so references survive. Add the bakes to **Baked
Viseme Signal**; it plays the one that belongs to the AudioSource's clip.

The profile still applies when a bake plays (gate, envelope, curve, shapes, spring), so one bake fits every character.
Clips made in code can be baked at runtime with `VoiceBaker.Bake(clip)` (the Crowd sample does).

## Level of detail

On **Talk Voice**, enable *Use Lod*:

| Detail | When | Cost |
|---|---|---|
| Full | Closer than *Full Detail Distance* | Level and shapes |
| Amplitude | Further | Level only, no FFT |
| Culled | Beyond *Cull Distance*, or *Visibility Renderer* off screen | Nothing read; the mouth closes, end-of-speech events still fire |

`TalkVoice.LodOverride` lets your own LOD or network relevance decide. Baked voices are cheaper still.

## Speaking indicator and listeners

**Speaking Indicator** shows an object (a bubble, an icon, a UI badge) while the voice speaks, pulses it with the
opening and keeps it a moment after the last word.

`SpeakerTracker` follows the loudest speaking voice near a listener, holding it for a moment when people talk over each
other. Set *Speaker Point* on a Talk Voice (head or mouth) to say where listeners should look. `TalkVoice.All` lists
every enabled voice.

## Integrations

Both are optional: nothing in the core package references them, and their code compiles only when the package is
installed (assembly version defines).

**Wobblefolk Active Ragdoll** (`com.wobblefolk.active-ragdoll`): use **Joint Mouth Driver** on a jaw rigidbody joined
to the head with a ConfigurableJoint that has an angular drive. The sample builds such a jaw at runtime, tunes the
drive from the jaw's inertia, holds it against gravity while the character is conscious and lets it hang open when it
is knocked out.

**Wobblefolk Look At** (`com.atthree.wobblefolk.lookat`): **Speaker Look Target** is a look target that points at
whoever speaks nearby, with a fallback target for silence. Listeners turn eyes and head to the speaker.

```
https://github.com/Atthree/wobblefolk-active-ragdoll.git?path=/Packages/com.wobblefolk.active-ragdoll
https://github.com/Atthree/wobblefolk-look-at.git?path=/Packages/com.atthree.wobblefolk.lookat
```

## Multiplayer

- **Do not network the mouth state.** Every client already hears the voice; run the analysis locally on the audio it
  plays (voice chat through **External Level Signal**, dialogue through Audio Source or Baked signals). It costs no
  bandwidth and stays in sync with the sound each player hears.
- **Delay offset:** voice chat audio reaches the speakers later than the SDK reports it. Raise *Delay* on External
  Level Signal (typically 0.05–0.3 s) until the mouth meets the sound. Drivers have their own *Delay* too.
- **LOD:** use level of detail (or `LodOverride` from your relevance system) so distant players cost little or nothing.
- **Muted or dead players:** `TalkVoice.SetEnabled(false)` closes the mouth smoothly without touching the audio.

## Animated characters

Drivers add the mouth on top of the animation. Whenever something else (an Animator, a script) moved the jaw or set a
blend shape since the driver last did, that becomes the new base, so nothing fights and nothing accumulates.

- If the Animator also animates the jaw, the mouth opens relative to the animated jaw. For a moment where the
  animation should own the jaw completely (a scream, eating), disable the driver.
- If a clip keys the jaw on some frames only, the jaw stays at its last keyed pose in between; key it on every frame
  of such clips, or mask the jaw out of the clip.
- Or drive the Animator instead: **Animator Parameter Driver** feeds a blend tree or talking layer.

## Split heads

A split head needs **separate meshes** for each moving piece: a top half and a bottom half (Unity's sphere cannot be
split). Put both under one parent, add an empty **hinge** at the back where they meet (it must not be inside a moving
piece), and use **Hinge Split Mouth Driver** with the Bouncy Split Head profile. The setup tool does this for children
named "Top Half" / "Bottom Half" (or Top / Upper and Bottom / Lower). Give the halves a lid in another material for
the inside of the mouth, as the sample does.

## Scripting

```csharp
voice.OnSpeechStarted += () => indicator.SetActive(true);
voice.OnSpeechLevel += state => Debug.Log(state.openness);   // every frame
voice.SetEnabled(false);                                     // fainted: the mouth closes smoothly
voice.LodOverride = TalkLod.Amplitude;                       // your LOD decides

externalSignal.PushSamples(pcm, channels, sampleRate);       // from a voice chat callback, any thread
textSignal.Say("Hello there!");                              // typed dialogue
BakedVoice baked = VoiceBaker.Bake(clipMadeInCode);          // runtime bake
```

All analysis types are plain C# (`TalkAnalyzer`, `NoiseGate`, `VoiceBaker`, `SpeakerTracker`) and can be used without
components.

## Tests

161 EditMode tests: analyzer, gate, envelope, spring, shapes, sources (external buffer, threading, text rhythm,
synthesiser), every driver (including a physical jaw simulated with `Physics.Simulate`), baking against live analysis,
LOD, indicator, speaker tracking and the setup tool. *Window > General > Test Runner > EditMode > Run All*.

## Troubleshooting

| Problem | Fix |
|---|---|
| The mouth does not move | Select the Talk Voice in Play Mode: is the level bar reaching the red gate line? Lower *Gate Threshold* or raise *Input Gain*. |
| Opens only a little | Lower *Full Level*, or raise *Exaggeration*. |
| Flutters in noise | Raise *Gate Threshold*, or enable *Adaptive*. |
| Streamed clip, no shapes | Set the clip to *Decompress On Load*, or bake it. |
| Lags behind voice chat audio | Increase *Delay* on External Level Signal (or decrease if the mouth is late). |
| Split head does not open | The hinge must not be a child of a moving piece (a warning says so). |
| Joint jaw does not follow | Give the joint a Slerp (or X) drive with a spring. |

## Credits and license

MIT, see [LICENSE](LICENSE). All code was written for this package; no third-party code or assets are included. Open
projects studied for ideas only are listed in
[THIRD_PARTY_NOTICES.md](Packages/com.atthree.wobblefolk.talkmouth/THIRD_PARTY_NOTICES.md).

---

# Türkçe

## İçindekiler

- [Özellikler](#özellikler)
- [Kurulum](#kurulum)
- [Hızlı başlangıç](#hızlı-başlangıç)
- [Örnekler](#örnekler)
- [Nasıl çalışır](#nasıl-çalışır)
- [Ses kaynakları](#ses-kaynakları)
- [Ağız sürücüleri](#ağız-sürücüleri)
- [Profiller ve hazır ayarlar](#profiller-ve-hazır-ayarlar)
- [Bake](#bake)
- [Detay seviyesi (LOD)](#detay-seviyesi-lod)
- [Konuşma göstergesi ve dinleyiciler](#konuşma-göstergesi-ve-dinleyiciler)
- [Entegrasyonlar](#entegrasyonlar)
- [Çok oyunculu](#çok-oyunculu)
- [Animasyonlu karakterler](#animasyonlu-karakterler)
- [Açılan kafalar](#açılan-kafalar)
- [Kod ile kullanım](#kod-ile-kullanım)
- [Testler](#testler)
- [Sorun giderme](#sorun-giderme)
- [Atıf ve lisans](#atıf-ve-lisans)

## Özellikler

- Tek analiz, çok ağız: bir sesi istediğin kadar sürücü izler (çene + dudak + dil + geç sallanan bir şapka).
- Histerezisli ve beklemeli gürültü kapısı; gürültülü mikrofonlar ve sessiz konuşanlar için isteğe bağlı uyarlanabilir kapı.
- Dilden bağımsız beş kaba ağız şekli (kapalı, AA, OH, EE, FF), bellek ayırmayan FFT ile; açılan kafalar ve robotlar için sadece genlik.
- Kare hızından bağımsız zarf ve tam çözümlü sönümlü yay: ağız açılır, aşar ve sekerek kapanır ama asla yüzün içinden geçmez.
- Sürücüler: Jaw Bone, Blend Shape, Transform, Hinge Split, Multi Bone, Sprite, Animator Parameter, Joint (fizik).
- Ses kaynağı: AudioSource, mikrofon, herhangi bir sesli sohbet SDK'sı, yazılı metin (isteğe bağlı mırıltı sesiyle) veya bake edilmiş klipler.
- Bake aracı, detay seviyesi, konuşma göstergesi, dinleyiciler için "kim konuşuyor" takibi.
- Kurulum aracı: *Wobblefolk > Setup Talk Mouth* çeneyi, blend shape'leri, eklemleri, yarımları ve sprite'ları bulur.
- Wobblefolk Active Ragdoll ve Wobblefolk Look At ile isteğe bağlı entegrasyon, zorunlu bağımlılık olmadan.
- Animator ile uyumlu (animasyonun üstüne eklenir), editör gizmo'ları, Inspector'da canlı ölçer.

## Kurulum

*Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-talk-mouth.git?path=/Packages/com.atthree.wobblefolk.talkmouth
```

Unity 6000.3 veya üstü. Bağımlılık yok. Entegrasyon örnekleri ilgili paketleri ister (bkz. [Entegrasyonlar](#entegrasyonlar)).

## Hızlı başlangıç

**Kurulum aracıyla:** karakterini seç, *Wobblefolk > Setup Talk Mouth* penceresini aç, bulduklarına bak, sesin nereden
geleceğini seç ve *Set Up Talk Mouth*'a bas. Tek bir geri alma adımında Talk Voice, ses kaynağı ve uygun sürücüyü
ekler; istersen hazır ayarı ince ayar için profil dosyası olarak kaydeder.

**Elle:**

1. Karaktere **Talk Voice** ve bir ses kaynağı ekle (ör. AudioSource'un yanına **Audio Source Signal**).
2. Hareket edecek parçaya bir sürücü ekle (ör. çeneye **Jaw Bone Mouth Driver**).
3. Play'e bas ve sesi seç: Inspector seviyeyi kapı eşiğine göre, açıklığı ve şekilleri canlı gösterir. Çene veya
   menteşe sürücüsünü seçince Scene görünümünde eksen ve açılma yayı çizilir.

## Örnekler

*Package Manager > Wobblefolk Talk Mouth > Samples* bölümünden içe aktar. Hepsi primitiflerden yapılmıştır; sesleri
başlangıçta üretilir, ses dosyası içermezler.

| Örnek | Gösterdiği | Tuşlar |
|---|---|---|
| **Humanoid** | Küre kafa, kapsül çene (Jaw Bone) ve şekle göre esneyen ağız (Transform), tek ses. | 1 / 2 / 3: sentetik ses, mikrofon, yazılı diyalog (Space: sonraki cümle). M: sustur. |
| **Split Head** | Üst yarı menteşeden açılır (Hinge Split, Bouncy Split Head); şapka geç gelir, alt yarı biraz düşer. | 1 / 2: ses, mikrofon. B: sekme. J: titreme. M: sustur. |
| **Monster** | Alt ve üst çene, yana açılan mandibulalar, sadece yüksek seste çıkan dil (Multi Bone). | 1 / 2, M. |
| **Beak** | Gaga yarımları, sırayla eğilen tepe tüyleri, yüksek hecelerde açılan kanatlar (Multi Bone). | 1 / 2, M. |
| **Crowd** | Başlangıçta bake edilen tek sesi paylaşan 20 karakter, LOD, detaya göre renklenen balonlar. | W / S: hareket. B: bake veya canlı. L: LOD. |
| **Active Ragdoll Integration** | Wobblefolk Active Ragdoll üzerinde ConfigurableJoint'li fiziksel çene (Joint sürücüsü). | WASD: yürü. 1 / 2, M. K: bayılt. |
| **Look At Integration** | Üç karakter sohbet eder; dinleyiciler gözünü ve başını konuşana çevirir, mikrofondan konuşursan sana da. | 2: mikrofon. Space: duraklat. |

## Nasıl çalışır

```
Ses kaynağı ──► Talk Voice (analiz) ──► MouthState ──► Ağız sürücüleri (LateUpdate)
 AudioSource      seviye → gürültü kapısı    açıklık 0–1       Jaw Bone, Blend Shape, Transform,
 Mikrofon         → zarf → eğri              şekil ağırlıkları Hinge Split, Multi Bone, Sprite,
 Harici SDK       → şekiller (FFT bantları)  konuşuyor mu      Animator Parameter, Joint
 Metin, Bake
```

| Adım | Ne yapar |
|---|---|
| Seviye | En yeni örneklerin RMS değeri (dB), artı giriş kazancı. |
| Gürültü kapısı | Eşikte açılır, bir histerezis altında kapanır; bekleme süresi kelime aralarında "konuşuyor" durumunu korur. |
| Uyarlanabilir (isteğe bağlı) | Kapı arka plan gürültüsünü, tam seviye sessiz konuşanları izler. |
| Zarf | Kare hızından bağımsız açılma, bırakma ve kapanma süreleri. |
| Tepki | Seviye eğrisi, abartma, isteğe bağlı basamaklar (robot aç/kapa), en büyük açıklık. |
| Şekiller | Bellek ayırmayan FFT ile dört bant, sonra beş şekilden en yakını. Mevcut şekil "yapışkan"dır, titremez. |
| Yay | Sürücülerde: sertlik ve aşma ile ayarlanan, her kare hızında kararlı tam çözümlü sönümlü yay. |

Analiz Update'te çalışır (sıra -50); sürücüler Animator'dan sonra LateUpdate'te uygular. Animator Parameter sürücüsü
ondan önce, Update'te çalışır.

## Ses kaynakları

| Kaynak | Ne için |
|---|---|
| **Audio Source Signal** | Diyalog klipleri. Klibi çalma konumundan okur; uzaklık, ses düzeyi ve susturma ağzı etkilemez. *Decompress On Load* gerekir; streaming klipler AudioSource çıkışına düşer (veya bake et). *Look Ahead* ağzı sesin biraz önüne alır. |
| **Baked Viseme Signal** | Bake edilmiş klipler: AudioSource'un çalma konumunu izler (pitch, duraklama, ileri sarma senkron kalır), çalışırken FFT yok, streaming ve sıkıştırılmış kliplerle çalışır. |
| **Microphone Signal** | Yerel oyuncu. Sadece analiz edilir, geri çalınmaz. Mobilde izin ister. WebGL'de yok. |
| **External Level Signal** | Herhangi bir sesli sohbet SDK'sı, bağımlılık olmadan: herhangi bir thread'den `PushSamples` veya `PushLevel`. *Delay* ağzı çalınan sese denk gelecek kadar geciktirir; kısa ağ boşlukları köprülenir. |
| **Text Driven Signal** | Yazılı diyalog: `Say("Merhaba!")`. Harfler şekli belirler, m/b/p dudakları kapatır, noktalama duraklatır. İsteğe bağlı mırıltı sesi ve daktilo efekti için harf olayları. |

Kendi kaynağın: `IVoiceSignalSource` uygula (`VoiceSignal`'a örnek, seviye veya bant yaz) ve `TalkVoice.SetSource` çağır.

## Ağız sürücüleri

| Sürücü | Hareket ettirdiği |
|---|---|
| **Jaw Bone** | Çene kemiği veya objesi; yerel eksen etrafında döner, isteğe bağlı kayar. "a" en geniş, "o" ve "e" daha az, "f" çok az açılır. |
| **Blend Shape** | Tablodan blend shape'ler: açıklık ve her şekil istenen blend shape'e ve ağırlığa. *Find Blend Shapes By Name* (bileşen menüsü) tabloyu `jawOpen`, `viseme_aa`, `mouth_O` gibi isimlerden doldurur. |
| **Transform** | Herhangi bir obje: tam açıklıkta konum, dönüş ve ölçek; isteğe bağlı şekle göre biçim. |
| **Hinge Split** | Menteşe etrafında dönen parçalar: açılan kafanın üstü, ters dönen alt yarı, geç gelen şapka. Parça başına oran, yön ve gecikme; hızlı açılma/bırakma, eğri, abartma, yüksek seste isteğe bağlı titreme. |
| **Multi Bone** | Her biri kendi ekseni, açısı, kayması, ölçeği, eğrisi, gecikmesi ve kanalı (açıklık veya bir şeklin ağırlığı) olan çok sayıda kemik. |
| **Sprite** | 2D: kapalıdan açığa kareler veya şekil başına sprite; en kısa gösterim süresi titremeyi önler; UI Image için olay. |
| **Animator Parameter** | Blend tree veya konuşma katmanı için Animator parametreleri (açıklık, konuşuyor mu, şekil ağırlıkları, en güçlü şekil). |
| **Joint** | Fiziksel çene, sadece `ConfigurableJoint.targetRotation` ile; hareketi eklemin açısal sürücüsü yapar. Ragdoll paketi gerekmez. |

Her sürücüde ortak ayarlar: *Amount* (açıklığı ölçekler), *Delay* (sesin gerisinde kalır), *Spring* (profilinki,
kendi yayı veya hiç) ve *Closed Bounce* (seken ağız kapalı pozdan geri seker, içinden geçmez). Sürücüyü kapatmak
dinlenme pozunu geri getirir.

## Profiller ve hazır ayarlar

*Create > Wobblefolk > Talk Mouth Profile*. Hazır ayarı Inspector'daki ⋮ menüsünden (veya başlığa sağ tıklayarak) uygula:

| Hazır ayar | Karakter |
|---|---|
| Human | Doğal: orta açıklık, yumuşak şekil geçişleri, neredeyse hiç sekme yok. |
| Cartoon | Geniş açılır, çabuk, sekerek kapanır. |
| Monster | Ağır: geç açılır, yavaş kapanır. |
| Bird | Hızlı ve keskin, sadece genlik. |
| Robot | Ya açık ya kapalı, arası yok. |
| Bouncy Split Head | Fısıltıya bile tepki verir (uyarlanabilir kapı), anında açılır, sallanarak kapanır. |

Profilsiz bir Talk Voice Human kullanır. Canlı ölçerle ayarla: sessiz sesler için *Gate Threshold*'u, daha geniş ağız
için *Full Level*'ı düşür.

## Bake

Ses kliplerini seç, *Assets > Wobblefolk > Bake Talk Mouth*: her birinin yanında 60 fps seviye ve spektrum içeren bir
`<klip>.talkmouth.asset` oluşur. Sıkıştırılmış veya streaming klipler bake sırasında kısa süreliğine açılır, sonra
import ayarları geri konur. Tekrar bake etmek dosyayı yerinde günceller, referanslar bozulmaz. Bake'leri **Baked Viseme
Signal**'a ekle; AudioSource'ta hangi klip çalıyorsa ona ait olanı oynatır.

Bake oynarken profil yine uygulanır (kapı, zarf, eğri, şekiller, yay); yani tek bake her karaktere uyar. Kodla
oluşturulan klipler çalışırken `VoiceBaker.Bake(clip)` ile bake edilebilir (Crowd örneği böyle yapar).

## Detay seviyesi (LOD)

**Talk Voice**'ta *Use Lod*'u aç:

| Detay | Ne zaman | Maliyet |
|---|---|---|
| Full | *Full Detail Distance*'tan yakın | Seviye ve şekiller |
| Amplitude | Daha uzak | Sadece seviye, FFT yok |
| Culled | *Cull Distance*'tan uzak veya *Visibility Renderer* ekran dışında | Hiçbir şey okunmaz; ağız kapanır, konuşma bitti olayları yine gelir |

`TalkVoice.LodOverride` ile kendi LOD sistemin veya ağ önceliğin karar verebilir. Bake edilmiş sesler daha da ucuzdur.

## Konuşma göstergesi ve dinleyiciler

**Speaking Indicator** ses konuşurken bir objeyi (balon, simge, UI rozeti) gösterir, açıklıkla nabız gibi atar ve son
kelimeden sonra bir süre daha tutar.

`SpeakerTracker` bir dinleyicinin yakınındaki en yüksek sesle konuşanı izler; insanlar üst üste konuşunca bir süre aynı
kişide kalır. Dinleyicilerin nereye bakacağını Talk Voice'taki *Speaker Point* (kafa veya ağız) belirler.
`TalkVoice.All` açık tüm sesleri listeler.

## Entegrasyonlar

İkisi de isteğe bağlıdır: çekirdek paket onlara başvurmaz, kodları sadece paket kuruluysa derlenir (assembly version defines).

**Wobblefolk Active Ragdoll** (`com.wobblefolk.active-ragdoll`): kafaya ConfigurableJoint ile bağlı, açısal sürücüsü olan
bir çene rigidbody'sine **Joint Mouth Driver** ekle. Örnek bu çeneyi çalışırken kurar, sürücüyü çenenin
eylemsizliğinden ayarlar, karakter ayıkken çeneyi yerçekimine karşı tutar, bayılınca sarkıtır.

**Wobblefolk Look At** (`com.atthree.wobblefolk.lookat`): **Speaker Look Target**, yakında kim konuşuyorsa onu hedef
alan bir bakış hedefidir; sessizlikte yedek hedefe döner. Dinleyiciler gözünü ve başını konuşana çevirir.

```
https://github.com/Atthree/wobblefolk-active-ragdoll.git?path=/Packages/com.wobblefolk.active-ragdoll
https://github.com/Atthree/wobblefolk-look-at.git?path=/Packages/com.atthree.wobblefolk.lookat
```

## Çok oyunculu

- **Ağız durumunu ağ üzerinden gönderme.** Her istemci sesi zaten duyuyor; analizi çaldığı ses üzerinde yerelde yap
  (sesli sohbet **External Level Signal** ile, diyalog Audio Source veya Baked kaynaklarıyla). Bant genişliği harcamaz
  ve her oyuncunun duyduğu sesle senkron kalır.
- **Gecikme ayarı:** sesli sohbetin sesi hoparlöre SDK'nın bildirdiğinden geç ulaşır. External Level Signal'daki
  *Delay*'i (genelde 0,05–0,3 sn) ağız sese denk gelene kadar artır. Sürücülerin de kendi *Delay*'i var.
- **LOD:** uzaktaki oyuncular az veya hiç maliyet getirmesin diye detay seviyesini (veya ilgi sisteminden `LodOverride`) kullan.
- **Susturulmuş veya ölü oyuncular:** `TalkVoice.SetEnabled(false)` sese dokunmadan ağzı yumuşakça kapatır.

## Animasyonlu karakterler

Sürücüler ağzı animasyonun üstüne ekler. Sürücünün son yazışından beri çeneyi başka bir şey (Animator, bir script)
oynattıysa veya blend shape'i değiştirdiyse, o yeni taban olur; böylece kavga da birikme de olmaz.

- Animator çeneyi de oynatıyorsa ağız, animasyonlu çeneye göre açılır. Animasyonun çeneyi tamamen yönetmesi gereken
  anlarda (çığlık, yemek yeme) sürücüyü kapat.
- Bir klip çeneyi sadece bazı karelerde anahtarlıyorsa aradaki karelerde çene son pozda kalır; bu kliplerde çeneyi her
  karede anahtarla veya klipten maskele.
- Ya da Animator'ı sür: **Animator Parameter Driver** bir blend tree'yi veya konuşma katmanını besler.

## Açılan kafalar

Açılan kafa her hareketli parça için **ayrı mesh** ister: üst yarı ve alt yarı (Unity'nin küresi bölünemez). İkisini
tek bir ebeveynin altına koy, birleştikleri yere arkaya boş bir **menteşe** objesi ekle (hareketli bir parçanın içinde
olmamalı) ve Bouncy Split Head profiliyle **Hinge Split Mouth Driver** kullan. Kurulum aracı "Top Half" / "Bottom Half"
(veya Top / Upper ve Bottom / Lower) adlı çocuklar için bunu kendisi yapar. Örnekteki gibi, ağzın içi için yarımlara
farklı malzemeli bir kapak ver.

## Kod ile kullanım

```csharp
voice.OnSpeechStarted += () => indicator.SetActive(true);
voice.OnSpeechLevel += state => Debug.Log(state.openness);   // her kare
voice.SetEnabled(false);                                     // bayıldı: ağız yumuşakça kapanır
voice.LodOverride = TalkLod.Amplitude;                       // kendi LOD'un karar verir

externalSignal.PushSamples(pcm, channels, sampleRate);       // sesli sohbet geri çağrısından, herhangi bir thread
textSignal.Say("Merhaba!");                                  // yazılı diyalog
BakedVoice baked = VoiceBaker.Bake(clipMadeInCode);          // çalışırken bake
```

Tüm analiz tipleri sade C#'tır (`TalkAnalyzer`, `NoiseGate`, `VoiceBaker`, `SpeakerTracker`), bileşen olmadan da kullanılabilir.

## Testler

161 EditMode testi: analiz, kapı, zarf, yay, şekiller, kaynaklar (harici tampon, thread güvenliği, metin ritmi,
sentezleyici), her sürücü (`Physics.Simulate` ile simüle edilen fiziksel çene dahil), canlı analize karşı bake, LOD,
gösterge, konuşmacı takibi ve kurulum aracı. *Window > General > Test Runner > EditMode > Run All*.

## Sorun giderme

| Sorun | Çözüm |
|---|---|
| Ağız oynamıyor | Play'de Talk Voice'u seç: seviye çubuğu kırmızı kapı çizgisine ulaşıyor mu? *Gate Threshold*'u düşür veya *Input Gain*'i artır. |
| Az açılıyor | *Full Level*'ı düşür veya *Exaggeration*'ı artır. |
| Gürültüde titriyor | *Gate Threshold*'u yükselt veya *Adaptive*'i aç. |
| Streaming klipte şekil yok | Klibi *Decompress On Load* yap veya bake et. |
| Sesli sohbet sesinin gerisinde | External Level Signal'da *Delay*'i artır (ağız geç kalıyorsa azalt). |
| Açılan kafa açılmıyor | Menteşe hareketli bir parçanın çocuğu olmamalı (uyarı mesajı bunu söyler). |
| Eklemli çene izlemiyor | Ekleme yaylı bir Slerp (veya X) sürücüsü ver. |

## Atıf ve lisans

MIT, bkz. [LICENSE](LICENSE). Tüm kod bu paket için yazıldı; üçüncü taraf kod veya asset içermez. Sadece fikir için
incelenen açık projeler [THIRD_PARTY_NOTICES.md](Packages/com.atthree.wobblefolk.talkmouth/THIRD_PARTY_NOTICES.md)
dosyasında listelenir.
