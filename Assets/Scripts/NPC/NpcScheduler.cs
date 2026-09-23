using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// NPC들이 하루를 어떻게 보내는지를 굴린다. 콘텐츠 쪽에서 적는 것은 <b>정류장</b>뿐이고
    /// (몇 시에 · 어느 맵의 · 어느 자리), 사이 경로는 길 찾기가 찾는다 — 걸음 좌표를 찍을 필요가 없다.
    /// "Path_{맵}" 레이어에 길을 칠해 두면 그 길을 따라 돌아서 간다.
    ///
    /// <b>모든 맵을 한꺼번에 굴린다.</b> NPC의 자리는 계산해서 짐작하는 값이 아니라
    /// (지금 있는 맵 + 그 안에서의 좌표)라는 <b>하나뿐인 사실</b>이고, 플레이어가 어느 맵에 있든
    /// 매 프레임 똑같이 앞으로 나아간다. 그래서 "마을에서 보면 걸어 나가는데 계곡에서 보면 이미
    /// 도착해 있다" 같은 어긋남이 아예 생기지 않는다.
    ///
    /// 화면 밖 맵의 지형은 <see cref="WorldGrid"/>가 가볍게 떠 둔 지도를 보고, 플레이어가 있는
    /// 맵만 진짜 <see cref="GameLocation"/>을 본다. 몸(<see cref="NpcActor"/>)은 플레이어와 같은
    /// 맵에 있는 NPC에게만 붙여 그 자리를 비춘다.
    /// </summary>
    public class NpcScheduler : MonoBehaviour
    {
        /// <summary>걷는 빠르기 (현실 초당 칸).</summary>
        private const float WalkSpeed = 2.0f;

        /// <summary>
        /// 길 위를 걸을 때와 아닐 때의 값. NPC는 <b>가장 짧은</b> 길이 아니라 <b>가장 싼</b> 길로 간다 —
        /// 잔디를 가로지르는 것이 길을 타는 것보다 세 배 비싸므로, 조금 돌아가더라도 길을 따라간다.
        /// 길("Path_{맵}" 레이어나 깔아 둔 길 설치물)을 하나도 안 칠했으면 값이 전부 같아져서
        /// 그냥 최단거리로 걷는다.
        /// </summary>
        private const int OnPathCost = 1, OffPathCost = 3;

        /// <summary>어슬렁거릴 때 한 걸음 쉬는 시간의 최소/최대 (초).</summary>
        private const float WanderMin = 2.5f, WanderMax = 6f;

        /// <summary>
        /// 앞이 막혔을 때 비켜 주기를 기다리는 시간. 게임으로 딱 한 칸(10분)이고, 그 뒤에는
        /// 미안하지만 그냥 지나간다 — 플레이어가 길목에 서 있다고 NPC의 하루가 멈추면 곤란하다.
        /// </summary>
        private const float Patience = DayClock.RealSecondsPerStep;

        /// <summary>길이 막혔을 때 다시 찾아보는 간격 (초). 매 프레임 찾을 이유는 없다.</summary>
        private const float RetryInterval = 0.25f;

        private GameManager _game;
        private readonly List<Npc> _npcs = new List<Npc>();
        private readonly HashSet<string> _warned = new HashSet<string>();
        private int _plannedDay = -1;
        private bool _wasBusy;

        /// <summary>NPC 한 명의 "지금". 이 값이 곧 사실이고, 화면은 이것을 비출 뿐이다.</summary>
        private class Npc
        {
            public NpcDefinition def;
            public ScheduleStop[] stops;   // 오늘 일과 (없으면 null → home에 서 있는다)
            public int stop = -1;

            public LocationId map;         // 지금 <b>있는</b> 맵
            public Vector2 position;       // 그 맵 안에서의 자리 (칸 단위)
            public Direction facing = Direction.Down;

            public List<Vector2Int> route; // 지금 걷는 길 (null이면 서 있다)
            public int routeIndex;
            public bool routeLeaves;       // 이 길의 끝이 맵 출구인지
            public LocationId nextMap;     // routeLeaves일 때 넘어갈 맵

            public bool repath;            // 길을 다시 찾아야 한다
            public float retry;            // 다시 찾기까지 남은 시간 (막혔을 때 매 프레임 찾지 않도록)
            public float blockedTime;      // 앞이 막혀 기다린 시간 (초)
            public float wanderCooldown;

            public NpcActor actor;         // 플레이어와 같은 맵일 때만 붙는 몸
            public Vector2Int Tile => Vector2Int.RoundToInt(position);
        }

        public void Init(GameManager game)
        {
            _game = game;
            NpcDatabase.Init();
            foreach (var def in NpcDatabase.All) _npcs.Add(new Npc { def = def });
            PlanDay();
        }

        // ---------- 하루 계획 ----------
        /// <summary>
        /// 그 날의 일과를 고르고, 지금 시각에 있어야 할 자리에 세운다.
        /// 날짜가 바뀔 때마다 (그리고 세이브를 불러올 때) 부른다.
        /// </summary>
        public void PlanDay()
        {
            _plannedDay = _game.Data.currentDay;
            var weather = _game.Today;
            int minutes = _game.Data.currentMinutes;

            foreach (var npc in _npcs)
            {
                var routine = npc.def.schedule != null ? npc.def.schedule.Pick(_plannedDay, weather) : null;
                npc.stops = routine != null ? Sorted(routine.stops) : null;
                npc.stop = StopIndexAt(npc.stops, minutes);
                npc.route = null;
                npc.blockedTime = 0f;

                // 하루를 시작하는 자리. 그 뒤로는 시계를 따라 저절로 굴러간다.
                npc.map = MapOf(npc, npc.stop);
                npc.position = TileOf(npc, npc.stop, npc.map);
                npc.retry = 0f;
                npc.repath = true;
            }
        }

        /// <summary>정류장을 시각 순으로. json에 순서를 뒤섞어 적어도 되도록.</summary>
        private static ScheduleStop[] Sorted(ScheduleStop[] stops)
        {
            var list = new List<ScheduleStop>();
            foreach (var s in stops) if (s != null) list.Add(s);
            list.Sort((a, b) => a.Minutes.CompareTo(b.Minutes));
            return list.ToArray();
        }

        /// <summary>
        /// 맵이 새로 만들어졌다. 몸은 옛 맵과 함께 사라졌으니 참조만 버리고, 진짜 지형에서
        /// 벽 속에 서 있게 된 NPC는 가장 가까운 걸을 수 있는 칸으로 옮긴다
        /// (화면 밖 지도는 어림값이라 나무 한 그루쯤은 어긋날 수 있다).
        /// </summary>
        public void EnterLocation(GameLocation location)
        {
            if (_plannedDay != _game.Data.currentDay) PlanDay();

            foreach (var npc in _npcs)
            {
                npc.actor = null;
                if (npc.map != location.id) continue;

                if (location.IsBlocked(npc.Tile.x, npc.Tile.y))
                {
                    npc.position = location.FindWalkableNear(npc.position);
                    npc.retry = 0f;
                    npc.repath = true;
                }
            }
            SyncActors();
        }

        // ---------- 매 프레임 ----------
        private void Update()
        {
            if (_game == null || _game.Data == null) return;

            // 대화창이 떠 있거나 연출이 도는 동안에는 시간도 NPC도 멈춘다.
            if (_game.Paused) { _wasBusy = true; return; }

            // 연출이 몸을 제자리로 돌려놓았을 수 있다 — 사실대로 다시 잡는다.
            if (_wasBusy) { _wasBusy = false; foreach (var npc in _npcs) { npc.repath = true; npc.retry = 0f; } }

            if (_plannedDay != _game.Data.currentDay) PlanDay();

            int minutes = _game.Data.currentMinutes;
            float dt = Time.deltaTime;
            foreach (var npc in _npcs) Step(npc, minutes, dt);
            SyncActors();
        }

        private void Step(Npc npc, int minutes, float dt)
        {
            int stop = StopIndexAt(npc.stops, minutes);
            if (stop != npc.stop) { npc.stop = stop; npc.repath = true; npc.retry = 0f; npc.blockedTime = 0f; }

            if (npc.repath)
            {
                npc.retry -= dt;
                if (npc.retry <= 0f)
                {
                    npc.retry = RetryInterval;
                    npc.repath = false;
                    Repath(npc);
                    if (npc.route != null) npc.blockedTime = 0f;
                }
            }

            if (npc.route != null) { Walk(npc, dt); return; }

            // 길이 아예 없다 — 막고 선 것이 비켜 주기를 기다린다 (오래되면 Path가 그냥 지나간다).
            if (npc.repath) { npc.blockedTime += dt; return; }
            Wander(npc, dt);
        }

        /// <summary>
        /// 지금 가야 할 곳까지의 길을 다시 찾는다. 목적지가 다른 맵이면 <b>그 쪽으로 나가는 출구</b>까지의
        /// 길을 찾고, 거기 닿으면 맵을 넘어가 다시 이 함수가 불린다 — 그렇게 맵을 몇 개든 건너간다.
        /// </summary>
        private void Repath(Npc npc)
        {
            npc.route = null;
            npc.routeLeaves = false;

            var targetMap = MapOf(npc, npc.stop);
            if (npc.map == targetMap)
            {
                var goal = Vector2Int.RoundToInt(TileOf(npc, npc.stop, npc.map));
                if (goal == npc.Tile) { FaceAtStop(npc); return; }

                npc.route = Path(npc, npc.Tile, goal, goalIsExit: false);
                npc.routeIndex = 1;
                if (npc.route == null) npc.repath = true;   // 지금은 막혔다 — 다음 프레임에 다시 본다
                return;
            }

            // 이웃이 아니면 통로를 따라 첫 걸음을 구한다.
            var hop = MapGraph.TryGetNextHop(npc.map, targetMap, out var next) ? next : targetMap;
            if (!ExitTile(npc.map, hop, npc.Tile, out var exit))
            {
                Warn($"{npc.def.id}: {npc.map}에서 {hop}(으)로 나가는 길이 없습니다. " +
                     "MapGraph에 통로를 적었는지, 출구 마커를 칠했는지 확인하세요.");
                return;
            }

            npc.nextMap = hop;
            if (exit == npc.Tile) { CrossTo(npc); return; }

            npc.route = Path(npc, npc.Tile, exit, goalIsExit: true);
            npc.routeIndex = 1;
            npc.routeLeaves = npc.route != null;
            if (npc.route == null) npc.repath = true;
        }

        /// <summary>경로를 따라 한 프레임만큼 나아간다.</summary>
        private void Walk(Npc npc, float dt)
        {
            var next = npc.route[npc.routeIndex];
            var destination = new Vector2(next.x, next.y);

            var delta = destination - npc.position;
            if (delta.sqrMagnitude > 1e-6f)
                npc.facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0 ? Direction.Right : Direction.Left)
                    : (delta.y > 0 ? Direction.Up : Direction.Down);

            // 아직 그 칸에 발을 들이지 않았는데 플레이어가 서 있으면, 참을성이 다할 때까지 기다린다.
            if (npc.Tile != next && PlayerAt(npc, next))
            {
                npc.blockedTime += dt;
                if (npc.blockedTime < Patience) return;
            }
            else npc.blockedTime = 0f;

            npc.position = Vector2.MoveTowards(npc.position, destination, WalkSpeed * dt);
            if ((npc.position - destination).sqrMagnitude > 1e-6f) return;

            npc.position = destination;
            if (++npc.routeIndex < npc.route.Count) return;

            // 길 끝에 닿았다.
            npc.route = null;
            if (npc.routeLeaves) { npc.routeLeaves = false; CrossTo(npc); return; }
            FaceAtStop(npc);
        }

        /// <summary>출구에 닿았다 — 다음 맵으로 넘어가 그 맵의 입구에 선다.</summary>
        private void CrossTo(Npc npc)
        {
            var from = npc.map;
            npc.map = npc.nextMap;
            var hint = Vector2Int.RoundToInt(TileOf(npc, npc.stop, npc.map));
            npc.position = EntryPoint(npc.map, from, hint);
            npc.blockedTime = 0f;
            npc.retry = 0f;
            npc.repath = true;
        }

        private void FaceAtStop(Npc npc)
        {
            var facing = FacingOf(npc, npc.stop);
            if (facing.HasValue) npc.facing = facing.Value;
        }

        /// <summary>정류장에 "wander"를 적어 뒀으면 가끔 그 반경 안의 아무 칸으로 한 번 걸어간다.</summary>
        private void Wander(Npc npc, float dt)
        {
            int radius = npc.stops != null && npc.stop >= 0 && npc.stop < npc.stops.Length
                ? npc.stops[npc.stop].wander : 0;
            if (radius <= 0) return;

            npc.wanderCooldown -= dt;
            if (npc.wanderCooldown > 0f) return;
            npc.wanderCooldown = Random.Range(WanderMin, WanderMax);

            var center = Vector2Int.RoundToInt(TileOf(npc, npc.stop, npc.map));
            var goal = new Vector2Int(center.x + Random.Range(-radius, radius + 1),
                                      center.y + Random.Range(-radius, radius + 1));
            if (goal == npc.Tile || Blocked(npc, npc.map, goal)) return;

            npc.route = Path(npc, npc.Tile, goal, goalIsExit: false);
            npc.routeIndex = 1;
        }

        // ---------- 몸 붙이기 ----------
        /// <summary>지금 플레이어가 있는 맵의 NPC에게만 몸을 붙이고, 사실대로 자리를 비춘다.</summary>
        private void SyncActors()
        {
            var location = _game.CurrentLocation;
            if (location == null) return;

            foreach (var npc in _npcs)
            {
                bool here = npc.map == location.id;
                if (here && npc.actor == null) npc.actor = _game.SpawnNpc(npc.def, npc.Tile);
                else if (!here && npc.actor != null) { _game.DespawnNpc(npc.actor); npc.actor = null; }

                if (npc.actor != null) npc.actor.Place(npc.position, npc.facing, location);
            }
        }

        // ---------- 지형 물어보기 ----------
        // 플레이어가 있는 맵은 진짜 지형(GameLocation)을, 나머지는 가볍게 떠 둔 지도(WorldGrid)를 본다.

        private bool IsCurrent(LocationId map)
            => _game.CurrentLocation != null && _game.CurrentLocation.id == map;

        private bool Blocked(Npc npc, LocationId map, Vector2Int tile)
            => IsCurrent(map)
                ? _game.CurrentLocation.IsBlockedForStory(tile, npc.actor)
                : WorldGrid.Blocked(map, tile);

        private bool IsPath(LocationId map, Vector2Int tile)
            => IsCurrent(map)
                ? _game.CurrentLocation.IsPath(tile.x, tile.y)
                : WorldGrid.IsPath(map, tile);

        private bool PlayerAt(Npc npc, Vector2Int tile)
            => IsCurrent(npc.map) && tile == _game.PlayerTile();

        private bool ExitTile(LocationId map, LocationId target, Vector2Int from, out Vector2Int tile)
        {
            if (IsCurrent(map) && _game.CurrentLocation.TryGetExitTile(target, from, out tile)) return true;
            return WorldGrid.TryGetExitTile(map, target, from, out tile);
        }

        private Vector2 EntryPoint(LocationId map, LocationId from, Vector2Int hint)
            => IsCurrent(map)
                ? _game.CurrentLocation.FindEntryFrom(from, hint)
                : WorldGrid.EntryFrom(map, from, hint);

        /// <summary>길(Path)을 되도록 밟는 경로. 오래 막혀 있었으면 플레이어를 없는 셈 친다.</summary>
        private List<Vector2Int> Path(Npc npc, Vector2Int start, Vector2Int goal, bool goalIsExit)
        {
            var map = npc.map;
            bool ignorePlayer = npc.blockedTime >= Patience;

            return StoryPathfinder.Find(start, goal,
                tile => (!(goalIsExit && tile == goal) && Blocked(npc, map, tile))
                        || (!ignorePlayer && tile != start && PlayerAt(npc, tile)),
                tile => IsPath(map, tile) ? OnPathCost : OffPathCost);
        }

        // ---------- 오늘 일과 읽기 ----------
        /// <summary>그 시각에 유효한 정류장 번호. 첫 정류장 시각 전이면 0 (아침부터 거기 있다).</summary>
        private static int StopIndexAt(ScheduleStop[] stops, int minutes)
        {
            if (stops == null || stops.Length == 0) return -1;
            int index = 0;
            for (int i = 0; i < stops.Length; i++)
                if (stops[i].Minutes <= minutes) index = i;
            return index;
        }

        /// <summary>그 정류장이 있는 맵. map을 비워 둔 정류장은 <b>앞 정류장</b>을 따라간다.</summary>
        private static LocationId MapOf(Npc npc, int stop)
        {
            if (npc.stops != null)
                for (int i = Mathf.Min(stop, npc.stops.Length - 1); i >= 0; i--)
                    if (!string.IsNullOrEmpty(npc.stops[i].map)
                        && System.Enum.TryParse(npc.stops[i].map, out LocationId id)) return id;
            return npc.def.HomeLocation;
        }

        /// <summary>
        /// 그 정류장의 칸. 마커를 아직 칠하지 않았어도 <b>NPC가 사라지지는 않게</b> 한다 —
        /// home으로, 그것도 설 수 없으면 맵 가운데의 걸을 수 있는 칸으로 떨어뜨리고 한 번 경고한다.
        /// </summary>
        private Vector2 TileOf(Npc npc, int stop, LocationId map)
        {
            if (npc.stops != null && stop >= 0 && stop < npc.stops.Length)
            {
                var s = npc.stops[stop];
                if (s.HasTile && !Blocked(npc, map, new Vector2Int(s.x, s.y))) return new Vector2(s.x, s.y);
                if (!s.HasTile && Waypoint(map, s.spot, out var tile)) return tile;

                Warn(s.HasTile
                    ? $"{npc.def.id}: {map} 맵의 ({s.x},{s.y})에 설 수 없습니다."
                    : $"{npc.def.id}: {map} 맵에 \"Obj_Spot{s.spot}\" 마커가 칠해져 있지 않습니다.");
            }

            var home = new Vector2Int(npc.def.home.x, npc.def.home.y);
            if (!Blocked(npc, map, home)) return home;

            var size = WorldGrid.Size(map);
            return WorldGrid.NearestWalkable(map, new Vector2Int(size.x / 2, size.y / 2));
        }

        private bool Waypoint(LocationId map, string name, out Vector2Int tile)
        {
            if (IsCurrent(map) && _game.CurrentLocation.TryGetWaypoint(name, out tile)) return true;
            return WorldGrid.TryGetWaypoint(map, name, out tile);
        }

        private static Direction? FacingOf(Npc npc, int stop)
            => npc.stops != null && stop >= 0 && stop < npc.stops.Length ? npc.stops[stop].Facing : null;

        /// <summary>같은 경고를 매 프레임 쏟지 않도록 한 번만 남긴다.</summary>
        private void Warn(string message)
        {
            if (_warned.Add(message)) Debug.LogWarning("[NpcSchedule] " + message);
        }
    }
}
