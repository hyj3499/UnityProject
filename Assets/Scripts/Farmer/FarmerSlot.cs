using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 이루는 <b>부위 한 칸</b>. 값의 순서가 곧 겹쳐 그리는 순서다 (앞일수록 뒤쪽).
    ///
    /// 에셋은 부위마다 가로 한 줄짜리 시트로 나뉘어 있고, 파일 이름이 이 부위 이름으로 끝난다.
    /// 맨몸은 이름이 부위 그대로고(Base/<b>chest</b>.png), 걸치는 것은 이름 뒤에 붙는다
    /// (Shirts/ribon_<b>shirts</b>.png · Pants/short_<b>pants</b>.png · Hair/Parm_long_<b>hairs</b>.png).
    ///
    /// 소매만 규칙이 다르다. 소매는 옷이 정한 <b>소매 갈래</b>에 따라 Sleeves 폴더에서 가져오므로
    /// (Sleeves/arm_left_<b>overshirts</b>.png), 파일 이름이 옷 이름과 상관없이 정해진다.
    /// </summary>
    public enum FarmerSlot
    {
        Legs,         // 맨다리 — 맨 뒤
        LegsWear,     // 바지

        Chest,        // 맨몸통
        Torso,        // 상의

        Head,         // 머리통
        Eyes,
        Hair,         // 머리카락 — 눈 위로 덮는다 (앞머리)

        ArmLeft,
        SleeveLeft,
        ArmRight,
        SleeveRight,  // 팔이 맨 위 — 소매가 팔 동작을 그대로 따라간다
    }

    /// <summary>
    /// 동작표에서 프레임 번호를 <b>하나로 묶어 지정하는 단위</b>.
    ///
    /// 걸치는 것은 맨몸 위에 덧그리는 것이라 늘 같은 프레임이어야 한다 — 바지는 다리를,
    /// 소매는 팔을 따라간다. 그래서 실제로 골라야 하는 것은 이 일곱 가지뿐이다.
    /// </summary>
    public enum FarmerPart
    {
        Legs,       // 맨다리 + 바지
        Chest,      // 맨몸통
        Head,       // 머리통
        Face,       // 눈 — 동작표의 eyes
        Hair,       // 머리카락
        ArmLeft,    // 왼팔 + 왼소매
        ArmRight,   // 오른팔 + 오른소매
    }

    /// <summary>그 부위가 시트의 <b>몇 번째 칸</b>을 쓸지 무엇이 정하는지.</summary>
    public enum FarmerFrameSource
    {
        /// <summary>동작표가 정한다. 서기·걷기처럼 시간에 따라 넘어간다.</summary>
        Pose,

        /// <summary>
        /// 바라보는 방향이 정한다 (앞0·오른쪽1·왼쪽2·뒤3).
        /// 방향마다 그림이 따로 있으므로 좌우로 뒤집지 않는다.
        /// </summary>
        Facing,
    }

    /// <summary>한 부위의 규격 — 파일 이름 끝에 붙는 이름과, 프레임 번호를 어디서 가져올지.</summary>
    public struct FarmerSlotInfo
    {
        /// <summary>파일 이름 끝에 붙는 부위 이름. 비어 있으면 경로가 이미 완성된 것이다 (소매).</summary>
        public string suffix;

        /// <summary>동작표에서 이 부위의 프레임 번호를 어느 칸에서 가져올지.</summary>
        public FarmerPart part;

        /// <summary>프레임 번호를 동작표에서 가져올지, 바라보는 방향에서 가져올지.</summary>
        public FarmerFrameSource frames;

        /// <summary>
        /// 왼쪽 위 두 픽셀이 그림이 아니라 <b>색 견본</b>인 부위.
        /// 상의가 그렇다 — 첫 픽셀이 소매 본색, 그 옆이 테두리색이다. 그릴 때는 지운다.
        /// </summary>
        public bool paletteHeader;
    }

    public static class FarmerSlots
    {
        /// <summary>
        /// 한 프레임의 크기.
        ///
        /// 부위마다 그림 파일이 따로 있어도 <b>전부 이 크기의 같은 칸</b>에, 서로 자리가 맞도록
        /// 그려져 있다 (한 캔버스에 통째로 그린 다음 부위별로 떼어 낸 것이다). 그래서 겹쳐
        /// 놓기만 하면 몸이 된다 — 부위마다 위치를 따로 잡아 줄 일이 없다.
        /// </summary>
        public const int FrameWidth = 32, FrameHeight = 48;

        /// <summary>그림 몇 픽셀을 한 칸으로 볼지. 타일과 같은 자로 재야 크기가 맞는다.</summary>
        public const float PixelsPerUnit = 16f;

        /// <summary>칸 한가운데에서 발바닥까지의 거리(픽셀). 캐릭터를 칸 바닥에 세울 때 쓴다.</summary>
        public const int FeetBelowCenter = 14;

        public static readonly FarmerSlot[] DrawOrder =
            (FarmerSlot[])System.Enum.GetValues(typeof(FarmerSlot));

        private static readonly Dictionary<FarmerSlot, FarmerSlotInfo> _info = Build();

        public static FarmerSlotInfo Info(FarmerSlot slot) => _info[slot];

        /// <summary>파일 이름 끝이 이 이름인 부위. 없으면 false.</summary>
        public static bool BySuffix(string suffix, out FarmerSlot slot)
        {
            foreach (var s in DrawOrder)
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

            void Set(FarmerSlot slot, string suffix, FarmerPart part,
                     FarmerFrameSource frames = FarmerFrameSource.Pose, bool paletteHeader = false)
                => d[slot] = new FarmerSlotInfo
                {
                    suffix = suffix, part = part, frames = frames, paletteHeader = paletteHeader,
                };

            Set(FarmerSlot.Legs, "legs", FarmerPart.Legs);
            Set(FarmerSlot.LegsWear, "pants", FarmerPart.Legs);

            Set(FarmerSlot.Chest, "chest", FarmerPart.Chest);
            // 상의는 방향마다 그림이 따로 있다 (앞·오른쪽·왼쪽·뒤 넉 장).
            Set(FarmerSlot.Torso, "shirts", FarmerPart.Chest, FarmerFrameSource.Facing, paletteHeader: true);

            Set(FarmerSlot.Head, "head", FarmerPart.Head);
            Set(FarmerSlot.Eyes, "eyes", FarmerPart.Face);
            Set(FarmerSlot.Hair, "hairs", FarmerPart.Hair);

            Set(FarmerSlot.ArmLeft, "arm_left", FarmerPart.ArmLeft);
            Set(FarmerSlot.ArmRight, "arm_right", FarmerPart.ArmRight);

            // 소매는 옷 이름이 아니라 소매 갈래로 경로가 정해지므로 끝에 붙일 이름이 없다.
            Set(FarmerSlot.SleeveLeft, "", FarmerPart.ArmLeft);
            Set(FarmerSlot.SleeveRight, "", FarmerPart.ArmRight);

            return d;
        }
    }
}
