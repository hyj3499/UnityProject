using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>애니메이션 한 칸 — 어느 프레임 번호를 몇 밀리초 동안 보여줄지.</summary>
    public struct FarmerKey
    {
        public int frame;
        public int ms;

        public FarmerKey(int frame, int ms) { this.frame = frame; this.ms = ms; }
    }

    /// <summary>한 동작의 한 방향.</summary>
    public class FarmerClip
    {
        public FarmerKey[] keys;
        public bool loop;

        /// <summary>전체 길이(초). 한 번짜리 동작이 언제 끝나는지 재는 데 쓴다.</summary>
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
        Harvest,        // 줍기·수확
        Tool,           // 곡괭이·도끼·괭이 (바위/대지 마법)
        Watering,       // 물뿌리개 (물 마법)
        Melee,          // 낫·검 (칼날 마법)
        FishCast,       // 낚싯줄 던지기
        FishWait,       // 입질 기다리기
        FishReel,       // 릴 감기 (미니게임)
        FishCaught,     // 낚아 올리기
        Eat,            // 먹기 (항상 정면)
        PassOut,        // 기절 (항상 정면)
    }

    /// <summary>
    /// 동작 표. 스타듀밸리 위키의 프레임 번호를 그대로 옮겨 적은 것이라, 새 동작을 넣을 때
    /// 그림을 그리지 않고 <b>번호만 적으면</b> 된다.
    ///
    /// 방향은 정면(Down)·측면(Side)·후면(Up) 세 벌만 있다. 왼쪽은 측면을 좌우로 뒤집어 쓴다
    /// (스타듀밸리도 같은 방식이다 — 오른쪽이 원본, 왼쪽이 뒤집은 것).
    /// </summary>
    public static class FarmerAnimations
    {
        /// <summary>이 동작에서 왼쪽을 볼 때 그림을 뒤집어야 하는지.</summary>
        public static bool Mirrored(Direction dir) => dir == Direction.Left;

        private static readonly Dictionary<FarmerAnim, FarmerClip[]> _clips = Build();

        /// <summary>동작·방향에 맞는 클립. 왼쪽은 측면 클립을 그대로 쓰고 그리기만 뒤집는다.</summary>
        public static FarmerClip Get(FarmerAnim anim, Direction dir)
        {
            var byDir = _clips[anim];
            switch (dir)
            {
                case Direction.Up: return byDir[2];
                case Direction.Left:
                case Direction.Right: return byDir[1];
                default: return byDir[0];
            }
        }

        // ---------- 표 ----------
        private static Dictionary<FarmerAnim, FarmerClip[]> Build()
        {
            var d = new Dictionary<FarmerAnim, FarmerClip[]>();

            void Set(FarmerAnim anim, bool loop, FarmerKey[] down, FarmerKey[] side, FarmerKey[] up)
                => d[anim] = new[]
                {
                    new FarmerClip { keys = down, loop = loop },
                    new FarmerClip { keys = side, loop = loop },
                    new FarmerClip { keys = up,   loop = loop },
                };

            // 서 있기 — 걷기의 첫 프레임을 그대로 쓴다.
            Set(FarmerAnim.Idle, true,
                K(0, 1000), K(6, 1000), K(12, 1000));

            // 걷기: R1F1, R1F2, R1F1, R1F3 …
            Set(FarmerAnim.Walk, true,
                K(0, 200, 1, 200, 0, 200, 2, 200),
                K(6, 200, 7, 200, 6, 200, 8, 200),
                K(12, 200, 13, 200, 12, 200, 14, 200));

            // 뛰기 — 4행의 전용 프레임이 중간에 끼어든다.
            Set(FarmerAnim.Run, true,
                K(0, 90, 1, 60, 18, 120, 1, 60, 0, 90, 2, 60, 19, 120, 2, 60),
                K(6, 90, 20, 140, 11, 100, 6, 90, 21, 140, 17, 100),
                K(12, 90, 13, 60, 22, 120, 13, 60, 12, 90, 14, 60, 23, 120, 14, 60));

            // 수확·줍기
            Set(FarmerAnim.Harvest, false,
                K(54, 100, 55, 100, 56, 100, 57, 100),
                K(58, 100, 59, 100, 60, 100, 61, 100),
                K(62, 100, 63, 100, 64, 100, 65, 100));

            // 도끼·곡괭이·괭이
            Set(FarmerAnim.Tool, false,
                K(66, 150, 67, 40, 68, 40, 69, 170, 70, 75),
                K(48, 100, 49, 40, 50, 40, 51, 220, 52, 75),
                K(36, 100, 37, 40, 38, 40, 63, 220, 62, 75));

            // 물뿌리개
            Set(FarmerAnim.Watering, false,
                K(54, 75, 55, 100, 25, 500),
                K(58, 75, 59, 100, 45, 500),
                K(62, 75, 63, 100, 46, 500));

            // 낫·검
            Set(FarmerAnim.Melee, false,
                K(24, 55, 25, 45, 26, 25, 27, 25, 28, 25, 29, 120),
                K(30, 55, 31, 45, 32, 25, 33, 25, 34, 25, 35, 120),
                K(36, 55, 37, 45, 38, 25, 39, 25, 40, 25, 41, 120));

            // 낚싯줄 던지기
            Set(FarmerAnim.FishCast, false,
                K(66, 100, 67, 40, 68, 40, 69, 80, 70, 200),
                K(48, 100, 49, 40, 50, 40, 51, 80, 52, 200),
                K(76, 100, 38, 40, 63, 40, 62, 80, 63, 200));

            // 입질 기다리기 / 릴 감기 — 한 프레임을 계속 유지한다.
            Set(FarmerAnim.FishWait, true, K(70, 1000), K(89, 1000), K(44, 1000));
            Set(FarmerAnim.FishReel, true, K(66, 1000), K(48, 1000), K(36, 1000));

            // 낚아 올리기 — 옆·뒤를 보고 있었어도 정면으로 돌아 물고기를 든다.
            Set(FarmerAnim.FishCaught, false,
                K(74, 200, 57, 200, 84, 800),
                K(72, 200, 57, 200, 84, 800),
                K(76, 200, 57, 200, 84, 800));

            // 먹기 / 기절 — 언제나 정면이라 세 방향이 같다.
            var eat = K(84, 250, 85, 400, 86, 400, 87, 250, 88, 250, 87, 250, 88, 250, 87, 250);
            Set(FarmerAnim.Eat, false, eat, eat, eat);

            var faint = K(16, 1000, 0, 500, 16, 1000, 4, 200, 5, 3000);
            Set(FarmerAnim.PassOut, false, faint, faint, faint);

            return d;
        }

        /// <summary>(프레임, 밀리초) 짝을 줄줄이 적어 클립 하나를 만든다.</summary>
        private static FarmerKey[] K(params int[] pairs)
        {
            var keys = new FarmerKey[pairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new FarmerKey(pairs[i * 2], pairs[i * 2 + 1]);
            return keys;
        }

        // ---------- 무슨 일을 할 때 무엇을 재생할지 ----------

        /// <summary>마법을 쓸 때의 동작. 하는 일이 같으면 같은 그림을 쓴다.</summary>
        public static FarmerAnim ForMagic(MagicType magic)
        {
            switch (magic)
            {
                case MagicType.Rock: return FarmerAnim.Tool;      // 바위 부수기 = 곡괭이질
                case MagicType.Earth: return FarmerAnim.Tool;     // 밭 갈기 = 괭이질
                case MagicType.Blade: return FarmerAnim.Melee;    // 나무 베기 = 도끼/낫질
                case MagicType.Water: return FarmerAnim.Watering;
                default: return FarmerAnim.Tool;
            }
        }

        public static FarmerAnim ForFishing(FishingState state)
        {
            switch (state)
            {
                case FishingState.Waiting: return FarmerAnim.FishWait;
                case FishingState.Bite: return FarmerAnim.FishWait;
                case FishingState.Minigame: return FarmerAnim.FishReel;
                default: return FarmerAnim.FishWait;
            }
        }

        public static FarmerAnim ForLocomotion(bool moving, bool running)
            => !moving ? FarmerAnim.Idle : running ? FarmerAnim.Run : FarmerAnim.Walk;
    }
}
