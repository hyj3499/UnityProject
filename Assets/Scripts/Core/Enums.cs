namespace FarmMVP
{
    public enum Direction { Down, Up, Left, Right }

    // MagicType은 Assets/Scripts/Magic/MagicSystem.cs 에서 관리한다.

    // ToolType은 더 이상 게임플레이에서 쓰이지 않지만, 기존 데이터 호환을 위해 남겨둠
    public enum ToolType { None, Hoe, WateringCan, Axe }

    public enum ItemType { Seed, Crop, Resource, Tool, Fish }

    /// <summary>
    /// 맵 하나하나의 id. 이 이름이 곧 <b>씬에 칠하는 타일맵 레이어 이름</b>("Location_Valley")이고
    /// <b>맵 이동 마커 이름</b>("Obj_ExitValley")이라, 한 번 정하면 바꾸지 않는 편이 좋다.
    /// 양쪽 맵에 칠한 Exit로부터 <see cref="MapGraph"/>가 연결을 찾는다.
    ///
    /// 새 맵은 반드시 <b>맨 뒤에</b> 덧붙인다 — JsonUtility가 enum을 숫자로 저장하기 때문에
    /// 중간에 끼워 넣으면 기존 세이브의 "지금 있는 맵"이 엉뚱한 곳을 가리킨다.
    /// </summary>
    public enum LocationId
    {
        // Keep serialized values stable; 1 and 11 belonged to removed maps.
        Farm = 0,
        FarmHouse = 2,
        Valley = 3,
        Campsite1 = 4,
        Campsite2 = 5,
        Meadow = 6,
        DeepForest = 7,
        Sea = 8,
        Mountain1 = 9,
        Mountain2 = 10,
        MountainPeak = 12,
    }
}
