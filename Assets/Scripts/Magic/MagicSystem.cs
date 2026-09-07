using UnityEngine;

namespace FarmMVP
{
    // 농기구 대신 마법을 사용 (대지=경작, 물=물주기, 칼날=나무 베기, 바위=바위 부수기)
    public enum MagicType { Earth, Water, Blade, Rock }

    /// <summary>한 마법의 스펙: MP 소모량, 길게 눌렀을 때 지속 시전 여부와 간격, 조준 표시 색상.</summary>
    public struct MagicDef
    {
        public string displayName;
        public int mpCost;
        public int timeCost;        // 성공 시 소모되는 게임 내 분
        public bool continuous;     // true면 길게 누르는 동안 tickInterval마다 반복 시전 (물마법)
        public float tickInterval;
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
            displayName = "대지마법",
            mpCost = 3,
            timeCost = 10,
            continuous = false,
            tickInterval = 0f,
            previewColor = new Color(0.55f, 0.42f, 0.2f, 0.45f),
        };

        public static readonly MagicDef Water = new MagicDef
        {
            displayName = "물마법",
            mpCost = 2,
            timeCost = 5,
            continuous = true,
            tickInterval = 0.5f,
            previewColor = new Color(0.25f, 0.5f, 0.9f, 0.45f),
        };

        public static readonly MagicDef Blade = new MagicDef
        {
            displayName = "칼날마법",
            mpCost = 4,
            timeCost = 8,
            continuous = false,
            tickInterval = 0f,
            previewColor = new Color(0.8f, 0.25f, 0.25f, 0.45f),
        };

        public static readonly MagicDef Rock = new MagicDef
        {
            displayName = "바위마법",
            mpCost = 5,
            timeCost = 8,
            continuous = false,
            tickInterval = 0f,
            previewColor = new Color(0.55f, 0.55f, 0.6f, 0.45f),
        };

        /// <summary>지금 이 칸에는 쓸 수 없다는 표시 — 조준 사각형을 어둡게 덮는다.</summary>
        public static readonly Color InvalidPreviewColor = new Color(0.08f, 0.08f, 0.1f, 0.45f);

        /// <summary>마법 종류 수 (Q키 순환에 쓰인다).</summary>
        public static readonly int Count = System.Enum.GetValues(typeof(MagicType)).Length;

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
                case MagicType.Blade: return location.HasChoppableTree(x, y);
                case MagicType.Rock: return location.HasBreakableRock(x, y);
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
                    return false;

                case MagicType.Rock: // 바위마법: 바위 부수기
                    if (location.BreakRock(x, y, out bool broken, out string rockTable))
                    {
                        if (broken) dropTableId = rockTable;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }
    }
}
