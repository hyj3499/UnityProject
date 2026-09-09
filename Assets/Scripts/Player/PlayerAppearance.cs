using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어의 외형. 새 게임을 시작할 때 고르고 세이브에 그대로 담긴다.
    ///
    /// 몸은 farmer_base(남) / farmer_girl_base(여) 시트에서 나오고, 피부·눈·부츠 색은 그 시트의
    /// 표식 색을 갈아 끼워 만든다 (FarmerSheet). 옷은 흰 그림 위에 색을 곱해 입힌다
    /// (FarmerClothes). 그래서 여기 담기는 것은 "#RRGGBB" 색값들과 성별뿐이고, 색깔별 그림은
    /// 한 장도 필요 없다.
    /// </summary>
    [Serializable]
    public class PlayerAppearance
    {
        // 기본값 — 피부·눈·부츠는 시트에 원래 칠해져 있는 색이라 손대지 않으면 원본 그대로 나온다.
        public const string DefaultSkin = "#F9AE89";
        public const string DefaultEyes = "#1C964A";
        public const string DefaultBoots = "#AD471B";
        public const string DefaultPants = "#3A5FA8";
        public const string DefaultShirt = "#C85A3C";
        public const string DefaultHair = "#6B3A1F";

        /// <summary>"male" 또는 "female" — 어느 시트를 쓸지 정한다.</summary>
        public string gender = "female";

        public string skin = DefaultSkin;
        public string eyes = DefaultEyes;
        public string boots = DefaultBoots;
        public string pants = DefaultPants;
        public string shirt = DefaultShirt;
        public string hair = DefaultHair;

        /// <summary>셔츠 무늬. 기본값은 무늬 없는 흰 티셔츠라 어떤 색으로 물들여도 깔끔하다.</summary>
        public int shirtStyle = FarmerClothes.DefaultShirt;

        /// <summary>머리 모양 (0 ~ FarmerClothes.HairCount-1).</summary>
        public int hairStyle = 0;

        public bool IsFemale => !string.Equals(gender, "male", StringComparison.OrdinalIgnoreCase);

        public PlayerAppearance Clone() => (PlayerAppearance)MemberwiseClone();

        public Color SkinColor => Parse(skin, DefaultSkin);
        public Color EyeColor => Parse(eyes, DefaultEyes);
        public Color BootsColor => Parse(boots, DefaultBoots);
        public Color PantsColor => Parse(pants, DefaultPants);
        public Color ShirtColor => Parse(shirt, DefaultShirt);
        public Color HairColor => Parse(hair, DefaultHair);

        /// <summary>시트를 칠할 색 묶음.</summary>
        public FarmerSheet.Palette ToPalette() => new FarmerSheet.Palette
        {
            skin = SkinColor,
            boots = BootsColor,
            eyes = EyeColor,
            pants = PantsColor,
            shirt = ShirtColor,
        };

        /// <summary>"#RRGGBB"를 Color로. 값이 비었거나 이상하면 기본값으로 돌아간다.</summary>
        private static Color Parse(string value, string fallback)
        {
            if (!string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out var c)) return c;
            ColorUtility.TryParseHtmlString(fallback, out var d);
            return d;
        }
    }

    /// <summary>모양 선택지 하나 (보여 줄 이름 + 저장할 값).</summary>
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

    /// <summary>캐릭터 만들기 화면의 선택지와, 색상 그래프의 "빠른 선택" 견본들.</summary>
    public static class AppearanceCatalog
    {
        public static readonly AppearanceOption[] Genders =
        {
            new AppearanceOption("female", "여성"),
            new AppearanceOption("male", "남성"),
        };

        /// <summary>스타듀밸리 skinColors 표에서 각 톤의 가장 밝은 칸을 뽑아 온 것.</summary>
        public static readonly (string label, string hex)[] SkinSwatches =
        {
            ("1", "#F9AE89"), ("2", "#E18C66"), ("3", "#F0A082"), ("4", "#F7B99A"),
            ("5", "#C46447"), ("6", "#AE5F39"), ("7", "#A24612"), ("8", "#D28A3B"),
            ("9", "#BD7944"), ("10", "#FFABB2"), ("11", "#D6B2A9"), ("12", "#8C5429"),
        };

        public static readonly (string label, string hex)[] EyeSwatches =
        {
            ("초록", "#1C964A"), ("파랑", "#2E6FC4"), ("갈색", "#7A4A22"),
            ("검정", "#3A3A44"), ("보라", "#7A3FA8"), ("회색", "#8A9199"),
        };

        /// <summary>shoeColors 표에서 가장 밝은 칸을 뽑아 온 것.</summary>
        public static readonly (string label, string hex)[] BootsSwatches =
        {
            ("빨강", "#D40000"), ("초록", "#88B24D"), ("갈색", "#B35500"),
            ("황토", "#A3762A"), ("회청", "#96A2A2"), ("검정", "#424242"),
            ("보라", "#BC00EB"), ("파랑", "#0B63B4"), ("흰색", "#F2F2F2"),
        };

        public static readonly (string label, string hex)[] HairSwatches =
        {
            ("갈색", "#6B3A1F"), ("검정", "#2B2B33"), ("금발", "#D9A441"),
            ("빨강", "#B23A1E"), ("회색", "#9AA0A6"), ("흰색", "#E8E4DC"),
            ("분홍", "#E08AA8"), ("파랑", "#4A6FB5"),
        };

        public static readonly (string label, string hex)[] ClothSwatches =
        {
            ("파랑", "#3A5FA8"), ("빨강", "#C85A3C"), ("초록", "#3D993D"),
            ("보라", "#8E52C4"), ("노랑", "#E0B23C"), ("분홍", "#E88AA8"),
            ("흰색", "#E8E8E8"), ("검정", "#39393F"),
        };

        /// <summary>머리 모양 선택지. 그림이 56벌 들어 있어 번호만 붙여 준다.</summary>
        public static readonly AppearanceOption[] HairStyles = BuildHairStyles();

        private static AppearanceOption[] BuildHairStyles()
        {
            var list = new AppearanceOption[FarmerClothes.HairCount];
            for (int i = 0; i < list.Length; i++)
                list[i] = new AppearanceOption(i.ToString(), (i + 1) + "번");
            return list;
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
