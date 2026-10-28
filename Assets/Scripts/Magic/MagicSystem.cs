using UnityEngine;

namespace FarmMVP
{
    // 농기구 대신 마법을 사용 (대지=경작, 물=물주기, 칼날=나무 베기, 바위=바위 부수기·땅 되돌리기, 바람=풀 베기, 삽=잔디땅 파기, 낚시=낚시)
    // 새로 추가할 때는 <b>맨 뒤에</b> 붙인다 — 저장된 "지금 든 마법"이 번호라서 중간에 끼우면 어긋난다.
    public enum MagicType { Earth, Water, Blade, Rock, Wind, Shovel, Fishing }

    /// <summary>한 마법의 스펙: MP 소모량과 조준 표시 색상.</summary>
    public struct MagicDef
    {
        [UnityEngine.SerializeField, UnityEngine.Serialization.FormerlySerializedAs("displayName")] private string _displayName;
        public string displayName { get => Localization.Resolve(_displayName); set => _displayName = value; }
        public int mpCost;
        public Color previewColor;  // 조준 중 표시할 타일 하이라이트 색상
    }

    /// <summary>
    /// 마법 종류별 스펙과 실제 발동 로직을 한곳에서 관리한다.
    /// GameManager/PlayerController는 이 클래스를 통해서만 마법을 다룬다.
    /// </summary>
    public static class MagicSystem
    {
        public static readonly MagicDef Earth = new MagicDef
        {
            displayName = "@items.magicsystem.earth_magic",
            mpCost = 3,
            previewColor = new Color(0.55f, 0.42f, 0.2f, 0.45f),
        };

        public static readonly MagicDef Water = new MagicDef
        {
            displayName = "@items.magicsystem.water_magic",
            mpCost = 2,
            previewColor = new Color(0.25f, 0.5f, 0.9f, 0.45f),
        };

        public static readonly MagicDef Blade = new MagicDef
        {
            displayName = "@items.magicsystem.blade_magic",
            mpCost = 4,
            previewColor = new Color(0.8f, 0.25f, 0.25f, 0.45f),
        };

        public static readonly MagicDef Rock = new MagicDef
        {
            displayName = "@items.magicsystem.rock_magic",
            mpCost = 5,
            previewColor = new Color(0.55f, 0.55f, 0.6f, 0.45f),
        };

        public static readonly MagicDef Wind = new MagicDef
        {
            displayName = "@items.magicsystem.wind_magic",
            mpCost = 2,
            previewColor = new Color(0.55f, 0.85f, 0.6f, 0.45f),
        };

        public static readonly MagicDef Shovel = new MagicDef
        {
            displayName = "@items.magicsystem.shovel_magic",
            mpCost = 2,
            previewColor = new Color(0.88f, 0.68f, 0.35f, 0.45f),
        };

        /// <summary>물가에서 낚시 미니게임을 시작하는 전용 마법.</summary>
        public static readonly MagicDef Fishing = new MagicDef
        {
            displayName = "@items.magicsystem.fishing_magic",
            mpCost = 2,
            previewColor = new Color(0.35f, 0.75f, 0.95f, 0.45f),
        };

        /// <summary>지금 이 칸에는 쓸 수 없다는 표시 — 조준 사각형을 어둡게 덮는다.</summary>
        public static readonly Color InvalidPreviewColor = new Color(0.08f, 0.08f, 0.1f, 0.45f);

        /// <summary>마법 종류 수 (Q키 순환에 쓰인다).</summary>
        public static readonly int Count = System.Enum.GetValues(typeof(MagicType)).Length;

        /// <summary>
        /// 퀵스킬 아이콘 시트와 원형 메뉴의 순서. 저장값은 MagicType 번호를 계속 쓰므로,
        /// 보기 좋은 순서를 바꿔도 기존 세이브의 선택 마법은 어긋나지 않는다.
        /// </summary>
        public static readonly MagicType[] QuickSkillOrder =
        {
            MagicType.Wind, MagicType.Water, MagicType.Fishing, MagicType.Rock,
            MagicType.Blade, MagicType.Shovel, MagicType.Earth
        };

        public static int QuickSkillIndex(MagicType type)
        {
            for (int i = 0; i < QuickSkillOrder.Length; i++)
                if (QuickSkillOrder[i] == type) return i;
            return 0;
        }

        public static MagicType QuickSkillAt(int index)
        {
            int normalized = index % QuickSkillOrder.Length;
            if (normalized < 0) normalized += QuickSkillOrder.Length;
            return QuickSkillOrder[normalized];
        }

