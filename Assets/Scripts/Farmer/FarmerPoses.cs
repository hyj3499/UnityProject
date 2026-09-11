using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>그림이 그려져 있는 방향. 왼쪽은 측면을 좌우로 뒤집어 쓰므로 세 가지뿐이다.</summary>
    public enum FarmerFacing { Down, Side, Up }

    /// <summary>
    /// 동작의 한 칸 — <b>얼마 동안</b> 그리고 <b>부위마다 몇 번 그림</b>을 쓸지.
    ///
    /// 값의 뜻:
    ///   · 0 이상 = 그 시트의 그 번째 그림 (첫 장이 0번)
    ///   · <b>-1</b> = 그리지 않음 (뒤를 볼 때의 눈처럼)
    ///
    /// 부위마다 그림 장수가 다를 수 있다. 없는 번호를 적으면 그 부위만 안 그려진다.
    /// 지금 맨몸은 머리통·몸통이 한 장뿐이고(정면 고정), 다리·팔·머리카락이 석 장이다
    /// (0번 모은 자세, 1·2번 걷는 자세).
    /// </summary>
    public struct FarmerKey
    {
        public int ms;

        public int eyes;      // 눈 (0 뜬 눈, 1 감은 눈)
        public int legs;      // 맨다리 + 바지
        public int armL;      // 왼팔 + 왼소매
        public int armR;      // 오른팔 + 오른소매
        public int hair;      // 머리카락
        public int chest;     // 맨몸통
        public int head;      // 머리통

        /// <summary>
        /// 이 칸에서 <b>몸 전체</b>를 몇 픽셀 띄울지 — 다리까지 통째로 땅에서 떠오른다.
        ///
        /// 몸이 한 덩어리로 움직이니 이음매가 벌어질 일이 없어 <b>그림을 고치지 않아도 된다</b>.
        /// 발이 땅에서 떨어지므로 폴짝 뛰어오르는 느낌이 난다.
        /// </summary>
        public int hop;

        /// <summary>
        /// 이 칸에서 <b>상체만</b> 몇 픽셀 띄울지 — 다리는 바닥에 둔 채 허리가 늘어난다.
        /// 머리에 붙은 눈·머리카락은 머리가 움직인 만큼 그대로 따라간다.
        ///
        /// 늘어난 만큼 다리 그림 위쪽이 드러나는데, 그 자리는 이미 골반으로 칠해져 있다
        /// (평소엔 몸통에 가려 보이지 않는다). 층을 붙여 맞추는 일은 없고 그려진 자리 그대로
        /// 겹치므로, 여기 적은 픽셀만큼만 상체가 오르내린다. 음수면 반대로 내려앉는다.
        /// </summary>
        public int bounce;

        public const int Hidden = -1;

        /// <summary>이 칸에서 그 부위가 쓸 그림 번호. 음수면 그리지 않는다는 뜻이다.</summary>
        public int FrameFor(FarmerPart part)
        {
            switch (part)
            {
                case FarmerPart.Face: return eyes;
                case FarmerPart.Legs: return legs;
                case FarmerPart.ArmLeft: return armL;
                case FarmerPart.ArmRight: return armR;
                case FarmerPart.Hair: return hair;
                case FarmerPart.Chest: return chest;
                case FarmerPart.Head: return head;
                default: return Hidden;
            }
        }
    }

    /// <summary>한 동작의 한 방향.</summary>
    public class FarmerClip
    {
        public FarmerKey[] keys;
        public bool loop;

        public float Duration
        {
            get
            {
                int total = 0;
                foreach (var k in keys) total += k.ms;
                return total / 1000f;
            }
        }
    }

    /// <summary>플레이어가 취하는 동작.</summary>
    public enum FarmerAnim
    {
        Idle,
        Walk,
        Run,
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
    /// 동작표. <b>여기 적힌 숫자가 화면에 나오는 그림을 그대로 정한다.</b>
    ///
    /// 부위마다 시트가 따로 있고 장수도 제각각이라, 칸 하나에 부위별 번호를 나란히 적는다.
    /// 팔만 다른 그림을 쓰고 싶으면 그 칸의 armL/armR만 바꾸면 되고, 다른 부위는 그대로 있다.
    ///
    /// 맨몸 그림이 <b>정면뿐</b>이라 세 방향이 같은 번호를 쓴다(Flat). 옆·뒤 그림을 그려 넣으면
    /// 그때 방향마다 다른 줄을 적어 주면 된다. 상의만은 방향별 그림이 따로 있어서 동작표가
    /// 아니라 바라보는 방향이 번호를 정한다(FacingFrame).
    ///
    /// 일감 동작(수확·연장·낚시)은 아직 전용 그림이 없어 서기·걷기 그림을 빌려 쓴다.
    /// </summary>
    public static class FarmerPoses
    {
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

        /// <summary>
        /// 방향마다 그림이 따로 있는 부위(상의)가 쓸 번호.
        /// 그림이 앞·오른쪽·왼쪽·뒤 차례로 넉 장 들어 있다.
        /// </summary>
        public static int FacingFrame(Direction dir)
        {
            switch (dir)
            {
                case Direction.Right: return 1;
                case Direction.Left: return 2;
                case Direction.Up: return 3;
                default: return 0;
            }
        }

        private static readonly Dictionary<FarmerAnim, FarmerClip[]> _clips = BuildClips();

        public static FarmerClip Get(FarmerAnim anim, Direction dir)
            => _clips[anim][(int)Facing(dir)];

        // =================================================================================
        //  동작표 — 고칠 곳은 여기뿐이다
        // =================================================================================
        private static Dictionary<FarmerAnim, FarmerClip[]> BuildClips()
        {
            var d = new Dictionary<FarmerAnim, FarmerClip[]>();

            // 세 방향이 같은 그림을 쓴다. 뒤를 볼 때만 눈을 지운다.
            void Flat(FarmerAnim anim, bool loop, params FarmerKey[] keys)
            {
                var clips = new[] { Clip(keys), Clip(keys), Clip(NoEyes(keys)) };
                foreach (var c in clips) c.loop = loop;
                d[anim] = clips;
            }

            // 서 있기 — 가만히 있다가 이따금 한 번 깜빡인다.
            Flat(FarmerAnim.Idle, loop: true,
                K(2800, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0),
                K( 120, eyes: 1, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            // 걷기 — 0번(모은 자세)과 1·2번(내딛는 자세)을 번갈아 밟는다.
            //
            // 한 걸음은 두 박이다. 다리가 몸 아래로 모이는 칸은 <b>뜨는 박</b>이라 hop을,
            // 발을 내디뎌 다리가 뻗는 칸은 <b>딛는 박</b>이라 bounce를 준다.
            // 뜰 땐 몸이 통째로 오르고, 디딜 땐 발이 땅에 붙은 채 허리만 늘어난다.
            // (bounce를 음수로 바꾸면 늘어나는 대신 내려앉는 무거운 걸음이 된다.)
            Flat(FarmerAnim.Walk, loop: true,
                K( 180, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0, hop: 1, bounce: 0),
                K( 180, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0, hop: 0, bounce: -1),
                K( 180, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0, hop: 1, bounce: 0),
                K( 180, eyes: 0, legs: 2, armL: 2, armR: 2, hair: 2, chest: 0, head: 0, hop: 0, bounce: -1));

            // 뛰기 — 걷기와 같은 박자로, 빠르고 더 크게 튄다.
            Flat(FarmerAnim.Run, loop: true,
                K( 110, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0, hop: 2, bounce: 0),
                K( 110, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0, hop: 0, bounce: -2),
                K( 110, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0, hop: 2, bounce: 0),
                K( 110, eyes: 0, legs: 2, armL: 2, armR: 2, hair: 2, chest: 0, head: 0, hop: 0, bounce: -2));

            // ---- 아래는 아직 전용 그림이 없어 서기·걷기 그림을 빌려 쓴다 ----

            Flat(FarmerAnim.Carry, loop: true,
                K(1000, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            Flat(FarmerAnim.Harvest, loop: false,
                K( 140, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0, bounce: 1),
                K( 260, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            Flat(FarmerAnim.Tool, loop: false,
                K( 130, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0, hop: 1),
                K( 220, eyes: 0, legs: 2, armL: 2, armR: 2, hair: 2, chest: 0, head: 0));

            Flat(FarmerAnim.Watering, loop: false,
                K( 120, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0),
                K( 420, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            Flat(FarmerAnim.Melee, loop: false,
                K(  90, eyes: 0, legs: 2, armL: 2, armR: 2, hair: 2, chest: 0, head: 0, hop: 1),
                K( 150, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0));

            Flat(FarmerAnim.FishCast, loop: false,
                K( 120, eyes: 0, legs: 1, armL: 1, armR: 1, hair: 1, chest: 0, head: 0),
                K( 220, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            Flat(FarmerAnim.FishWait, loop: true,
                K(1000, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            Flat(FarmerAnim.FishReel, loop: true,
                K( 220, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0),
                K( 220, eyes: 0, legs: 0, armL: 1, armR: 1, hair: 0, chest: 0, head: 0));

            Flat(FarmerAnim.FishCaught, loop: false,
                K( 200, eyes: 0, legs: 2, armL: 2, armR: 2, hair: 2, chest: 0, head: 0, hop: 2),
                K( 700, eyes: 0, legs: 0, armL: 0, armR: 0, hair: 0, chest: 0, head: 0));

            return d;
        }

        private static FarmerClip Clip(FarmerKey[] keys) => new FarmerClip { keys = keys };

        /// <summary>뒤를 볼 때 쓸 칸들. 눈만 지운 사본이다.</summary>
        private static FarmerKey[] NoEyes(FarmerKey[] keys)
        {
            var copy = (FarmerKey[])keys.Clone();
            for (int i = 0; i < copy.Length; i++) copy[i].eyes = FarmerKey.Hidden;
            return copy;
        }

        private static FarmerKey K(int ms, int eyes, int legs, int armL, int armR, int hair,
                                   int chest, int head, int hop = 0, int bounce = 0)
            => new FarmerKey
            {
                ms = ms, eyes = eyes, legs = legs, armL = armL, armR = armR, hair = hair,
                chest = chest, head = head, hop = hop, bounce = bounce,
            };

        // ---------- 무슨 일을 할 때 무엇을 재생할지 ----------

        public static FarmerAnim ForMagic(MagicType magic)
        {
            switch (magic)
            {
                case MagicType.Rock: return FarmerAnim.Tool;
                case MagicType.Earth: return FarmerAnim.Tool;
                case MagicType.Blade: return FarmerAnim.Melee;
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
