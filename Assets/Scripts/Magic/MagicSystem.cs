using UnityEngine;

namespace FarmMVP
{
    // 농기구 대신 마법을 사용 (대지=경작, 물=물주기, 칼날=나무 베기)
    public enum MagicType { Earth, Water, Blade }

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

        public static MagicDef Get(MagicType type)
        {
            switch (type)
            {
                case MagicType.Earth: return Earth;
                case MagicType.Water: return Water;
                case MagicType.Blade: return Blade;
                default: return Earth;
            }
        }

        /// <summary>
        /// 지정한 타일에 마법 효과를 적용 시도한다 (MP 소모/환불은 호출자 책임).
        /// 실제로 무언가 바뀌었으면 true.
        /// </summary>
        public static bool TryApply(MagicType type, GameLocation location, Inventory inventory, int x, int y)
        {
            switch (type)
            {
                case MagicType.Earth: // 대지마법: 경작
                    return location.Till(x, y);

                case MagicType.Water: // 물마법: 물주기
                    return location.Water(x, y);

                case MagicType.Blade: // 칼날마법: 나무 베기
                    if (location.ChopTree(x, y, out bool destroyed))
                    {
                        if (destroyed) inventory.Add("wood", 3);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }
    }
}
