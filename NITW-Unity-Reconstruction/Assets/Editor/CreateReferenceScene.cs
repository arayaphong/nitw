using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateReferenceScene
{
    private const string TitleTexture = "Assets/Reference/TitleLogo.png";
    private const string IntroScene = "Assets/ReferenceScene.unity";
    private const string TitleScene = "Assets/TitlePrototype.unity";
    private const string PoemScene = "Assets/IntroPoem.unity";
    private const string StationScene = "Assets/BusStation.unity";

    private static Sprite ImportSprite(string path, float pixelsPerUnit)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new Exception("Could not import " + path);
        return sprite;
    }

    private static void AddCamera(float size)
    {
        var cameraObject = new GameObject("Main Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -6);
        camera.orthographic = true;
        camera.orthographicSize = size;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
    }

    [MenuItem("NITW/Create reference scenes and Linux build")]
    public static void Build()
    {
        PlayerSettings.companyName = "NITWReconstruction";
        PlayerSettings.productName = "NITWPrototype";
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 960;
        PlayerSettings.defaultScreenHeight = 540;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var introCameraObject = new GameObject("Main Camera");
        introCameraObject.tag = "MainCamera";
        introCameraObject.transform.position = new Vector3(0, 0, -6);
        var introCamera = introCameraObject.AddComponent<Camera>();
        introCamera.orthographic = false;
        introCamera.fieldOfView = 60f;
        introCamera.nearClipPlane = 0.3f;
        introCamera.farClipPlane = 1000f;
        introCamera.clearFlags = CameraClearFlags.SolidColor;
        introCamera.backgroundColor = Color.black;
        introCamera.cullingMask = unchecked((int)4290765535u);
        introCameraObject.AddComponent<AudioListener>();
        introCameraObject.AddComponent<RecoveredIntroColor>();
        var fade = introCameraObject.AddComponent<RecoveredIntroFade>();
        introCameraObject.AddComponent<RecoveredIntroAA>();
        var logo = RecoveredIntroImporter.Import();
        var sequence = logo.AddComponent<StartupPreview>();
        sequence.text = logo.transform.Find("logos_animation/Text").gameObject;
        var buildNumberAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RecoveredIntro/BuildNumber.txt");
        var buildLabel = sequence.text.transform.Find("BuildNumber").GetComponent<TextMesh>();
        buildLabel.text = "Weird Autumn Edition (Steam), Build: " + buildNumberAsset.text.Split('\n')[0];
        sequence.transitioner = fade;
        var source = new GameObject("Recovered logo audio").AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        const string logoAudioPath = "Assets/Resources/RecoveredIntro/LogosAppear.wav";
        var audioImporter = (AudioImporter)AssetImporter.GetAtPath(logoAudioPath);
        audioImporter.forceToMono = false;
        audioImporter.loadInBackground = false;
        var audioSettings = audioImporter.defaultSampleSettings;
        audioSettings.loadType = AudioClipLoadType.DecompressOnLoad;
        audioSettings.compressionFormat = AudioCompressionFormat.PCM;
        audioSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        audioImporter.defaultSampleSettings = audioSettings;
        audioImporter.SaveAndReimport();
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(logoAudioPath);
        if (source.clip == null) throw new Exception("Recovered FMOD logo audio is missing.");
        sequence.logoSound = source;
        RecoveredIntroImporter.VerifySource(logo);
        if (!EditorSceneManager.SaveScene(scene, IntroScene)) throw new Exception("Could not save intro scene.");

        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AddCamera(6.5f);
        RecoveredOptionsImporter.Import();
        RecoveredTitleImporter.Import(Camera.main);
        RecoveredTitleMenuImporter.Import(Camera.main);
        if (!EditorSceneManager.SaveScene(scene, TitleScene)) throw new Exception("Could not save title scene.");
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RecoveredPoemImporter.Import();
        if (!EditorSceneManager.SaveScene(scene, PoemScene)) throw new Exception("Could not save poem scene.");
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RecoveredStationImporter.Import();
        if (!EditorSceneManager.SaveScene(scene, StationScene)) throw new Exception("Could not save station scene.");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(IntroScene, true), new EditorBuildSettingsScene(TitleScene, true), new EditorBuildSettingsScene(PoemScene, true), new EditorBuildSettingsScene(StationScene, true) };
        string output = Path.GetFullPath("Builds/Linux/NITWReference.x86_64");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { IntroScene, TitleScene, PoemScene, StationScene }, locationPathName = output,
            target = BuildTarget.StandaloneLinux64, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Linux build failed: " + report.summary.result);
        Debug.Log("NITW_REFERENCE_BUILD_SUCCESS " + output);
    }
}
