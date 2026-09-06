using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 나무를 베거나 바위를 캐는 등 무언가를 파괴/수확했을 때, 그 자리에 실제 월드
    /// 아이템(WorldItem)을 떨어뜨리는 단일 진입점. 호출자는 LootTableDatabase에 등록된
    /// 드랍 테이블 id 하나만 넘기면 되고, 몇 개가 어떤 확률로 나오는지는 전혀 몰라도 된다.
    /// 새로운 드랍 소스(사과나무, 바위, ...)를 추가해도 이 파일은 수정할 필요가 없다.
    /// </summary>
    public static class ItemDropSpawner
    {
        public static void Spawn(Transform parent, GameManager game, Vector2Int tile, string dropTableId)
        {
            var table = LootTableDatabase.Get(dropTableId);
            if (table == null) return;

            var tilePos = new Vector2(tile.x, tile.y);
            foreach (var drop in table.Roll())
                WorldItem.Create(parent, game, drop.itemId, drop.count, tilePos);
        }
    }
}
