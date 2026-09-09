namespace FarmMVP
{
    /// <summary>
    /// 화면(Canvas)끼리의 겹침 순서를 한곳에서 정한다.
    ///
    /// ScreenSpaceOverlay 캔버스가 여럿일 때 sortingOrder가 전부 0이면 <b>어느 것이 위로 갈지
    /// 정해져 있지 않다</b>. 그래서 불투명한 배경을 깐 화면이 나중에 띄운 화면을 덮어 버리는 일이
    /// 생긴다 (타이틀 배경이 캐릭터 만들기를 가려 아무것도 안 보이던 일이 그것이다).
    /// 캔버스를 하나 더 만들 때는 여기에 값을 하나 적고 그것을 쓴다.
    /// </summary>
    public static class UILayers
    {
        /// <summary>게임 중 HUD·인벤토리·상점 등 (UIManager).</summary>
        public const int Gameplay = 0;

        /// <summary>타이틀 화면.</summary>
        public const int Title = 10;

        /// <summary>캐릭터 만들기 — 타이틀 위에 뜬다.</summary>
        public const int CharacterCreator = 20;
    }
}
