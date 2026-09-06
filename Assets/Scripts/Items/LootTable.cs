using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>한 아이템이 드랍될 조건: 개수 범위와 확률.</summary>
    public class LootEntry
    {
        public string itemId;
        public int minCount = 1;
        public int maxCount = 1;
        public float chance = 1f; // 0~1. 이 항목이 실제로 드랍될 확률.
    }

    /// <summary>실제로 굴려서 나온 결과 하나 (아이템 id + 개수).</summary>
    public struct LootDrop
    {
        public string itemId;
        public int count;
        public LootDrop(string itemId, int count) { this.itemId = itemId; this.count = count; }
    }

    /// <summary>
    /// 나무/바위/몬스터 등 무언가를 파괴했을 때 무엇이 얼마나 나올지 정의하는 테이블.
    /// Add()를 체이닝해서 선언적으로 구성한다 (LootTableDatabase 참고).
    /// </summary>
    public class LootTable
    {
        private readonly List<LootEntry> _entries = new List<LootEntry>();

        public LootTable Add(string itemId, int minCount, int maxCount, float chance = 1f)
        {
            _entries.Add(new LootEntry { itemId = itemId, minCount = minCount, maxCount = maxCount, chance = chance });
            return this;
        }

        /// <summary>각 항목을 독립적으로 판정해서 실제로 드랍될 목록을 만든다.</summary>
        public List<LootDrop> Roll()
        {
            var result = new List<LootDrop>();
            foreach (var e in _entries)
            {
                if (Random.value > e.chance) continue;
                int count = e.minCount >= e.maxCount ? e.minCount : Random.Range(e.minCount, e.maxCount + 1);
                if (count > 0) result.Add(new LootDrop(e.itemId, count));
            }
            return result;
        }
    }
}
