using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 걸치는 것이 놓이는 <b>부위 한 칸</b>.
    ///
    /// <b>게임 화면을 그리는 데는 쓰지 않는다.</b> 플레이어는 모든 동작이 합쳐진 시트 한 장으로
    /// 그려지므로(FarmerPoses · FarmerAnimator) 부위를 겹칠 일이 없다. 여기 남아 있는 것은
    /// 캐릭터 만들기 화면이 고를 수 있는 옷·머리 모양을 <b>파일로 찾아가기 위한 이름표</b>다.
    ///
    /// 에셋은 부위마다 가로 한 줄짜리 시트로 나뉘어 있고, 파일 이름이 이 부위 이름으로 끝난다
    /// (Shirts/ribon_<b>shirts</b>.png · Pants/short_<b>pants</b>.png · Hair/Parm_long_<b>hairs</b>.png).
    ///
    /// 소매만 규칙이 다르다. 소매는 옷이 정한 <b>소매 갈래</b>에 따라 Sleeves 폴더에서 가져오므로
    /// (Sleeves/arm_left_<b>overshirts</b>.png), 파일 이름이 옷 이름과 상관없이 정해진다.
    /// </summary>
    public enum FarmerSlot
    {
        Legs,         // 맨다리
        LegsWear,     // 바지

        Chest,        // 맨몸통
        Torso,        // 상의

        Head,         // 머리통
        Eyes,
        Hair,         // 머리카락

        ArmLeft,
        SleeveLeft,
        ArmRight,
        SleeveRight,
    }

    /// <summary>한 부위의 규격 — 파일 이름 끝에 붙는 이름.</summary>
    public struct FarmerSlotInfo
    {
        /// <summary>파일 이름 끝에 붙는 부위 이름. 비어 있으면 경로가 이미 완성된 것이다 (소매).</summary>
        public string suffix;

        /// <summary>
        /// 왼쪽 위 두 픽셀이 그림이 아니라 <b>색 견본</b>인 부위.
        /// 상의가 그렇다 — 첫 픽셀이 소매 본색, 그 옆이 테두리색이다. 그릴 때는 지운다.
        /// </summary>
        public bool paletteHeader;
    }

    public static class FarmerSlots
    {
        /// <summary>걸치는 것 시트의 한 프레임 크기. 플레이어 동작 시트와는 규격이 다르다.</summary>
        public const int FrameWidth = 32, FrameHeight = 48;

        /// <summary>그림 몇 픽셀을 한 칸으로 볼지. 타일과 같은 자로 재야 크기가 맞는다.</summary>
        public const float PixelsPerUnit = 16f;

        public static readonly FarmerSlot[] All =
            (FarmerSlot[])System.Enum.GetValues(typeof(FarmerSlot));

        private static readonly Dictionary<FarmerSlot, FarmerSlotInfo> _info = Build();

        public static FarmerSlotInfo Info(FarmerSlot slot) => _info[slot];

        /// <summary>파일 이름 끝이 이 이름인 부위. 없으면 false.</summary>
        public static bool BySuffix(string suffix, out FarmerSlot slot)
        {
            foreach (var s in All)
            {
                var info = Info(s);
                if (!string.IsNullOrEmpty(info.suffix) && info.suffix == suffix) { slot = s; return true; }
            }
            slot = default;
            return false;
        }

        private static Dictionary<FarmerSlot, FarmerSlotInfo> Build()
        {
            var d = new Dictionary<FarmerSlot, FarmerSlotInfo>();

            void Set(FarmerSlot slot, string suffix, bool paletteHeader = false)
                => d[slot] = new FarmerSlotInfo { suffix = suffix, paletteHeader = paletteHeader };

            Set(FarmerSlot.Legs, "legs");
            Set(FarmerSlot.LegsWear, "pants");

            Set(FarmerSlot.Chest, "chest");
            Set(FarmerSlot.Torso, "shirts", paletteHeader: true);

            Set(FarmerSlot.Head, "head");
            Set(FarmerSlot.Eyes, "eyes");
            Set(FarmerSlot.Hair, "hairs");

            Set(FarmerSlot.ArmLeft, "arm_left");
            Set(FarmerSlot.ArmRight, "arm_right");

            // 소매는 옷 이름이 아니라 소매 갈래로 경로가 정해지므로 끝에 붙일 이름이 없다.
            Set(FarmerSlot.SleeveLeft, "");
            Set(FarmerSlot.SleeveRight, "");

            return d;
        }
    }
}
