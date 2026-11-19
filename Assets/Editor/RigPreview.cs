using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Banjjak.EditorTools
{
    /// <summary>Renders the first-person hands/gun to PNGs without building the player.</summary>
    public static class RigPreview
    {
        public static void BakeAndRender()
        {
            TextureBaker.Bake();
            Render();
        }

        [MenuItem("반짝 워시/손 미리보기 렌더")]
        public static void Render()
        {
            string outDir = Path.GetFullPath("../rigpreview");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Mats.Init();
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2f;
            sun.color = new Color(1f, .955f, .885f);
            sun.transform.rotation = Quaternion.Euler(46, -35, 0);
            GameManager.ApplyAmbient();

            var camGo = new GameObject("cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62;
            cam.nearClipPlane = .01f;
            cam.farClipPlane = 10;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.58f, .66f, .74f);
            var rig = new GameObject("rig").AddComponent<HandRig>();
            rig.transform.SetParent(cam.transform, false);
            rig.Build(null);

            Shoot(cam, Path.Combine(outDir, "rig_fp.png"));
            rig.SetTool(1, WasherTool.NozzleColor(30), Agents.All[0].Foam);
            Shoot(cam, Path.Combine(outDir, "rig_fp_foam.png"));
            rig.SetTool(0, WasherTool.NozzleColor(30), Agents.All[0].Foam);

            // side and top views to judge the hand shapes
            var side = new GameObject("side").AddComponent<Camera>();
            side.fieldOfView = 35; side.nearClipPlane = .01f; side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = cam.backgroundColor;
            var gunPos = rig.Gun.position;
            side.transform.position = gunPos + new Vector3(.75f, .25f, .25f);
            side.transform.LookAt(gunPos + rig.Gun.forward * .2f);
            Shoot(side, Path.Combine(outDir, "rig_side.png"));
            side.transform.position = gunPos + new Vector3(-.55f, .45f, .55f);
            side.transform.LookAt(gunPos + rig.Gun.forward * .25f);
            Shoot(side, Path.Combine(outDir, "rig_front.png"));
            Debug.Log("[Banjjak] rig preview written to " + outDir);
        }

        static void Shoot(Camera cam, string path)
        {
            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
        }
    }
}
