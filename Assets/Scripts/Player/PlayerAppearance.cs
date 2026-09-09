using System;
using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 이루는 그림 층. 한 동작 폴더 안에서 이 순서대로 겹쳐 그린다
    /// (Resources/Sprites/Player/{동작}/{이 폴더}/...).
    /// </summary>
    public enum PlayerLayer
    {
        Skin,      // Skins/{1..4}
        Clothes,   // Clothers/Farm/{색}
        Eyes,      // Eyes/{Male|Female}/{색}
        Hair,      // Hair's/{모양}/{색}
        Acc,       // Acc/{장식}          — 없을 수 있다
        Weapon,    // Weapons/{도구}      — 동작에 따라 있을 수도, 없을 수도
    }

    /// <summary>
    /// 플레이어의 외형. 새 게임을 시작할 때 고르고 세이브에 그대로 담긴다.
    /// 값은 전부 <b>Resources 폴더 이름 그대로</b>라서, 새 머리 모양이나 옷을 넣으면
    /// 폴더만 추가하고 아래 목록에 한 줄 적으면 된다.
    /// </summary>
    [Serializable]
    public class PlayerAppearance
    {
        public string skin = "1";           // Skins/1.png
        public string eyeSet = "Male";      // Eyes/Male 또는 Eyes/Female
        public string eyeColor = "Black";
        public string hairStyle = "Standard";
        public string hairColor = "Brown";
        public string clothes = "Blue";     // Clothers/Farm/Blue.png
        public string acc = "";             // Acc 아래의 상대 경로. 빈 값이면 장식 없음.

        public PlayerAppearance Clone() => (PlayerAppearance)MemberwiseClone();

        /// <summary>
        /// 이 층이 쓸 그림의 <b>동작 폴더 안에서의 상대 경로</b>. 쓰지 않는 층이면 null.
        /// (예: Hair -> "Hair's/Standard/Brown")
        /// </summary>
        public string PathFor(PlayerLayer layer)
        {
            switch (layer)
            {
                case PlayerLayer.Skin: return "Skins/" + Or(skin, "1");
                case PlayerLayer.Clothes: return "Clothers/Farm/" + Or(clothes, "Blue");
                case PlayerLayer.Eyes: return $"Eyes/{Or(eyeSet, "Male")}/{Or(eyeColor, "Black")}";
                case PlayerLayer.Hair: return $"Hair's/{Or(hairStyle, "Standard")}/{Or(hairColor, "Brown")}";
                case PlayerLayer.Acc: return string.IsNullOrEmpty(acc) ? null : "Acc/" + acc;
                default: return null;
            }
        }

        private static string Or(string v, string fallback) => string.IsNullOrEmpty(v) ? fallback : v;
    }

    /// <summary>고를 수 있는 항목 하나 (보여 줄 이름 + 실제 폴더 이름).</summary>
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
        public static readonly AppearanceOption[] Skins =
        {
            new AppearanceOption("1", "1"), new AppearanceOption("2", "2"),
            new AppearanceOption("3", "3"), new AppearanceOption("4", "4"),
        };

        public static readonly AppearanceOption[] EyeSets =
        {
            new AppearanceOption("Male", "남성"),
            new AppearanceOption("Female", "여성"),
        };

        public static readonly AppearanceOption[] EyeColors =
        {
            new AppearanceOption("Black", "검정"), new AppearanceOption("Blue", "파랑"),
            new AppearanceOption("Brown", "갈색"), new AppearanceOption("Green", "초록"),
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

        public static readonly AppearanceOption[] HairColors =
        {
            new AppearanceOption("Black", "검정"), new AppearanceOption("Blonde", "금발"),
            new AppearanceOption("Brown", "갈색"), new AppearanceOption("Ginger", "빨강"),
        };

        public static readonly AppearanceOption[] Clothes =
        {
            new AppearanceOption("Blue", "파랑"), new AppearanceOption("Green", "초록"),
            new AppearanceOption("Pink", "분홍"), new AppearanceOption("Purple", "보라"),
            new AppearanceOption("Red", "빨강"),
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

        /// <summary>목록에서 지금 값이 몇 번째인지 (없으면 0).</summary>
        public static int IndexOf(AppearanceOption[] options, string value)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].value == value) return i;
            return 0;
        }
    }
}
