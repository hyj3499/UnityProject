using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 마커가 놓일 때 <b>추가로</b> 해야 하는 일. 대부분의 오브젝트는 None이면 충분하고,
    /// 여기 들어 있는 것들만 특별한 처리(출입구 지정, 배송함 등록, 저장되는 나무/바위 등)가 있다.
    /// </summary>
    public enum MarkerRole
    {
        None,           // 그림을 놓고 발판을 막기만 한다
        House,          // 아래줄 가운데 칸이 농가로 들어가는 문이 된다
        ShippingBox,    // 배송함으로 등록 (뚜껑 여닫기)
        Shop,           // 상점 수레로 등록
        Bed,            // 잠자는 자리로 등록
        Door,           // 실내 출입문
        Tree,           // 저장되는 나무 — 새 게임일 때만 데이터로 들어간다
        Rock,           // 저장되는 바위 — 새 게임일 때만 데이터로 들어간다
    }

    /// <summary>
    /// 마커 타일 하나가 무엇을 놓는지에 대한 순수한 데이터.
    ///
    /// 이름을 따로 짓지 않는다 — 그림이 있는 오브젝트는 <b>스프라이트 파일 이름</b>이 곧 이름이고
    /// (Sprites/Environment/ShopCart.png → 마커 타일 Obj_ShopCart), 나무처럼 이미 자기 id가 있는
    /// 것은 <b>그 id</b>가 이름이 된다 (TreeDef.treeId "apricot" → Obj_Apricot).
    /// 같은 것을 가리키는 이름이 하나뿐이라 서로 어긋날 일이 없다.
    /// </summary>
    public class ObjectMarkerDef
    {
        /// <summary>Resources 경로. 계절판(ShopCart_Winter.png)이 있으면 그게 쓰인다.</summary>
        public string spritePath;

        /// <summary>스프라이트 파일 이름과 다른 이름을 쓰고 싶을 때만 채운다 (나무는 treeId를 쓴다).</summary>
        public string nameOverride;

        /// <summary>스프라이트를 칸 중심에서 얼마나 옮겨 그릴지 (큰 그림의 발밑을 맞출 때 쓴다).</summary>
        public Vector2 offset = Vector2.zero;

        /// <summary>차지하는 칸 수.</summary>
        public Vector2Int size = Vector2Int.one;

        /// <summary>발판의 왼쪽 아래가 마커 칸에서 얼마나 떨어져 있는지 (침대처럼 위로 자란 그림용).</summary>
        public Vector2Int footprintAnchor = Vector2Int.zero;

        /// <summary>발판을 막을지. 러그 같은 바닥 장식은 false.</summary>
        public bool blocks = true;

        /// <summary>러그처럼 바닥에 깔려 아무것도 가리지 않는 장식이면 true (y정렬에서 빠진다).</summary>
        public bool floorDecor = false;

        /// <summary>같은 줄에 있는 것들 사이의 앞뒤 미세 조정. 보통은 0.</summary>
        public int sortBias = 0;

        public MarkerRole role = MarkerRole.None;

        /// <summary>role이 Tree일 때 어떤 나무를 심을지 (TreeDatabase의 treeId).</summary>
        public string treeId;

        /// <summary>role이 Rock일 때 어떤 바위 그림을 쓸지 (AssetLibrary.RockVariants 인덱스).</summary>
        public int rockVariant;

        /// <summary>이 오브젝트의 이름. 마커 타일 이름은 "Obj_" + 이 값이다.</summary>
        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(nameOverride)) return nameOverride;
                if (string.IsNullOrEmpty(spritePath)) return null;
                int slash = spritePath.LastIndexOf('/');
                return slash >= 0 ? spritePath.Substring(slash + 1) : spritePath;
            }
        }

        public Sprite GetSprite()
        {
            // 나무는 자기 성장 단계 그림을 갖고 있으니 다 자란 모습을 팔레트에 보여 준다.
            if (role == MarkerRole.Tree)
            {
                var stages = TreeDatabase.Get(treeId)?.stageSprites?.Invoke();
                return stages != null && stages.Length > 0 ? stages[stages.Length - 1] : null;
            }
            return string.IsNullOrEmpty(spritePath) ? null : AssetLibrary.GetSeasonal(spritePath);
        }
    }

    /// <summary>
    /// "Objects_{맵}" 레이어에 칠할 수 있는 오브젝트들의 카탈로그.
    ///
    /// 오브젝트를 하나 더 넣는 데 필요한 것은 <b>여기 Register 한 줄</b>뿐이다 —
    /// 배치 코드(ObjectMarkerPlacer)도, 마커 타일 생성기도 이 표를 그대로 읽는다.
    /// 나무와 바위는 아예 적지 않는다 — TreeDatabase/바위 변종에서 자동으로 만들어지므로,
    /// 나무 종류를 추가하면 Obj_{그 나무} 마커가 저절로 생긴다.
    /// </summary>
    public static class ObjectMarkerDatabase
    {
        private static readonly Dictionary<string, ObjectMarkerDef> _defs =
            new Dictionary<string, ObjectMarkerDef>();
        private static readonly List<ObjectMarkerDef> _all = new List<ObjectMarkerDef>();
        private static bool _init;

        public static IReadOnlyList<ObjectMarkerDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;
            AssetLibrary.EnsureLoaded();

            // ---- 야외 ----
            Register(new ObjectMarkerDef
            {
                spritePath = "Sprites/Environment/House",
                // 마커 칸이 집의 왼쪽 아래. 발판은 그림에 맞춘다 — House.png는 72x95px(PPU 16)라
                // 4.5x5.94칸이고, offset 때문에 마커 칸 기준 x는 -0.75~3.75칸을 덮는다.
                // 가로를 5칸으로 두면 그림이 25%만 걸치는 x+4 열까지 막힌다.
                size = new Vector2Int(4, 5),
                offset = new Vector2(1.5f, 2.0f),
                role = MarkerRole.House,
            });

            Register(new ObjectMarkerDef
            {
                spritePath = "Sprites/Environment/ShippingBox",
                offset = new Vector2(0f, 0.15f),
                role = MarkerRole.ShippingBox,
            });

            Register(new ObjectMarkerDef
            {
                spritePath = "Sprites/Environment/ShopCart",
                offset = new Vector2(0f, 0.6f),
                role = MarkerRole.Shop,
            });

            // ---- 실내 ----
            Register(new ObjectMarkerDef
            {
                spritePath = "Sprites/Interior/Bed",
                size = new Vector2Int(1, 2),
                footprintAnchor = new Vector2Int(0, -1),   // 마커 칸과 그 아래 칸을 함께 막는다
                role = MarkerRole.Bed,
            });

            Register(new ObjectMarkerDef
            {
                spritePath = "Sprites/Interior/Door",
                offset = new Vector2(0f, -0.4f),
                blocks = false,
                role = MarkerRole.Door,
            });

            Register(new ObjectMarkerDef { spritePath = "Sprites/Interior/Fireplace" });
            Register(new ObjectMarkerDef { spritePath = "Sprites/Interior/Plant" });

            Register(new ObjectMarkerDef
            {
                spritePath = "Sprites/Interior/Rug",
                blocks = false,
                floorDecor = true,   // 바닥에 깔리는 장식 — 아무도 가리지 않는다
            });

            // 오브젝트를 더 넣으려면 여기에 한 줄 (이름은 파일 이름이 된다):
            //   Register(new ObjectMarkerDef { spritePath = "Sprites/Environment/Barrel" });
            // 그러면 Tools/Farm 메뉴가 Obj_Barrel 마커 타일을 만들어 주고, 칠하면 그대로 놓인다.

            RegisterTrees();
            RegisterRocks();
        }

        /// <summary>
        /// 나무 종류마다 마커를 하나씩. TreeDatabase에 참나무를 추가하면 Obj_Oak 마커가 저절로 생긴다.
        /// </summary>
        private static void RegisterTrees()
        {
            foreach (var tree in TreeDatabase.All)
            {
                Register(new ObjectMarkerDef
                {
                    nameOverride = Capitalize(tree.treeId),
                    role = MarkerRole.Tree,
                    treeId = tree.treeId,
                });
            }
        }

        /// <summary>바위 그림 변종마다 마커를 하나씩 (Rock_0 ... Rock_4). 이름은 그림 파일 그대로.</summary>
        private static void RegisterRocks()
        {
            int count = AssetLibrary.RockVariants != null ? AssetLibrary.RockVariants.Length : 0;
            for (int i = 0; i < count; i++)
            {
                Register(new ObjectMarkerDef
                {
                    spritePath = "Sprites/Environment/Rock_" + i,
                    role = MarkerRole.Rock,
                    rockVariant = i,
                });
            }
        }

        private static void Register(ObjectMarkerDef def)
        {
            string key = Normalize(def.Name);
            if (string.IsNullOrEmpty(key)) return;
            _defs[key] = def;
            _all.Add(def);
        }

        /// <summary>칠한 타일 이름으로 찾는다. "Obj_ShopCart", "obj_shopcart", "ShopCart" 모두 같은 것.</summary>
        public static ObjectMarkerDef Find(string tileName)
        {
            Init();
            string key = Normalize(StripPrefix(tileName));
            return string.IsNullOrEmpty(key) ? null
                 : _defs.TryGetValue(key, out var d) ? d : null;
        }

        /// <summary>마커 타일 에셋에 붙는 이름 ("Obj_ShopCart").</summary>
        public static string TileName(ObjectMarkerDef def) => "Obj_" + def.Name;

        /// <summary>이름 앞의 "Obj_"를 뗀다 (있을 때만).</summary>
        public static string StripPrefix(string tileName)
        {
            if (string.IsNullOrEmpty(tileName)) return null;
            var n = tileName.Trim();
            return n.StartsWith("Obj_", System.StringComparison.OrdinalIgnoreCase) ? n.Substring(4) : n;
        }

        /// <summary>대소문자와 밑줄을 무시한 비교용 이름. "Rock_0" 과 "rock0" 은 같은 것.</summary>
        public static string Normalize(string name)
            => string.IsNullOrEmpty(name) ? null : name.Replace("_", "").ToLowerInvariant();

        private static string Capitalize(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
