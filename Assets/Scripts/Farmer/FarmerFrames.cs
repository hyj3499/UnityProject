namespace FarmMVP
{
    /// <summary>몸이 그려져 있는 방향. 왼쪽은 오른쪽을 뒤집어 쓰므로 세 가지뿐이다.</summary>
    public enum FarmerFacing { Down, Right, Up }

    /// <summary>
    /// 한 프레임에서 <b>머리가 어디에 있는지</b>. 셔츠·머리카락·모자·장신구는 몸에 그려져 있지
    /// 않고 따로 얹는 것이라, 프레임마다 몸이 흔들린 만큼 같이 옮겨 줘야 자연스럽다.
    /// </summary>
    public struct FarmerFrameInfo
    {
        public FarmerFacing facing;
        public sbyte dx, dy;   // 그 방향의 기준 프레임에서 얼마나 옮겨졌는지 (픽셀)
    }

    /// <summary>
    /// 프레임 번호 -> 방향과 흔들림. 126칸짜리 표다.
    ///
    /// 값은 손으로 적은 것이 아니라 <b>시트에서 재서</b> 넣었다. 기준 프레임(정면 0 / 측면 6 /
    /// 후면 12)의 머리 모양을 본떠 각 프레임 위에 겹쳐 보고, 가장 잘 맞는 자리를 찾은 결과다.
    /// 126칸 거의 전부가 정확히 일치했다.
    /// </summary>
    public static class FarmerFrames
    {
        // 표를 한눈에 읽히게 두려고 FarmerFacing 값을 이름만으로 쓴다 (Down / Right / Up).
        private const FarmerFacing Down = FarmerFacing.Down;
        private const FarmerFacing Right = FarmerFacing.Right;
        private const FarmerFacing Up = FarmerFacing.Up;

        public const int Count = 126;

        private static FarmerFrameInfo F(FarmerFacing facing, int dx, int dy)
            => new FarmerFrameInfo { facing = facing, dx = (sbyte)dx, dy = (sbyte)dy };

        private static readonly FarmerFrameInfo[] Table =
        {
            F(Down,  0,  0), F(Down,  0,  1), F(Down,  0,  1), F(Down,  0, -1), F(Down,  0,  4), F(Down,  0,  5),   // 0~5
            F(Right,  0,  0), F(Right,  0,  1), F(Right,  0,  1), F(Right,  0,  0), F(Down,  0, -1), F(Right,  0,  1),   // 6~11
            F(Up,    0,  0), F(Up,    0,  1), F(Up,    0,  1), F(Up,    0,  0), F(Down,  0,  1), F(Right,  0,  1),   // 12~17
            F(Down,  0,  2), F(Down,  0,  2), F(Right,  0,  1), F(Right,  0,  1), F(Up,    0,  1), F(Up,    0,  1),   // 18~23
            F(Down,  0, -1), F(Down,  0, -1), F(Down,  0,  1), F(Down,  0,  1), F(Down,  0,  3), F(Down,  0,  3),   // 24~29
            F(Right,  0, -1), F(Right,  0, -1), F(Right,  0,  0), F(Right,  0,  1), F(Right,  0,  0), F(Right,  0,  0),   // 30~35
            F(Up,    0,  1), F(Up,    0,  1), F(Up,    0,  0), F(Up,    0,  0), F(Up,    0,  1), F(Up,    0,  1),   // 36~41
            F(Down,  0,  0), F(Right,  0, -1), F(Up,    0,  0), F(Right,  0, -3), F(Up,    0, -1), F(Down,  0,  0),   // 42~47
            F(Right, -1,  0), F(Right,  0, -1), F(Right,  0, -2), F(Right,  0, -3), F(Right,  0, -2), F(Up,    0, -1),   // 48~53
            F(Down,  0,  4), F(Down,  0,  3), F(Down,  0, -1), F(Down,  0, -1), F(Right,  0,  2), F(Right, -1,  1),   // 54~59
            F(Right,  0, -2), F(Right,  0, -1), F(Up,    0,  4), F(Up,    0,  2), F(Up,    0,  0), F(Up,    0,  0),   // 60~65
            F(Down,  0,  1), F(Down,  0,  0), F(Down,  0, -1), F(Down,  0, -2), F(Down,  0,  0), F(Up,    0, -2),   // 66~71
            F(Right, -1, -1), F(Right, -1, -1), F(Down,  0,  0), F(Down,  0,  0), F(Up,    0,  1), F(Up,    0,  1),   // 72~77
            F(Down,  0,  0), F(Down,  0,  0), F(Right,  0, -1), F(Right,  0, -1), F(Up,    0,  0), F(Up,    0,  0),   // 78~83
            F(Down,  0,  0), F(Down,  0, -2), F(Down,  0, -3), F(Down,  0, -3), F(Down,  0, -3), F(Right,  0,  0),   // 84~89
            F(Down,  0,  0), F(Down,  0, -1), F(Down,  0, -2), F(Down,  0, -2), F(Down,  0, -1), F(Down,  0,  3),   // 90~95
            F(Down,  0,  0), F(Right,  0, -1), F(Down,  0, -1), F(Down,  0,  0), F(Down,  0, -1), F(Right,  4, -1),   // 96~101
            F(Down,  0,  0), F(Down,  0, -1), F(Down,  0,  0), F(Down,  0,  1), F(Right, -1, -4), F(Down,  0, -5),   // 102~107
            F(Down,  0, -2), F(Down,  0, -1), F(Down,  0, -1), F(Right,  0,  1), F(Right,  0,  0), F(Up,    0, -4),   // 108~113
            F(Right,  0, -2), F(Right,  0, -1), F(Right,  0, -1), F(Right, -1, -4), F(Down,  0,  0), F(Down,  0,  0),   // 114~119
            F(Up,    0, -1), F(Up,    0,  0), F(Up,    0,  0), F(Down,  0,  1), F(Down,  0,  0), F(Down,  0,  0),   // 120~125
        };

        public static FarmerFrameInfo Info(int frame)
            => frame >= 0 && frame < Table.Length ? Table[frame] : Table[0];
    }
}
