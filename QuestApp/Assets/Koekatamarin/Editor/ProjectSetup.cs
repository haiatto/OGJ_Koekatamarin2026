using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Koekatamarin.Editor
{
    public static class ProjectSetup
    {
        const string Generated = "Assets/Koekatamarin/Generated";
        const string ScenePath = Generated + "/Playground.unity";

        public static void ImportTextResources()
        {
            if (Directory.Exists("Assets/TextMesh Pro/Resources")) { EditorApplication.Exit(0); return; }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            var file = Directory.GetFiles(package.resolvedPath, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (file == null) throw new FileNotFoundException("TMP Essential Resources package missing", package.resolvedPath);
            AssetDatabase.importPackageCompleted += _ => { AssetDatabase.SaveAssets(); EditorApplication.Exit(0); };
            AssetDatabase.importPackageFailed += (_, reason) => { Debug.LogError(reason); EditorApplication.Exit(1); };
            Debug.Log("Importing TMP resources: " + file);
            UnityEditor.AssetPackage.Package.Import(file, false);
        }

        [MenuItem("Koekatamarin/Generate Quest playground")]
        public static void Generate()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            PlayerSettings.companyName = "haiatto";
            PlayerSettings.productName = "Koekatamarin 2026";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.haiatto.koekatamarin2026");
            PlayerSettings.bundleVersion = "0.5.0";
            PlayerSettings.Android.bundleVersionCode = 5;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            // New Input System, required by OpenXR. No legacy Input calls in the app.
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            playerSettings.FindProperty("activeInputHandler").intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();

            var config = OVRProjectConfig.CachedProjectConfig;
            config.targetDeviceTypes = new System.Collections.Generic.List<OVRProjectConfig.DeviceType> { OVRProjectConfig.DeviceType.Quest3 };
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
            config.boundaryVisibilitySupport = OVRProjectConfig.FeatureSupport.Supported;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersOnly;
            OVRProjectConfig.CommitProjectConfig(config);
            ConfigureXR();
            ConfigureRendering();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Generated + "/Japanese.asset");
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Koekatamarin/Resources/NotoSansCJKjp-Regular.otf");
                font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "Noto Sans CJK JP Dynamic";
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                AssetDatabase.CreateAsset(font, Generated + "/Japanese.asset");
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            }
            var panel = Material("Panel", new Color(.025f, .10f, .14f));
            var beam = Material("Beam", new Color(.15f, .95f, .8f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            if (prefab == null) throw new InvalidOperationException("Meta XR camera prefab missing");
            var rigObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rig = rigObject.GetComponent<OVRCameraRig>();
            var manager = rigObject.GetComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
            manager.isInsightPassthroughEnabled = true;
            manager.shouldBoundaryVisibilityBeSuppressed = false;
            rigObject.AddComponent<OVRPassthroughLayer>(); // Underlay by default.
            var app = new GameObject("Koekatamarin").AddComponent<Experience>();
            app.rig = rig; app.font = font; app.panelMaterial = panel; app.beamMaterial = beam;
            app.environment = LegacyEnvironmentSetup.Create();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KOEKATAMARIN_SETUP_OK");
        }

        static Material Material(string name, Color color)
        {
            string path = Generated + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            return material;
        }

        static void ConfigureRendering()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/QuestURP.asset");
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Generated + "/QuestRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.msaaSampleCount = 4;
                pipeline.supportsHDR = false;
                pipeline.renderScale = 1;
                AssetDatabase.CreateAsset(pipeline, Generated + "/QuestURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
        }

        static void ConfigureXR()
        {
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Generated + "/XRSettings.asset");
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, Generated + "/XRSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            }
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                general.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.AddObjectToAsset(general, perTarget);
                AssetDatabase.AddObjectToAsset(general.Manager, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            }
            general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                throw new InvalidOperationException("OpenXR loader could not be assigned");
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            foreach (var feature in xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
            {
                if (feature.GetType().Name == "MetaQuestFeature" || feature.GetType().Name == "MetaXRFeature" ||
                    feature.GetType().Name == "OculusTouchControllerProfile")
                    feature.enabled = true;
                if (feature is UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature quest)
                    quest.ForceRemoveInternetPermission = false;
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(perTarget);
            EditorUtility.SetDirty(xr);
        }

        [MenuItem("Koekatamarin/Build Quest APK")]
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = "Builds/Koekatamarin2026.apk", target = BuildTarget.Android,
                options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("APK build failed: " + report.summary.result);
            Debug.Log("KOEKATAMARIN_BUILD_OK " + report.summary.totalSize);
        }
    }
}
