using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>動物角色：蛇身兩色交替、臉部顏色與耳朵形狀（數值與網頁版 art.js 相同）。</summary>
    public sealed class AnimalDef
    {
        public string Id, Ear;                 // Ear: point / floppy / long / round / none
        public Color Body, Dark, Belly, EarIn, EarCol, Nose, Seg0, Seg1, Tail;
        public float TailScale;
        public bool EyesTop, HasEarCol, HasEarIn, HasNose, HasBeak, HasMuzzle, Patches, Cheeks;
        public Color Beak, Muzzle;
    }

    public sealed class ThemeDef
    {
        public Color FloorA, FloorB, Wall, WallTop, Rock0, Rock1, Outer, Sky, LightColor, Hemi0, Hemi1;
        public float LightIntensity; public bool Night;
    }

    public static class Data
    {
        public static Color Hex(string s)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(s, out c)) c = Color.magenta;
            return c;
        }

        static AnimalDef A(string id, string body, string dark, string belly, string ear, string earIn, string earCol, string nose,
            string seg0, string seg1, string tail, float tailScale, bool eyesTop = false, string beak = null, string muzzle = null, bool patches = false, string cheeks = null)
        {
            var a = new AnimalDef
            {
                Id = id, Ear = ear, Body = Hex(body), Dark = Hex(dark), Belly = Hex(belly), Seg0 = Hex(seg0), Seg1 = Hex(seg1),
                Tail = Hex(tail), TailScale = tailScale, EyesTop = eyesTop, Patches = patches, Cheeks = cheeks != null
            };
            if (earIn != null) { a.EarIn = Hex(earIn); a.HasEarIn = true; }
            if (earCol != null) { a.EarCol = Hex(earCol); a.HasEarCol = true; }
            if (nose != null) { a.Nose = Hex(nose); a.HasNose = true; }
            if (beak != null) { a.Beak = Hex(beak); a.HasBeak = true; }
            if (muzzle != null) { a.Muzzle = Hex(muzzle); a.HasMuzzle = true; }
            return a;
        }

        public static readonly string[] AnimalIds = { "cat", "dog", "bunny", "bear", "panda", "fox", "frog", "penguin" };

        public static readonly AnimalDef[] Animals =
        {
            A("cat", "#ffb86b", "#e88f3b", "#fff0da", "point", "#ff9db4", null, "#ff7f9f", "#ffb259", "#f08d2a", "#e07a1f", 0.8f),
            A("dog", "#e8b87c", "#c58b4d", "#fff4e2", "floppy", null, "#a8693a", "#3a2a2a", "#e8b87c", "#b98650", "#fff4e2", 0.9f, muzzle: "#fff4e2"),
            A("bunny", "#fff7fb", "#e6cfe0", "#ffe3ef", "long", "#ffb3d1", null, "#ff8aa5", "#fffafd", "#ffc9de", "#ffffff", 1.5f),
            A("bear", "#b9824f", "#936235", "#f4dab6", "round", "#ebc496", null, "#3a2a2a", "#b9824f", "#8b5a30", "#8b5a30", 0.9f, muzzle: "#f4dab6"),
            A("panda", "#ffffff", "#d9d9e0", "#ffffff", "round", null, "#2d2d36", "#2d2d36", "#ffffff", "#3b3b46", "#3b3b46", 1f, patches: true),
            A("fox", "#ff8c3f", "#dd6420", "#fff3e6", "point", "#3a2a2a", null, "#3a2a2a", "#ff8c3f", "#fff1de", "#ffffff", 1.4f, cheeks: "#fff3e6"),
            A("frog", "#72d56c", "#43b04a", "#e0f8b0", "none", null, null, null, "#72d56c", "#b8ec70", "#43b04a", 0.7f, eyesTop: true),
            A("penguin", "#465176", "#2c3452", "#ffffff", "none", null, null, null, "#4a5a8c", "#ffffff", "#2c3452", 0.8f, beak: "#ffb43b")
        };

        static Dictionary<string, AnimalDef> animalMap;
        public static AnimalDef Animal(string id)
        {
            if (animalMap == null) { animalMap = new Dictionary<string, AnimalDef>(); foreach (var a in Animals) animalMap[a.Id] = a; }
            AnimalDef r;
            return id != null && animalMap.TryGetValue(id, out r) ? r : Animals[0];
        }

        static ThemeDef T(string a, string b, string wall, string wallTop, string r0, string r1, string outer, string sky, float li, string lc, string h0, string h1, bool night = false)
        {
            return new ThemeDef
            {
                FloorA = Hex(a), FloorB = Hex(b), Wall = Hex(wall), WallTop = Hex(wallTop), Rock0 = Hex(r0), Rock1 = Hex(r1), Outer = Hex(outer),
                Sky = Hex(sky), LightIntensity = li, LightColor = Hex(lc), Hemi0 = Hex(h0), Hemi1 = Hex(h1), Night = night
            };
        }

        public static readonly ThemeDef[] Themes =
        {
            T("#b9e47f", "#a7d86a", "#b9803f", "#d9a25f", "#a7aeb5", "#8a929a", "#7fc25f", "#cdeeff", 1.0f, "#fff4d6", "#ffffff", "#9fcf7a"),
            T("#ffe2ee", "#ffcfe2", "#ff7fb0", "#ffb1cf", "#ffd23d", "#ff9f2e", "#ffc2dc", "#ffe3f1", 1.0f, "#fff0f6", "#fff6fb", "#ffb7d6"),
            T("#fbeab4", "#f3da98", "#3fb6c9", "#7fdcec", "#f0a98a", "#d98462", "#f0d18a", "#bfeaff", 1.1f, "#fff1c8", "#ffffff", "#f4d894"),
            T("#f7fdff", "#dfeff7", "#8fd0ee", "#c9ecfa", "#ffffff", "#cfe0ea", "#d6e8f1", "#d9eef9", 0.95f, "#f2f8ff", "#ffffff", "#bcd6e6"),
            T("#3f6a86", "#375f7a", "#7a5638", "#a47a52", "#6b78a0", "#525d82", "#223d58", "#222a55", 0.75f, "#cfd8ff", "#8fa0ff", "#2a3f5e", true),
            T("#dcf0b4", "#cbe59c", "#7bb85b", "#a6d985", "#b4ad92", "#938c72", "#a9d68c", "#e3f4cf", 1.0f, "#fff8dc", "#ffffff", "#a9d68c")
        };

        public static ThemeDef Theme(int id) { return Themes[((id % Themes.Length) + Themes.Length) % Themes.Length]; }

        public static readonly Color[] FruitBody = { Hex("#ff5a52"), Hex("#ffa63d"), Hex("#8fdc5a") };
        public static readonly Color[] FruitDark = { Hex("#d93a3a"), Hex("#e0801a"), Hex("#5eb534") };
        public static readonly Color FruitLeaf = Hex("#4cc26a");

        public static Color SlotColor(int slot) { return Hex(Rules.SLOT_COLORS[((slot % 8) + 8) % 8]); }

        public static readonly Color Ink = Hex("#4a3320");
        public static readonly Color Cream = Hex("#fff7e3");
    }
}
