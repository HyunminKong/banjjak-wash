using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>All materials, built from the procedural textures.</summary>
    public static partial class Mats
    {
        public static Material Stone, Brick, Wood, DarkWood, BlackMetal, Metal, StoneSmooth, Clay, Soil,
            BinGreen, BinLid, Lantern, LampGlow, Grass, Plaster, Roof, Bark, Glass, WhiteTrim, Water, Concrete,
            Leaf, LeafDark, LeafLight, GrassBlades, Rubber, Glove, Sleeve, GunYellow, GunDark, Hose, Steel, NozzleTip, FoamBottle;
        public static Material Particle, Mist;
        public static Texture2D Dot;
        static readonly Dictionary<Material, Material> doubleCache = new Dictionary<Material, Material>();
        static bool ready;

        /// <summary>Every generated texture. The editor bakes these to Assets/Resources/Tex; the game falls back to generating.</summary>
        public static readonly (string name, System.Func<TexGen.Pair> gen, bool linear, bool clamp)[] Recipes =
        {
            ("dirtNoise", () => new TexGen.Pair { Albedo = TexGen.DirtNoise() }, true, false),
            ("pavers", () => TexGen.Pavers(), false, false),
            ("brick", () => TexGen.Brick(), false, false),
            ("wood", () => TexGen.Wood(512, new Color(.7f, .5f, .32f), new Color(.45f, .3f, .18f), 1), false, false),
            ("fine", () => TexGen.Fine(512, "fine", .22f, 6, 4, 2.5f), false, false),
            ("rough", () => TexGen.Fine(512, "rough", .35f, 10, 9, 5f), false, false),
            ("grass", () => TexGen.Grass(), false, false),
            ("plaster", () => TexGen.Plaster(), false, false),
            ("roof", () => TexGen.Roof(), false, false),
            ("bark", () => TexGen.Bark(), false, false),
            ("leaves", () => new TexGen.Pair { Albedo = TexGen.Leaves() }, false, true),
            ("concrete", () => TexGen.Concrete(), false, false),
            ("asphalt", () => TexGen.Asphalt(), false, false),
            ("skate", () => TexGen.Skate(), false, false),
            ("siding", () => TexGen.Siding(), false, false),
            ("tile", () => TexGen.Tile(), false, false),
            ("vinyl", () => TexGen.Vinyl(), false, false),
            ("deck", () => TexGen.Deck(), false, false),
            ("diamond", () => TexGen.Diamond(), false, false),
            ("rubber", () => TexGen.Rubber(), false, false),
            ("camperSide", () => new TexGen.Pair { Albedo = TexGen.CamperSide() }, false, true),
            ("camperFront", () => new TexGen.Pair { Albedo = TexGen.CamperFront() }, false, true),
            ("camperBack", () => new TexGen.Pair { Albedo = TexGen.CamperBack() }, false, true),
            ("hoodFront", () => new TexGen.Pair { Albedo = TexGen.HoodFront() }, false, true),
            ("hoodSide", () => new TexGen.Pair { Albedo = TexGen.HoodSide() }, false, true),
            ("hubcap", () => new TexGen.Pair { Albedo = TexGen.Hubcap() }, false, true),
            ("door", () => new TexGen.Pair { Albedo = TexGen.Door() }, false, true),
            ("garage", () => new TexGen.Pair { Albedo = TexGen.Garage() }, false, true),
            ("acunit", () => new TexGen.Pair { Albedo = TexGen.AcUnit() }, false, true),
            ("pump", () => new TexGen.Pair { Albedo = TexGen.Pump() }, false, true),
            ("storeSign", () => new TexGen.Pair { Albedo = TexGen.StoreSign() }, false, true),
            ("vending", () => new TexGen.Pair { Albedo = TexGen.Vending() }, false, true),
            ("fascia", () => new TexGen.Pair { Albedo = TexGen.Fascia() }, false, true),
            ("canopyUnder", () => new TexGen.Pair { Albedo = TexGen.CanopyUnder() }, false, true),
            ("towerSign", () => new TexGen.Pair { Albedo = TexGen.TowerSign() }, false, true),
        };

        static TexGen.Pair Tex(string name)
        {
            var a = Resources.Load<Texture2D>("Tex/" + name);
            if (a != null) return new TexGen.Pair { Albedo = a, Normal = Resources.Load<Texture2D>("Tex/" + name + "_n") };
            foreach (var r in Recipes) if (r.name == name) return r.gen();
            return default;
        }

        public static void Init()
        {
            if (ready) return;
            ready = true;
            var bank = ShaderBank.I;
            Shader.SetGlobalTexture("_DirtNoise", Tex("dirtNoise").Albedo);
            var black3 = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false);
            black3.SetPixelData(new byte[4], 0); black3.Apply();
            Shader.SetGlobalTexture("_Mask3", black3);
            Shader.SetGlobalTexture("_Type3", black3);

            var pavers = Tex("pavers");
            var brick = Tex("brick");
            var wood = Tex("wood");
            var fine = Tex("fine");
            var rough = Tex("rough");
            var grass = Tex("grass");
            var plaster = Tex("plaster");
            var roof = Tex("roof");
            var bark = Tex("bark");
            var leaves = Tex("leaves").Albedo;
            var white = TexGen.Solid(Color.white);

            Stone = Lit(pavers, 1, new Color(1, 1, 1), .22f, 0, .5f, 1f);
            Brick = Lit(brick, 1, Color.white, .12f, 0, 1f, 1f);
            Wood = Lit(wood, 1, Color.white, .28f, 0, 1f, .6f);
            DarkWood = Lit(wood, 1, new Color(.62f, .55f, .5f), .25f, 0, 1f, .6f);
            BlackMetal = Lit(fine, 1, new Color(.13f, .13f, .14f), .5f, .6f, 2f, .3f);
            Metal = Lit(fine, 1, new Color(.62f, .64f, .66f), .55f, .85f, 2f, .3f);
            Steel = Lit(fine, 1, new Color(.78f, .8f, .83f), .78f, .95f, 4f, .1f);
            StoneSmooth = Lit(rough, 1, new Color(.78f, .76f, .72f), .2f, 0, 1f, .8f);
            Concrete = Lit(rough, 1, new Color(.66f, .65f, .62f), .15f, 0, .7f, 1f);
            Clay = Lit(rough, 1, new Color(.72f, .38f, .22f), .25f, 0, 1.5f, .5f);
            Soil = Lit(rough, 1, new Color(.23f, .16f, .11f), .05f, 0, 2f, 1.2f);
            BinGreen = Lit(fine, 1, new Color(.16f, .36f, .22f), .45f, 0, 2f, .3f);
            BinLid = Lit(fine, 1, new Color(.12f, .28f, .17f), .5f, 0, 2f, .3f);
            Lantern = Lit(fine, 1, new Color(.1f, .1f, .11f), .55f, .7f, 2f, .2f);
            LampGlow = Lit(white, 0, new Color(1f, .95f, .82f), .6f, 0, 1f, 0);
            LampGlow.SetColor("_Emission", new Color(1f, .82f, .55f) * .6f);
            Grass = Lit(grass, 1, Color.white, .08f, 0, .25f, 1f);
            Plaster = Lit(plaster, 1, Color.white, .1f, 0, .5f, 1f);
            Roof = Lit(roof, 1, new Color(.36f, .33f, .32f), .18f, 0, .5f, 1f);
            Bark = Lit(bark, 1, Color.white, .1f, 0, 1f, 1f);
            Glass = Lit(white, 0, new Color(.08f, .12f, .16f), .95f, .1f, 1f, 0);
            WhiteTrim = Lit(fine, 1, new Color(.93f, .93f, .91f), .35f, 0, 2f, .2f);
            Water = Lit(white, 0, new Color(.09f, .22f, .26f), .96f, .1f, 1f, 0);
            Rubber = Lit(fine, 1, new Color(.08f, .08f, .08f), .25f, 0, 3f, .3f);

            Glove = Lit(rough, 1, new Color(.22f, .3f, .42f), .3f, 0, 6f, .6f);
            Sleeve = Lit(rough, 1, new Color(.2f, .27f, .24f), .15f, 0, 4f, .8f);
            GunYellow = Lit(fine, 1, new Color(.95f, .74f, .12f), .55f, 0, 3f, .2f);
            GunDark = Lit(fine, 1, new Color(.11f, .12f, .13f), .4f, 0, 3f, .3f);
            Hose = Lit(fine, 1, new Color(.1f, .1f, .1f), .35f, 0, 3f, .3f);
            NozzleTip = Lit(white, 0, new Color(.19f, .64f, .42f), .5f, 0, 1f, 0);
            FoamBottle = Lit(white, 0, new Color(.9f, .95f, 1f), .7f, 0, 1f, 0);

            Leaf = Foliage(leaves, new Color(1, 1, 1), .035f);
            LeafDark = Foliage(leaves, new Color(.78f, .86f, .7f), .03f);
            LeafLight = Foliage(leaves, new Color(1f, 1.04f, .92f), .04f);
            GrassBlades = Foliage(white, Color.white, 0);
            GrassBlades.SetFloat("_GrassMode", 1);
            GrassBlades.SetFloat("_Cutoff", .1f);
            GrassBlades.SetFloat("_Translucency", .25f);

            Dot = TexGen.SoftDot();
            Particle = new Material(bank.particles) { name = "particle" };
            Particle.SetTexture("_BaseMap", Dot);
            Mist = new Material(bank.particles) { name = "mist" };
            Mist.SetTexture("_BaseMap", Dot);
            InitMore(pavers, fine, rough, plaster, wood, white);
        }

        public static Material Lit(TexGen.Pair p, float useNormal, Color tint, float smooth, float metal, float uvScale, float bump)
        {
            var m = new Material(ShaderBank.I.dirtLit);
            m.SetTexture("_BaseMap", p.Albedo);
            if (p.Normal != null && useNormal > 0)
            {
                m.SetTexture("_BumpMap", p.Normal);
                m.SetFloat("_UseNormal", 1);
                m.SetFloat("_BumpScale", bump);
            }
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_UVScale", uvScale);
            m.enableInstancing = true;
            return m;
        }

        public static Material Lit(Texture2D albedo, float useNormal, Color tint, float smooth, float metal, float uvScale, float bump)
            => Lit(new TexGen.Pair { Albedo = albedo }, useNormal, tint, smooth, metal, uvScale, bump);

        public static Material Solid(Color c, float smooth = .4f, float metal = 0)
        {
            var m = new Material(ShaderBank.I.dirtLit);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        public static Material Foliage(Texture2D tex, Color tint, float wind)
        {
            var m = new Material(ShaderBank.I.foliage);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Wind", wind);
            m.SetFloat("_Cutoff", .45f);
            m.enableInstancing = true;
            return m;
        }

        public static Material DoubleSided(Material m)
        {
            if (m.shader != ShaderBank.I.dirtLit) return m;
            if (doubleCache.TryGetValue(m, out var d)) return d;
            d = new Material(m) { name = m.name + "_2s" };
            d.SetFloat("_Cull", 0);
            doubleCache[m] = d;
            return d;
        }
    }
}
