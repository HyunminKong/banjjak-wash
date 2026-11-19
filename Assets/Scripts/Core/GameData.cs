using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    public static class G
    {
        public const float Cell = 0.05f;
        public const float GroundCell = 0.07f;
        public const float BaseRange = 7.5f;
        public const float CleanT = 0.14f;
        public const float HighY = 2.4f;
        public const int MaxVoxels = 400000;

        public static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static string Money(float v) => "$" + Mathf.RoundToInt(v).ToString("N0");

        public static string Clock(float s)
        {
            int t = Mathf.FloorToInt(s);
            return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
        }
    }

    public class DirtType
    {
        public int I;
        public string Key, Name;
        public float Tough;
        public Color C1, C2;
        public float Rough, Lo, Hi;
        public Color Ui;
    }

    public static class Dirt
    {
        public static readonly List<DirtType> All = new List<DirtType>();
        static readonly Dictionary<string, int> byKey = new Dictionary<string, int>();
        public static int Paint => byKey["paint"];

        static Dirt()
        {
            Add("dust", "먼지", 1.0f, 0x9a8f78, 0x7b7262, .95f, .3f, .7f, 0xa39780);
            Add("pollen", "꽃가루", .8f, 0xd8b63a, 0xbf9e2a, .9f, .3f, .7f, 0xd9b83a);
            Add("mud", "진흙", 1.8f, 0x5e4631, 0x3d2d1f, 1f, .3f, .7f, 0x7a5a3c);
            Add("leaves", "낙엽 얼룩", 2.2f, 0x8a5326, 0x5a3416, .8f, .42f, .58f, 0x8a5326);
            Add("bird", "새똥", 2.2f, 0xe8e4d8, 0xb9b3a3, .7f, .3f, .7f, 0xcfc9b8);
            Add("grime", "찌든 때", 2.6f, 0x34312d, 0x524c43, .9f, .3f, .7f, 0x4d4840);
            Add("oil", "기름 얼룩", 2.8f, 0x1d1b19, 0x2f2a25, .22f, .3f, .7f, 0x2b2723);
            Add("tire", "타이어 자국", 3.2f, 0x1a1a1a, 0x2c2a28, .6f, .3f, .7f, 0x262422);
            Add("soot", "그을음", 2.4f, 0x151413, 0x2b2825, 1f, .3f, .7f, 0x1f1d1b);
            Add("mildew", "곰팡이", 3.0f, 0x1d2320, 0x3d4639, .9f, .45f, .55f, 0x38413a);
            Add("algae", "녹조", 2.8f, 0x3a6a2c, 0x245a33, .3f, .3f, .7f, 0x3f7a35);
            Add("moss", "이끼", 3.6f, 0x4c6a30, 0x2b421f, .95f, .3f, .7f, 0x5d7f3a);
            Add("rust", "녹", 3.2f, 0x8e4a1f, 0x5c2c12, .85f, .3f, .7f, 0xa0562a);
            Add("lime", "물때", 4.5f, 0xebe7dc, 0xcdc8b8, 1f, .3f, .7f, 0xd9d4c4);
            Add("gum", "껌 자국", 7f, 0x3c3a37, 0x5a5753, .5f, .45f, .55f, 0x4a4744);
            Add("paint", "낙서", 10f, 0xe23d8f, 0x2d7be0, .5f, .47f, .53f, 0xe0559a);
        }

        static void Add(string key, string name, float tough, uint c1, uint c2, float rough, float lo, float hi, uint ui)
        {
            var d = new DirtType { I = All.Count, Key = key, Name = name, Tough = tough, C1 = G.Hex(c1), C2 = G.Hex(c2), Rough = rough, Lo = lo, Hi = hi, Ui = G.Hex(ui) };
            byKey[key] = d.I;
            All.Add(d);
        }

        public static int Index(string key) => byKey.TryGetValue(key, out var i) ? i : 0;

        public static readonly uint[,] PaintPairs = {
            { 0xe23d8f, 0x2d7be0 }, { 0xf2c12e, 0x7b3fd1 }, { 0x19b5a5, 0xf05a28 },
            { 0x1a1a1a, 0xe23d8f }, { 0x3fbf3f, 0x2a2a2a }, { 0xf05a28, 0x1d6fd6 },
        };

        public static readonly Dictionary<string, string> Hints = new Dictionary<string, string>
        {
            { "paint", "낙서는 물로는 거의 안 지워져요. 페인트·껌 리무버 거품을 뿌리고 불린 뒤 헹구세요." },
            { "gum", "껌 자국은 아주 질겨요. 휠로 0°까지 좁혀 바짝 대고 쏘세요." },
            { "lime", "물때는 물때·녹 제거제 거품이 잘 들어요." },
            { "moss", "이끼는 이끼·곰팡이 제거제 거품을 뿌리면 금방이에요." },
            { "mildew", "곰팡이는 이끼·곰팡이 제거제 거품이 잘 들어요." },
            { "algae", "녹조는 이끼·곰팡이 제거제 거품이 잘 들어요." },
            { "rust", "녹은 물때·녹 제거제 거품이 잘 들어요." },
            { "oil", "기름 얼룩은 기름때 분해제 거품이 잘 들어요." },
            { "grime", "찌든 때는 거품을 먼저 뿌리거나 좁은 각도가 좋아요." },
            { "soot", "그을음은 기름때 분해제 거품이 잘 들어요." },
        };

        /// <summary>Upload dirt colours / params as shader globals (linear colour space).</summary>
        public static void PushShaderGlobals()
        {
            var c1 = new Vector4[16];
            var c2 = new Vector4[16];
            var dp = new Vector4[16];
            for (int i = 0; i < 16; i++)
            {
                var d = All[i];
                c1[i] = d.C1.linear;
                c2[i] = d.C2.linear;
                dp[i] = new Vector4(d.Lo, d.Hi, d.Rough, 0);
            }
            Shader.SetGlobalVectorArray("_DC1", c1);
            Shader.SetGlobalVectorArray("_DC2", c2);
            Shader.SetGlobalVectorArray("_DP", dp);
            var foam = new Vector4[5];
            for (int i = 0; i < 5; i++) foam[i] = Agents.All[i].Foam.linear;
            Shader.SetGlobalVectorArray("_FoamCols", foam);
            Shader.SetGlobalFloat("_DirtHL", 0f);
        }
    }

    public class Agent
    {
        public string Id, Name, Desc;
        public Color Ui, Foam;
        public int Cost;
        public float[] Eff;
    }

    public static class Agents
    {
        public static readonly List<Agent> All = new List<Agent>();

        static Agents()
        {
            Add("ag_all", "범용 세제", 0xe6f3ff, new Color(.93f, .97f, 1f), 0, "대부분의 때가 2.5배 빨리 지워져요",
                ("dust", 2.5f), ("pollen", 2.5f), ("mud", 2.5f), ("leaves", 2.5f), ("bird", 2.5f), ("grime", 2.5f), ("oil", 2.2f), ("soot", 2.2f), ("other", 1.5f));
            Add("ag_bio", "이끼·곰팡이 제거제", 0xcff3c9, new Color(.82f, 1f, .82f), 450, "이끼·곰팡이·녹조 6배",
                ("moss", 6f), ("mildew", 6f), ("algae", 6f), ("rust", 2f), ("other", 1.8f));
            Add("ag_grease", "기름때 분해제", 0xfff0b3, new Color(1f, .95f, .74f), 350, "찌든 때·기름·타이어 자국·그을음 6배",
                ("grime", 6f), ("oil", 6.5f), ("tire", 6f), ("soot", 6f), ("mud", 3.5f), ("other", 1.8f));
            Add("ag_acid", "물때·녹 제거제", 0xd6e8ff, new Color(.85f, .92f, 1f), 500, "물때 8배, 녹 6배",
                ("lime", 8f), ("rust", 6f), ("algae", 3f), ("other", 1.6f));
            Add("ag_paint", "페인트·껌 리무버", 0xffd3e6, new Color(1f, .84f, .93f), 650, "낙서 10배, 껌 자국 7배",
                ("paint", 10f), ("gum", 7f), ("other", 1.5f));
        }

        static void Add(string id, string name, uint ui, Color foam, int cost, string desc, params (string key, float v)[] eff)
        {
            var map = new Dictionary<string, float>();
            foreach (var e in eff) map[e.key] = e.v;
            var arr = new float[Dirt.All.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                var k = Dirt.All[i].Key;
                arr[i] = map.TryGetValue(k, out var v) ? v : map.TryGetValue("other", out var o) ? o : 1f;
            }
            All.Add(new Agent { Id = id, Name = name, Ui = G.Hex(ui), Foam = foam, Cost = cost, Desc = desc, Eff = arr });
        }
    }

    public class ItemDef
    {
        public string Id, Name, Desc;
        public int Cost;
        public ItemDef(string id, string name, string desc, int cost) { Id = id; Name = name; Desc = desc; Cost = cost; }
    }

    public class BotDef
    {
        public string Key, Type, Name, Desc;
        public int Cost, Max;
    }

    public static class Shop
    {
        public const int TrashPay = 6, WeedPay = 8;

        /// <summary>Tool slots 1-4.</summary>
        public static readonly ItemDef[] Tools =
        {
            new ItemDef("gun", "고압 건", "휠로 분사 각도를 0°~45°로 바꿔요", 0),
            new ItemDef("foam", "거품 분사기", "세제 거품으로 때를 불려요", 0),
            new ItemDef("surface", "원형 바닥 세척기", "걸어 다니며 넓은 바닥을 한 번에", 700),
            new ItemDef("mower", "자동 잔디 깎기", "4를 누르면 혼자 돌아다니며 잔디를 깎아요", 0),
        };

        public static readonly ItemDef[] Upgrades =
        {
            new ItemDef("turbo", "터보 노즐 팁", "휠을 0° 아래로 내리면 회전 터보 모드", 800),
            new ItemDef("ext", "연장 랜스", "분사 거리 7.5m → 10m", 500),
            new ItemDef("tank", "대용량 세제 탱크", "세제 용량 두 배", 300),
            new ItemDef("bigdisc", "대형 원판", "바닥 세척기 폭 1m → 1.5m", 600),
            new ItemDef("bigbag", "큰 쓰레기 봉투", "한 번에 담는 쓰레기 15개 → 35개", 250),
            new ItemDef("lift", "고소 작업대", "L로 설치하고 올라타서 높이 조절", 2400),
        };

        public static readonly BotDef[] Bots =
        {
            new BotDef { Key = "robots", Type = "floor", Name = "바닥 세척 로봇", Desc = "바닥을 돌아다니며 알아서 닦아요", Cost = 1500, Max = 3 },
            new BotDef { Key = "trashBots", Type = "trash", Name = "쓰레기 줍는 로봇", Desc = "흩어진 쓰레기를 주워서 쓰레기통에 비워요", Cost = 1200, Max = 2 },
            new BotDef { Key = "drones", Type = "drone", Name = "벽·유리창 드론", Desc = "날아다니며 벽, 창문, 높은 곳을 닦아요", Cost = 2600, Max = 2 },
            new BotDef { Key = "mowBots", Type = "mower", Name = "잔디 깎기 로봇", Desc = "잔디밭을 돌며 잔디를 깎아요", Cost = 900, Max = 1 },
        };

        public static int CostOf(string id)
        {
            foreach (var t in Tools) if (t.Id == id) return t.Cost;
            foreach (var u in Upgrades) if (u.Id == id) return u.Cost;
            foreach (var a in Agents.All) if (a.Id == id) return a.Cost;
            return 0;
        }
    }

    public class WasherModel
    {
        public string Psi, Name;
        public float Power;
        public int Cost;
    }

    public static class Washers
    {
        public static readonly WasherModel[] All =
        {
            new WasherModel { Psi = "1,800", Name = "입문형 전기식", Power = 17, Cost = 0 },
            new WasherModel { Psi = "2,600", Name = "가정용 고급형", Power = 23, Cost = 900 },
            new WasherModel { Psi = "3,300", Name = "엔진식 업무용", Power = 30, Cost = 2200 },
            new WasherModel { Psi = "4,200", Name = "산업용 특급", Power = 39, Cost = 4500 },
            new WasherModel { Psi = "5,000", Name = "트럭 탑재형", Power = 50, Cost = 9000 },
        };
    }
}
