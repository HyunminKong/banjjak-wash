using System.IO;
using UnityEditor;
using UnityEngine;

namespace Banjjak.EditorTools
{
    /// <summary>Bakes the procedural textures to PNG assets so the game loads instantly.</summary>
    public static class TextureBaker
    {
        const string Dir = "Assets/Resources/Tex";

        [MenuItem("반짝 워시/텍스처 다시 굽기")]
        public static void Bake()
        {
            Directory.CreateDirectory(Dir);
            foreach (var r in Mats.Recipes)
            {
                var p = r.gen();
                Write(p.Albedo, r.name, r.linear, r.clamp);
                if (p.Normal != null) Write(p.Normal, r.name + "_n", true, false);
            }
            AssetDatabase.Refresh();
            foreach (var r in Mats.Recipes)
            {
                Configure(r.name, r.linear, r.clamp, r.name == "leaves");
                Configure(r.name + "_n", true, false, false);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Banjjak] textures baked");
        }

        static void Write(Texture2D t, string name, bool linear, bool clamp)
        {
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), t.EncodeToPNG());
        }

        static void Configure(string name, bool linear, bool clamp, bool alpha)
        {
            var path = Dir + "/" + name + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = !linear;
            imp.mipmapEnabled = true;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = alpha;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
    }
}
