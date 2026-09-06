namespace FarmMVP
{
    public enum Direction { Down, Up, Left, Right }

    // MagicType은 Assets/Scripts/Magic/MagicSystem.cs 에서 관리한다.

    // ToolType은 더 이상 게임플레이에서 쓰이지 않지만, 기존 데이터 호환을 위해 남겨둠
    public enum ToolType { None, Hoe, WateringCan, Axe }

    public enum ItemType { Seed, Crop, Resource, Tool }

    public enum LocationId { Farm1, Farm2, FarmHouse }
}
