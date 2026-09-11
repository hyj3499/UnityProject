using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 색 하나를 고른 상태. 그림에 딸린 색 대응표에서 한 색을 고르고, 그 위에 색상·채도·명도를
    /// 더 틀어 쓴다. 그래서 표에 없는 색도 만들 수 있으면서 원래 음영은 그대로 살아난다.
    /// </summary>
    [Serializable]
    public class AppearanceColor
    {
        /// <summary>대응표에서 몇 번째 색인지 (1부터).</summary>
        public int palette = 1;

        public float hue = 0f;          // -180 ~ 180
        public float saturation = 1f;   // 0 ~ 2
        public float value = 1f;        // 0 ~ 2

        public AppearanceColor() { }
        public AppearanceColor(int palette) { this.palette = palette; }

        public AppearanceColor Clone() => (AppearanceColor)MemberwiseClone();

        public FarmerTint ToTint(string lutPath) => new FarmerTint
        {
            lutPath = lutPath,
            column = palette,
            hueShift = hue,
            saturation = saturation,
            value = value,
        };
    }

    /// <summary>
    /// 플레이어의 외형. 새 게임을 시작할 때 고르고 세이브에 그대로 담긴다.
    ///
    /// 담기는 것은 <b>고른 품목의 경로</b>와 <b>색</b>뿐이다. 그림은 부위별로 이미 다 그려져 있고
    /// (FarmerArt), 색은 대응표로 칠하므로 조합마다 그림을 따로 만들 필요가 없다.
    /// 체형은 하나뿐이라 성별 구분이 없다.
    /// </summary>
    [Serializable]
    public class PlayerAppearance
    {
        // 처음 시작할 때 걸치는 것. 비워 두면 맨몸으로 시작하고, 캐릭터 만들기에서 고른다.
        public const string DefaultHair = "";
        public const string DefaultEyes = "";
        public const string DefaultTop = "";
        public const string DefaultPants = "";
        public const string DefaultShoes = "";

        /// <summary>
        /// 맨몸 그림이 있는 곳. 체형은 하나뿐이다.
        /// 폴더로 끝나는 경로라 파일 이름이 부위 이름 그대로다 (Base/chest.png 처럼).
        /// </summary>
        public const string BasePath = "Base/";

        /// <summary>
        /// 소매 그림이 있는 곳. 소매는 옷마다 있는 것이 아니라 <b>소매 갈래</b>마다 하나씩 있고
        /// (Sleeves/arm_left_overshirts.png), 색만 입은 옷 것으로 칠해 쓴다.
        /// </summary>
        public const string SleevePath = "Sleeves/";

        public const string BaseLut = "Base/lut";                     // 피부색 대응표
        public const string EyesLut = "Eyes/lut";                     // 눈 색 대응표
        public const string HairLut = "Hair/lut";                     // 머리색 대응표

        // 고른 품목 (빈 문자열이면 걸치지 않은 것)
        public string hair = DefaultHair;
        public string eyes = DefaultEyes;
        public string facialHair = "";
        public string top = DefaultTop;
        public string pants = DefaultPants;

        /// <summary>드레스·정장·로브처럼 위아래가 한 벌인 옷. 이게 있으면 상·하의 대신 입는다.</summary>
        public string outfit = "";

        public string shoes = DefaultShoes;
        public string headGear = "";
        public string faceGear = "";
        public string backGear = "";

        // 색
        public AppearanceColor skin = new AppearanceColor(1);
        public AppearanceColor hairColor = new AppearanceColor(1);
        public AppearanceColor eyeColor = new AppearanceColor(1);
        public AppearanceColor shoesColor = new AppearanceColor(1);

        public PlayerAppearance Clone()
        {
            var c = (PlayerAppearance)MemberwiseClone();
            c.skin = skin.Clone();
            c.hairColor = hairColor.Clone();
            c.eyeColor = eyeColor.Clone();
            c.shoesColor = shoesColor.Clone();
            return c;
        }

        /// <summary>
        /// 이 부위를 어느 품목에서 가져올지. 한 벌짜리 옷을 입고 있으면 상·하의보다 그쪽이 이긴다.
        /// 걸친 것이 없으면 null.
        /// </summary>
        public string ItemFor(FarmerSlot slot)
        {
            switch (slot)
            {
                case FarmerSlot.Hair: return hair;

                // 눈은 따로 고른 것이 없으면 맨몸에 딸린 것을 쓴다 (Base/eyes.png).
                case FarmerSlot.Eyes: return string.IsNullOrEmpty(eyes) ? BasePath : eyes;

                case FarmerSlot.Torso: return Worn;
                case FarmerSlot.SleeveLeft: return SleeveOf("arm_left");
                case FarmerSlot.SleeveRight: return SleeveOf("arm_right");

                case FarmerSlot.LegsWear: return Dressed ? outfit : pants;

                // 맨몸은 언제나 같은 곳에서 온다.
                case FarmerSlot.Legs:
                case FarmerSlot.Chest:
                case FarmerSlot.Head:
                case FarmerSlot.ArmLeft:
                case FarmerSlot.ArmRight: return BasePath;

                default: return null;
            }
        }

        private bool Dressed => !string.IsNullOrEmpty(outfit);

        /// <summary>윗도리로 입고 있는 것. 한 벌짜리 옷이 상의보다 이긴다.</summary>
        private string Worn => Dressed ? outfit : top;

        /// <summary>
        /// 이쪽 소매 그림의 경로. 입은 옷이 정한 소매 갈래를 가져다 붙인다.
        /// 소매가 없는 옷이거나 아무것도 안 입었으면 null.
        /// </summary>
        private string SleeveOf(string side)
        {
            var item = FarmerCatalog.Find(Worn);
            if (item == null || string.IsNullOrEmpty(item.sleeve)) return null;
            return SleevePath + side + "_" + item.sleeve;
        }

        /// <summary>이 부위를 어떤 색으로 칠할지.</summary>
        public FarmerTint TintFor(FarmerSlot slot)
        {
            switch (slot)
            {
                case FarmerSlot.Legs:
                case FarmerSlot.Chest:
                case FarmerSlot.Head:
                case FarmerSlot.ArmLeft:
                case FarmerSlot.ArmRight: return skin.ToTint(BaseLut);

                case FarmerSlot.Eyes: return eyeColor.ToTint(EyesLut);

                case FarmerSlot.Hair: return hairColor.ToTint(FarmerArt.LutPathOr(hair, HairLut));

                // 소매 그림은 검정·갈색 두 색뿐이라, 입은 옷에 적힌 색으로 갈아 칠해 쓴다.
                case FarmerSlot.SleeveLeft:
                case FarmerSlot.SleeveRight: return SleeveTint();

                default: return FarmerTint.None;
            }
        }

        private FarmerTint SleeveTint()
        {
            var tint = FarmerTint.None;
            string worn = Worn;
            if (!string.IsNullOrEmpty(worn)) tint.sleevePalette = FarmerArt.PathFor(worn, FarmerSlot.Torso);
            return tint;
        }
    }
}
