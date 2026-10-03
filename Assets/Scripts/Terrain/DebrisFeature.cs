using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>농장 바닥에 놓이는 잔해의 재질. 저장 데이터에 번호로 들어가므로 순서를 바꾸지 않는다.</summary>
    public enum DebrisKind { Wood, Stone }

    /// <summary>
    /// 잔해 한 종류의 정의. 그림은 Resources/Sprites/Environment/{Wood|Stone}_{계절}.png 를 잘라 놓은
    /// 조각 "{Wood|Stone}_{계절}_{variant}" 이다 (계절판이 없으면 봄 그림을 쓴다).
    ///
    ///   variant 0·1  나뭇가지 / 돌멩이 — 한 번에 치워지고, 계절 첫날 새로 깔리고, 밤마다 옆으로 번진다.
    ///   variant 2·3  큰 통나무 / 큰 바위 — 여러 칸을 차지하고 체력이 매우 높다. 저절로 생기지도 번지지도
    ///                않는다. "Objects_{맵}" 레이어에 그 조각을 칠해 직접 놓고, 한 번 부수면 다시 나오지 않는다.
    /// </summary>
    public class DebrisDef
    {
        public DebrisKind kind;
        public int variant;
        public int maxHp = 1;

        /// <summary>차지하는 칸 수 (기준 칸은 왼쪽 아래).</summary>
        public Vector2Int size = Vector2Int.one;

        /// <summary>밤마다 옆 칸으로 번지고, 계절 첫날 새로 깔리는 종류인지.</summary>
        public bool spreads;

        public int dropMin = 1, dropMax = 1;

        public string Sheet => kind == DebrisKind.Wood ? "Wood" : "Stone";
        /// <summary>마커 타일 이름이 되는 봄 그림 조각 ("Wood_spring_2" → 마커 Obj_Wood_spring_2).</summary>
        public string MarkerSpritePath => "Sprites/Environment/" + Sheet + "_spring_" + variant;
        public string DropItemId => kind == DebrisKind.Wood ? "wood" : "stone";
        public string DropTableId => "debris_" + DropItemId + "_" + variant;

        public Sprite GetSprite()
        {
            string prefix = "Sprites/Environment/" + Sheet + "_";
            var seasonal = AssetLibrary.GetSpriteOptional(
                prefix + Seasons.Key(Seasons.Current).ToLowerInvariant() + "_" + variant);
            return seasonal != null ? seasonal : AssetLibrary.GetSprite(prefix + "spring_" + variant);
        }
    }

    /// <summary>잔해 카탈로그. 드랍 테이블은 여기서 자동으로 따라 만들어진다 (LootTableDatabase).</summary>
    public static class DebrisDatabase
    {
        /// <summary>번지는 작은 잔해의 그림 수 (variant 0 ~ SmallVariants-1). 놓일 때 이 중 아무거나 고른다.</summary>
        public const int SmallVariants = 2;

        private static readonly List<DebrisDef> _all = new List<DebrisDef>();
        private static bool _init;

        public static IReadOnlyList<DebrisDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            foreach (DebrisKind kind in System.Enum.GetValues(typeof(DebrisKind)))
            {
                for (int v = 0; v < SmallVariants; v++)
                    _all.Add(new DebrisDef { kind = kind, variant = v, spreads = true });
            }

            // 큰 것들은 그림 크기(16px = 한 칸)에 맞춰 발판을 잡는다.
            _all.Add(new DebrisDef { kind = DebrisKind.Wood, variant = 2, maxHp = 10, size = new Vector2Int(3, 1), dropMin = 6, dropMax = 8 });
            _all.Add(new DebrisDef { kind = DebrisKind.Wood, variant = 3, maxHp = 20, size = new Vector2Int(5, 2), dropMin = 14, dropMax = 18 });
            _all.Add(new DebrisDef { kind = DebrisKind.Stone, variant = 2, maxHp = 10, size = new Vector2Int(3, 1), dropMin = 6, dropMax = 8 });
            _all.Add(new DebrisDef { kind = DebrisKind.Stone, variant = 3, maxHp = 20, size = new Vector2Int(4, 2), dropMin = 14, dropMax = 18 });
        }

        public static DebrisDef Get(DebrisKind kind, int variant)
        {
            Init();
            foreach (var d in _all)
                if (d.kind == kind && d.variant == variant) return d;
            return null;
        }
    }

    /// <summary>맵에 놓인 잔해 하나. (x, y)는 발판의 왼쪽 아래 칸이다.</summary>
    public class DebrisFeature
    {
        public int x, y;
        public DebrisKind kind;
        public int variant;
        public int hp;

        public DebrisFeature(int x, int y, DebrisDef def)
        {
            this.x = x;
            this.y = y;
            kind = def.kind;
            variant = def.variant;
            hp = def.maxHp;
            Def = def;
        }

        public DebrisDef Def { get; }

        /// <summary>한 번 친다. 이번 타격으로 부서졌으면 true.</summary>
        public bool Hit()
        {
            hp--;
            return hp <= 0;
        }
    }
}
