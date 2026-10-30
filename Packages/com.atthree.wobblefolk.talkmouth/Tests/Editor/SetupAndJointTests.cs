using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wobblefolk.TalkMouth.Editor;

namespace Wobblefolk.TalkMouth.Tests
{
    public sealed class SetupAndJointTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        GameObject Root(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }

        [Test]
        public void Setup_SplitHeadGetsAHingeBehindTheHalves()
        {
            GameObject root = Root("Talker");
            Transform top = Shape(PrimitiveType.Sphere, "Top Half", root.transform, new Vector3(0f, 0.25f, 0f)).transform;
            Transform bottom = Shape(PrimitiveType.Sphere, "Bottom Half", root.transform, new Vector3(0f, -0.25f, 0f)).transform;

            TalkMouthSetupPlan plan = TalkMouthSetup.Analyze(root);
            Assert.AreEqual(SetupDriver.HingeSplit, plan.driver);
            Assert.AreEqual(TalkMouthPreset.BouncySplitHead, plan.preset);
            Assert.AreSame(top, plan.upperHalf);
            Assert.AreSame(bottom, plan.lowerHalf);

            TalkVoice voice = TalkMouthSetup.Apply(plan, null);
            Assert.IsNotNull(voice);
            Assert.IsInstanceOf<AudioSourceSignal>(voice.Source);
            Assert.IsNotNull(root.GetComponent<AudioSource>());

            var split = root.GetComponent<HingeSplitMouthDriver>();
            Assert.AreSame(voice, split.Voice);
            Assert.AreEqual(2, split.Parts.Count);
            Assert.IsTrue(split.Parts[1].reverse);
            Transform hinge = split.Hinge;
            Assert.AreEqual("Talk Hinge", hinge.name);
            Assert.Less(hinge.position.z, -0.45f, "behind the head");
            Assert.AreEqual(0f, hinge.position.y, 0.01f, "where the halves meet");
        }

        [Test]
        public void Setup_PrefersBlendShapesAndFillsTheTable()
        {
            GameObject root = Root("Face Character");
            new GameObject("Jaw").transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            created.Add(mesh);
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            foreach (string shape in new[] { "jawOpen", "viseme_aa", "viseme_O", "Blink" })
            {
                mesh.AddBlendShapeFrame(shape, 100f, new Vector3[3], null, null);
            }

            var face = new GameObject("Face").AddComponent<SkinnedMeshRenderer>();
            face.transform.SetParent(root.transform, false);
            face.sharedMesh = mesh;

            TalkMouthSetupPlan plan = TalkMouthSetup.Analyze(root);
            Assert.AreEqual(SetupDriver.BlendShapes, plan.driver, "blend shapes win over a jaw object");
            plan.source = SetupSource.Text;
            TalkVoice voice = TalkMouthSetup.Apply(plan, null);
            Assert.IsInstanceOf<TextDrivenSignal>(voice.Source);
            Assert.AreEqual(3, face.GetComponent<BlendShapeMouthDriver>().Bindings.Count);
        }

        [Test]
        public void Setup_FindsAJawJointOrBone()
        {
            GameObject root = Root("Ragdoll");
            root.AddComponent<Rigidbody>();
            GameObject jaw = Shape(PrimitiveType.Capsule, "Lower Jaw", root.transform, new Vector3(0f, -0.1f, 0.1f));
            Assert.AreEqual(SetupDriver.JawBone, TalkMouthSetup.Analyze(root).driver);

            jaw.AddComponent<ConfigurableJoint>().connectedBody = root.GetComponent<Rigidbody>();
            TalkMouthSetupPlan plan = TalkMouthSetup.Analyze(root);
            Assert.AreEqual(SetupDriver.Joint, plan.driver);
            TalkMouthSetup.Apply(plan, null);
            Assert.IsNotNull(jaw.GetComponent<JointMouthDriver>());
        }

