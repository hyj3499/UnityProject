using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>그림이 그려져 있는 방향. 왼쪽은 측면을 좌우로 뒤집어 쓰므로 세 가지뿐이다.</summary>
    public enum FarmerFacing { Down, Side, Up }

    /// <summary>
    /// 한 동작의 한 방향 — 시트의 <b>어느 줄</b>을 <b>몇 칸</b>, <b>얼마씩</b> 넘길지.
    ///
    /// 한 줄이 곧 한 동작이고 칸은 왼쪽부터 차례로 넘어간다. 칸마다 다른 시간을 주던 예전
    /// 동작표와 달리 지금은 한 줄이 같은 박자로 넘어가므로 시간이 하나뿐이다.
    /// </summary>
    public class FarmerClip
    {
        public int row;      // 시트의 몇 번째 줄 (맨 윗줄이 0번)
        public int count;    // 그 줄에 그려져 있는 칸 수
        public int ms;       // 한 칸을 얼마 동안 보여 줄지
        public bool loop;

        public float Duration => count * ms / 1000f;
    }

    /// <summary>플레이어가 취하는 동작.</summary>
    public enum FarmerAnim
    {
        Idle,
        Walk,
        Run,
        Interact,       // 손을 뻗어 만지는 동작
        Jump,
        Carry,          // 무언가를 들고 있는 자세
        Harvest,        // 줍기·수확
        Tool,           // 곡괭이·괭이 (바위/대지 마법)
        Watering,       // 물뿌리개 (물 마법)
        Melee,          // 낫·도끼 (칼날 마법)
        FishCast,
        FishWait,
        FishReel,
        FishCaught,
    }

    /// <summary>
    /// 동작표. <b>여기 적힌 줄과 박자가 화면에 나오는 그림을 그대로 정한다.</b>
    ///
    /// 그림은 부위별로 나뉘어 있지 않다. <b>Base/base_animations.png</b> 한 장에 모든 동작이
    /// 이미 합쳐진 채로 들어 있고, 한 칸이 곧 한 장면이다. 예전처럼 다리·팔·머리를 따로 불러
    /// 겹쳐 그리지 않으므로 부위를 맞춰 줄 일도, 칸 번호를 부위마다 따로 적을 일도 없다.
    ///
    /// 시트는 <b>한 줄에 한 태그</b>씩, 아래 차례로 놓여 있다 (가로 여섯 칸 자리 중 앞에서부터
    /// 쓰고 남는 칸은 비어 있다). 줄 차례는 <see cref="Sheet"/> × <see cref="FarmerFacing"/>다.
    ///
    ///    줄  태그             칸
    ///     0  Idle_Down         4      3  Walk_Down     4      6  Run_Down       6
    ///     1  Idle_Side         4      4  Walk_Side     4      7  Run_Side       6
    ///     2  Idle_Up           4      5  Walk_Up       4      8  Run_Up         6
    ///     9  Interact_Down     4     12  Jump_Down     5
    ///    10  Interact_Side     4     13  Jump_Side     5
    ///    11  Interact_Up       4     14  Jump_Up       5
    ///
    /// 왼쪽을 볼 때는 측면 줄을 좌우로 뒤집어 쓴다.
    ///
    /// 일감 동작(수확·연장·낚시)은 아직 전용 줄이 없어 만지기·뛰기 줄을 빌려 쓴다.
    /// </summary>
    public static class FarmerPoses
    {
        /// <summary>시트가 있는 곳 (Resources 아래 경로, 확장자는 뺀다).</summary>
        public const string SheetPath = "Sprites/Farmer/Base/base_animations";

        /// <summary>시트 한 칸의 크기.</summary>
        public const int FrameWidth = 32, FrameHeight = 32;

        /// <summary>한 줄에 놓인 칸 자리 수. 줄마다 실제로 쓰는 칸은 이보다 적을 수 있다.</summary>
        public const int Columns = 6;

        /// <summary>그림 몇 픽셀을 한 칸으로 볼지. 타일과 같은 자로 재야 크기가 맞는다.</summary>
        public const float PixelsPerUnit = 16f;

        /// <summary>
        /// 칸 한가운데에서 발바닥까지의 거리(픽셀). 캐릭터를 타일 바닥에 세울 때 쓴다.
        /// 이 시트는 발이 칸 맨 아랫줄에 닿아 있으므로 칸 높이의 절반이다.
        /// </summary>
        public const int FeetBelowCenter = FrameHeight / 2;

        // =================================================================================
        //  동작표 — 고칠 곳은 여기뿐이다
        // =================================================================================

        /// <summary>시트에 실제로 그려져 있는 동작. <b>이 차례가 곧 줄 차례</b>다.</summary>
        private enum Sheet { Idle, Walk, Run, Interact, Jump }

        /// <summary>줄마다 그려져 있는 칸 수. 방향이 달라도 같다.</summary>
        private static readonly int[] Frames = { 4, 4, 6, 4, 5 };

        /// <summary>한 칸을 얼마 동안 보여 줄지. 빠르게·느리게 하려면 이 숫자만 고치면 된다.</summary>
        private static readonly int[] Millis = { 200, 140, 90, 90, 100 };

        /// <summary>줄이 되풀이되는 것인지. 한 번짜리 줄은 마지막 칸에서 멈춘다.</summary>
        private static readonly bool[] Loops = { true, true, true, false, false };

        /// <summary>
        /// 이 동작이 끝나고 처음으로 돌아갈지.
        ///
        /// 보통은 줄이 정하지만, <b>상태가 유지되는 동안 계속 재생하는 동작</b>은 빌려 온 줄이
        /// 한 번짜리라도 되풀이해야 한다. 낚싯줄 감기가 그렇다 — 한 번 재생하고 멈춰 버리면
        /// 물고기와 씨름하는 내내 마지막 칸에 굳어 있게 된다.
        /// </summary>
        private static bool LoopFor(FarmerAnim anim, Sheet sheet)
            => Loops[(int)sheet] || anim == FarmerAnim.FishReel;

        /// <summary>이 동작을 어느 줄로 그릴지. 전용 줄이 없는 것은 비슷한 줄을 빌려 쓴다.</summary>
        private static Sheet SheetFor(FarmerAnim anim)
        {
            switch (anim)
            {
                case FarmerAnim.Walk: return Sheet.Walk;
                case FarmerAnim.Run: return Sheet.Run;

                // 폴짝 뛰는 동작 — 물고기를 낚아 올렸을 때도 이걸 쓴다.
                case FarmerAnim.Jump:
                case FarmerAnim.FishCaught: return Sheet.Jump;

                // 손을 뻗는 동작 — 수확·연장질·물주기·휘두르기·낚싯대 던지기가 모두 여기 얹힌다.
                case FarmerAnim.Interact:
                case FarmerAnim.Harvest:
                case FarmerAnim.Tool:
                case FarmerAnim.Watering:
                case FarmerAnim.Melee:
                case FarmerAnim.FishCast:
                case FarmerAnim.FishReel: return Sheet.Interact;

                // 서 있기 — 무언가 들고 있을 때와 입질을 기다릴 때도 가만히 서 있는다.
                default: return Sheet.Idle;
            }
        }

        // ---------- 위에서 만들어지는 것 ----------

        /// <summary>이 방향을 볼 때 그림을 좌우로 뒤집어야 하는지.</summary>
        public static bool Mirrored(Direction dir) => dir == Direction.Left;

        public static FarmerFacing Facing(Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return FarmerFacing.Up;
                case Direction.Left:
                case Direction.Right: return FarmerFacing.Side;
                default: return FarmerFacing.Down;
            }
        }

        /// <summary>시트에서 이 동작·방향이 놓인 줄. 태그를 놓은 차례 그대로다.</summary>
        public static int Row(FarmerAnim anim, FarmerFacing facing)
            => (int)SheetFor(anim) * 3 + (int)facing;

        /// <summary>시트에 들어 있는 줄 수.</summary>
        public static int RowCount => System.Enum.GetValues(typeof(Sheet)).Length * 3;

        private static readonly Dictionary<FarmerAnim, FarmerClip[]> _clips = BuildClips();

        public static FarmerClip Get(FarmerAnim anim, Direction dir)
            => _clips[anim][(int)Facing(dir)];

        private static Dictionary<FarmerAnim, FarmerClip[]> BuildClips()
        {
            var d = new Dictionary<FarmerAnim, FarmerClip[]>();

            foreach (FarmerAnim anim in System.Enum.GetValues(typeof(FarmerAnim)))
            {
                int sheet = (int)SheetFor(anim);
                var clips = new FarmerClip[3];

                for (int facing = 0; facing < clips.Length; facing++)
                    clips[facing] = new FarmerClip
                    {
                        row = sheet * 3 + facing,
                        count = Frames[sheet],
                        ms = Millis[sheet],
                        loop = LoopFor(anim, (Sheet)sheet),
                    };

                d[anim] = clips;
            }
            return d;
        }

        // ---------- 무슨 일을 할 때 무엇을 재생할지 ----------

        public static FarmerAnim ForMagic(MagicType magic)
        {
            switch (magic)
            {
                case MagicType.Rock: return FarmerAnim.Tool;
                case MagicType.Earth: return FarmerAnim.Tool;
                case MagicType.Blade: return FarmerAnim.Melee;
                case MagicType.Wind: return FarmerAnim.Melee;   // 휘둘러 베는 동작
                case MagicType.Water: return FarmerAnim.Watering;
                default: return FarmerAnim.Tool;
            }
        }

        public static FarmerAnim ForFishing(FishingState state)
        {
            switch (state)
            {
                case FishingState.Minigame: return FarmerAnim.FishReel;
                default: return FarmerAnim.FishWait;
            }
        }

        public static FarmerAnim ForLocomotion(bool moving, bool running)
            => !moving ? FarmerAnim.Idle : running ? FarmerAnim.Run : FarmerAnim.Walk;
    }
}
