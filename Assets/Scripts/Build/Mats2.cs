using UnityEngine;

namespace Banjjak
{
    public static partial class Mats
    {
        public static Material Pavers, Asphalt, Skate, Siding, Stucco, StuccoWarm, Tile, Vinyl, Deck, Diamond, RubberRed, RubberGreen,
            White, DarkUnder, DarkMetal, WindowGlass, CamperSide, CamperFront, CamperBack, HoodFront, HoodSide, Hubcap, DoorM, Garage, AcUnit,
            Pump, StoreSign, Vending, Fascia, CanopyUnder, TowerSign, Blue, RedMetal, Yellow, SlideBlue, Seat, Orange, CarRed, CarGlass, Bollard,
            DeskTop, Whiteboard, Shelf, Grey, Ceiling, PanelLight, Sand, Alu, ChairWood, ChairDark, Cardboard, Cabinet, Taillight, Headlight, RoofSlate;

        static void InitMore(TexGen.Pair pavers, TexGen.Pair fine, TexGen.Pair rough, TexGen.Pair plaster, TexGen.Pair wood, Texture2D white)
        {
            Pavers = Lit(pavers, 1, new Color(.92f, .9f, .88f), .2f, 0, .9f, 1f);
            Concrete = Lit(Tex("concrete"), 1, Color.white, .12f, 0, 1 / 3f, .8f);
            Asphalt = Lit(Tex("asphalt"), 1, Color.white, .08f, 0, .4f, .8f);
            Skate = Lit(Tex("skate"), 1, Color.white, .2f, 0, .5f, .6f);
            Siding = Lit(Tex("siding"), 1, Color.white, .3f, 0, 1 / 1.6f, 1f);
            Stucco = Lit(plaster, 1, G.Hex(0xe9e4da), .1f, 0, .5f, 1f);
            StuccoWarm = Lit(plaster, 1, G.Hex(0xf1dcc4), .1f, 0, .5f, 1f);
            Tile = Lit(Tex("tile"), 1, Color.white, .7f, 0, 1 / 1.2f, .6f);
            Vinyl = Lit(Tex("vinyl"), 1, Color.white, .45f, 0, .5f, .4f);
            Deck = Lit(Tex("deck"), 1, Color.white, .2f, 0, 1f, .8f);
            Diamond = Lit(Tex("diamond"), 1, Color.white, .55f, .6f, 2f, 1f);
            var rub = Tex("rubber");
            RubberRed = Lit(rub, 1, G.Hex(0xc0573e), .05f, 0, 1f, .5f);
            RubberGreen = Lit(rub, 1, G.Hex(0x5b8f4e), .05f, 0, 1f, .5f);

            White = Lit(fine, 1, G.Hex(0xf2efe6), .6f, 0, 2f, .1f);
            DarkUnder = Lit(fine, 1, G.Hex(0x2a2c2e), .2f, 0, 2f, .2f);
            DarkMetal = Lit(fine, 1, G.Hex(0x40454a), .55f, .55f, 2f, .2f);
            WindowGlass = Lit(white, 0, G.Hex(0x6d8796), .95f, .4f, 1f, 0);
            Glass = WindowGlass;

            CamperSide = Decal("camperSide", .7f);
            CamperFront = Decal("camperFront", .7f);
            CamperBack = Decal("camperBack", .7f);
            HoodFront = Decal("hoodFront", .7f);
            HoodSide = Decal("hoodSide", .7f);
            Hubcap = Decal("hubcap", .7f, .6f);
            DoorM = Decal("door", .55f);
            Garage = Decal("garage", .5f);
            AcUnit = Decal("acunit", .55f, .3f);
            Pump = Decal("pump", .6f);
            StoreSign = Decal("storeSign", .6f);
            Vending = Decal("vending", .65f);
            Fascia = Decal("fascia", .6f);
            CanopyUnder = Decal("canopyUnder", .4f);
            TowerSign = Decal("towerSign", .6f);

            Blue = Lit(fine, 1, G.Hex(0x2c5d9e), .55f, 0, 2f, .1f);
            RedMetal = Lit(fine, 1, G.Hex(0xc8412f), .6f, .35f, 2f, .2f);
            Yellow = Lit(fine, 1, G.Hex(0xf0b429), .55f, 0, 2f, .1f);
            SlideBlue = Lit(fine, 1, G.Hex(0x2f7fd0), .75f, 0, 2f, .05f);
            Seat = Lit(rub, 1, G.Hex(0x3a3a3a), .1f, 0, 2f, .5f);
            Orange = Lit(fine, 1, G.Hex(0xef7b2f), .6f, 0, 2f, .1f);
            CarRed = Lit(fine, 1, G.Hex(0xb3261e), .78f, .45f, 2f, .05f);
            CarGlass = Lit(white, 0, G.Hex(0x2c3a44), .95f, .5f, 1f, 0);
            Bollard = Lit(fine, 1, G.Hex(0xf2c230), .55f, 0, 2f, .1f);
            DeskTop = Lit(wood, 1, G.Hex(0xe6d3b8), .5f, 0, 1f, .2f);
            Whiteboard = Lit(white, 0, G.Hex(0xfbfbf8), .85f, 0, 1f, 0);
            Shelf = Lit(fine, 1, G.Hex(0x5a7fa6), .5f, .3f, 2f, .2f);
            Grey = Lit(fine, 1, G.Hex(0x8d949a), .5f, 0, 2f, .1f);
            Ceiling = Lit(fine, 1, G.Hex(0xf1efe9), .1f, 0, 1f, .1f);
            PanelLight = Lit(white, 0, G.Hex(0xfffaf0), .6f, 0, 1f, 0);
            PanelLight.SetColor("_Emission", G.Hex(0xfff4dc) * 1.4f);
            Sand = Lit(rough, 1, G.Hex(0xd9c08a), .05f, 0, 3f, .6f);
            Alu = Lit(fine, 1, G.Hex(0xb9c0c6), .65f, .6f, 2f, .1f);
            ChairWood = Lit(wood, 1, G.Hex(0x6b4a32), .3f, 0, 1f, .3f);
            ChairDark = Lit(fine, 1, G.Hex(0x2b2f35), .4f, 0, 2f, .1f);
            Cardboard = Lit(rough, 1, G.Hex(0xb88a56), .1f, 0, 2f, .4f);
            Cabinet = Lit(fine, 1, G.Hex(0x9aa1a8), .55f, .4f, 2f, .1f);
            Taillight = Lit(white, 0, G.Hex(0xc4262a), .7f, 0, 1f, 0);
            Headlight = Lit(white, 0, G.Hex(0xfff6dd), .8f, 0, 1f, 0);
            Headlight.SetColor("_Emission", G.Hex(0xfff2c8) * .3f);
            RoofSlate = Lit(Tex("roof"), 1, G.Hex(0x4a5059), .2f, 0, .5f, 1f);
        }

        static Material Decal(string name, float smooth, float metal = 0)
        {
            var m = Lit(Tex(name), 0, Color.white, smooth, metal, 1f, 0);
            m.name = name;
            return m;
        }

        /// <summary>6-material array for RoundedBox(..., perFace) in face order +X, -X, +Y, -Y, +Z, -Z.</summary>
        public static Material[] Faces(Material px, Material nx, Material py, Material ny, Material pz, Material nz) => new[] { px, nx, py, ny, pz, nz };
    }
}
