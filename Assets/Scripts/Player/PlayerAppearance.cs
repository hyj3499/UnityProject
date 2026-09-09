using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 이루는 그림 층. 한 동작 폴더 안에서 이 순서대로 겹쳐 그린다
    /// (Resources/Sprites/Player/{동작}/{이 폴더}/...).
    /// </summary>
    public enum PlayerLayer
    {
        Skin,      // Skins/{1..4} 밑그림 + 색상 그래프에서 고른 피부색
        Clothes,   // Clothers/Farm/{색} 밑그림 + 고른 옷 색
        Eyes,      // Eyes/{Male|Female}/{색} 밑그림 + 고른 눈 색
        Hair,      // Hair's/{모양}/{색} 밑그림 + 고른 머리색
        Acc,       // Acc/{장식}          — 없을 수 있다
        Weapon,    // Weapons/{도구}      — 동작에 따라 있을 수도, 없을 수도
    }

    /// <summary>
    /// 플레이어의 외형. 새 게임을 시작할 때 고르고 세이브에 그대로 담긴다.
    ///
    /// 피부·눈·머리·옷의 <b>색</b>은 캐릭터 만들기의 색상 그래프에서 고른 "#RRGGBB" 그대로 저장한다 —
    /// 프리셋 파일 이름이 아니다. 실제로 그 색을 입히는 일은 PlayerPaletteSwap이 맡는다: 미리 있는
    /// 그림 한 장(예: Clothers/Farm/Blue)을 밑그림 삼아, 사용자가 고른 색으로 다시 칠한다.
    /// 그래서 새 색을 추가하는 데 그림이 한 장도 필요 없다.
    ///
    /// 눈매(Male/Female)·머리 모양·장식은 <b>모양</b> 선택이라 여전히 폴더 이름(프리셋)을 쓴다 —
    /// 이건 그림 자체가 다르므로 색상 그래프로 바꿀 수 있는 것이 아니다.
    /// </summary>
    [Serializable]
    public class PlayerAppearance
    {
        // 기본값 = 예전 프리셋(피부 1 / 검은 눈 / 갈색 머리 / 파란 옷)의 대표색.
        public const string DefaultSkin = "#F9E6CF";
        public const string DefaultEyeColor = "#1D1D1D";
        public const string DefaultHairColor = "#8A3625";
        public const string DefaultClothes = "#1A3473";

        public string skin = DefaultSkin;
        public string eyeSet = "Male";          // Eyes/Male 또는 Eyes/Female (모양)
        public string eyeColor = DefaultEyeColor;
        public string hairStyle = "Standard";   // 모양
        public string hairColor = DefaultHairColor;
        public string clothes = DefaultClothes;
        public string acc = "";                 // Acc 아래의 상대 경로. 빈 값이면 장식 없음.

        public PlayerAppearance Clone() => (PlayerAppearance)MemberwiseClone();

        public Color SkinColor => ParseHex(skin, AppearanceCatalog.LegacySkins, DefaultSkin);
        public Color EyeColorValue => ParseHex(eyeColor, AppearanceCatalog.LegacyEyeColors, DefaultEyeColor);
        public Color HairColorValue => ParseHex(hairColor, AppearanceCatalog.LegacyHairColors, DefaultHairColor);
        public Color ClothesColorValue => ParseHex(clothes, AppearanceCatalog.LegacyClothes, DefaultClothes);

        /// <summary>도구·장식처럼 파일을 그대로 쓰는 층의 동작 폴더 안 상대 경로. 색을 입히는 층은
        /// PlayerPaletteSwap이 따로 처리하므로 여기 포함하지 않는다.</summary>
        public string PathFor(PlayerLayer layer)
            => layer == PlayerLayer.Acc && !string.IsNullOrEmpty(acc) ? "Acc/" + acc : null;

        /// <summary>색을 입힐 층이 어느 폴더의 그림을 밑그림으로 쓸지 (모양 선택이 반영된다).</summary>
        public string FolderFor(PlayerLayer layer)
        {
            switch (layer)
            {
                case PlayerLayer.Skin: return "Skins";
                case PlayerLayer.Clothes: return "Clothers/Farm";
                case PlayerLayer.Eyes: return "Eyes/" + Or(eyeSet, "Male");
                case PlayerLayer.Hair: return "Hair's/" + Or(hairStyle, "Standard");
                default: return null;
            }
        }

        private static string Or(string v, string fallback) => string.IsNullOrEmpty(v) ? fallback : v;

        /// <summary>
        /// "#RRGGBB"를 Color로 바꾼다. 예전 세이브에 남아 있는 프리셋 이름("Blue", "Brown", "1" 등)이
        /// 들어오면 그때 그 이름이 뜻하던 색으로 옮겨 준다 — 그래야 예전에 만든 캐릭터가 업데이트
        /// 후에 갑자기 색이 바뀌어 보이지 않는다.
        /// </summary>
        private static Color ParseHex(string value, IReadOnlyDictionary<string, string> legacy, string fallback)
        {
            string hex = value;
            if (!string.IsNullOrEmpty(hex) && legacy != null && legacy.TryGetValue(hex, out var mapped)) hex = mapped;

            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            ColorUtility.TryParseHtmlString(fallback, out var d);
            return d;
        }
    }

    /// <summary>모양 선택지 하나 (보여 줄 이름 + 실제 폴더 이름).</summary>
    public struct AppearanceOption
    {
        public string value;
        public string label;

        public AppearanceOption(string value, string label)
        {
            this.value = value;
            this.label = label;
        }
    }

    /// <summary>
    /// 캐릭터 만들기 화면에 걸리는 선택지 목록.
    ///
    /// Resources를 뒤져 자동으로 모으지 않고 <b>여기 적어 둔다</b> — 폴더마다 이름이 겹쳐서
    /// (머리 모양 일곱 가지가 전부 Black/Blonde/Brown/Ginger를 쓴다) 이름만으로는 구분되지 않고,
    /// 어차피 보여 줄 한글 이름은 사람이 정해야 하기 때문이다.
    /// 그림을 추가했으면 여기에 한 줄 더하면 화면에 바로 나온다.
    /// </summary>
    public static class AppearanceCatalog
    {
        // ---------- 모양(그림 자체가 다른 것) — 색상 그래프로 바꿀 수 없어 프리셋 그대로 ----------
        public static readonly AppearanceOption[] EyeSets =
        {
            new AppearanceOption("Male", "남성"),
            new AppearanceOption("Female", "여성"),
        };

        public static readonly AppearanceOption[] HairStyles =
        {
            new AppearanceOption("Standard", "기본"),
            new AppearanceOption("Fawn", "폰"),
            new AppearanceOption("Iridessa", "이리데사"),
            new AppearanceOption("Josh", "조쉬"),
            new AppearanceOption("Lyria", "리리아"),
            new AppearanceOption("Sebastian", "세바스찬"),
            new AppearanceOption("Silvermist", "실버미스트"),
        };

        /// <summary>
        /// 장식. 첫 항목은 "없음"(빈 값)이고, 나머지는 Acc 폴더 아래의 상대 경로다.
        /// 동작에 따라 그림이 없는 장식이 있어도 그 동작에서만 조용히 빠진다.
        /// </summary>
        public static readonly AppearanceOption[] Accessories = BuildAccessories();

        private static AppearanceOption[] BuildAccessories()
        {
            var list = new List<AppearanceOption> { new AppearanceOption("", "없음") };

            void Add(string value, string label) => list.Add(new AppearanceOption(value, label));

            Add("Beret", "베레모");
            Add("Chicken", "닭 모자");
            Add("Cook", "요리사 모자");
            Add("Cow", "소 모자");
            Add("Deer", "사슴뿔");
            Add("Farm", "밀짚모자");
            Add("Frog", "개구리 모자");
            Add("Leprechaun", "요정 모자");
            Add("Pirate", "해적 모자");
            Add("Santa Hat", "산타 모자");
            Add("Wizard", "마법사 모자");
            Add("pirate eye patch", "안대");

            string[] hairColors = { "Black", "Blonde", "Brown", "Ginger" };
            string[] hairColorNames = { "검정", "금발", "갈색", "빨강" };
            for (int i = 0; i < hairColors.Length; i++)
                Add("Beard/" + hairColors[i], "수염 " + hairColorNames[i]);

            string[] butterfly = { "Blue", "Green", "Pink", "Purple", "Red" };
            string[] butterflyNames = { "파랑", "초록", "분홍", "보라", "빨강" };
            for (int i = 0; i < butterfly.Length; i++)
                Add("Butterfly/" + butterfly[i], "나비 " + butterflyNames[i]);

            for (int i = 1; i <= 4; i++)
                Add("Elf/" + i, "요정 귀 " + i);

            return list.ToArray();
        }

        // ---------- 색(HEX 그래프로 자유롭게 고른다) — 아래는 그 그래프의 "빠른 선택" 버튼과
        // 예전 세이브 호환용으로만 쓰는 대표색 표다. 실제 색 선택 범위는 이 목록에 갇히지 않는다. ----------

        /// <summary>피부색 빠른 선택 (그림 1~4번의 대표색).</summary>
        public static readonly (string label, string hex)[] SkinSwatches =
        {
            ("1", "#F9E6CF"), ("2", "#FFD59A"), ("3", "#FFC68B"), ("4", "#E68E5B"),
        };

        public static readonly (string label, string hex)[] EyeColorSwatches =
        {
            ("검정", "#1D1D1D"), ("파랑", "#1A3473"), ("갈색", "#8A3625"), ("초록", "#115536"),
        };

        public static readonly (string label, string hex)[] HairColorSwatches =
        {
            ("검정", "#0E0E0E"), ("금발", "#FFC71B"), ("갈색", "#8A3625"), ("빨강", "#EE6A0E"),
        };

        public static readonly (string label, string hex)[] ClothesSwatches =
        {
            ("파랑", "#1A3473"), ("초록", "#3D993D"), ("분홍", "#FFAAB0"),
            ("보라", "#B83EAB"), ("빨강", "#C41B24"),
        };

        // 예전 세이브가 파일 프리셋 이름("Blue", "Brown", "1" ...)을 그대로 들고 있을 때 옮겨 줄 색.
        public static readonly IReadOnlyDictionary<string, string> LegacySkins = ToLegacyMap(SkinSwatches);
        public static readonly IReadOnlyDictionary<string, string> LegacyEyeColors = new Dictionary<string, string>
        {
            { "Black", "#1D1D1D" }, { "Blue", "#1A3473" }, { "Brown", "#8A3625" }, { "Green", "#115536" },
        };
        public static readonly IReadOnlyDictionary<string, string> LegacyHairColors = new Dictionary<string, string>
        {
            { "Black", "#0E0E0E" }, { "Blonde", "#FFC71B" }, { "Brown", "#8A3625" }, { "Ginger", "#EE6A0E" },
        };
        public static readonly IReadOnlyDictionary<string, string> LegacyClothes = new Dictionary<string, string>
        {
            { "Blue", "#1A3473" }, { "Green", "#3D993D" }, { "Pink", "#FFAAB0" },
            { "Purple", "#B83EAB" }, { "Red", "#C41B24" },
        };

        private static Dictionary<string, string> ToLegacyMap((string label, string hex)[] swatches)
        {
            var map = new Dictionary<string, string>();
            foreach (var (label, hex) in swatches) map[label] = hex;
            return map;
        }

        /// <summary>목록에서 지금 값이 몇 번째인지 (없으면 0).</summary>
        public static int IndexOf(AppearanceOption[] options, string value)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].value == value) return i;
            return 0;
        }
    }
}
