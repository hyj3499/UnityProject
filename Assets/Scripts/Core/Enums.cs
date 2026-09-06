namespace FarmMVP
{
    public enum Direction { Down, Up, Left, Right }

    // 농기구 대신 마법을 사용 (대지=경작, 물=물주기, 칼날=나무 베기)
    public enum MagicType { Earth, Water, Blade }

    // ToolType은 더 이상 게임플레이에서 쓰이지 않지만, 기존 데이터 호환을 위해 남겨둠
    public enum ToolType { None, Hoe, WateringCan, Axe }

    public enum ItemType { Seed, Crop, Resource, Tool }

    public enum LocationId { Farm1, Farm2, FarmHouse }
}
