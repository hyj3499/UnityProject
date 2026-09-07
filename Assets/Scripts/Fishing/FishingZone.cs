using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 한 낚시터에서 나오는 물고기 목록. LootTable과 같은 발상이지만 규칙이 다르다 —
    /// LootTable은 항목마다 독립적으로 굴려 여러 개가 나올 수 있고, 여기서는 <b>정확히 한 마리</b>가
    /// 가중치에 따라 뽑힌다. 그래서 별도 클래스로 두었다.
    /// </summary>
    public class FishingZone
    {
        private struct Entry
        {
            public string fishId;
            public float weight;
        }

        public readonly string zoneId;
        public readonly string displayName;
        private readonly List<Entry> _entries = new List<Entry>();
        private float _totalWeight;

        public FishingZone(string zoneId, string displayName)
        {
            this.zoneId = zoneId;
            this.displayName = displayName;
        }

        /// <summary>가중치가 클수록 자주 잡힌다. 확률이 아니라 비율이라 나중에 항목을 더해도 다 안 고쳐도 된다.</summary>
        public FishingZone Add(string fishId, float weight)
        {
            if (weight <= 0f) return this;
            _entries.Add(new Entry { fishId = fishId, weight = weight });
            _totalWeight += weight;
            return this;
        }

        public bool IsEmpty => _entries.Count == 0;

        /// <summary>이 낚시터에서 한 마리를 뽑는다. 비어 있으면 null.</summary>
        public FishDef Roll()
        {
            if (_entries.Count == 0) return null;

            float r = Random.value * _totalWeight;
            foreach (var e in _entries)
            {
                r -= e.weight;
                if (r <= 0f) return FishDatabase.Get(e.fishId);
            }
            return FishDatabase.Get(_entries[_entries.Count - 1].fishId);   // 부동소수점 오차 대비
        }
    }
}
