using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>나무 종류 하나의 정의. 새 나무 = 새 TreeDef 등록.</summary>
    public class TreeDef
    {
        public string treeId;
        public string name;
        public int maxGrowthStage;   // 이 단계가 되면 다 자란 나무
        public int daysPerStage;     // 한 단계 자라는 데 걸리는 날
        public int maxHp;            // 다 자란 나무를 벨 때 필요한 타격 수
        public string dropTableId;   // 다 자란 나무를 벴을 때 나오는 것
        public Func<Sprite[]> stageSprites;
    }

    /// <summary>
    /// 나무 카탈로그. 살구나무처럼 종류를 추가하려면 여기에 Register 한 줄만 더하면 되고,
    /// 성장/베기/렌더링 로직은 그대로 재사용된다.
    /// </summary>
    public static class TreeDatabase
    {
        public const string DefaultTreeId = "apricot";

        private static readonly Dictionary<string, TreeDef> _defs = new Dictionary<string, TreeDef>();
        private static readonly List<TreeDef> _all = new List<TreeDef>();
        private static bool _init;

        /// <summary>등록된 나무 전부. 나무 종류마다 배치 마커를 자동으로 만드는 데 쓰인다.</summary>
        public static IReadOnlyList<TreeDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(new TreeDef
            {
                treeId = "apricot",
                name = "살구나무",
                // 4장 (Apricot_0..3): 씨앗 → 묘목 → 성장 → 다 자람(3)
                maxGrowthStage = 3,
                daysPerStage = 2,
                maxHp = 3,
                dropTableId = "tree_apricot",
                stageSprites = () => { AssetLibrary.EnsureLoaded(); return AssetLibrary.ApricotStages; }
            });
        }

        private static void Register(TreeDef d)
        {
            _defs[d.treeId] = d;
            _all.Add(d);
        }

        public static TreeDef Get(string treeId)
        {
            Init();
            if (!string.IsNullOrEmpty(treeId) && _defs.TryGetValue(treeId, out var d)) return d;
            return _defs[DefaultTreeId];
        }
    }
}
