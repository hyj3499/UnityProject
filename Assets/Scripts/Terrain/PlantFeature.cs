using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 한 칸을 덮는 풀 (잔디 한 무더기 / 잡초 한 무더기).
    ///
    /// 그림이 칸 격자(16x16)와 어긋나는 크기인데(잔디 15x20, 잡초 15x12), 그게 이 기능의 핵심이다 —
    /// 한 칸에 여러 포기를 <b>칸 안에서 조금씩 어긋나게</b> 심어 격자가 보이지 않게 한다.
    /// 몇 포기를 어디에 심을지는 칸 좌표에서 계산하므로(<see cref="Tufts"/>) 저장할 것이 없고,
    /// 같은 칸은 언제 다시 그려도 같은 모양이다.
    /// </summary>
    [Serializable]
    public class PlantFeature
    {
        public int x, y;
        public string plantId;

        /// <summary>잔디는 성장 단계(0~2), 잡초는 생김새 번호(0~2).</summary>
        public int stage;
        public int dayCounter;

        public PlantFeature(int x, int y, string plantId, int stage)
        {
            this.x = x;
            this.y = y;
            this.plantId = plantId;
            this.stage = stage;
        }

        public PlantDef Def => PlantDatabase.Get(plantId);

        public bool IsMature
        {
            get { var d = Def; return d != null && stage >= d.maxStage; }
        }

        /// <summary>지금 계절의 그림. 겨울처럼 그림이 없는 계절이면 null.</summary>
        public Sprite GetSprite()
        {
            var d = Def;
            return d == null ? null : PlantArt.Stage(d.sheet, stage, Seasons.Current);
        }

        /// <summary>하루가 지난다. 그림이 바뀌었으면 true (그때만 다시 그리면 된다).</summary>
        public bool Grow()
        {
            var d = Def;
            if (d == null || stage >= d.maxStage || d.daysPerStage <= 0) return false;

            dayCounter++;
            if (dayCounter < d.daysPerStage) return false;

            dayCounter = 0;
            stage = Mathf.Min(stage + 1, d.maxStage);
            return true;
        }

        /// <summary>벨 때 나오는 것. 아직 덜 자랐으면 아무것도 안 나온다.</summary>
        public string DropTableId
        {
            get
            {
                var d = Def;
                return d == null || stage < d.minDropStage ? null : d.DropTableId;
            }
        }

        // ---------- 칸 안에 흩어 심기 ----------

        /// <summary>한 포기가 칸 안 어디에 어떻게 놓이는지.</summary>
        public struct Tuft
        {
            public float x, y;   // 칸 아래 가운데를 0,0으로 본 자리 (칸 단위)
            public bool flip;    // 좌우 뒤집기 — 같은 그림이 반복돼 보이지 않게
        }

        /// <summary>칸 안에 심을 포기들. 칸 좌표에서 계산하므로 저장하지 않아도 늘 같다.</summary>
        public Tuft[] Tufts()
        {
            var d = Def;
            if (d == null) return Array.Empty<Tuft>();

            int min = Mathf.Max(1, d.tuftsMin);
            int count = min + (int)(Hash(x, y, 0) % (uint)Mathf.Max(1, d.tuftsMax - min + 1));

            var tufts = new Tuft[count];
            for (int i = 0; i < count; i++)
            {
                // 가로는 칸을 살짝 넘어가도 좋다 — 옆 칸의 풀과 맞물려 격자가 사라진다.
                tufts[i].x = Range(Hash(x, y, i * 3 + 1), -d.scatterX, d.scatterX);
                // 세로는 위로만. 위로 갈수록 뒤에 그려져 한 무더기 안에서도 앞뒤가 생긴다.
                tufts[i].y = Range(Hash(x, y, i * 3 + 2), 0f, d.scatterY);
                tufts[i].flip = (Hash(x, y, i * 3 + 3) & 1) == 0;
            }
            return tufts;
        }

        /// <summary>칸 좌표에서 늘 같은 값이 나오는 해시 (Unity의 Random과 달리 순서에 휘둘리지 않는다).</summary>
        public static uint Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Range(uint h, float from, float to)
            => from + (to - from) * ((h & 0xffff) / 65535f);
    }
}