        /// <summary>
        /// 지금 이 칸에 이 마법이 실제로 먹히는지 미리 물어본다 (아무것도 바꾸지 않는다).
        /// 조준 표시가 이걸 보고 색을 바꾼다. MP는 보지 않는다 — 그건 호출자(GameManager) 몫.
        /// TryApply와 같은 조건을 쓰도록 GameLocation의 Can* 판정을 공유한다.
        /// </summary>
        public static bool CanApply(MagicType type, GameLocation location, int x, int y)
        {
            if (location == null) return false;

            switch (type)
            {
                case MagicType.Earth: return location.CanTill(x, y);
                case MagicType.Water: return location.CanWater(x, y);
                // 칼날마법은 나무를 베고, 바닥의 나뭇가지·통나무를 치운다.
                case MagicType.Blade: return location.HasChoppableTree(x, y) || location.HasDebris(x, y, DebrisKind.Wood);
                // 바위마법은 바위를 부수고, 놓아 둔 설치물(울타리·길·가구)을 걷어낸다.
                // "Fixed_{맵}"에 칠한 배경 오브젝트와 마커로 놓인 것은 여기 걸리지 않아 부술 수 없다.
                // 땅에 쓰면 한 단계씩 되돌린다: 갈아 둔 밭 -> 흙땅 -> 삽질하기 전의 잔디땅.
                case MagicType.Rock: return location.HasBreakableRock(x, y) || location.HasPlaced(x, y)
                                         || location.HasDebris(x, y, DebrisKind.Stone)
                                         || location.CanUntill(x, y) || location.CanFillDirt(x, y);
                // 바람마법: 땅을 덮은 풀(잔디·잡초)을 베어 낸다.
                case MagicType.Wind: return location.IsHarvestableAt(x, y) || location.HasCuttablePlant(x, y);
                // 삽마법: "Tillable_{맵}"을 칠해 둔 잔디땅을 흙땅으로 파낸다. 대지마법은 그 흙땅에만 먹힌다.
                case MagicType.Shovel: return location.CanDig(x, y);
                case MagicType.Fishing: return FishingController.CanFishAt(location, x, y);
                default: return false;
            }
        }

        public static MagicDef Get(MagicType type)
        {
            switch (type)
            {
                case MagicType.Earth: return Earth;
                case MagicType.Water: return Water;
                case MagicType.Blade: return Blade;
                case MagicType.Rock: return Rock;
                case MagicType.Wind: return Wind;
                case MagicType.Shovel: return Shovel;
                case MagicType.Fishing: return Fishing;
                default: return Earth;
            }
        }

        /// <summary>
        /// 지정한 타일에 마법 효과를 적용 시도한다 (MP 소모/환불은 호출자 책임).
        /// 실제로 무언가 바뀌었으면 true. 무언가 파괴되어 아이템을 드랍해야 하면
        /// dropTableId를 돌려주고, 실제 스폰은 호출자(GameManager)가 ItemDropSpawner로 한다 —
        /// 인벤토리에 바로 넣지 않고 월드에 떨어뜨려야 하기 때문에 여기서는 직접 지급하지 않는다.
        /// </summary>
        public static bool TryApply(MagicType type, GameLocation location, int x, int y, out string dropTableId)
        {
            dropTableId = null;

            switch (type)
            {
                case MagicType.Earth: // 대지마법: 경작
                    return location.Till(x, y);

                case MagicType.Water: // 물마법: 물주기
                    return location.Water(x, y);

                case MagicType.Blade: // 칼날마법: 나무 베기
                    if (location.ChopTree(x, y, out bool destroyed, out string tableId))
                    {
                        if (destroyed) dropTableId = tableId;
                        return true;
                    }
                    return location.HitDebris(x, y, DebrisKind.Wood, out _, out dropTableId);

                case MagicType.Rock: // 바위마법: 바위 부수기 + 설치물(울타리·길·가구) 철거
                    if (location.BreakRock(x, y, out bool broken, out string rockTable))
                    {
                        if (broken) dropTableId = rockTable;
                        return true;
                    }
                    if (location.HitDebris(x, y, DebrisKind.Stone, out _, out dropTableId)) return true;
                    // 걷어낸 울타리·길·가구는 그 자리에 떨어져서 다시 주워 놓을 수 있다.
                    if (location.RemovePlaced(x, y, out dropTableId)) return true;
                    // 땅을 한 단계 되돌린다: 밭이면 흙땅으로, 흙땅이면 삽질하기 전으로.
                    return location.Untill(x, y) || location.FillDirt(x, y);

                case MagicType.Wind: // 바람마법: 풀 베기 (잔디 -> 목초, 잡초 -> 섬유)
                    if (location.IsHarvestableAt(x, y))
                    {
                        dropTableId = location.HarvestAt(x, y);
                        return dropTableId != null;
                    }
                    return location.CutPlant(x, y, out dropTableId);

                case MagicType.Shovel: // 삽마법: 잔디땅 -> 흙땅 (그 칸의 풀은 함께 걷힌다)
                    return location.Dig(x, y, out dropTableId);

                default:
                    return false;
            }
        }
    }
}
