using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 무엇이 무엇을 가리는지를 한곳에서 정한다.
    ///
    /// 규칙은 하나뿐이다 — <b>화면에서 아래에 있는 것이 앞에 그려진다.</b> 그래야 나무 위쪽에 서면
    /// 나무에 가려지고, 아래로 내려오면 나무 앞으로 나오는 자연스러운 느낌이 난다.
    /// 플레이어·NPC·나무·바위·집·작물·떨어진 아이템이 <b>모두 같은 식</b>을 쓴다. 예전처럼 종류마다
    /// 기준값이 다르면(나무 600, NPC 700, 플레이어 1000) 같은 줄에 있어도 앞뒤가 뒤집힌다.
    ///
    /// 기준이 되는 y는 그림이 그려지는 위치가 아니라 <b>발밑(서 있는 칸)</b>이다. 나무 그림은 발밑보다
    /// 한참 위까지 올라가므로, 그림 위치로 정렬하면 나무가 실제보다 뒤에 있는 것처럼 보인다.
    /// </summary>
    public static class Depth
    {
        // ---------- 바닥 (y정렬 없음, 항상 맨 뒤) ----------
        /// <summary>코드가 까는 기본 바닥.</summary>
        public const int Ground = -110;
        /// <summary>타일 팔레트로 칠한 바닥.</summary>
        public const int PaintedGround = -100;
        /// <summary>
        /// 물 타일. 칠한 바닥보다 뒤에 그려져, Location 바닥 스프라이트의 투명한 부분에서만 보인다.
        /// 기본 코드 바닥(-110)보다는 앞이므로 물이 바닥색에 가려지지는 않는다.
        /// </summary>
        public const int Water = -101;
        /// <summary>
        /// 꽃·잔디 같은 바닥 장식("Decor_{맵}" 레이어). 칠한 바닥보다 앞, <b>경작지보다 뒤</b>다 —
        /// 밭을 갈면 불투명한 흙 타일에 덮여 저절로 사라지고, 흙이 없어지면 다시 드러난다.
        /// 그래서 장식을 숨겼다 되살리는 상태도, 저장할 것도 없다.
        /// </summary>
        public const int Decor = -60;

        /// <summary>절벽("Cliff_{맵}" 레이어). 장식 위에 얹히지만 서 있는 것들보다는 뒤.</summary>
        public const int Cliff = -55;

        /// <summary>경작된 흙.</summary>
        public const int Soil = -50;
        /// <summary>젖은 흙 (경작지 위에 덧그린다).</summary>
        public const int WetSoil = -49;
        /// <summary>러그처럼 바닥에 깔려 아무것도 가리지 않는 장식.</summary>
        public const int FloorDecor = -40;
        /// <summary>조준 표시. 바닥 위, 하지만 서 있는 모든 것보다는 뒤.</summary>
        public const int Highlight = -30;

        /// <summary>낚시 느낌표처럼 무조건 맨 앞에 보여야 하는 것.</summary>
        public const int Overlay = 30000;

        /// <summary>y정렬의 기준값. 한 칸 내려올 때마다 PerTile 만큼 앞으로 나온다.</summary>
        private const int YSortBase = 20000;

        /// <summary>
        /// 칸 하나당 정렬 간격. 100칸을 띄워 두는 이유는 같은 줄에 있는 것들 사이에서
        /// 미세 조정(bias)을 할 여지를 남기기 위해서다.
        /// </summary>
        private const int PerTile = 100;

        /// <summary>
        /// 발밑 y좌표로 정렬 순서를 만든다. y가 작을수록(화면 아래) 큰 값 = 앞에 그려진다.
        /// bias는 같은 줄에서의 앞뒤 조정용 (음수면 조금 뒤).
        /// </summary>
        public static int YSort(float footY, int bias = 0)
            => YSortBase - Mathf.RoundToInt(footY * PerTile) + bias;

        /// <summary>같은 칸에서 작물은 그 위에 선 캐릭터보다 뒤에 그려진다.</summary>
        public const int CropBias = -20;

        /// <summary>떨어진 아이템은 바닥에 놓인 것이라 같은 칸의 캐릭터보다 뒤.</summary>
        public const int DroppedItemBias = -10;
    }
}
