using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>설치물의 큰 갈래. 울타리는 막고, 길은 밟고 지나간다.</summary>
    public enum PlaceableKind { Fence, Road }

    /// <summary>
    /// 이웃 연결 상태를 나타내는 비트와, 그 상태에 붙이는 <b>이름 꼬리표</b>.
    /// 같은 종류끼리만 이어진 것으로 본다 — 울타리 옆의 길은 이웃으로 치지 않는다.
    ///
    /// 꼬리표는 언제나 <b>북→동→남→서(N→E→S→W) 순서</b>로 이어진 방향만 적는다.
    /// 조합 하나에 이름이 딱 하나뿐이라 스프라이트 에디터에서 헷갈릴 일이 없다:
    ///
    ///   None 홀로     N 북만      E 동만 ╴    S 남만      W 서만 ╶
    ///   NS │ 세로     EW ─ 가로
    ///   NE └          NW ┘        ES ┌        SW ┐
    ///   NES ├         NEW ┴       NSW ┤       ESW ┬       NESW ✚
    /// </summary>
    public static class Connect
    {
        public const int N = 1, E = 2, S = 4, W = 8;
        public const int Count = 16;   // 4비트 조합 전부

        /// <summary>홀로 놓였을 때 쓰는 꼬리표.</summary>
        public const string NoneSuffix = "None";

        /// <summary>없는 조합을 한 장으로 때울 때 쓰는 꼬리표 (가로와 세로가 만나는 모든 경우).</summary>
        public const string JunctionSuffix = "Junction";

        /// <summary>한 칸짜리 문의 닫힘/열림 꼬리표.</summary>
        public const string GateClosedSuffix = "GateClosed";
        public const string GateOpenSuffix = "GateOpen";

        /// <summary>
        /// 두 칸짜리 문의 네 장. 왼쪽/오른쪽 칸 x 닫힘/열림.
        ///   {접두사}_GateClosedL  {접두사}_GateClosedR
        ///   {접두사}_GateOpenL    {접두사}_GateOpenR
        /// </summary>
        public const string GateClosedLeft = "GateClosedL";
        public const string GateClosedRight = "GateClosedR";
        public const string GateOpenLeft = "GateOpenL";
        public const string GateOpenRight = "GateOpenR";

        /// <summary>
        /// 세로 담(│)의 두 가지 판. 담이 어느 쪽 모서리로 꺾이느냐에 따라 생김새가 다르다.
        ///   NS_L — ┌ 나 └ 처럼 <b>동쪽으로</b> 꺾이는 모서리에 붙는 왼쪽 담
        ///   NS_R — ┐ 나 ┘ 처럼 <b>서쪽으로</b> 꺾이는 모서리에 붙는 오른쪽 담
        /// 둘 중 하나만 있거나 판단할 수 없으면 그냥 NS를 쓴다.
        /// </summary>
        public const string VerticalLeft = "NS_L";
        public const string VerticalRight = "NS_R";

        /// <summary>연결 상태 -> 이름 꼬리표 ("NE", "ESW", "None" ...).</summary>
        public static string Suffix(int mask)
        {
            mask &= 0xF;
            if (mask == 0) return NoneSuffix;

            string s = "";
            if ((mask & N) != 0) s += "N";
            if ((mask & E) != 0) s += "E";
            if ((mask & S) != 0) s += "S";
            if ((mask & W) != 0) s += "W";
            return s;
        }

        public static bool HasVertical(int mask) => (mask & (N | S)) != 0;
        public static bool HasHorizontal(int mask) => (mask & (E | W)) != 0;
    }

    /// <summary>
    /// 설치물 한 종류의 정의. 그림은 <b>스프라이트 이름</b>으로 찾는다 — 시트를 스프라이트 에디터에서
    /// 16x16으로 자르고 조각마다 "{접두사}_{꼬리표}"로 이름만 지어 두면 코드에 좌표표를 적을 필요가 없다.
    /// 울타리·길을 한 종류 더 넣는 데 필요한 것은 Register 한 덩어리뿐이다.
    ///
    /// 예: WhiteFence.png 를 잘라 WhiteFence_NS, WhiteFence_EW, WhiteFence_NE ... 로 이름 짓기.
    /// </summary>
    public class PlaceableDef
    {
        /// <summary>저장과 아이템에 함께 쓰는 id. 아이템 id도 같은 값을 쓴다.</summary>
        public string id;
        public string displayName;
        public PlaceableKind kind;

        /// <summary>잘라 쓸 시트의 Resources 경로 (예: "Sprites/Fence/WhiteFence").</summary>
        public string sheetPath;

        /// <summary>
        /// 스프라이트 이름 앞부분. 비우면 시트 파일 이름을 그대로 쓴다.
        /// 시트 한 장에 여러 종류를 넣었을 때만 따로 적으면 된다 (Road.png 안의 WoodRoad_* 처럼).
        /// </summary>
        public string spritePrefix;

        /// <summary>지나갈 수 없는지. 울타리는 true, 길은 false.</summary>
        public bool blocks;

        /// <summary>문이면 true — 우클릭으로 여닫을 수 있고, 열려 있는 동안에는 지나갈 수 있다.</summary>
        public bool isGate;

        /// <summary>철거했을 때 떨어지는 것 (LootTableDatabase의 id).</summary>
        public string dropTableId;

        /// <summary>그림을 칸 중심에서 얼마나 올려 그릴지. 울타리처럼 키가 있는 것에 쓴다.</summary>
        public Vector2 offset = Vector2.zero;

        /// <summary>인벤토리 아이콘에 쓸 꼬리표. 비우면 홀로 놓인 모습(None)을 쓴다.</summary>
        public string iconSuffix;

        /// <summary>스프라이트 이름 앞부분 (spritePrefix가 비어 있으면 시트 파일 이름).</summary>
        public string Prefix
        {
            get
            {
                if (!string.IsNullOrEmpty(spritePrefix)) return spritePrefix;
                if (string.IsNullOrEmpty(sheetPath)) return null;
                int slash = sheetPath.LastIndexOf('/');
                return slash >= 0 ? sheetPath.Substring(slash + 1) : sheetPath;
            }
        }

        /// <summary>이 종류끼리 이어진 것으로 볼지 판단한다. 울타리는 울타리끼리, 길은 길끼리.</summary>
        public bool ConnectsTo(PlaceableDef other) => other != null && other.kind == kind;

        /// <summary>지금 상태에서 지나갈 수 없는지 (열린 문은 지나갈 수 있다).</summary>
        public bool BlocksNow(bool open) => blocks && !(isGate && open);

        /// <summary>
        /// 지금 상태에 쓸 그림. 딱 맞는 이름이 없으면 차례로 물러선다 —
        /// 갈림길이면 Junction, 곧게 이어졌으면 곧은 줄, 그다음엔 이어진 방향을 하나씩 빼 가며
        /// 가장 비슷한 그림을 찾는다. 그래서 시트에 그림이 몇 장 없어도 빈 칸이 생기지 않는다.
        /// </summary>
        /// <param name="part">두 칸짜리 문의 어느 쪽인지 (0=왼쪽, 1=오른쪽). 문이 아니면 -1.</param>
        /// <param name="verticalSide">세로 담의 판 (-1=왼쪽 담, +1=오른쪽 담, 0=아무거나).</param>
        public Sprite GetSprite(int mask, bool open, int part = -1, int verticalSide = 0)
        {
            if (isGate)
            {
                var gate = FindGate(open, part);
                if (gate != null) return gate;
            }

            var exact = Find(Connect.Suffix(mask));
            if (exact != null) return exact;

            // 가로와 세로가 만나는 조합은 Junction 한 장으로 때울 수 있다 (길처럼 모서리 그림이 없는 시트용).
            if (Connect.HasVertical(mask) && Connect.HasHorizontal(mask))
            {
                var junction = Find(Connect.JunctionSuffix);
                if (junction != null) return junction;
            }

            // 곧은 줄로 대신한다. 두 경우 모두 여기서 걸린다:
            //  - 한쪽으로만 이어졌을 때(위 칸에만 붙은 N) — 끝맺음 그림이 없는 시트가 대부분이라,
            //    이걸 빼먹으면 홀로(None) 모습이 되어 세로로 이어 놓아도 끊겨 보인다.
            //  - T자·십자처럼 양쪽으로 곧게 지나갈 때 — 가지 하나가 없는 것처럼 보이는 편이 자연스럽다.
            bool vertical = Connect.HasVertical(mask), horizontal = Connect.HasHorizontal(mask);
            if (vertical && (!horizontal || (mask & (Connect.N | Connect.S)) == (Connect.N | Connect.S)))
            {
                var straight = FindVertical(verticalSide);
                if (straight != null) return straight;
            }
            if (horizontal && (!vertical || (mask & (Connect.E | Connect.W)) == (Connect.E | Connect.W)))
            {
                var straight = Find("EW");
                if (straight != null) return straight;
            }

            // 그래도 없으면 방향을 하나씩 빼 가며 가장 비슷한 그림을 찾는다.
            for (int drop = 1; drop <= 4; drop++)
            {
                for (int candidate = 15; candidate >= 0; candidate--)
                {
                    if ((candidate & mask) != candidate) continue;              // 부분집합만
                    if (CountBits(mask) - CountBits(candidate) != drop) continue;
                    var similar = Find(Connect.Suffix(candidate));
                    if (similar != null) return similar;
                }
            }

            // 홀로 놓인 모습이 없는 시트(돌담처럼)라도 빈 칸이 되지 않게.
            return Find("EW") ?? FindVertical(verticalSide);
        }

        /// <summary>
        /// 문 그림. 두 칸짜리(네 장)를 먼저 찾고, 없으면 한 칸짜리 GateOpen을 쓴다.
        /// 닫힌 문은 전용 그림이 없으면 null을 돌려주어 <b>울타리와 똑같이 이어지도록</b> 둔다.
        /// </summary>
        private Sprite FindGate(bool open, int part)
        {
            if (IsWideGate)
            {
                // 나란히 놓아 짝을 이뤘을 때만 문이 된다.
                if (part >= 0)
                {
                    string suffix = open
                        ? (part == 0 ? Connect.GateOpenLeft : Connect.GateOpenRight)
                        : (part == 0 ? Connect.GateClosedLeft : Connect.GateClosedRight);
                    var wide = Find(suffix);
                    if (wide != null) return wide;
                }
                // 혼자 서 있는 문은 울타리를 홀로 놓은 모습 그대로다.
                return Find(Connect.NoneSuffix);
            }
            return open ? Find(Connect.GateOpenSuffix) : Find(Connect.GateClosedSuffix);
        }

        /// <summary>세로 담. 붙어 있는 모서리가 꺾이는 쪽에 맞는 판을 고른다.</summary>
        private Sprite FindVertical(int side)
        {
            if (side < 0) { var l = Find(Connect.VerticalLeft); if (l != null) return l; }
            if (side > 0) { var r = Find(Connect.VerticalRight); if (r != null) return r; }
            return Find("NS") ?? Find(Connect.VerticalLeft) ?? Find(Connect.VerticalRight);
        }

        /// <summary>이 설치물이 두 칸짜리 문인지 (네 장짜리 그림이 있으면 그렇다).</summary>
        public bool IsWideGate => isGate && Find(Connect.GateClosedLeft) != null;

        private static int CountBits(int mask)
        {
            int n = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) n++;
            return n;
        }

        /// <summary>인벤토리에 보여 줄 그림.</summary>
        public Sprite GetIcon()
        {
            if (!string.IsNullOrEmpty(iconSuffix))
            {
                var icon = Find(iconSuffix);
                if (icon != null) return icon;
            }
            if (isGate)
            {
                // 문 아이콘은 울타리와 구분되어야 하므로 문짝 그림을 먼저 쓴다
                // (혼자 놓인 문은 울타리와 똑같이 생겨서 아이콘까지 같으면 고를 수가 없다).
                var gate = Find(Connect.GateClosedLeft)
                           ?? Find(Connect.GateClosedSuffix)
                           ?? Find(Connect.GateOpenSuffix);
                if (gate != null) return gate;
            }
            return GetSprite(0, false);
        }

        /// <summary>
        /// 조각 하나를 이름으로 찾는다. <b>계절판을 먼저</b> 본다 —
        /// "WoodFence_EW_Winter" 처럼 이름 뒤에 계절을 붙여 두면 그 계절에만 그 그림이 쓰인다.
        /// (나무·상점 수레의 "_Winter" 규칙과 같다. 없으면 계절 없는 이름을 쓴다.)
        /// </summary>
        private Sprite Find(string suffix)
        {
            string baseName = Prefix + "_" + suffix;
            var seasonal = PlaceableSheet.Find(sheetPath, baseName + "_" + Seasons.Key(Seasons.Current));
            return seasonal != null ? seasonal : PlaceableSheet.Find(sheetPath, baseName);
        }
    }

    /// <summary>
    /// 잘라 둔 시트에서 스프라이트를 <b>이름으로</b> 찾아 주는 캐시.
    /// Resources.LoadAll은 시트 하나당 한 번만 부르고 이름표로 만들어 들고 있는다.
    /// </summary>
    public static class PlaceableSheet
    {
        private static readonly Dictionary<string, Dictionary<string, Sprite>> _sheets =
            new Dictionary<string, Dictionary<string, Sprite>>();

        /// <summary>없으면 null (호출부가 다른 이름으로 다시 물어본다).</summary>
        public static Sprite Find(string sheetPath, string spriteName)
        {
            if (string.IsNullOrEmpty(sheetPath) || string.IsNullOrEmpty(spriteName)) return null;
            var sheet = LoadSheet(sheetPath);
            if (sheet == null) return null;
            return sheet.TryGetValue(spriteName, out var s) ? s : null;
        }

        /// <summary>시트에 실제로 들어 있는 이름들 (무엇을 더 잘라야 하는지 알리는 데 쓴다).</summary>
        public static ICollection<string> NamesIn(string sheetPath)
        {
            var sheet = LoadSheet(sheetPath);
            return sheet != null ? sheet.Keys : (ICollection<string>)new List<string>();
        }

        private static Dictionary<string, Sprite> LoadSheet(string sheetPath)
        {
            if (_sheets.TryGetValue(sheetPath, out var cached)) return cached;

            var all = Resources.LoadAll<Sprite>(sheetPath);
            Dictionary<string, Sprite> map = null;
            if (all != null && all.Length > 0)
            {
                map = new Dictionary<string, Sprite>(all.Length);
                foreach (var s in all) map[s.name] = s;
            }
            else
            {
                Debug.LogWarning($"[PlaceableSheet] 시트를 찾지 못했거나 잘라 놓지 않았습니다: {sheetPath} " +
                                 "— 스프라이트 에디터에서 16x16으로 자르고 조각마다 이름을 지어 주세요.");
            }
            _sheets[sheetPath] = map;
            return map;
        }
    }
}
