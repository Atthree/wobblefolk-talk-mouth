using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Tests
{
    /// <summary>Multi bone, sprite and animator drivers.</summary>
    public sealed partial class DriverTests
    {
        [Test]
        public void MultiBone_EachBoneMovesItsOwnWay()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject head = New("Head");
            Transform lower = New("Lower", head.transform).transform;
            Transform upper = New("Upper", head.transform).transform;
            Transform side = New("Side", head.transform).transform;
            var driver = Add<MultiBoneMouthDriver>(head, voice);
            MouthBone lowerBone = driver.AddBone(lower, Vector3.right, 30f);
            MouthBone upperBone = driver.AddBone(upper, Vector3.right, -10f);
            MouthBone sideBone = driver.AddBone(side, Vector3.up, 40f);
            sideBone.multiplier = 0.5f;

            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreEqual(30f, lowerBone.CurrentAngle, 0.5f);
            Assert.AreEqual(-10f, upperBone.CurrentAngle, 0.5f);
            Assert.AreEqual(20f, Quaternion.Angle(Quaternion.identity, side.localRotation), 0.5f);
            Assert.Less(lower.forward.y, 0f, "positive about X drops the front");
            Assert.Greater(upper.forward.y, 0f, "negative lifts it");

            driver.Restore();
            Assert.AreEqual(Quaternion.identity, lower.localRotation);
            Assert.AreEqual(Quaternion.identity, side.localRotation);
        }

        [Test]
        public void MultiBone_CurveOffsetAndScale()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject head = New("Head");
            Transform tongue = New("Tongue", head.transform).transform;
            var driver = Add<MultiBoneMouthDriver>(head, voice);
            MouthBone bone = driver.AddBone(tongue, Vector3.right, 0f);
            bone.openOffset = new Vector3(0f, 0f, 0.2f);
            bone.openScale = new Vector3(1f, 1f, 2f);
            bone.curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f));

            source.level = 0.4f;
            Run(voice, 1f, driver);
            Assert.AreEqual(0f, tongue.localPosition.z, 1e-3f, "stays in until loud");

            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreEqual(0.2f, tongue.localPosition.z, 0.005f);
            Assert.AreEqual(2f, tongue.localScale.z, 0.03f);
        }

        [Test]
        public void MultiBone_FollowsAShapeChannelAndDelay()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            GameObject head = New("Head");
            Transform lips = New("Lips", head.transform).transform;
            Transform late = New("Late", head.transform).transform;
            var driver = Add<MultiBoneMouthDriver>(head, voice);
            MouthBone round = driver.AddBone(lips, Vector3.right, 20f);
            round.channel = MouthChannel.OH;
            MouthBone lagging = driver.AddBone(late, Vector3.right, 20f, 0.15f);

            source.level = 1f;
            source.shape = Viseme.AA;
            Run(voice, 0.1f, driver);
            Assert.Less(lagging.CurrentAngle, 1f, "not yet");
            Run(voice, 1f, driver);
            Assert.Less(round.CurrentAngle, 3f, "aa is not oh");
            Assert.AreEqual(20f, lagging.CurrentAngle, 0.5f);

            source.shape = Viseme.OH;
            Run(voice, 1f, driver);
            Assert.Greater(round.CurrentAngle, 15f);
        }

        Sprite NewSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            created.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            created.Add(sprite);
            return sprite;
        }

        [Test]
        public void Sprite_OpeningFramesFollowTheOpening()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            Sprite rest = NewSprite("Rest"), shut = NewSprite("Shut"), half = NewSprite("Half"), wide = NewSprite("Wide");
            var renderer = New("Mouth").AddComponent<SpriteRenderer>();
            renderer.sprite = rest;
            var driver = Add<SpriteMouthDriver>(renderer.gameObject, voice);
            driver.OpeningFrames = new[] { shut, half, wide };

            Run(voice, 0.3f, driver);
            Assert.AreSame(shut, renderer.sprite);
            source.level = 0.5f;
            Run(voice, 1f, driver);
            Assert.AreSame(half, renderer.sprite);
            source.level = 1f;
            Run(voice, 1f, driver);
            Assert.AreSame(wide, renderer.sprite);

            driver.Restore();
            Assert.AreSame(rest, renderer.sprite);
        }

        [Test]
        public void Sprite_ShapesPickTheStrongestShape()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            Sprite closed = NewSprite("Closed"), aa = NewSprite("AA"), oh = NewSprite("OH"), ee = NewSprite("EE");
            var renderer = New("Mouth").AddComponent<SpriteRenderer>();
            var driver = Add<SpriteMouthDriver>(renderer.gameObject, voice);
            driver.Mode = SpriteMouthMode.Shapes;
            driver.SetShapeSprites(closed, aa, oh, ee, null);

            Run(voice, 0.3f, driver);
            Assert.AreSame(closed, renderer.sprite);

            source.level = 1f;
            foreach ((Viseme shape, Sprite expected) in new[] { (Viseme.AA, aa), (Viseme.EE, ee), (Viseme.OH, oh) })
            {
                source.shape = shape;
                Run(voice, 1f, driver);
                Assert.AreSame(expected, renderer.sprite, shape.ToString());
            }

            source.shape = Viseme.FF; // no ff sprite: the strongest shape that has one
            Run(voice, 1f, driver);
            CollectionAssert.Contains(new[] { aa, oh, ee }, renderer.sprite);
        }

        [Test]
        public void Sprite_HoldTimeStopsFlicker()
        {
            TalkVoice voice = NewVoice(out LevelSource source, TalkMouthPreset.Robot); // jumps between open and shut
            var driver = Add<SpriteMouthDriver>(New("Mouth"), voice);
            driver.OpeningFrames = new[] { NewSprite("Shut"), NewSprite("Open") };
            driver.MinHoldTime = 0.2f;
            var shown = new List<Sprite>();
            driver.SpriteChanged.AddListener(shown.Add);

            for (int i = 0; i < 60; i++)
            {
                source.level = i / 5 % 2 == 0 ? 1f : 0f; // a syllable every 1/6 s
                voice.Tick(Dt);
                driver.Tick(Dt);
            }

            Assert.Greater(shown.Count, 1, "it does change");
            Assert.LessOrEqual(shown.Count, 6, "at most once per hold time");
            Assert.AreSame(shown[shown.Count - 1], driver.Current, "the event works without a renderer");
        }

        Animator NewAnimator(out AnimatorController controller)
        {
            controller = new AnimatorController { name = "Talk" };
            created.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("MouthOpen", AnimatorControllerParameterType.Float);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shape", AnimatorControllerParameterType.Int);
            controller.AddParameter("Wrong", AnimatorControllerParameterType.Int);

            var animator = New("Character").AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        [Test]
        public void Animator_GetsTheVoiceParameters()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            Animator animator = NewAnimator(out _);
            var driver = Add<AnimatorParameterDriver>(animator.gameObject, voice);
            driver.SetParameters("MouthOpen", "Talking", shape: "Shape");
            Assert.AreEqual(0, driver.MissingParameters);

            source.level = 1f;
            source.shape = Viseme.EE;
            Run(voice, 1f, driver);
            Assert.AreEqual(1f, animator.GetFloat("MouthOpen"), 0.02f);
            Assert.IsTrue(animator.GetBool("Talking"));
            Assert.AreEqual((int)Viseme.EE, animator.GetInteger("Shape"));

            driver.Restore();
            Assert.AreEqual(0f, animator.GetFloat("MouthOpen"));
            Assert.IsFalse(animator.GetBool("Talking"));
        }

        [Test]
        public void Animator_MissingOrMistypedParametersAreSkipped()
        {
            TalkVoice voice = NewVoice(out LevelSource source);
            Animator animator = NewAnimator(out _);
            var driver = Add<AnimatorParameterDriver>(animator.gameObject, voice);
            driver.SetParameters("MouthOpen", "", aaName: "NotThere", ffName: "Wrong");
            Assert.AreEqual(2, driver.MissingParameters, "an absent name and an int used as a float");

            source.level = 1f;
            Run(voice, 0.5f, driver);
            Assert.Greater(animator.GetFloat("MouthOpen"), 0.5f, "the valid ones still work");
        }
    }
}
