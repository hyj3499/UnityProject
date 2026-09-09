namespace FarmMVP
{
    /// <summary>
    /// 플레이어가 취할 수 있는 동작. Resources/Sprites/Player 아래의 폴더 하나와 짝을 이룬다
    /// (Backup(no_use) 제외 열일곱 가지 전부).
    /// </summary>
    public enum PlayerAnim
    {
        Idle,
        Walk,
        Run,

        CarryIdle,
        CarryWalk,
        CarryRun,
        CarryPickUp,
        CarryThrow,

        Pickaxe,      // 바위마법 · 곡괭이 · 호미 · 잠자리채가 함께 쓴다
        Shovel,       // 대지마법(밭 갈기)
        Watering,     // 물마법(작물에 물 주기)
        AxeSickle,    // 칼날마법(나무 베기 · 풀 베기)

        FishCast,
        FishWait,
        FishBite,
        FishReel,
        FishCatch,
    }

    /// <summary>동작 하나의 재생 정보. 프레임 수는 그림에서 재므로 여기 적지 않는다.</summary>
    public class PlayerAnimDef
    {
        /// <summary>Resources/Sprites/Player 아래의 폴더 이름.</summary>
        public string folder;

        /// <summary>초당 프레임 수.</summary>
        public float fps = 10f;

        /// <summary>끝까지 가면 처음으로 돌아갈지. false면 한 번만 재생하고 멈춘다.</summary>
        public bool loop = true;

        /// <summary>같이 그릴 도구 그림 (동작 폴더 안의 상대 경로). 비우면 도구 없음.</summary>
        public string weapon;

        /// <summary>한 번만 재생되는 동작인지 (한 번 끝나면 걷기/서기로 돌아간다).</summary>
        public bool OneShot => !loop;
    }

    /// <summary>
    /// 동작 카탈로그. "무슨 일을 할 때 어떤 그림을 쓸지"는 전부 여기 한 표에 모여 있다 —
    /// 그래서 비슷한 일에 같은 동작을 붙이는 것도 한 줄 고치면 된다.
    ///
    /// 도구 그림은 동작 폴더의 Weapons 아래에 있고, 같은 동작이라도 무엇을 들었느냐에 따라
    /// 달라진다 (곡괭이 동작은 곡괭이·호미·잠자리채가 나눠 쓴다). 그래서 표에는 <b>기본값</b>만
    /// 두고, 부르는 쪽에서 바꿔 넣을 수 있게 했다.
    /// </summary>
    public static class PlayerAnimations
    {
        // 도구 그림 경로 (동작 폴더 기준). 도구 시트는 1~10 등급이 있고 지금은 10만 들어 있다.
        public const string WeaponPickaxe = "Weapons/Pickaxe/10";
        public const string WeaponHoe = "Weapons/Hoe/10";
        public const string WeaponBugNet = "Weapons/Bug net";
        public const string WeaponShovel = "Weapons/Shovel/10";
        public const string WeaponWatering = "Weapons/Watering/10";
        public const string WeaponAxe = "Weapons/Axe/10";
        public const string WeaponSickle = "Weapons/Sickle/10";

        /// <summary>낚싯대 등급 (Fishing - * 폴더의 Weapons/1 ~ Weapons/10).</summary>
        public const string WeaponRod = "Weapons/10";

        private static readonly PlayerAnimDef[] _defs = BuildDefs();

        public static PlayerAnimDef Get(PlayerAnim anim) => _defs[(int)anim];

        private static PlayerAnimDef[] BuildDefs()
        {
            var defs = new PlayerAnimDef[System.Enum.GetValues(typeof(PlayerAnim)).Length];

            void Set(PlayerAnim anim, string folder, float fps, bool loop, string weapon = null)
                => defs[(int)anim] = new PlayerAnimDef
                { folder = folder, fps = fps, loop = loop, weapon = weapon };

            // ---- 이동 ----
            Set(PlayerAnim.Idle, "Idle", 6f, true);
            Set(PlayerAnim.Walk, "Walk", 10f, true);
            Set(PlayerAnim.Run, "Run", 13f, true);

            // ---- 무언가를 들고 있을 때 (머리 위로 아이템을 든 자세) ----
            Set(PlayerAnim.CarryIdle, "Carrying - Idle", 6f, true);
            Set(PlayerAnim.CarryWalk, "Carrying - Walk", 10f, true);
            Set(PlayerAnim.CarryRun, "Carrying - Run", 13f, true);
            Set(PlayerAnim.CarryPickUp, "Carrying - Pick Up", 12f, false);
            Set(PlayerAnim.CarryThrow, "Carrying - Throwing items", 14f, false);

            // ---- 도구 동작. 마법도 결국 같은 일을 하므로 같은 그림을 쓴다 ----
            Set(PlayerAnim.Pickaxe, "Pickaxe", 12f, false, WeaponPickaxe);
            Set(PlayerAnim.Shovel, "Shovel", 12f, false, WeaponShovel);
            Set(PlayerAnim.Watering, "Watering", 14f, false, WeaponWatering);
            Set(PlayerAnim.AxeSickle, "Axe and Sickle", 12f, false, WeaponAxe);

            // ---- 낚시 ----
            Set(PlayerAnim.FishCast, "Fishing - Cast", 16f, false, WeaponRod);
            Set(PlayerAnim.FishWait, "Fishing - Wait", 6f, true, WeaponRod);
            Set(PlayerAnim.FishBite, "Fishing - Bite", 12f, true, WeaponRod);
            Set(PlayerAnim.FishReel, "Fishing - Reel", 14f, true, WeaponRod);
            Set(PlayerAnim.FishCatch, "Fishing - Catch", 10f, false, WeaponRod);

            return defs;
        }

        // ---------- 무슨 일을 할 때 무엇을 재생할지 ----------

        /// <summary>
        /// 마법을 쓸 때의 동작. 하는 일이 같으면 같은 그림을 쓴다 —
        /// 바위를 부수는 것은 곡괭이질이고, 밭을 가는 것은 삽질이며,
        /// 나무를 베는 것은 도끼질이고, 물을 주는 것은 물뿌리개질이다.
        /// </summary>
        public static PlayerAnim ForMagic(MagicType magic)
        {
            switch (magic)
            {
                case MagicType.Rock: return PlayerAnim.Pickaxe;
                case MagicType.Earth: return PlayerAnim.Shovel;
                case MagicType.Blade: return PlayerAnim.AxeSickle;
                case MagicType.Water: return PlayerAnim.Watering;
                default: return PlayerAnim.Pickaxe;
            }
        }

        /// <summary>낚시 단계에 맞는 동작.</summary>
        public static PlayerAnim ForFishing(FishingState state)
        {
            switch (state)
            {
                case FishingState.Waiting: return PlayerAnim.FishWait;
                case FishingState.Bite: return PlayerAnim.FishBite;
                case FishingState.Minigame: return PlayerAnim.FishReel;
                default: return PlayerAnim.FishWait;
            }
        }

        /// <summary>
        /// 제자리/걷기/뛰기 세 가지. 무언가를 들고 있으면 들고 있는 판을 쓴다.
        /// </summary>
        public static PlayerAnim ForLocomotion(bool moving, bool running, bool carrying)
        {
            if (carrying)
                return !moving ? PlayerAnim.CarryIdle
                     : running ? PlayerAnim.CarryRun : PlayerAnim.CarryWalk;

            return !moving ? PlayerAnim.Idle
                 : running ? PlayerAnim.Run : PlayerAnim.Walk;
        }
    }
}
