using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Banjjak
{
    /// <summary>
    /// Player graphics options (Esc → 설정): a quality preset, render resolution, a frame-rate cap and an FPS readout.
    /// Kept per device in PlayerPrefs, not in the cloud save, since what runs well depends on the machine.
    /// </summary>
    public static class GfxSettings
    {
        public static readonly string[] QualityNames = { "낮음", "보통", "높음" };
        public static readonly int[] Scales = { 50, 70, 85, 100 };
        public static readonly int[] FpsCaps = { 30, 60, 0 };          // 0 = no cap (follow the screen)
        static readonly int[] PresetScale = { 70, 85, 100 };

        public static int Quality = 2;   // 0 low, 1 medium, 2 high
        public static int Scale = 100;   // render resolution, percent
        public static int Fps;           // frame cap, 0 = none
        public static bool ShowFps;

        /// <summary>Set by GameManager once the post-processing volume exists.</summary>
        public static Bloom Bloom;

        const string Key = "banjjak-gfx";
        static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;

        public static void Load()
        {
            Defaults();
            var s = PlayerPrefs.GetString(Key, "");
            var p = s.Split(',');
            if (p.Length == 4 && int.TryParse(p[0], out int q) && int.TryParse(p[1], out int sc) && int.TryParse(p[2], out int f))
            {
                Quality = Mathf.Clamp(q, 0, 2);
                Scale = System.Array.IndexOf(Scales, sc) >= 0 ? sc : PresetScale[Quality];
                Fps = System.Array.IndexOf(FpsCaps, f) >= 0 ? f : 0;
                ShowFps = p[3] == "1";
            }
        }

        /// <summary>Browsers start on medium (they share the GPU with everything else); the desktop build on high.</summary>
        public static void Defaults()
        {
            Quality = Web ? 1 : 2;
            Scale = PresetScale[Quality];
            Fps = 0;
            ShowFps = false;
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, $"{Quality},{Scale},{Fps},{(ShowFps ? 1 : 0)}");
            PlayerPrefs.Save();
        }

        /// <summary>Picking a preset also picks its resolution; the resolution row can still override it.</summary>
        public static void SetQuality(int q) { Quality = Mathf.Clamp(q, 0, 2); Scale = PresetScale[Quality]; Apply(); Save(); }
        public static void SetScale(int s) { Scale = s; Apply(); Save(); }
        public static void SetFps(int f) { Fps = f; Apply(); Save(); }
        public static void SetShowFps(bool on) { ShowFps = on; Save(); }

        public static void Apply()
        {
            // low: no MSAA, short hard shadows, no SSAO or bloom. medium: light versions. high: everything (as shipped).
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Scale / 100f;
                urp.msaaSampleCount = Quality == 0 ? 1 : Quality == 1 ? 2 : 4;
                urp.shadowDistance = Quality == 0 ? 20 : Quality == 1 ? 32 : 45;
                urp.shadowCascadeCount = Quality == 0 ? 1 : Quality == 1 ? 2 : 4;
                SetShadowmap(urp, Quality == 0 ? 1024 : Quality == 1 ? 2048 : 4096);
            }
            foreach (var f in Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>())
                if (f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(Quality == 2);
            if (Bloom != null) Bloom.active = Quality > 0;
            if (RenderSettings.sun != null) RenderSettings.sun.shadows = Quality == 0 ? LightShadows.Hard : LightShadows.Soft;

            if (Fps > 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = Fps; }
            else
            {
                // no cap: browsers pace frames to the screen themselves; the desktop build waits for v-sync
                if (!Web) QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }
        }

        /// <summary>The URP asset only exposes the shadow map size to the editor, so set the serialized field directly.</summary>
        static void SetShadowmap(UniversalRenderPipelineAsset urp, int size)
        {
            try
            {
                var fi = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fi != null) fi.SetValue(urp, System.Enum.ToObject(fi.FieldType, size));
            }
            catch (System.Exception e) { Debug.LogWarning("[Banjjak] shadow map size not set: " + e.Message); }
        }
    }
}
