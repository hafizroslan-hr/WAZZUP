using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wardline.EditorTools
{
    /// <summary>
    /// One-click project setup and iOS build. Runs automatically the first time the project is opened.
    /// Menu: Wardline/Setup Project, Wardline/Build iOS (Xcode project).
    /// Command line: Unity -batchmode -quit -projectPath . -executeMethod Wardline.EditorTools.WardlineSetup.BuildIOS
    /// </summary>
    [InitializeOnLoad]
    public static class WardlineSetup
    {
        const string ScenePath = "Assets/Wardline/Scenes/Wardline.unity";
        const string MaterialsDir = "Assets/Wardline/Resources/WardlineMaterials";
        const string RenderingDir = "Assets/Wardline/Rendering";
        const string DoneKey = "Wardline.SetupDone.v1";

        static WardlineSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(DoneKey, false) && (!File.Exists(ScenePath) || !File.Exists(MaterialsDir + "/Lit.mat")))
                {
                    SessionState.SetBool(DoneKey, true);
                    Setup();
                }
            };
        }

        [MenuItem("Wardline/Setup Project", priority = 0)]
        public static void Setup()
        {
            EnsureRenderPipeline();
            EnsureMaterials();
            EnsureScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Wardline] Project setup complete. Press Play in the Wardline scene, or use Wardline/Build iOS.");
        }

        static void EnsureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;
            try
            {
                Directory.CreateDirectory(RenderingDir);
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RenderingDir + "/WardlineRenderer.asset");
                var urp = UniversalRenderPipelineAsset.Create(rendererData);
                urp.name = "WardlineURP";
                AssetDatabase.CreateAsset(urp, RenderingDir + "/WardlineURP.asset");
                urp.supportsHDR = false;              // mobile-friendly defaults
                urp.msaaSampleCount = 2;
                urp.renderScale = 0.85f;
                urp.shadowDistance = 40f;
                GraphicsSettings.defaultRenderPipeline = urp;
                int current = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = urp;
                }
                QualitySettings.SetQualityLevel(current, false);
                EditorUtility.SetDirty(urp);
                Debug.Log("[Wardline] Created and assigned a URP asset.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Wardline] Could not create a URP asset automatically (" + e.Message + "). " +
                                 "Create one via Assets > Create > Rendering > URP Asset (with Universal Renderer) and assign it in Project Settings > Graphics. " +
                                 "The game also runs on the Built-in pipeline as a fallback.");
            }
        }

        static void EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialsDir);
            bool urp = GraphicsSettings.defaultRenderPipeline != null;
            CreateMat("Lit", urp ? "Universal Render Pipeline/Lit" : "Standard");
            CreateMat("Unlit", urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        }

        static void CreateMat(string name, string shaderName)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[Wardline] Shader not found: " + shaderName); return; }
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { if (existing.shader != shader) { existing.shader = shader; EditorUtility.SetDirty(existing); } return; }
            var m = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (EditorSceneManager.GetActiveScene().path != ScenePath && !EditorSceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(ScenePath);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany" ? "Wardline" : PlayerSettings.companyName;
            PlayerSettings.productName = "Wardline";
            var ios = NamedBuildTarget.iOS;
            string id = PlayerSettings.GetApplicationIdentifier(ios);
            if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany")) PlayerSettings.SetApplicationIdentifier(ios, "com.yourcompany.wardline");
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Low);

            // Landscape only, full screen, defer edge swipes so iOS gestures don't steal touches mid-fight.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);   // Metal
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Enable both input backends: code prefers the Input System and falls back to the legacy manager.
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps != null && ps.Length > 0)
            {
                var so = new SerializedObject(ps[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2) { prop.intValue = 2; so.ApplyModifiedProperties(); Debug.Log("[Wardline] Active Input Handling set to Both (restart the editor if prompted)."); }
            }
        }

        [MenuItem("Wardline/Build iOS (Xcode project)", priority = 20)]
        public static void BuildIOS()
        {
            Setup();
            string outDir = Path.Combine("Builds", "iOS");
            Directory.CreateDirectory(outDir);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outDir,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            });
            Debug.Log($"[Wardline] iOS build {report.summary.result}: {report.summary.totalErrors} errors, output {Path.GetFullPath(outDir)}. Open Unity-iPhone.xcodeproj in Xcode to sign and deploy.");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("Wardline/Play From Start", priority = 40)]
        public static void PlayFromStart()
        {
            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
