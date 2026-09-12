using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>발생 신호를 받아 한 번에 하나의 이벤트를 실행한다. 보상은 정상 종료 때만 반영한다.</summary>
    public class StoryEventDirector : MonoBehaviour
    {
        public bool IsRunning { get; private set; }
        public string CurrentEventId { get; private set; }
        private GameManager _game;
        private UIManager _ui;
        private List<StoryEventDefinition> _definitions;
        private readonly Queue<PendingEvent> _pending = new Queue<PendingEvent>();
        private readonly HashSet<string> _failed = new HashSet<string>();
        private readonly Dictionary<NpcActor, Vector3> _npcPositions = new Dictionary<NpcActor, Vector3>();
        private readonly Dictionary<NpcActor, Direction> _npcDirections = new Dictionary<NpcActor, Direction>();
        private readonly Dictionary<string, int> _affection = new Dictionary<string, int>();
        private StoryEventState _working;
        private int _money;
        private Vector3 _playerPosition;
        private Direction _playerDirection;
        private Coroutine _routine;
        private int _releaseFrame;

        private class PendingEvent
        {
            public StoryEventDefinition definition;
            public GameLocation location;
            public int day;
        }

        public void Init(GameManager game, UIManager ui)
        {
            _game = game;
            _ui = ui;
            _definitions = StoryEventDatabase.Load();
            if (game.Data.story == null) game.Data.story = new StoryEventState(); // 기존 저장: 인트로 재생 안 함
            game.Data.story.Normalize();
            if (game.Data.story.introPending) Signal("NewGame");
            Signal("EnterLocation");
            Signal("DayStarted");
        }

        /// <summary>커스텀 상호작용에서는 game.Events.Signal("Manual", "신호이름") 호출.</summary>
        public void Signal(string trigger, string triggerId = null)
        {
            if (_definitions == null) return;
            foreach (var def in _definitions)
            {
                if (def.trigger != trigger || (!string.IsNullOrEmpty(def.triggerId) && def.triggerId != triggerId)) continue;
                // 같은 발생 신호가 실행 도중 반복되어 큐를 무한히 채우지 않도록 한다.
                if (CurrentEventId == def.id || ContainsPending(def.id)) continue;
                _pending.Enqueue(new PendingEvent { definition = def, location = _game.CurrentLocation, day = _game.Data.currentDay });
            }
        }

        private bool ContainsPending(string id)
        {
            foreach (var p in _pending) if (p.definition.id == id) return true;
            return false;
        }

        public bool TryInteract(string npcId)
        {
            if (!CanStart()) return false;
            foreach (var def in _definitions)
            {
                if (def.trigger != "InteractNpc" || def.triggerId != npcId || !Eligible(def)) continue;
                StartEvent(def);
                return true;
            }
            return false;
        }

        private bool CanStart() => !IsRunning && Time.frameCount > _releaseFrame && !_game.Paused
                                  && !(_game.Fishing != null && _game.Fishing.IsActive);

        private bool Eligible(StoryEventDefinition def) => !_failed.Contains(def.id)
            && StoryEventRules.CanRepeat(def, _game.Data.story, _game.Data.currentDay)
            && StoryEventRules.Matches(def.conditions, _game.Data, _game.Data.story, _game.Money, _game.HeartsOf);

        private void Update()
        {
            if (_game == null || !CanStart()) return;
            while (_pending.Count > 0)
            {
                var next = _pending.Dequeue();
                if (next.location != _game.CurrentLocation || next.day != _game.Data.currentDay || !Eligible(next.definition)) continue;
                StartEvent(next.definition);
                break;
            }
        }

        private void StartEvent(StoryEventDefinition def)
        {
            IsRunning = true; // 코루틴 시작 전부터 모든 입력/저장 차단
            CurrentEventId = def.id;
            _playerPosition = _game.Player.transform.position;
            _playerDirection = _game.Player.facing;
            _npcPositions.Clear();
            _npcDirections.Clear();
            _affection.Clear();
            _working = new StoryEventState { flags = new List<string>(_game.Data.story.flags), completed = _game.Data.story.completed };
            _money = _game.Money;
            _game.Player.BeginStoryControl();
            _ui.SyncPaused();
            _routine = StartCoroutine(RunSafely(def));
        }

        // 중첩 IEnumerator의 예외까지 이쪽에서 처리해야 Move 실패 시에도 잠금이 반드시 풀린다.
        private IEnumerator RunSafely(StoryEventDefinition def)
        {
            yield return null; // 시작시킨 입력이 대화창으로 전파되지 않도록 분리
            var stack = new Stack<IEnumerator>();
            stack.Push(Execute(def));
            bool succeeded = true;
            while (stack.Count > 0)
            {
                object yielded = null;
                bool moved = false;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) yielded = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StoryEvent] {def.id} 중단: {e.Message}");
                    _failed.Add(def.id);
                    succeeded = false;
                    break;
                }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                yield return yielded;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            // 마지막 클릭이 농사/마법으로 새지 않도록 완료 프레임도 잠금을 유지한다.
            yield return null;
            try
            {
                if (succeeded)
                {
                    _game.Data.story.flags = _working.flags;
                    foreach (var pair in _affection) _game.NpcState(pair.Key).affection = pair.Value;
                    _game.Data.farmer.money = _money;
                    _game.Data.story.Complete(def.id, _game.Data.currentDay);
                    if (def.trigger == "NewGame")
                        _game.Data.story.introPending = _definitions.Exists(d => d.trigger == "NewGame" && _game.Data.story.LastDay(d.id) < 0);
                    _game.AddMoney(0); // 모든 상태를 확정한 뒤 HUD에 알린다.
                }
            }
            finally { Cleanup(true); }
            _routine = null;
        }

        private IEnumerator Execute(StoryEventDefinition def)
        {
            var labels = new Dictionary<string, int>();
            for (int i = 0; i < def.commands.Length; i++)
                if (!string.IsNullOrEmpty(def.commands[i].label)) labels.Add(def.commands[i].label, i);
            int budget = 4096;
            for (int pc = 0; pc < def.commands.Length; pc++)
            {
                if (--budget < 0) throw new InvalidOperationException("명령 실행 상한 초과. 분기 순환을 확인하세요.");
                var c = def.commands[pc];
                switch (c.op)
                {
                    case "Say":
                    case "Choice":
                        bool finished = false;
                        string target = null;
                        _ui.ShowStoryDialogue(c, selected => { target = selected; finished = true; }, () => finished = true);
                        while (!finished) yield return null;
                        if (target != null) pc = labels[target] - 1;
                        break;
                    case "Move": yield return Move(c); break;
                    case "Face":
                        var direction = (Direction)Enum.Parse(typeof(Direction), c.direction);
                        if (c.actor == "player") _game.Player.SetStoryMotion(false, direction);
                        else Actor(c.actor).SetFacing(direction);
                        break;
                    case "Animate":
                        _game.Player.SetStoryPose((FarmerAnim)Enum.Parse(typeof(FarmerAnim), c.animation));
                        yield return Wait(c.seconds);
                        _game.Player.ClearStoryPose();
                        break;
                    case "Wait": yield return Wait(c.seconds); break;
                    case "Flag":
                        _working.flags.Remove(c.id);
                        if (c.value) _working.flags.Add(c.id);
                        break;
                    case "Money":
                        long money = (long)_money + c.amount;
                        if (money < 0 || money > int.MaxValue) throw new InvalidOperationException("소지금 부족 또는 범위 초과");
                        _money = (int)money;
                        break;
                    case "Affection":
                        _affection[c.id] = (int)Math.Max(0L, Math.Min(1000L, (long)Affection(c.id) + c.amount));
                        break;
                    case "If":
                        var branch = StoryEventRules.Matches(c.conditions, _game.Data, _working, _money, id => Affection(id) / 100)
                            ? c.target : c.otherwise;
                        if (!string.IsNullOrEmpty(branch)) pc = labels[branch] - 1;
                        break;
                    case "Goto": pc = labels[c.target] - 1; break;
                    case "End": yield break;
                }
            }
        }

        private int Affection(string id) => _affection.TryGetValue(id, out int value) ? value : _game.NpcState(id).affection;

        private NpcActor Actor(string id)
        {
            var actor = _game.FindNpcActor(id);
            if (actor == null) throw new InvalidOperationException("현재 장소에 NPC가 없습니다: " + id);
            if (!_npcPositions.ContainsKey(actor))
            { _npcPositions.Add(actor, actor.transform.position); _npcDirections.Add(actor, actor.Facing); }
            return actor;
        }

        private static IEnumerator Wait(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds) { elapsed += Time.unscaledDeltaTime; yield return null; }
        }

        private IEnumerator Move(StoryCommand c)
        {
            var npc = c.actor == "player" ? null : Actor(c.actor);
            Transform actor = npc == null ? _game.Player.transform : npc.transform;
            foreach (var point in c.path)
            {
                var start = Vector2Int.RoundToInt(actor.position);
                var goal = new Vector2Int(point.x, point.y);
                var route = StoryPathfinder.Find(start, goal, tile =>
                    _game.CurrentLocation.IsBlockedForStory(tile, npc)
                    || (npc != null && tile == _game.PlayerTile()));
                if (route == null) throw new InvalidOperationException($"{c.actor}: {goal}까지 걸을 수 없습니다.");
                // 첫 위치가 타일 중심에서 벗어나 있어도 모서리를 가로질러 이동하지 않게 먼저 정렬한다.
                foreach (var tile in route)
                {
                    Vector3 destination = new Vector3(tile.x, tile.y, 0);
                    while ((actor.position - destination).sqrMagnitude > 0.00001f)
                    {
                        Vector3 delta = destination - actor.position;
                        var facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                            ? (delta.x > 0 ? Direction.Right : Direction.Left)
                            : (delta.y > 0 ? Direction.Up : Direction.Down);
                        if (npc == null) _game.Player.SetStoryMotion(true, facing);
                        else npc.SetFacing(facing);
                        actor.position = Vector3.MoveTowards(actor.position, destination, c.speed * Time.unscaledDeltaTime);
                        if (npc != null) npc.SyncStoryPosition(_game.CurrentLocation);
                        yield return null;
                    }
                    actor.position = destination;
                    if (npc != null) npc.SyncStoryPosition(_game.CurrentLocation);
                }
            }
            if (npc == null) _game.Player.SetStoryMotion(false, _game.Player.facing);
        }

        /// <summary>에디터/외부 흐름에서 취소. 완료 기록·보상을 남기지 않고 배우 위치를 복원한다.</summary>
        public void Cancel()
        {
            if (!IsRunning) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            Cleanup(true);
            _pending.Clear();
        }

        private void Cleanup(bool restorePlayer)
        {
            if (_ui != null) _ui.CloseStoryDialogue();
            foreach (var pair in _npcPositions)
            {
                if (pair.Key == null) continue;
                pair.Key.transform.position = pair.Value;
                pair.Key.SetFacing(_npcDirections[pair.Key]);
                if (_game.CurrentLocation != null) pair.Key.SyncStoryPosition(_game.CurrentLocation);
            }
            // 서로의 원래 자리를 경유한 여러 NPC를 복원할 때 점유 집합의 제거 순서에 영향받지 않는다.
            _game.RebuildNpcBlocks();
            if (_game.Player != null)
            {
                if (restorePlayer) { _game.Player.transform.position = _playerPosition; _game.Player.facing = _playerDirection; }
                _game.Player.EndStoryControl();
                _game.Data.farmer.posX = _game.Player.transform.position.x;
                _game.Data.farmer.posY = _game.Player.transform.position.y;
                _game.Data.farmer.direction = (int)_game.Player.facing;
            }
            IsRunning = false;
            CurrentEventId = null;
            _releaseFrame = Time.frameCount;
            if (_ui != null) _ui.SyncPaused();
        }

        private void OnDisable() { Cancel(); }
    }
}
