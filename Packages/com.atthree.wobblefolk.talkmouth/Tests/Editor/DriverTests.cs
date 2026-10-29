using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class DriverTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();

        sealed class LevelSource : IVoiceSignalSource
        {
            public float level;
            public Viseme shape = Viseme.Closed;

            public void Read(VoiceSignal signal)
            {
                if (level > 0f) signal.SetNormalized(level, shape);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        GameObject New(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            else created.Add(go);
            return go;
        }

        TalkVoice NewVoice(out LevelSource source, TalkMouthPreset preset = TalkMouthPreset.Human)
        {
            TalkVoice voice = New("Voice").AddComponent<TalkVoice>();
            TalkMouthProfile profile = TestSignals.Profile(preset);
            created.Add(profile);
            voice.Profile = profile;
            source = new LevelSource();
            voice.SetSource(source);
            return voice;
        }

        static T Add<T>(GameObject go, TalkVoice voice, MouthSpringMode spring = MouthSpringMode.Off) where T : MouthDriver
        {
            var driver = go.AddComponent<T>();
            driver.Voice = voice;
            driver.SpringMode = spring;
            return driver;
        }

        static void Run(TalkVoice voice, float seconds, params MouthDriver[] drivers)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                voice.Tick(Dt);
                foreach (MouthDriver d in drivers) d.Tick(Dt);
            }
        }

        [Test]
        public void History_DelaysAndInterpolates()
        {
            var history = new MouthHistory(8);
            history.Push(0.0, new MouthSample(0f, VisemeWeights.Rest, false));
            history.Push(0.1, new MouthSample(1f, VisemeWeights.Only(Viseme.AA), true));

            Assert.AreEqual(1f, history.Sample(0f).openness);
            Assert.AreEqual(0.5f, history.Sample(0.05f).openness, 1e-4f);
            Assert.AreEqual(0.5f, history.Sample(0.05f).visemes.aa, 1e-4f);
            Assert.AreEqual(0f, history.Sample(1f).openness, "before the first entry: closed");

            for (int i = 0; i < 20; i++) history.Push(0.2 + i * 0.1, new MouthSample(i, VisemeWeights.Rest, true));
            Assert.AreEqual(8, history.Count, "fixed size");
            Assert.AreEqual(19f, history.Sample(0f).openness);
            Assert.AreEqual(17.5f, history.Sample(0.15f).openness, 1e-3f);
        }

        [Test]
        public void Jaw_OpensToTheOpenAngleAndCloses()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject jaw = New("Jaw");
            Quaternion rest = Quaternion.Euler(5f, 10f, 0f);
            jaw.transform.localRotation = rest;
            var driver = Add<JawBoneMouthDriver>(jaw, voice);
            driver.ShapeInfluence = 0f;
            driver.OpenAngle = 20f;

            Run(voice, 0.3f, driver);
            Assert.AreEqual(0f, Quaternion.Angle(rest, jaw.transform.localRotation), 1e-3f, "silent: rest pose");

            source.level = 1f;
            Run(voice, 1f, driver);
            Quaternion bend = Quaternion.Inverse(rest) * jaw.transform.localRotation;
            bend.ToAngleAxis(out float angle, out Vector3 axis);
            Assert.AreEqual(20f, angle, 0.5f);
            Assert.Greater(Vector3.Dot(axis, Vector3.right), 0.999f, "about the local X axis");

            source.level = 0f;
            Run(voice, 1f, driver);
            Assert.AreEqual(0f, Quaternion.Angle(rest, jaw.transform.localRotation), 0.1f);

            source.level = 1f;
            Run(voice, 0.5f, driver);
            driver.Restore();
            Assert.AreEqual(0f, Quaternion.Angle(rest, jaw.transform.localRotation), 1e-3f, "restore gives the rest pose back");
        }

        [Test]
        public void Jaw_IsAddedOnTopOfAnimationWithoutAccumulating()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject jaw = New("Jaw");
            var driver = Add<JawBoneMouthDriver>(jaw, voice);
            driver.ShapeInfluence = 0f;
            source.level = 1f;
            Run(voice, 1f, driver);

            // An Animator writes its pose every frame before LateUpdate.
            Quaternion animated = Quaternion.Euler(0f, 30f, 0f);
            for (int i = 0; i < 30; i++)
            {
                jaw.transform.localRotation = animated;
                voice.Tick(Dt);
                driver.Tick(Dt);
            }

            Assert.AreEqual(20f, Quaternion.Angle(animated, jaw.transform.localRotation), 0.5f, "animation plus mouth");

            // Nothing animates any more: the last animated pose stays the base, the opening does not pile up.
            Run(voice, 0.5f, driver);
            Assert.AreEqual(20f, Quaternion.Angle(animated, jaw.transform.localRotation), 0.5f);
        }

        [Test]
        public void Jaw_ShapeChangesTheOpening()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject jaw = New("Jaw");
            var driver = Add<JawBoneMouthDriver>(jaw, voice);
            driver.ShapeInfluence = 1f;
            source.level = 1f;

            source.shape = Viseme.AA;
            Run(voice, 1f, driver);
            float aa = driver.CurrentAngle;

            source.shape = Viseme.EE;
            Run(voice, 1f, driver);
            float ee = driver.CurrentAngle;

            Assert.AreEqual(20f, aa, 1.5f);
            Assert.AreEqual(10f, ee, 2f, "ee opens half as far by default");
        }

        [Test]
        public void Jaw_OffsetSlidesWithTheOpening()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject jaw = New("Jaw");
            jaw.transform.localPosition = new Vector3(0f, 1f, 0f);
            var driver = Add<JawBoneMouthDriver>(jaw, voice);
            driver.ShapeInfluence = 0f;
            driver.OpenOffset = new Vector3(0f, -0.1f, 0.05f);
            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreEqual(new Vector3(0f, 0.9f, 0.05f).ToString("F2"), jaw.transform.localPosition.ToString("F2"));
        }

        [Test]
        public void Spring_OvershootsOpenAndBouncesShutWithoutPassingClosed()
        {
            TalkVoice voice = NewVoice(out LevelSource source, TalkMouthPreset.Robot); // instant open/shut steps
            GameObject jaw = New("Jaw");
            var driver = Add<JawBoneMouthDriver>(jaw, voice, MouthSpringMode.Custom);
            driver.ShapeInfluence = 0f;
            driver.CustomSpring.frequency = 5f;
            driver.CustomSpring.overshoot = 0.3f;

            source.level = 1f;
            float max = 0f;
            for (float t = 0f; t < 1f; t += Dt)
            {
                voice.Tick(Dt);
                driver.Tick(Dt);
                max = Mathf.Max(max, driver.CurrentOpening);
            }

            Assert.Greater(max, 1.15f, "pops past fully open");
            Assert.AreEqual(1f, driver.CurrentOpening, 0.05f, "and settles");

            source.level = 0f;
            float min = 1f;
            for (float t = 0f; t < 2f; t += Dt)
            {
                voice.Tick(Dt);
                driver.Tick(Dt);
                min = Mathf.Min(min, driver.CurrentOpening);
            }

            Assert.GreaterOrEqual(min, 0f, "never through the face");
            Assert.AreEqual(0f, driver.CurrentOpening, 0.01f);
        }

        [Test]
        public void Spring_ProfileModeUsesTheVoiceProfile()
        {
            TalkVoice voice = NewVoice(out _, TalkMouthPreset.BouncySplitHead);
            var driver = Add<JawBoneMouthDriver>(New("Jaw"), voice, MouthSpringMode.Profile);
            Assert.AreSame(voice.Profile.spring, driver.Spring);
            driver.SpringMode = MouthSpringMode.Off;
            Assert.IsNull(driver.Spring);
        }

        [Test]
        public void Amount_ScalesTheOpening()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            var driver = Add<JawBoneMouthDriver>(New("Jaw"), voice);
            driver.ShapeInfluence = 0f;
            driver.Amount = 0.5f;
            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreEqual(10f, driver.CurrentAngle, 0.5f);
        }

        HingeSplitMouthDriver NewSplitHead(TalkVoice voice, out Transform top, out Transform bottom, out Transform hinge)
        {
            GameObject head = New("Head");
            hinge = New("Hinge", head.transform).transform;
            hinge.localPosition = new Vector3(0f, 0f, -0.5f);
            top = New("Top", head.transform).transform;
            top.localPosition = new Vector3(0f, 0.25f, 0f);
            bottom = New("Bottom", head.transform).transform;
            bottom.localPosition = new Vector3(0f, -0.25f, 0f);

            var driver = Add<HingeSplitMouthDriver>(head, voice);
            driver.Hinge = hinge;
            driver.Axis = Vector3.left;
            driver.MaxOpenAngle = 45f;
            driver.AttackTime = 0f;
            driver.ReleaseTime = 0f;
            return driver;
        }

        [Test]
        public void Hinge_SwingsThePiecesAboutThePivot()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            HingeSplitMouthDriver driver = NewSplitHead(voice, out Transform top, out Transform bottom, out Transform hinge);
            driver.AddPart(top);
            driver.AddPart(bottom, 0.5f, reverse: true);

            Vector3 topRest = top.position - hinge.position;
            source.level = 1f;
            Run(voice, 1f, driver);

            Vector3 topNow = top.position - hinge.position;
            Assert.AreEqual(topRest.magnitude, topNow.magnitude, 1e-4f, "stays on its arc");
            Assert.AreEqual(45f, Vector3.Angle(topRest, topNow), 0.5f);
            Assert.Greater(top.forward.y, 0.6f, "the front of the top half lifts");
            Assert.AreEqual(-22.5f, driver.Parts[1].CurrentAngle, 0.5f, "half as far, the other way");
            Assert.Less(bottom.forward.y, -0.3f, "the bottom half drops");

            source.level = 0f;
            Run(voice, 1.5f, driver);
            Assert.AreEqual(0f, Vector3.Angle(topRest, top.position - hinge.position), 0.2f, "closes again");

            source.level = 1f;
            Run(voice, 0.5f, driver);
            driver.Restore();
            Assert.AreEqual(new Vector3(0f, 0.25f, 0f), top.localPosition);
            Assert.AreEqual(Quaternion.identity, top.localRotation);
        }

        [Test]
        public void Hinge_DelayedPieceFollowsLater()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            HingeSplitMouthDriver driver = NewSplitHead(voice, out Transform top, out Transform bottom, out _);
            HingePart lead = driver.AddPart(top);
            HingePart late = driver.AddPart(bottom, 1f, false, 0.15f);

            source.level = 1f;
            Run(voice, 0.1f, driver);
            Assert.Greater(lead.CurrentAngle, 20f);
            Assert.Less(late.CurrentAngle, 1f, "not yet");

            Run(voice, 1f, driver);
            Assert.AreEqual(lead.CurrentAngle, late.CurrentAngle, 0.5f);
        }

        [Test]
        public void Hinge_ExaggerationAndCurveShapeTheSwing()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            HingeSplitMouthDriver driver = NewSplitHead(voice, out Transform top, out _, out _);
            HingePart part = driver.AddPart(top);

            source.level = 0.25f;
            Run(voice, 1f, driver);
            float plain = part.CurrentAngle;

            driver.Exaggeration = 2f;
            Run(voice, 1f, driver);
            Assert.AreEqual(plain * 2f, part.CurrentAngle, 1f);

            driver.Exaggeration = 4f;
            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreEqual(45f, part.CurrentAngle, 0.5f, "capped at the full angle");

            driver.Exaggeration = 1f;
            driver.LevelCurve = AnimationCurve.Constant(0f, 1f, 0.5f);
            Run(voice, 1f, driver);
            Assert.AreEqual(22.5f, part.CurrentAngle, 0.5f);
        }

        [Test]
        public void Hinge_JittersOnlyWhenLoud()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            HingeSplitMouthDriver driver = NewSplitHead(voice, out Transform top, out _, out _);
            HingePart part = driver.AddPart(top);
            driver.SetJitter(true, 0.7f, 5f, 14f);

            source.level = 0.5f;
            Run(voice, 1f, driver);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 60; i++)
            {
                Run(voice, Dt, driver);
                min = Mathf.Min(min, part.CurrentAngle);
                max = Mathf.Max(max, part.CurrentAngle);
            }

            Assert.Less(max - min, 0.2f, "steady below the threshold");

            source.level = 1f;
            Run(voice, 1f, driver);
            min = float.MaxValue;
            max = float.MinValue;
            for (int i = 0; i < 60; i++)
            {
                Run(voice, Dt, driver);
                min = Mathf.Min(min, part.CurrentAngle);
                max = Mathf.Max(max, part.CurrentAngle);
            }

            Assert.Greater(max - min, 1f, "rattles when loud");
            Assert.LessOrEqual(max, 45f + 5f + 0.5f);
        }

        [Test]
        public void Hinge_InsideAMovingPieceIsRefused()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            HingeSplitMouthDriver driver = NewSplitHead(voice, out Transform top, out _, out Transform hinge);
            hinge.SetParent(top, true);
            driver.AddPart(top);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("hinge must not be Top"));
            source.level = 1f;
            Run(voice, 0.5f, driver);
            Assert.AreEqual(Quaternion.identity, top.localRotation);
        }

        SkinnedMeshRenderer NewFace(params string[] shapes)
        {
            var mesh = new Mesh { name = "Face" };
            created.Add(mesh);
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            var delta = new Vector3[3];
            foreach (string shape in shapes) mesh.AddBlendShapeFrame(shape, 100f, delta, null, null);

            var smr = New("Face").AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            return smr;
        }

        [Test]
        public void BlendShapes_FollowTheTable()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            SkinnedMeshRenderer face = NewFace("jawOpen", "viseme_aa", "viseme_O");
            var driver = Add<BlendShapeMouthDriver>(face.gameObject, voice);
            driver.Bindings.Add(new BlendShapeBinding("jawOpen", MouthChannel.Open));
            driver.Bindings.Add(new BlendShapeBinding("viseme_aa", MouthChannel.AA));
            driver.Bindings.Add(new BlendShapeBinding("viseme_O", MouthChannel.OH, 80f));
            driver.Bindings.Add(new BlendShapeBinding("missing", MouthChannel.EE));

            source.level = 1f;
            source.shape = Viseme.AA;
            Run(voice, 1f, driver);

            Assert.AreEqual(1, driver.MissingBindings);
            Assert.AreEqual(100f, face.GetBlendShapeWeight(0), 2f);
            Assert.Greater(face.GetBlendShapeWeight(1), 60f);
            Assert.Less(face.GetBlendShapeWeight(2), 20f);

            source.shape = Viseme.OH;
            Run(voice, 1f, driver);
            Assert.Greater(face.GetBlendShapeWeight(2), 50f);
            Assert.LessOrEqual(face.GetBlendShapeWeight(2), 80f + 1e-3f, "row weight is the most it reaches");

            driver.Restore();
            for (int i = 0; i < 3; i++) Assert.AreEqual(0f, face.GetBlendShapeWeight(i));
        }

        [Test]
        public void BlendShapes_AddToAnimatedWeights()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            SkinnedMeshRenderer face = NewFace("jawOpen");
            var driver = Add<BlendShapeMouthDriver>(face.gameObject, voice);
            driver.Bindings.Add(new BlendShapeBinding("jawOpen", MouthChannel.Open));
            source.level = 0.5f;
            Run(voice, 1f, driver);
            Assert.AreEqual(50f, face.GetBlendShapeWeight(0), 2f);

            for (int i = 0; i < 10; i++)
            {
                face.SetBlendShapeWeight(0, 30f); // an animation
                voice.Tick(Dt);
                driver.Tick(Dt);
            }

            Assert.AreEqual(80f, face.GetBlendShapeWeight(0), 2f);
            Run(voice, 0.5f, driver);
            Assert.AreEqual(80f, face.GetBlendShapeWeight(0), 2f, "does not pile up");
        }

        [Test]
        public void BlendShapes_FindByName()
        {
            SkinnedMeshRenderer face = NewFace("Brow_Up", "Face.jawOpen", "viseme_PP", "mouth_O", "viseme_aa", "V_EE", "FF", "viseme_aa_copy");
            var driver = face.gameObject.AddComponent<BlendShapeMouthDriver>();
            Assert.AreEqual(6, driver.FindBlendShapesByName());
            Assert.AreEqual("Face.jawOpen", driver.Bindings[0].blendShape);
            Assert.AreEqual(MouthChannel.Open, driver.Bindings[0].channel);
            Assert.AreEqual(0, driver.FindBlendShapesByName(), "rows already present are kept, not doubled");
        }

        [TestCase("jawOpen", MouthChannel.Open)]
        [TestCase("Mouth Open", MouthChannel.Open)]
        [TestCase("viseme_PP", MouthChannel.Closed)]
        [TestCase("vrc.v_aa", MouthChannel.AA)]
        [TestCase("A", MouthChannel.AA)]
        [TestCase("viseme_O", MouthChannel.OH)]
        [TestCase("mouth_U", MouthChannel.OH)]
        [TestCase("viseme_I", MouthChannel.EE)]
        [TestCase("ee", MouthChannel.EE)]
        [TestCase("viseme_FF", MouthChannel.FF)]
        public void BlendShapes_GuessChannel(string name, MouthChannel expected)
        {
            Assert.IsTrue(BlendShapeMouthDriver.GuessChannel(name, out MouthChannel channel));
            Assert.AreEqual(expected, channel);
        }

        [TestCase("Brow_Up")]
        [TestCase("Blink_L")]
        [TestCase("")]
        public void BlendShapes_GuessChannelIgnoresOtherShapes(string name)
        {
            Assert.IsFalse(BlendShapeMouthDriver.GuessChannel(name, out _));
        }

        [Test]
        public void Transform_MovesAndScalesWithTheOpening()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject mouth = New("Mouth");
            mouth.transform.localPosition = new Vector3(0f, 1f, 0f);
            mouth.transform.localScale = new Vector3(0.2f, 0.1f, 0.1f);
            var driver = Add<TransformMouthDriver>(mouth, voice);
            driver.OpenPosition = new Vector3(0f, -0.05f, 0f);
            driver.OpenScale = new Vector3(1f, 3f, 1f);

            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreEqual(0.95f, mouth.transform.localPosition.y, 1e-3f);
            Assert.AreEqual(0.3f, mouth.transform.localScale.y, 1e-3f);
            Assert.AreEqual(0.2f, mouth.transform.localScale.x, 1e-4f);

            driver.UseShapes = true;
            driver.SetShapeScales(Vector3.one, Vector3.one, new Vector3(2f, 0.5f, 1f), Vector3.one);
            source.shape = Viseme.EE;
            Run(voice, 1f, driver);
            Assert.Greater(mouth.transform.localScale.x, 0.3f, "ee widens");

            driver.Restore();
            Assert.AreEqual(new Vector3(0.2f, 0.1f, 0.1f), mouth.transform.localScale);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), mouth.transform.localPosition);
        }

        [Test]
        public void OneVoice_DrivesManyMouths()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            var jaw = Add<JawBoneMouthDriver>(New("Jaw"), voice);
            jaw.ShapeInfluence = 0f;
            var lips = Add<TransformMouthDriver>(New("Lips"), voice);
            source.level = 1f;
            Run(voice, 1f, jaw, lips);
            Assert.AreEqual(20f, jaw.CurrentAngle, 0.5f);
            Assert.AreEqual(1f, lips.CurrentOpening, 0.02f);
        }

        [Test]
        public void NoVoice_KeepsTheRestPose()
        {
            GameObject jaw = New("Jaw");
            var driver = jaw.AddComponent<JawBoneMouthDriver>();
            for (int i = 0; i < 10; i++) driver.Tick(Dt);
            Assert.AreEqual(Quaternion.identity, jaw.transform.localRotation);
        }
    }
}
