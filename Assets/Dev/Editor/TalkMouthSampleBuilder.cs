using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wobblefolk.TalkMouth;
using Object = UnityEngine.Object;

/// <summary>
/// Development only, not part of the package: builds the sample scenes, materials, meshes and profiles from
/// primitives. Copy a sample's Scripts from Samples~ to Assets/TalkMouthSamples/&lt;Sample&gt;, run
/// <c>-executeMethod TalkMouthSampleBuilder.BuildAll</c> in batch mode, then copy the folder back into Samples~.
/// Sample components are looked up by name, so this compiles without the samples present.
/// </summary>
static class TalkMouthSampleBuilder
{
    const string Root = "Assets/TalkMouthSamples";

    /// <summary>Builds every sample whose Scripts folder was copied to <see cref="Root"/> (the others keep their assets).</summary>
    public static void BuildAll()
    {
        Build("Humanoid", BuildHumanoid);
        Build("SplitHead", BuildSplitHead);
        Build("Monster", BuildMonster);
        Build("Beak", BuildBeak);
        AssetDatabase.SaveAssets();
        Debug.Log("Talk Mouth samples built.");
    }

    static void Build(string sample, Action build)
    {
        if (!Directory.Exists(Root + "/" + sample + "/Scripts")) return;
        build();
        Debug.Log("Built sample " + sample);
    }

