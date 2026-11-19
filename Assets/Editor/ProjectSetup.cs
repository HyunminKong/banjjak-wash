using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Banjjak.EditorTools
{
    /// <summary>
    /// One-shot project configuration: URP asset + renderer (with SSAO), linear colour,
    /// input handling, layers, shader bank and the main scene. Safe to run repeatedly.
    /// Batchmode: Unity -batchmode -quit -projectPath . -executeMethod Banjjak.EditorTools.ProjectSetup.Setup
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string UrpPath = SettingsDir + "/BanjjakURP.asset";
        const string RendererPath = SettingsDir + "/BanjjakURP_Renderer.asset";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("반짝 워시/프로젝트 설정 다시 적용")]
        public static void Setup()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rd == null)
            {
                var create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
                rd = (UniversalRendererData)create.Invoke(null, new object[] { UrpPath, RendererType.UniversalRenderer, true, "Renderer" });
            }
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (rp == null)
            {
                rp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(rp, UrpPath);
            }

            if (!rd.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion))
            {
                var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ao, rd);
                rd.rendererFeatures.Add(ao);
                typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(rd, null);
            }
            foreach (var f in rd.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>())
            {
                var fo = new SerializedObject(f);
                SetProp(fo, "m_Settings.Intensity", 1.4f);
                SetProp(fo, "m_Settings.Radius", 0.3f);
                SetProp(fo, "m_Settings.DirectLightingStrength", 0.2f);
                fo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(f);
            }
            EditorUtility.SetDirty(rd);

            var so = new SerializedObject(rp);
            SetProp(so, "m_MSAA", 4);
            SetProp(so, "m_SupportsHDR", true);
            SetProp(so, "m_ShadowDistance", 45f);
            SetProp(so, "m_ShadowCascadeCount", 4);
            SetProp(so, "m_MainLightShadowmapResolution", 4096);
            SetProp(so, "m_SoftShadowsSupported", true);
            SetProp(so, "m_SoftShadowQuality", 3);
            SetProp(so, "m_AdditionalLightsRenderingMode", 1);
            SetProp(so, "m_AdditionalLightShadowsSupported", true);
            SetProp(so, "m_ColorGradingMode", 1);
            SetProp(so, "m_RequireDepthTexture", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);

            GraphicsSettings.defaultRenderPipeline = rp;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = rp;
            }
            QualitySettings.SetQualityLevel(cur, false);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "Banjjak";
            PlayerSettings.productName = "Banjjak Wash";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var psAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (psAsset != null)
            {
                var pso = new SerializedObject(psAsset);
                var p = pso.FindProperty("activeInputHandler");
                if (p != null) { p.intValue = 2; pso.ApplyModifiedPropertiesWithoutUndo(); }
            }

            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            layers.GetArrayElementAtIndex(Layers.Rig).stringValue = "Rig";
            layers.GetArrayElementAtIndex(Layers.SprayOnly).stringValue = "SprayOnly";
            layers.GetArrayElementAtIndex(Layers.Player).stringValue = "Player";
            layers.GetArrayElementAtIndex(Layers.PlayerOnly).stringValue = "PlayerOnly";
            tm.ApplyModifiedPropertiesWithoutUndo();

            var bank = AssetDatabase.LoadAssetAtPath<ShaderBank>("Assets/Resources/GameShaders.asset");
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ShaderBank>();
                AssetDatabase.CreateAsset(bank, "Assets/Resources/GameShaders.asset");
            }
            bank.dirtLit = Shader.Find("Banjjak/DirtLit");
            bank.foliage = Shader.Find("Banjjak/Foliage");
            bank.particles = Shader.Find("Banjjak/Particle");
            bank.skybox = Shader.Find("Skybox/Procedural");
            bank.unlit = Shader.Find("Universal Render Pipeline/Unlit");
            EditorUtility.SetDirty(bank);
            if (bank.dirtLit == null || bank.foliage == null) Debug.LogError("[Banjjak] custom shaders missing");

            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Game");
                go.AddComponent<GameManager>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            Debug.Log("[Banjjak] setup done: " + rp.name + " / features " + rd.rendererFeatures.Count);
        }

        static void SetProp(SerializedObject so, string path, object v)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning("[Banjjak] missing property " + path); return; }
            switch (v)
            {
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case bool b: p.boolValue = b; break;
            }
        }
    }
}
