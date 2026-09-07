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
            public SeasonFlags seasons;
        }

        public readonly string zoneId;
        public readonly string displayName;
        private readonly List<Entry> _entries = new List<Entry>();

        public FishingZone(string zoneId, string displayName)
        {
            this.zoneId = zoneId;
            this.displayName = displayName;
        }

        /// <summary>
        /// 가중치가 클수록 자주 잡힌다. 확률이 아니라 비율이라 나중에 항목을 더해도 다 안 고쳐도 된다.
        /// seasons를 주면 그 계절에만 잡힌다 — 낚시터를 계절마다 따로 만들지 않아도 되게.
        /// </summary>
        public FishingZone Add(string fishId, float weight, SeasonFlags seasons = SeasonFlags.All)
        {
            if (weight <= 0f) return this;
            _entries.Add(new Entry { fishId = fishId, weight = weight, seasons = seasons });
            return this;
        }

        /// <summary>지금 계절에 잡히는 게 하나도 없으면 이 낚시터는 비어 있는 셈이다.</summary>
        public bool IsEmpty => TotalWeight(Seasons.Current) <= 0f;

        public bool IsEmptyIn(Season season) => TotalWeight(season) <= 0f;

        private float TotalWeight(Season season)
        {
            float total = 0f;
            foreach (var e in _entries)
                if (Seasons.Allows(e.seasons, season)) total += e.weight;
            return total;
        }

        /// <summary>이 낚시터에서 지금 계절에 잡히는 물고기 한 마리를 뽑는다. 없으면 null.</summary>
        public FishDef Roll() => Roll(Seasons.Current);

        public FishDef Roll(Season season)
        {
            float total = TotalWeight(season);
            if (total <= 0f) return null;

            float r = Random.value * total;
            Entry last = default;
            bool any = false;
            foreach (var e in _entries)
            {
                if (!Seasons.Allows(e.seasons, season)) continue;
                last = e; any = true;
                r -= e.weight;
                if (r <= 0f) return FishDatabase.Get(e.fishId);
            }
            return any ? FishDatabase.Get(last.fishId) : null;   // 부동소수점 오차 대비
        }
    }
}
