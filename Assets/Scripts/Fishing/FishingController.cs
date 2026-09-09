using UnityEngine;

namespace FarmMVP
{
    /// <summary>낚시 진행 단계.</summary>
    public enum FishingState
    {
        Idle,       // 낚시 중이 아님
        Waiting,    // 찌를 던지고 입질을 기다리는 중
        Bite,       // 머리 위에 느낌표 — 이 안에 눌러야 한다
        Minigame,   // 타이밍 미니게임 진행 중
    }

    /// <summary>
    /// 낚시 한 판의 상태 기계. 물고기 데이터(FishDatabase/FishingZoneDatabase)와
    /// 화면 표시(FishingUI)를 잇는 자리이고, 무엇이 잡히는지는 전혀 알지 못한다 —
    /// 낚시터에서 한 마리 뽑아 그 난이도만 쓴다.
    ///
    /// 흐름: 물마법을 물 타일에 사용 → Waiting(1.2~4초) → Bite(느낌표, 반응 시간 안에 클릭)
    ///       → Minigame(-O-O-O- 타이밍) → 성공하면 인벤토리에 물고기.
    /// </summary>
    public class FishingController : MonoBehaviour
    {
        // 입질까지 걸리는 시간
        private const float MinWait = 1.2f, MaxWait = 4.0f;
        // 미니게임 전체 제한 시간
        private const float MinigameTimeLimit = 15f;
        private const int MissesAllowed = 3;

        public FishingState State { get; private set; } = FishingState.Idle;
        public bool IsActive => State != FishingState.Idle;

        private GameManager _game;
        private PlayerController _player;
        private FishingUI _ui;

        private FishDef _fish;
        private float _timer;          // 남은 대기/반응 시간
        private float _reactionWindow;

        // ---- 미니게임 상태 ----
        private float _markerPos;      // 0~1, 트랙 위 커서 위치
        private int _markerDir = 1;
        private float _markerSpeed;    // 초당 이동 비율
        private float _zoneWidth;      // 0~1
        private float[] _zoneCenters;
        private bool[] _zoneHit;
        private int _misses;
        private float _minigameTimer;

        /// <summary>
        /// 낚시를 시작/전환한 프레임 번호. 같은 프레임에 PlayerController와 이 스크립트가 모두
        /// 같은 마우스 입력을 보기 때문에, 던진 클릭이 곧바로 "너무 일찍 챘다"로 이어지는 걸 막는다.
        /// </summary>
        private int _inputGuardFrame = -1;

        public static FishingController Create(GameManager game, PlayerController player, FishingUI ui)
        {
            var go = new GameObject("FishingController");
            var fc = go.AddComponent<FishingController>();
            fc._game = game;
            fc._player = player;
            fc._ui = ui;
            return fc;
        }

        /// <summary>
        /// 이 칸에서 낚시를 시작할 수 있는지. 물이어야 하고, 그 맵에 낚시터가 정의돼 있어야 한다.
        /// </summary>
        public static bool CanFishAt(GameLocation loc, int x, int y)
        {
            if (loc == null || !loc.IsWater(x, y)) return false;
            var zone = FishingZoneDatabase.ForLocation(loc.id);
            return zone != null && !zone.IsEmpty;
        }

        /// <summary>물마법을 물 타일에 썼을 때 호출된다. 시작했으면 true.</summary>
        public bool TryStartFishing(GameLocation loc, Vector2Int spot)
        {
            if (IsActive) return false;
            if (!CanFishAt(loc, spot.x, spot.y)) return false;

            _fish = null;
            State = FishingState.Waiting;
            _inputGuardFrame = Time.frameCount;
            _timer = Random.Range(MinWait, MaxWait);
            _ui.ShowCasting();
            return true;
        }

        /// <summary>도중에 그만둔다 (맵 이동, 일시정지, 대화 등).</summary>
        public void Cancel(string reason = null)
        {
            if (!IsActive) return;
            State = FishingState.Idle;
            _fish = null;
            _ui.Hide();
            if (!string.IsNullOrEmpty(reason)) UIManager.Instance?.Toast(reason);
        }

        private void Update()
        {
            if (!IsActive) return;

            // 이동하거나 일시정지하면 낚시가 풀린다
            if (_game == null || _game.Paused) { Cancel(); return; }

            switch (State)
            {
                case FishingState.Waiting: UpdateWaiting(); break;
                case FishingState.Bite: UpdateBite(); break;
                case FishingState.Minigame: UpdateMinigame(); break;
            }
        }

