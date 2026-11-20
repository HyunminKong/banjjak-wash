using UnityEngine;

namespace Banjjak
{
    /// <summary>
    /// Player looks: an original animal character, a work-suit colour and a hat. Encoded as "animal-suit-hat"
    /// (e.g. "bear-0-1") for the save file and for other players in co-op.
    /// </summary>
    public static class Skins
    {
        public class Animal
        {
            public string Id, Name;
            public Color Fur, Face, Nose, Inner;
            public string Ears;    // round, point, long, flop, none
            public bool Muzzle, Beak, BigEyes, Patches;
        }

        public static readonly Animal[] Animals =
        {
            new Animal { Id = "bear", Name = "곰", Fur = G.Hex(0x8a5a3c), Face = G.Hex(0xe2c29b), Nose = G.Hex(0x2a1d16), Inner = G.Hex(0xc99a74), Ears = "round", Muzzle = true },
            new Animal { Id = "cat", Name = "고양이", Fur = G.Hex(0x8f97a3), Face = G.Hex(0xf1eee8), Nose = G.Hex(0xe58a9a), Inner = G.Hex(0xf0b3bd), Ears = "point", Muzzle = true },
            new Animal { Id = "rabbit", Name = "토끼", Fur = G.Hex(0xf3ede3), Face = G.Hex(0xffffff), Nose = G.Hex(0xe58a9a), Inner = G.Hex(0xf2b8c3), Ears = "long", Muzzle = true },
            new Animal { Id = "penguin", Name = "펭귄", Fur = G.Hex(0x2a3444), Face = G.Hex(0xf4f4f0), Nose = G.Hex(0xf2a23a), Inner = G.Hex(0x2a3444), Ears = "none", Beak = true },
            new Animal { Id = "frog", Name = "개구리", Fur = G.Hex(0x71b84f), Face = G.Hex(0xc8e6a8), Nose = G.Hex(0x3d6b2a), Inner = G.Hex(0x71b84f), Ears = "none", BigEyes = true },
            new Animal { Id = "dog", Name = "강아지", Fur = G.Hex(0xd7a06a), Face = G.Hex(0xf6e3c8), Nose = G.Hex(0x2a1d16), Inner = G.Hex(0x9c6a40), Ears = "flop", Muzzle = true },
            new Animal { Id = "panda", Name = "판다", Fur = G.Hex(0xf4f2ee), Face = G.Hex(0xffffff), Nose = G.Hex(0x1e1e22), Inner = G.Hex(0x1e1e22), Ears = "round", Muzzle = true, Patches = true },
        };

        public static readonly string[] SuitNames = { "노랑", "주황", "파랑", "초록", "분홍", "회색" };
        public static readonly Color[] Suits = { G.Hex(0xf2c230), G.Hex(0xf08a3c), G.Hex(0x3a7bd5), G.Hex(0x30a46c), G.Hex(0xe86fa6), G.Hex(0x7d8794) };
        public static readonly string[] HatNames = { "없음", "안전모", "비니", "캡" };
        /// <summary>Fur colour: 0 keeps the animal's own colour.</summary>
        public static readonly string[] FurNames = { "기본", "흰색", "크림", "갈색", "회색", "검정" };
        public static readonly Color[] Furs = { Color.clear, G.Hex(0xf6f4ef), G.Hex(0xf0dcb8), G.Hex(0x8a5a3c), G.Hex(0x9aa0a8), G.Hex(0x34353a) };

        public struct Look
        {
            public int Animal, Suit, Hat, Fur;
            public override string ToString() => $"{Animals[Animal].Id}-{Suit}-{Hat}-{Fur}";
            public Color FurColor => Fur > 0 ? Furs[Fur] : Animals[Animal].Fur;

            public static Look Parse(string s)
            {
                var l = new Look { Animal = 0, Suit = 0, Hat = 1 };
                if (string.IsNullOrEmpty(s)) return l;
                var p = s.Split('-');
                for (int i = 0; i < Animals.Length; i++) if (Animals[i].Id == p[0]) l.Animal = i;
                if (p.Length > 1 && int.TryParse(p[1], out int su)) l.Suit = Mathf.Clamp(su, 0, Suits.Length - 1);
                if (p.Length > 2 && int.TryParse(p[2], out int h)) l.Hat = Mathf.Clamp(h, 0, HatNames.Length - 1);
                if (p.Length > 3 && int.TryParse(p[3], out int f)) l.Fur = Mathf.Clamp(f, 0, Furs.Length - 1);
                return l;
            }
        }
    }
}