        [Test]
        public void Setup_DoesNotTakeLimbsForHeadHalves()
        {
            GameObject root = Root("Person");
            Shape(PrimitiveType.Capsule, "UpperArm", root.transform, Vector3.up);
            Shape(PrimitiveType.Capsule, "LowerLeg", root.transform, Vector3.zero);
            Shape(PrimitiveType.Sphere, "Mouth", root.transform, Vector3.up * 1.5f);

            TalkMouthSetupPlan plan = TalkMouthSetup.Analyze(root);
            Assert.IsNull(plan.upperHalf);
            Assert.AreEqual(SetupDriver.Transform, plan.driver);
            Assert.AreEqual("Mouth", plan.mouthObject.name);
        }

        [Test]
        public void Setup_IsOneUndoStep()
        {
            GameObject root = Root("Talker");
            Shape(PrimitiveType.Sphere, "Mouth", root.transform, Vector3.zero);
            Undo.IncrementCurrentGroup();
            TalkMouthSetup.Apply(TalkMouthSetup.Analyze(root), null);
            Assert.IsNotNull(root.GetComponent<TalkVoice>());

            Undo.PerformUndo();
            Assert.IsTrue(root.GetComponent<TalkVoice>() == null);
            Assert.IsTrue(root.GetComponent<AudioSource>() == null);
            Assert.IsTrue(root.transform.Find("Mouth").GetComponent<TransformMouthDriver>() == null);
        }

        [Test]
        public void Joint_TargetRotationIsTheLocalTurnInJointSpace()
        {
            Quaternion identityFrame = JointMouthDriver.JointToLocal(Vector3.right, Vector3.up);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, identityFrame), 1e-3f);
            Quaternion target = JointMouthDriver.TargetRotation(20f, Vector3.right, identityFrame);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Inverse(Quaternion.AngleAxis(20f, Vector3.right)), target), 1e-3f);
        }

        sealed class Loud : IVoiceSignalSource
        {
            public float level;
            public void Read(VoiceSignal signal)
            {
                if (level > 0f) signal.SetNormalized(level, Viseme.AA);
            }
        }

        [Test]
        public void Joint_DriveSwingsAPhysicalJaw([Values(0, 1)] int axisSetup)
        {
            SimulationMode mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                GameObject head = Root("Head");
                var headBody = head.AddComponent<Rigidbody>();
                headBody.isKinematic = true;

                GameObject jaw = Root("Jaw");
                jaw.transform.position = new Vector3(0f, -0.05f, 0f);
                var jawBody = jaw.AddComponent<Rigidbody>();
                jawBody.useGravity = false;
                jawBody.mass = 0.2f;
                jawBody.inertiaTensor = Vector3.one * 0.01f;
                jawBody.inertiaTensorRotation = Quaternion.identity;

                var joint = jaw.AddComponent<ConfigurableJoint>();
                // Joint frames that differ from the body's axes must not change the result.
                if (axisSetup == 1)
                {
                    joint.axis = Vector3.forward;
                    joint.secondaryAxis = Vector3.right;
                }

                joint.connectedBody = headBody;
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = new JointDrive { positionSpring = 50f, positionDamper = 1f, maximumForce = float.MaxValue };

                TalkVoice voice = Root("Voice").AddComponent<TalkVoice>();
                var source = new Loud();
                voice.SetSource(source);
                var driver = jaw.AddComponent<JointMouthDriver>();
                driver.Voice = voice;
                driver.ShapeInfluence = 0f;
                driver.OpenAngle = 25f;

                void Run(float seconds)
                {
                    for (float t = 0f; t < seconds; t += Dt)
                    {
                        voice.Tick(Dt);
                        driver.Tick(Dt);
                        Physics.Simulate(Dt);
                    }
                }

                source.level = 1f;
                Run(1.5f);
                Quaternion turn = jaw.transform.rotation;
                turn.ToAngleAxis(out float angle, out Vector3 axis);
                Assert.AreEqual(25f, angle, 2f, "the drive pulls the jaw to the target");
                Assert.Greater(Vector3.Dot(axis, Vector3.right), 0.98f, "about the jaw's own X axis");

                source.level = 0f;
                Run(1.5f);
                Assert.Less(Quaternion.Angle(Quaternion.identity, jaw.transform.rotation), 2f, "and closes it");

                driver.Restore();
                Assert.AreEqual(Quaternion.identity, joint.targetRotation);
            }
            finally
            {
                Physics.simulationMode = mode;
            }
        }
    }
}