    static void BuildMonster()
    {
        string dir = Root + "/Monster";
        Scene scene = NewStage(dir, new Vector3(1.0f, 1.05f, 2.1f), new Vector3(0f, 0.7f, 0f));

        Material skin = Mat(dir, "Skin", new Color(0.55f, 0.35f, 0.78f));
        Material jawSkin = Mat(dir, "JawSkin", new Color(0.48f, 0.3f, 0.7f));
        Material inside = Mat(dir, "Inside", new Color(0.3f, 0.04f, 0.1f), 0.1f);
        Material tongue = Mat(dir, "Tongue", new Color(0.95f, 0.4f, 0.5f), 0.5f);
        Material tooth = Mat(dir, "Tooth", new Color(0.98f, 0.96f, 0.86f), 0.6f);
        Material horn = Mat(dir, "Mandible", new Color(0.9f, 0.85f, 0.7f), 0.4f);
        Material eyeWhite = Mat(dir, "EyeWhite", new Color(0.97f, 0.97f, 0.95f), 0.6f);
        Material pupil = Mat(dir, "Pupil", new Color(0.05f, 0.05f, 0.07f), 0.8f);
        Floor(dir);
        TalkMouthProfile profile = Profile(dir, "MonsterVoice", TalkMouthPreset.Monster);

        var root = new GameObject("Monster");
        Prim(PrimitiveType.Sphere, "Body", root.transform, new Vector3(0f, 0.75f, 0f), Vector3.one * 1.1f, skin);
        Prim(PrimitiveType.Sphere, "Foot L", root.transform, new Vector3(-0.22f, 0.12f, 0.05f), new Vector3(0.25f, 0.14f, 0.32f), jawSkin);
        Prim(PrimitiveType.Sphere, "Foot R", root.transform, new Vector3(0.22f, 0.12f, 0.05f), new Vector3(0.25f, 0.14f, 0.32f), jawSkin);
        Eye(root.transform, new Vector3(-0.2f, 1.12f, 0.33f), 0.18f, eyeWhite, pupil);
        Eye(root.transform, new Vector3(0.2f, 1.12f, 0.33f), 0.18f, eyeWhite, pupil);
        Eye(root.transform, new Vector3(0f, 1.25f, 0.26f), 0.22f, eyeWhite, pupil).name = "Eye Middle";
        Prim(PrimitiveType.Sphere, "Mouth Inside", root.transform, new Vector3(0f, 0.62f, 0.38f), new Vector3(0.55f, 0.2f, 0.35f), inside);

        Transform upperPivot = Empty("Upper Jaw", root.transform, new Vector3(0f, 0.68f, 0.1f));
        Prim(PrimitiveType.Sphere, "Upper Lip", upperPivot, new Vector3(0f, 0.03f, 0.3f), new Vector3(0.6f, 0.16f, 0.45f), skin);
        Teeth(upperPivot, -0.035f, 0.47f, tooth, true);

        Transform lowerPivot = Empty("Lower Jaw", root.transform, new Vector3(0f, 0.6f, 0.1f));
        Prim(PrimitiveType.Sphere, "Chin", lowerPivot, new Vector3(0f, -0.06f, 0.3f), new Vector3(0.62f, 0.2f, 0.5f), jawSkin);
        Teeth(lowerPivot, 0.03f, 0.5f, tooth, false);
        Transform tonguePivot = Empty("Tongue", lowerPivot, new Vector3(0f, 0f, 0.15f));
        Prim(PrimitiveType.Capsule, "Tongue Tip", tonguePivot, new Vector3(0f, 0.02f, 0.12f), new Vector3(0.16f, 0.18f, 0.06f), tongue, new Vector3(90f, 0f, 0f));

        Transform mandibleL = Empty("Mandible L", root.transform, new Vector3(-0.3f, 0.58f, 0.38f));
        Prim(PrimitiveType.Capsule, "Horn", mandibleL, new Vector3(0f, 0f, 0.12f), new Vector3(0.07f, 0.13f, 0.07f), horn, new Vector3(90f, 0f, 0f));
        Transform mandibleR = Empty("Mandible R", root.transform, new Vector3(0.3f, 0.58f, 0.38f));
        Prim(PrimitiveType.Capsule, "Horn", mandibleR, new Vector3(0f, 0f, 0.12f), new Vector3(0.07f, 0.13f, 0.07f), horn, new Vector3(90f, 0f, 0f));

        AudioSource audio = VoiceAudio(root);
        var clipSignal = root.AddComponent<AudioSourceSignal>();
        var mic = root.AddComponent<MicrophoneSignal>();
        mic.enabled = false;
        TalkVoice voice = Voice(root, profile, clipSignal);

        var jaws = root.AddComponent<MultiBoneMouthDriver>();
        jaws.Voice = voice;
        jaws.AddBone(lowerPivot, Vector3.right, 32f);
        jaws.AddBone(upperPivot, Vector3.right, -12f);
        jaws.AddBone(mandibleL, Vector3.up, -35f, 0.03f);
        jaws.AddBone(mandibleR, Vector3.up, 35f, 0.03f);
        MouthBone tip = jaws.AddBone(tonguePivot, Vector3.right, -12f, 0.08f);
        tip.openOffset = new Vector3(0f, 0.02f, 0.3f);
        tip.curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.55f, 0f), new Keyframe(1f, 1f));

        SampleControls(root, "Wobblefolk.TalkMouth.Samples.MonsterTalkSample", voice, audio, clipSignal, mic);
        Save(scene, dir + "/Monster.unity");
    }

    static void Teeth(Transform jaw, float y, float z, Material material, bool pointDown)
    {
        float[] xs = { -0.2f, -0.08f, 0.08f, 0.2f };
        foreach (float x in xs)
        {
            float edge = Mathf.Abs(x) > 0.15f ? -0.06f : 0f;
            Prim(PrimitiveType.Cube, "Tooth", jaw, new Vector3(x, y, z + edge), new Vector3(0.05f, 0.07f, 0.05f), material,
                new Vector3(0f, 45f, pointDown ? 180f : 0f));
        }
    }

    static void BuildBeak()
    {
        string dir = Root + "/Beak";
        Scene scene = NewStage(dir, new Vector3(0.9f, 1.05f, 1.6f), new Vector3(0f, 0.8f, 0.1f));

        Material feathers = Mat(dir, "Feathers", new Color(1f, 0.82f, 0.25f));
        Material wing = Mat(dir, "Wing", new Color(0.95f, 0.7f, 0.2f));
        Material beak = Mat(dir, "Beak", new Color(1f, 0.5f, 0.12f), 0.5f);
        Material crest = Mat(dir, "Crest", new Color(0.9f, 0.3f, 0.25f));
        Material eyeWhite = Mat(dir, "EyeWhite", new Color(0.97f, 0.97f, 0.95f), 0.6f);
        Material pupil = Mat(dir, "Pupil", new Color(0.05f, 0.05f, 0.07f), 0.8f);
        Floor(dir);
        TalkMouthProfile profile = Profile(dir, "BirdVoice", TalkMouthPreset.Bird);

        var root = new GameObject("Bird");
        Prim(PrimitiveType.Sphere, "Body", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.7f), feathers);
        Prim(PrimitiveType.Cylinder, "Leg L", root.transform, new Vector3(-0.1f, 0.15f, 0f), new Vector3(0.04f, 0.15f, 0.04f), beak);
        Prim(PrimitiveType.Cylinder, "Leg R", root.transform, new Vector3(0.1f, 0.15f, 0f), new Vector3(0.04f, 0.15f, 0.04f), beak);
        Transform head = Empty("Head", root.transform, new Vector3(0f, 1f, 0.12f));
        Prim(PrimitiveType.Sphere, "Skull", head, Vector3.zero, Vector3.one * 0.42f, feathers);
        Eye(head, new Vector3(-0.1f, 0.05f, 0.17f), 0.09f, eyeWhite, pupil);
        Eye(head, new Vector3(0.1f, 0.05f, 0.17f), 0.09f, eyeWhite, pupil);

        Transform upper = Empty("Upper Beak", head, new Vector3(0f, -0.01f, 0.18f));
        Prim(PrimitiveType.Capsule, "Upper", upper, new Vector3(0f, 0f, 0.07f), new Vector3(0.11f, 0.09f, 0.06f), beak, new Vector3(90f, 0f, 0f));
        Transform lower = Empty("Lower Beak", head, new Vector3(0f, -0.035f, 0.18f));
        Prim(PrimitiveType.Capsule, "Lower", lower, new Vector3(0f, -0.005f, 0.06f), new Vector3(0.09f, 0.075f, 0.045f), beak, new Vector3(90f, 0f, 0f));

        var crestFeathers = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            crestFeathers[i] = Empty("Crest " + (i + 1), head, new Vector3(0f, 0.2f - i * 0.01f, 0.06f - i * 0.06f));
            Prim(PrimitiveType.Capsule, "Feather", crestFeathers[i], new Vector3(0f, 0.06f, 0f), new Vector3(0.03f, 0.07f, 0.03f), crest, new Vector3(-15f, 0f, 0f));
        }

        Transform wingL = Empty("Wing L", root.transform, new Vector3(-0.28f, 0.75f, 0f));
        Prim(PrimitiveType.Sphere, "Wing", wingL, new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.32f, 0.45f), wing);
        Transform wingR = Empty("Wing R", root.transform, new Vector3(0.28f, 0.75f, 0f));
        Prim(PrimitiveType.Sphere, "Wing", wingR, new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.32f, 0.45f), wing);

        AudioSource audio = VoiceAudio(root);
        var clipSignal = root.AddComponent<AudioSourceSignal>();
        var mic = root.AddComponent<MicrophoneSignal>();
        mic.enabled = false;
        TalkVoice voice = Voice(root, profile, clipSignal);

        var bones = root.AddComponent<MultiBoneMouthDriver>();
        bones.Voice = voice;
        bones.AddBone(upper, Vector3.right, -10f);
        bones.AddBone(lower, Vector3.right, 30f);
        for (int i = 0; i < 3; i++) bones.AddBone(crestFeathers[i], Vector3.right, 25f, 0.05f * i);
        var flap = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0f), new Keyframe(1f, 1f));
        bones.AddBone(wingL, Vector3.forward, -25f, 0.04f).curve = flap;
        bones.AddBone(wingR, Vector3.forward, 25f, 0.04f).curve = new AnimationCurve(flap.keys);

        SampleControls(root, "Wobblefolk.TalkMouth.Samples.BeakTalkSample", voice, audio, clipSignal, mic);
        Save(scene, dir + "/Beak.unity");
    }

    static void SampleControls(GameObject root, string typeName, TalkVoice voice, AudioSource audio, AudioSourceSignal clip, MicrophoneSignal mic)
    {
        Component sample = AddByName(root, typeName);
        SetPublic(sample, "voice", voice);
        SetPublic(sample, "voiceAudio", audio);
        SetPublic(sample, "clipSignal", clip);
        SetPublic(sample, "microphoneSignal", mic);
    }

    static void BuildHumanoid()
    {
        string dir = Root + "/Humanoid";
        Scene scene = NewStage(dir, new Vector3(0f, 1.45f, 1.7f), new Vector3(0f, 1.35f, 0f));

        Material skin = Mat(dir, "Skin", new Color(0.96f, 0.76f, 0.62f));
        Material chin = Mat(dir, "Chin", new Color(0.9f, 0.68f, 0.55f));
        Material eyeWhite = Mat(dir, "EyeWhite", new Color(0.97f, 0.97f, 0.95f), 0.6f);
        Material pupil = Mat(dir, "Pupil", new Color(0.05f, 0.05f, 0.07f), 0.8f);
        Material mouth = Mat(dir, "Mouth", new Color(0.35f, 0.05f, 0.08f));
        Material shirt = Mat(dir, "Shirt", new Color(0.25f, 0.45f, 0.8f));
        Floor(dir);

        TalkMouthProfile profile = Profile(dir, "HumanVoice", TalkMouthPreset.Human);

        var root = new GameObject("Talking Humanoid");
        Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.55f, 0.55f, 0.4f), shirt);
        Transform head = Empty("Head", root.transform, new Vector3(0f, 1.6f, 0f));
        Prim(PrimitiveType.Sphere, "Skull", head, Vector3.zero, Vector3.one * 0.5f, skin);
        Eye(head, new Vector3(-0.085f, 0.04f, 0.2f), 0.09f, eyeWhite, pupil);
        Eye(head, new Vector3(0.085f, 0.04f, 0.2f), 0.09f, eyeWhite, pupil);
        Prim(PrimitiveType.Sphere, "Nose", head, new Vector3(0f, -0.015f, 0.25f), new Vector3(0.06f, 0.07f, 0.07f), chin);
        GameObject lips = Prim(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, -0.1f, 0.215f), new Vector3(0.12f, 0.025f, 0.06f), mouth);
        Transform jawPivot = Empty("Jaw Pivot", head, new Vector3(0f, -0.06f, -0.04f));
        Prim(PrimitiveType.Capsule, "Chin", jawPivot, new Vector3(0f, -0.105f, 0.185f), new Vector3(0.1f, 0.14f, 0.1f), chin, new Vector3(0f, 0f, 90f));

        AudioSource audio = VoiceAudio(root);
        var clipSignal = root.AddComponent<AudioSourceSignal>();
        var mic = root.AddComponent<MicrophoneSignal>();
        mic.enabled = false;
        var text = root.AddComponent<TextDrivenSignal>();
        var babble = new GameObject("Babble").AddComponent<AudioSource>();
        babble.transform.SetParent(head, false);
        babble.playOnAwake = false;
        SetPrivate(text, "babbleOutput", babble);
        TalkVoice voice = Voice(root, profile, clipSignal);

        var jaw = jawPivot.gameObject.AddComponent<JawBoneMouthDriver>();
        jaw.Voice = voice;
        jaw.Axis = Vector3.right;
        jaw.OpenAngle = 14f;

        var lipDriver = lips.AddComponent<TransformMouthDriver>();
        lipDriver.Voice = voice;
        lipDriver.OpenScale = new Vector3(1f, 3.2f, 1f);
        lipDriver.OpenPosition = new Vector3(0f, -0.012f, 0f);
        lipDriver.UseShapes = true;

        Component sample = AddByName(root, "Wobblefolk.TalkMouth.Samples.HumanoidTalkSample");
        SetPublic(sample, "voice", voice);
        SetPublic(sample, "voiceAudio", audio);
        SetPublic(sample, "clipSignal", clipSignal);
        SetPublic(sample, "microphoneSignal", mic);
        SetPublic(sample, "textSignal", text);

        Save(scene, dir + "/Humanoid.unity");
    }

    static void BuildSplitHead()
    {
        string dir = Root + "/SplitHead";
        Scene scene = NewStage(dir, new Vector3(0.6f, 1.6f, 1.7f), new Vector3(0f, 1.45f, 0f));

        Material skin = Mat(dir, "Skin", new Color(0.45f, 0.78f, 0.4f));
        Material inside = Mat(dir, "Inside", new Color(0.45f, 0.08f, 0.12f), 0.1f);
        Material tongue = Mat(dir, "Tongue", new Color(0.92f, 0.42f, 0.48f), 0.5f);
        Material eyeWhite = Mat(dir, "EyeWhite", new Color(0.97f, 0.97f, 0.95f), 0.6f);
        Material pupil = Mat(dir, "Pupil", new Color(0.05f, 0.05f, 0.07f), 0.8f);
        Material hat = Mat(dir, "Hat", new Color(0.15f, 0.15f, 0.2f));
        Material band = Mat(dir, "HatBand", new Color(0.85f, 0.25f, 0.2f));
        Material shirt = Mat(dir, "Shirt", new Color(0.95f, 0.75f, 0.25f));
        Floor(dir);

        const float radius = 0.3f;
        Mesh topMesh = SaveMesh(dir, Hemisphere("TopHalf", radius, true));
        Mesh bottomMesh = SaveMesh(dir, Hemisphere("BottomHalf", radius, false));
        TalkMouthProfile profile = Profile(dir, "BouncySplitHead", TalkMouthPreset.BouncySplitHead);

        var root = new GameObject("Split Head Talker");
        Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 0.55f, 0.4f), shirt);
        Transform head = Empty("Head", root.transform, new Vector3(0f, 1.62f, 0f));
        Transform hinge = Empty("Hinge", head, new Vector3(0f, 0f, -radius * 0.97f));

        Transform top = MeshObject("Top Half", head, topMesh, skin, inside);
        Eye(top, new Vector3(-0.11f, 0.13f, 0.2f), 0.17f, eyeWhite, pupil);
        Eye(top, new Vector3(0.11f, 0.13f, 0.2f), 0.17f, eyeWhite, pupil);

        Transform bottom = MeshObject("Bottom Half", head, bottomMesh, skin, inside);
        Prim(PrimitiveType.Sphere, "Tongue", bottom, new Vector3(0f, 0f, 0.06f), new Vector3(0.3f, 0.05f, 0.3f), tongue);

        Transform topHat = Empty("Hat", head, new Vector3(0f, radius - 0.02f, -0.02f));
        Prim(PrimitiveType.Cylinder, "Brim", topHat, Vector3.zero, new Vector3(0.34f, 0.01f, 0.34f), hat);
        Prim(PrimitiveType.Cylinder, "Crown", topHat, new Vector3(0f, 0.08f, 0f), new Vector3(0.2f, 0.08f, 0.2f), hat);
        Prim(PrimitiveType.Cylinder, "Band", topHat, new Vector3(0f, 0.025f, 0f), new Vector3(0.205f, 0.02f, 0.205f), band);

        AudioSource audio = VoiceAudio(root);
        var clipSignal = root.AddComponent<AudioSourceSignal>();
        var mic = root.AddComponent<MicrophoneSignal>();
        mic.enabled = false;
        TalkVoice voice = Voice(root, profile, clipSignal);

        var split = head.gameObject.AddComponent<HingeSplitMouthDriver>();
        split.Voice = voice;
        split.Hinge = hinge;
        split.Axis = Vector3.left;
        split.MaxOpenAngle = 45f;
        split.AddPart(top);
        split.AddPart(topHat, 1.08f, false, 0.06f);
        split.AddPart(bottom, 0.15f, true);
        split.SetJitter(true, 0.75f, 3f, 14f);

        Component sample = AddByName(root, "Wobblefolk.TalkMouth.Samples.SplitHeadTalkSample");
        SetPublic(sample, "voice", voice);
        SetPublic(sample, "splitHead", split);
        SetPublic(sample, "voiceAudio", audio);
        SetPublic(sample, "clipSignal", clipSignal);
        SetPublic(sample, "microphoneSignal", mic);

        Save(scene, dir + "/SplitHead.unity");
    }

    // ---------------------------------------------------------------- scene helpers

    static Scene NewStage(string dir, Vector3 cameraPosition, Vector3 lookAt)
    {
        Directory.CreateDirectory(dir);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.TryGetComponent(out Camera cam))
            {
                go.transform.position = cameraPosition;
                go.transform.LookAt(lookAt);
                cam.fieldOfView = 40f;
                cam.nearClipPlane = 0.05f;
            }
            else if (go.TryGetComponent(out Light light))
            {
                go.transform.rotation = Quaternion.Euler(40f, 160f, 0f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
            }
        }

        return scene;
    }

    static void Floor(string dir)
    {
        Material floor = Mat(dir, "Floor", new Color(0.55f, 0.57f, 0.6f), 0.1f);
        GameObject plane = Prim(PrimitiveType.Plane, "Floor", null, Vector3.zero, Vector3.one * 3f, floor);
        plane.isStatic = true;
    }

    static AudioSource VoiceAudio(GameObject root)
    {
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = true;
        audio.spatialBlend = 0f;
        return audio;
    }

    static TalkVoice Voice(GameObject root, TalkMouthProfile profile, VoiceSignalSource source)
    {
        var voice = root.AddComponent<TalkVoice>();
        voice.Profile = profile;
        voice.SetSource(source);
        return voice;
    }

    static GameObject Eye(Transform parent, Vector3 position, float size, Material white, Material pupil)
    {
        GameObject eye = Prim(PrimitiveType.Sphere, position.x < 0f ? "Eye L" : "Eye R", parent, position, Vector3.one * size, white);
        Prim(PrimitiveType.Sphere, "Pupil", eye.transform, new Vector3(0f, 0.02f, 0.38f), Vector3.one * 0.45f, pupil);
        return eye;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static Transform Empty(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        return go.transform;
    }

    static Transform MeshObject(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return go.transform;
    }

    static void Save(Scene scene, string path)
    {
        if (!EditorSceneManager.SaveScene(scene, path)) throw new Exception("Could not save " + path);
    }

    // ---------------------------------------------------------------- assets

    static Material Mat(string dir, string name, Color color, float smoothness = 0.3f)
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        Shader shader = pipeline != null && pipeline.defaultMaterial != null ? pipeline.defaultMaterial.shader : Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        return Create(material, dir + "/Materials/" + name + ".mat");
    }

    static TalkMouthProfile Profile(string dir, string name, TalkMouthPreset preset)
    {
        TalkMouthProfile profile = TalkMouthPresets.Create(preset);
        profile.name = name;
        return Create(profile, dir + "/Profiles/" + name + ".asset");
    }

    static Mesh SaveMesh(string dir, Mesh mesh) => Create(mesh, dir + "/Meshes/" + mesh.name + ".asset");

    static T Create<T>(T asset, string path) where T : Object
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>
    /// Half a sphere with a flat lid, as two sub-meshes: the dome (0) and the lid (1), so the inside of the mouth
    /// can have its own material. The lid faces the cut (down for the top half, up for the bottom half).
    /// </summary>
    static Mesh Hemisphere(string name, float radius, bool upper, int segments = 40, int rings = 12)
    {
        float sign = upper ? 1f : -1f;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var dome = new List<int>();
        var lid = new List<int>();

        for (int i = 0; i <= rings; i++)
        {
            float phi = 0.5f * Mathf.PI * i / rings;
            float y = sign * radius * Mathf.Sin(phi);
            float ring = radius * Mathf.Cos(phi);
            for (int j = 0; j <= segments; j++)
            {
                float theta = 2f * Mathf.PI * j / segments;
                var v = new Vector3(ring * Mathf.Sin(theta), y, ring * Mathf.Cos(theta));
                vertices.Add(v);
                normals.Add(v / radius);
                uvs.Add(new Vector2((float)j / segments, 0.5f + 0.5f * sign * i / rings));
            }
        }

        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = i * (segments + 1) + j;
                int b = a + segments + 1;
                Triangle(dome, vertices, a, b, a + 1, normals[a]);
                Triangle(dome, vertices, a + 1, b, b + 1, normals[a + 1]);
            }
        }

        var lidNormal = new Vector3(0f, -sign, 0f);
        int center = vertices.Count;
        vertices.Add(Vector3.zero);
        normals.Add(lidNormal);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int j = 0; j <= segments; j++)
        {
            float theta = 2f * Mathf.PI * j / segments;
            vertices.Add(new Vector3(radius * Mathf.Sin(theta), 0f, radius * Mathf.Cos(theta)));
            normals.Add(lidNormal);
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Sin(theta), 0.5f + 0.5f * Mathf.Cos(theta)));
        }

        for (int j = 0; j < segments; j++) Triangle(lid, vertices, center, center + 1 + j, center + 2 + j, lidNormal);

        var mesh = new Mesh { name = name, subMeshCount = 2 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(dome, 0);
        mesh.SetTriangles(lid, 1);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>Adds a triangle wound so its front faces <paramref name="outward"/>.</summary>
    static void Triangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 outward)
    {
        Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (face.sqrMagnitude < 1e-12f)
        {
            // Degenerate at the pole: keep the winding of its neighbours.
            face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[b]);
        }

        triangles.Add(a);
        if (Vector3.Dot(face, outward) >= 0f)
        {
            triangles.Add(b);
            triangles.Add(c);
        }
        else
        {
            triangles.Add(c);
            triangles.Add(b);
        }
    }

    // ---------------------------------------------------------------- sample components by name

    static Component AddByName(GameObject go, string typeName)
    {
        foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
        {
            if (type.FullName == typeName) return go.AddComponent(type);
        }

        throw new Exception($"{typeName} not found. Copy the sample scripts to {Root} first.");
    }

    static void SetPublic(Component component, string field, Object value)
    {
        var so = new SerializedObject(component);
        SerializedProperty property = so.FindProperty(field) ?? throw new Exception($"{component.GetType().Name}.{field} not found");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrivate(Component component, string field, Object value) => SetPublic(component, field, value);
}
