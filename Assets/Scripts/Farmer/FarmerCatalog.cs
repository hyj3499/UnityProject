using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>고를 수 있는 것들의 갈래.</summary>
    public enum FarmerCategory
    {
        Hair, Eyes, FacialHair,
        Top, Pants, Skirt, Dress, Overalls, Suit, Robe, Underwear, Shoes,
        HeadGear, FaceGear, BackGear,
    }

    /// <summary>고를 수 있는 한 가지 — 머리 모양 하나, 옷 한 벌.</summary>
    public class FarmerItem
    {
        public FarmerCategory category;

        /// <summary>부위 이름을 뺀 경로. 여기에 "_shirts" 같은 부위 이름을 붙이면 파일이 된다.</summary>
        public string path;

        public string label;

        /// <summary>이 품목이 실제로 가지고 있는 부위들.</summary>
        public FarmerSlot[] slots;

        /// <summary>
        /// 이 옷이 쓰는 소매 갈래. 소매 그림은 옷마다 있는 것이 아니라 갈래마다 하나씩 있고
        /// (Sleeves/arm_left_<b>overshirts</b>.png), 색만 이 옷 것으로 칠해 쓴다.
        /// 소매가 없는 옷이면 비어 있다.
        /// </summary>
        public string sleeve;

        public bool Has(FarmerSlot slot)
        {
            foreach (var s in slots) if (s == slot) return true;
            return false;
        }
    }

    /// <summary>
    /// 에셋 폴더에 무엇이 들어 있는지 적어 둔 목록.
    ///
    /// 실행 중에 폴더를 훑지 않고 목록으로 들고 있는 이유는, Resources 폴더를 통째로 뒤지면
    /// 쓰지도 않을 그림이 전부 메모리에 올라오기 때문이다. 그림을 새로 넣을 때마다 아래
    /// <b>Raw</b>에 한 줄씩 더하면 된다.
    ///
    /// 한 줄의 뜻: (갈래, 부위 이름을 뺀 경로, 화면에 보일 이름, 가진 부위들, 소매 갈래)
    ///   · 경로+부위 이름이 곧 파일이다 — "Shirts/ribon" + "_shirts" → Shirts/ribon_shirts.png
    ///   · 소매 갈래는 상의에만 적는다. Sleeves 폴더의 arm_left_&lt;갈래&gt;.png 를 가져다 쓴다.
    /// </summary>
    public static class FarmerCatalog
    {
        private class Entry
        {
            public FarmerCategory cat; public string path, label, slots, sleeve;
            public Entry(FarmerCategory c, string p, string l, string s, string sleeve = null)
            { cat = c; path = p; label = l; slots = s; this.sleeve = sleeve; }
        }

        private static readonly Entry[] Raw =
        {
            new Entry(FarmerCategory.Hair, "Hair/Parm_long", "long hair", "hairs"),
            new Entry(FarmerCategory.Pants, "Pants/short", "short pants", "pants"),
            new Entry(FarmerCategory.Top, "Shirts/ribon", "ribon shirt", "shirts", sleeve: "overshirts"),
        };

        private static Dictionary<FarmerCategory, List<FarmerItem>> _byCategory;

        /// <summary>이 갈래에서 고를 수 있는 것들.</summary>
        public static IReadOnlyList<FarmerItem> Of(FarmerCategory category)
        {
            if (_byCategory == null) Build();
            return _byCategory.TryGetValue(category, out var list) ? list : new List<FarmerItem>();
        }

        /// <summary>경로로 하나 찾기. 없으면 null.</summary>
        public static FarmerItem Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_byCategory == null) Build();
            foreach (var list in _byCategory.Values)
                foreach (var item in list)
                    if (item.path == path) return item;
            return null;
        }

        /// <summary>목록에서 몇 번째인지 (없으면 0).</summary>
        public static int IndexOf(FarmerCategory category, string path)
        {
            var list = Of(category);
            for (int i = 0; i < list.Count; i++) if (list[i].path == path) return i;
            return 0;
        }

        private static void Build()
        {
            _byCategory = new Dictionary<FarmerCategory, List<FarmerItem>>();
            foreach (var e in Raw)
            {
                if (!_byCategory.TryGetValue(e.cat, out var list))
                    _byCategory[e.cat] = list = new List<FarmerItem>();

                list.Add(new FarmerItem
                {
                    category = e.cat,
                    path = e.path,
                    label = e.label,
                    slots = ParseSlots(e.slots),
                    sleeve = e.sleeve,
                });
            }
        }

        private static FarmerSlot[] ParseSlots(string packed)
        {
            var found = new List<FarmerSlot>();
            foreach (var p in packed.Split('|'))
                if (FarmerSlots.BySuffix(p, out var slot) && !found.Contains(slot))
                    found.Add(slot);
            return found.ToArray();
        }
    }
}