        // ---------- 입질 대기 ----------
        private void UpdateWaiting()
        {
            // 대기 중에 누르면 헛챔질 — 다시 던져야 한다
            if (ClickedThisFrame())
            {
                Cancel("너무 일찍 챘다!");
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            // 이 순간 물고기가 정해지고, 그 난이도가 반응 시간과 미니게임을 좌우한다
            var zone = FishingZoneDatabase.ForLocation(_game.CurrentLocation.id);
            _fish = zone != null ? zone.Roll() : null;
            if (_fish == null) { Cancel(); return; }

            State = FishingState.Bite;
            _reactionWindow = Mathf.Max(0.45f, 1.15f - 0.13f * _fish.difficulty);
            _timer = _reactionWindow;
            _ui.ShowBite(_player.transform);
        }

        // ---------- 느낌표 (반응) ----------
        private void UpdateBite()
        {
            if (ClickedThisFrame())
            {
                StartMinigame();
                _inputGuardFrame = Time.frameCount;   // 챈 클릭이 첫 타이밍 판정으로 새지 않게
                return;
            }

            _timer -= Time.deltaTime;
            _ui.UpdateBite(_timer / _reactionWindow);
            if (_timer <= 0f) Cancel("놓쳤다...");
        }

        // ---------- 타이밍 미니게임 ----------
        private void StartMinigame()
        {
            int d = Mathf.Clamp(_fish.difficulty, 1, 5);
            int zoneCount = 1 + d;                       // 2~6개를 다 맞혀야 한다

            // 판정 창 = 존 폭 / 커서 속도. 이 값이 난이도의 실체다 —
            // 난이도 1은 327ms, 5는 105ms(약 6프레임)로 어렵지만 사람이 칠 수 있는 범위에 둔다.
            _markerSpeed = 0.45f + 0.10f * d;
            _zoneWidth = 0.20f - 0.02f * d;
            _markerPos = 0f;
            _markerDir = 1;
            _misses = 0;
            _minigameTimer = MinigameTimeLimit;

            // 존을 트랙에 고르게 나눠 놓고 칸 안에서만 흔든다 — 겹치거나 뭉치지 않게
            _zoneCenters = new float[zoneCount];
            _zoneHit = new bool[zoneCount];
            float slot = 1f / zoneCount;
            for (int i = 0; i < zoneCount; i++)
            {
                float lo = i * slot + _zoneWidth * 0.5f;
                float hi = (i + 1) * slot - _zoneWidth * 0.5f;
                _zoneCenters[i] = hi > lo ? Random.Range(lo, hi) : (i + 0.5f) * slot;
            }

            State = FishingState.Minigame;
            _ui.ShowMinigame(_fish, _zoneCenters, _zoneWidth, MissesAllowed);
        }

        private void UpdateMinigame()
        {
            _minigameTimer -= Time.deltaTime;
            if (_minigameTimer <= 0f) { Fail("시간 초과! 놓쳤다..."); return; }

            // 커서가 트랙 끝에서 튕겨 돌아온다
            _markerPos += _markerDir * _markerSpeed * Time.deltaTime;
            if (_markerPos >= 1f) { _markerPos = 1f; _markerDir = -1; }
            else if (_markerPos <= 0f) { _markerPos = 0f; _markerDir = 1; }

            _ui.UpdateMinigame(_markerPos, _zoneHit, _misses, _minigameTimer / MinigameTimeLimit);

            if (!ClickedThisFrame()) return;

            int hitIndex = FindZoneUnder(_markerPos);
            if (hitIndex >= 0)
            {
                _zoneHit[hitIndex] = true;
                _ui.FlashHit(hitIndex);
                foreach (bool hit in _zoneHit)
                    if (!hit) return;   // 아직 남았다
                Succeed();
            }
            else
            {
                _misses++;
                _ui.FlashMiss();
                if (_misses >= MissesAllowed) Fail("놓쳤다...");
            }
        }

        private int FindZoneUnder(float pos)
        {
            float half = _zoneWidth * 0.5f;
            for (int i = 0; i < _zoneCenters.Length; i++)
            {
                if (_zoneHit[i]) continue;
                if (Mathf.Abs(pos - _zoneCenters[i]) <= half) return i;
            }
            return -1;
        }

        private void Succeed()
        {
            var fish = _fish;
            State = FishingState.Idle;
            _fish = null;
            _ui.Hide();
            // 낚아 올리는 동작을 한 번 보여 준 뒤에 평소 자세로 돌아간다.
            _player?.PlayFishCatch();
            _game.OnFishCaught(fish);
        }

        private void Fail(string message)
        {
            State = FishingState.Idle;
            _fish = null;
            _ui.Hide();
            UIManager.Instance?.Toast(message);
        }

        /// <summary>좌클릭이든 우클릭이든 "챈다". UI 위에서 시작된 클릭은 무시한다.</summary>
        private bool ClickedThisFrame()
        {
            if (Time.frameCount == _inputGuardFrame) return false;
            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return false;
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es == null || !es.IsPointerOverGameObject();
        }
    }
}
