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
    /// 스프라이트를 Resources 경로로 들고 있어서 AssetLibrary에 필드를 새로 만들 필요가 없고,
    /// 계절판(Tree_Winter.png)도 자동으로 적용된다.
    /// </summary>
    public class ObjectMarkerDef
    {
        /// <summary>마커 타일 이름에서 "Obj_"를 뗀 것. 소문자.</summary>
        public string key;

        /// <summary>Resources 경로. 계절판이 있으면 그게 쓰인다.</summary>
        public string spritePath;

        /// <summary>스프라이트를 칸 중심에서 얼마나 옮겨 그릴지 (큰 그림의 발밑을 맞출 때 쓴다).</summary>
        public Vector2 offset = Vector2.zero;

        /// <summary>차지하는 칸 수.</summary>
        public Vector2Int size = Vector2Int.one;

        /// <summary>발판의 왼쪽 아래가 마커 칸에서 얼마나 떨어져 있는지 (침대처럼 위로 자란 그림용).</summary>
        public Vector2Int footprintAnchor = Vector2Int.zero;

        /// <summary>발판을 막을지. 러그 같은 바닥 장식은 false.</summary>
        public bool blocks = true;

        public int sortingOrder = 500;

        public MarkerRole role = MarkerRole.None;

        public Sprite GetSprite() => AssetLibrary.GetSeasonal(spritePath);
    }

    /// <summary>
    /// "Objects_{맵}" 레이어에 칠할 수 있는 오브젝트들의 카탈로그.
    ///
    /// 오브젝트를 하나 더 넣는 데 필요한 것은 <b>여기 Register 한 줄</b>뿐이다 —
    /// 배치 코드(ObjectMarkerPlacer)도, 마커 타일 생성기도 이 표를 그대로 읽는다.
    /// 그림만 놓고 지나가지 못하게 하는 평범한 오브젝트는 role을 적을 필요도 없다.
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

            // ---- 야외 ----
            Register(new ObjectMarkerDef
            {
                key = "house",
                spritePath = "Sprites/Environment/House",
                // 마커 칸이 집의 왼쪽 아래. 5x5를 차지하고 그림은 그 가운데쯤에 놓인다.
                size = new Vector2Int(5, 5),
                offset = new Vector2(1.5f, 2.0f),
                role = MarkerRole.House,
            });

            Register(new ObjectMarkerDef
            {
                key = "shippingbox",
                spritePath = "Sprites/Environment/ShippingBox",
                offset = new Vector2(0f, 0.15f),
                sortingOrder = 600,
                role = MarkerRole.ShippingBox,
            });

            Register(new ObjectMarkerDef
            {
                key = "shop",
                spritePath = "Sprites/Environment/ShopCart",
                offset = new Vector2(0f, 0.6f),
                sortingOrder = 600,
                role = MarkerRole.Shop,
            });

            // 나무와 바위는 그림을 여기서 놓지 않는다 — 자라고 베이는 저장 데이터라
            // 새 게임일 때 초기 배치로만 넣고, 그리는 것은 RestoreFeatures가 한다.
            Register(new ObjectMarkerDef { key = "tree", spritePath = "Sprites/Environment/Tree", role = MarkerRole.Tree });
            Register(new ObjectMarkerDef { key = "rock", spritePath = "Sprites/Environment/Rock_0", role = MarkerRole.Rock });

            // ---- 실내 ----
            Register(new ObjectMarkerDef
            {
                key = "bed",
                spritePath = "Sprites/Interior/Bed",
                size = new Vector2Int(1, 2),
                footprintAnchor = new Vector2Int(0, -1),   // 마커 칸과 그 아래 칸을 함께 막는다
                role = MarkerRole.Bed,
            });

            Register(new ObjectMarkerDef
            {
                key = "door",
                spritePath = "Sprites/Interior/Door",
                offset = new Vector2(0f, -0.4f),
                blocks = false,
                role = MarkerRole.Door,
            });

            Register(new ObjectMarkerDef { key = "fireplace", spritePath = "Sprites/Interior/Fireplace" });
            Register(new ObjectMarkerDef { key = "plant", spritePath = "Sprites/Interior/Plant" });

            Register(new ObjectMarkerDef
            {
                key = "rug",
                spritePath = "Sprites/Interior/Rug",
                blocks = false,
                sortingOrder = -50,   // 바닥 장식이라 캐릭터 아래
            });

            // 오브젝트를 더 넣으려면 여기에 한 줄:
            //   Register(new ObjectMarkerDef { key = "barrel", spritePath = "Sprites/Environment/Barrel" });
            // 그러면 Tools/Farm 메뉴가 Obj_Barrel 마커 타일을 만들어 주고, 칠하면 그대로 놓인다.
        }

        private static void Register(ObjectMarkerDef def)
        {
            _defs[def.key] = def;
            _all.Add(def);
        }

        public static ObjectMarkerDef Get(string key)
        {
            Init();
            if (string.IsNullOrEmpty(key)) return null;
            return _defs.TryGetValue(key, out var d) ? d : null;
        }

        /// <summary>마커 타일 에셋에 붙는 이름 ("Obj_House").</summary>
        public static string TileName(ObjectMarkerDef def) => "Obj_" + Capitalize(def.key);

        private static string Capitalize(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
