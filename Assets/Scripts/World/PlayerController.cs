using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    /// <summary>
    /// WASD 이동(평소 뛰기 · Shift 걷기), 좌클릭 마법, 우클릭 상호작용을 맡는다.
    /// 그림은 하나도 다루지 않는다 — <b>무엇을 하고 있는지</b>만 FarmerAnimator에 넘기고,
    /// 어떤 프레임이 나올지는 그쪽이 정한다 (design doc §3).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        /// <summary>평소 속도(뛰기)와 Shift를 눌렀을 때의 속도(걷기).</summary>
        public float runSpeed = 5.2f;
        public float walkSpeed = 2.6f;

        [Range(0, 3)]
        [Tooltip("절벽의 작은 돌출부를 피할 수 있는 최대 픽셀 수. 0이면 보정을 끕니다.")]
        public int cliffSlidePixels = 3;

        public Direction facing = Direction.Down;

        private GameManager _game;
        private FarmerAnimator _anim;
        private bool _moving;
        private bool _running;
        private bool _storyControlled;
        private Vector2 _cliffSlideAway, _cliffSlideTangent;
        private GameLocation _cliffSlideLocation;
        private bool _cliffUseFeet;
        private GameLocation _cliffFootprintLocation;
        private Direction CliffCollisionFacing => _cliffUseFeet ? Direction.Up : facing;

        public void BeginStoryControl()
        {
            CancelHolds();
            _storyControlled = true;
            _moving = _running = false;
            _anim?.ClearOverride();
            _anim?.SetLocomotion(false, false, facing);
        }

        public void SetStoryMotion(bool moving, Direction direction)
        {
            facing = direction;
            _anim?.SetLocomotion(moving, false, facing);
        }

        public void SetStoryPose(FarmerAnim pose) => _anim?.SetOverride(pose);
        public void ClearStoryPose() => _anim?.ClearOverride();

        public void EndStoryControl()
        {
            _storyControlled = false;
            _moving = _running = false;
            _anim?.ClearOverride();
            UpdateAnimation();
        }

        // ---------- 마법(좌클릭) / 씨앗·수확(우클릭) 조준 상태 ----------
        private static readonly Color SeedPreviewColor = new Color(0.35f, 0.85f, 0.35f, 0.45f);

        private TargetIndicator _indicator;
        private bool _magicHeld;
        private float _magicChargeStarted;
        private MagicType _heldMagic;
        private GameLocation _chargeLocation;
        private readonly List<Vector2Int> _magicTiles = new List<Vector2Int>(54);
        private bool _seedHeld;

        /// <summary>지난 프레임에 서 있던 칸. 칸이 바뀔 때만 작물을 스치게 하려고 들고 있다.</summary>
        private Vector2Int _lastTile = new Vector2Int(int.MinValue, int.MinValue);

        public void Init(GameManager game)
        {
            _game = game;
            AssetLibrary.EnsureLoaded();

            // 그림은 층을 겹쳐 그리는 FarmerAnimator가 전부 맡는다 (플레이어 본체에는 그림이 없다).
            _anim = GetComponent<FarmerAnimator>();
            if (_anim == null) _anim = gameObject.AddComponent<FarmerAnimator>();
            _anim.Init(game.Data.farmer.appearance);

            _indicator = TargetIndicator.Create();
        }

        /// <summary>캐릭터 외형을 바꾼다 (세이브에도 반영된다).</summary>
        public void SetAppearance(PlayerAppearance appearance)
        {
            if (_game != null) _game.Data.farmer.appearance = appearance.Clone();
            _anim?.SetAppearance(appearance);
        }

        private void Update()
        {
            if (_storyControlled) return;
            if (_game == null || _game.Paused)
            {
                CancelHolds();
                _moving = false;
                UpdateAnimation();
                return;
            }

            HandleMovement();
            if (_game.Paused) return;
            HandleHotbarKeys();
            HandleInteraction();
        }

        /// <summary>인벤토리를 열거나 팝업이 뜨는 등 입력이 막히면 진행 중인 조준을 취소한다.</summary>
        private void CancelHolds()
        {
            if (!_magicHeld && !_seedHeld) return;
            _magicHeld = false;
            _seedHeld = false;
            _chargeLocation = null;
            _indicator?.Hide();
        }

        private void OnDisable() => CancelHolds();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelHolds();
        }

        private void OnDestroy()
        {
            if (_indicator != null) Destroy(_indicator.gameObject);
        }

        /// <summary>인벤토리/핫바 등 UI 위에서 시작된 클릭이 월드 상호작용으로 새는 것을 막는다.</summary>
        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// 서 있는 위치에 따라 앞뒤를 다시 정한다. 나무 위쪽으로 올라가면 나무에 가려지고
        /// 아래로 내려오면 나무 앞으로 나온다. 움직이므로 매 프레임 갱신해야 한다.
        /// </summary>
        private void LateUpdate()
        {
            _anim?.SetSortingOrder(Depth.YSort(transform.position.y));

            // 밟고 선 칸이 바뀌는 순간에만 스친다 (매 프레임 흔들면 계속 떨린다)
            var tile = new Vector2Int(Mathf.RoundToInt(transform.position.x),
                                      Mathf.RoundToInt(transform.position.y));
            if (tile == _lastTile) return;
            _lastTile = tile;
            _game?.CurrentLocation?.BrushAt(tile);
        }

        private void HandleMovement()
        {
            float h = 0, v = 0;
            if (Input.GetKey(KeyCode.W)) v += 1;
            if (Input.GetKey(KeyCode.S)) v -= 1;
            if (Input.GetKey(KeyCode.A)) h -= 1;
            if (Input.GetKey(KeyCode.D)) h += 1;

            var move = new Vector2(h, v);
            _moving = move.sqrMagnitude > 0.001f;

            // 평소에는 뛰고, Shift를 누르고 있는 동안만 걷는다 (조심스럽게 움직이고 싶을 때).
            _running = !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            if (_moving)
            {
                // update facing (prioritise vertical for animation rows like the sheets)
                if (Mathf.Abs(h) > Mathf.Abs(v))
                    facing = h > 0 ? Direction.Right : Direction.Left;
                else
                    facing = v > 0 ? Direction.Up : Direction.Down;

                move = move.normalized * (_running ? runSpeed : walkSpeed) * Time.deltaTime;
                TryMove(move);
            }
            else ClearCliffSlide();

            UpdateAnimation();
        }

        private void TryMove(Vector2 delta)
        {
            var loc = _game.CurrentLocation;
            float cx = transform.position.x, cy = transform.position.y;
            RefreshCliffFootprint(loc, cx, cy);
            if (_cliffSlideLocation != loc || cliffSlidePixels <= 0
                || Vector2.Dot(delta, _cliffSlideTangent) <= 0f
                || Vector2.Dot(delta, _cliffSlideAway) > 0f)
                ClearCliffSlide();
            else if (_cliffSlideAway != Vector2.zero)
            {
                // 벽이 끝나면 즉시 원래 대각선 입력으로 돌아간다.
                var inward = -_cliffSlideAway;
                var probe = new Vector2(cx, cy) + inward *
                    (Mathf.Clamp(cliffSlidePixels, 0, 3) / FarmerPoses.PixelsPerUnit + CliffPixelCollision.MovementStep);
                Direction side = inward.x != 0f ? (inward.x > 0 ? Direction.Right : Direction.Left)
                    : (inward.y > 0 ? Direction.Up : Direction.Down);
                if (!loc.IsBlockedAtWorld(probe.x, probe.y, FarmerBodyCollision.LeadingEdge(CliffCollisionFacing, side)))
                    ClearCliffSlide();
            }
            // 반 픽셀씩 검사해 저프레임/달리기에서도 1px 절벽을 건너뛰지 않는다.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y))
                / CliffPixelCollision.MovementStep));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                // 방향 전환 직후 상체가 절벽에 겹쳐 있으면 발끝 판정을 유지한다.
                // 소단계마다 확인하므로 절벽에서 벗어나면 즉시 원래 외곽으로 복귀한다.
                RefreshCliffFootprint(loc, cx, cy);
                var xEdge = step.x != 0f ? FarmerBodyCollision.LeadingEdge(CliffCollisionFacing,
                    step.x > 0f ? Direction.Right : Direction.Left) : null;
                var yEdge = step.y != 0f ? FarmerBodyCollision.LeadingEdge(CliffCollisionFacing,
                    step.y > 0f ? Direction.Up : Direction.Down) : null;
                float beforeX = cx, beforeY = cy;
                if (step.x != 0f && step.x * _cliffSlideAway.x >= 0f)
                {
                    float nx = cx + step.x;
                    if (!loc.IsBlockedAtWorld(nx, cy, xEdge)) cx = nx;
                    else if (TryLeaveThroughEdge(loc, nx, cy)) return;
                }
                if (step.y != 0f && step.y * _cliffSlideAway.y >= 0f)
                {
                    float ny = cy + step.y;
                    if (!loc.IsBlockedAtWorld(cx, ny, yEdge)) cy = ny;
                    else if (TryLeaveThroughEdge(loc, cx, ny)) return;
                }

                // 한 축이라도 진행 중이면 기존 축 분리 슬라이딩을 유지한다.
                // 완전히 걸린 경우에만 3px 이내의 안전한 우회 경로를 찾는다.
                if (cx == beforeX && cy == beforeY && cliffSlidePixels > 0)
                {
                    var position = new Vector2(cx, cy);
                    Vector2 correction;
                    bool horizontalFirst = _cliffSlideTangent != Vector2.zero
                        ? _cliffSlideTangent.x != 0f : Mathf.Abs(step.x) >= Mathf.Abs(step.y);
                    bool slid = horizontalFirst
                        ? TryCliffSlide(loc, position, new Vector2(step.x, 0f), xEdge, step.y, out correction)
                        : TryCliffSlide(loc, position, new Vector2(0f, step.y), yEdge, step.x, out correction);
                    if (!slid)
                        slid = horizontalFirst
                            ? TryCliffSlide(loc, position, new Vector2(0f, step.y), yEdge, step.x, out correction)
                            : TryCliffSlide(loc, position, new Vector2(step.x, 0f), xEdge, step.y, out correction);
                    if (slid)
                    {
                        // 옆으로 순간이동하지 않고, 이번 소단계의 이동량만큼만 비켜 간다.
                        // 이 소단계에는 전진을 더하지 않으므로 보정으로 이동 속도가 늘지 않는다.
                        cx += correction.x;
                        cy += correction.y;
                    }
                }

                transform.position = new Vector3(cx, cy, 0);
                CheckExits(loc);
                if (_game.CurrentLocation != loc) return;
            }
        }

        private void RefreshCliffFootprint(GameLocation loc, float x, float y)
        {
            if (_cliffFootprintLocation != loc)
            {
                _cliffFootprintLocation = loc;
                _cliffUseFeet = false;
            }
            if (facing == Direction.Up)
            {
                _cliffUseFeet = true;
                return;
            }
            if (!_cliffUseFeet) return;
            // 발끝으로 합법적으로 접근한 위치에서 몸 판정을 갑자기 확대하면 갇힌다.
            // 시각적인 방향은 그대로 바꾸고, 몸 전체가 절벽 밖일 때만 판정을 확대한다.
            for (int d = 0; d < 4; d++)
                if (loc.IsCliffBlockedAtWorld(x, y, FarmerBodyCollision.LeadingEdge(facing, (Direction)d)))
                    return;
            _cliffUseFeet = false;
        }

        private void ClearCliffSlide()
        {
            _cliffSlideAway = _cliffSlideTangent = Vector2.zero;
            _cliffSlideLocation = null;
        }

        private bool TryCliffSlide(GameLocation loc, Vector2 position, Vector2 forward,
            Vector2[] forwardEdge, float preferredSide, out Vector2 correction)
        {
            correction = Vector2.zero;
            if (forwardEdge == null || forward.sqrMagnitude == 0f) return false;
            var target = position + forward;
            // 물·오브젝트·NPC·맵 경계에 대한 충돌은 절벽 보정으로 우회하지 않는다.
            if (loc.IsBlockedByNonCliffAtWorld(target.x, target.y)
                || !loc.IsBlockedAtWorld(target.x, target.y, forwardEdge)) return false;

            bool horizontal = forward.x != 0f;
            var side = horizontal ? Vector2.up : Vector2.right;
            if (preferredSide < 0f) side = -side;
            Direction positive = horizontal ? (side.y > 0 ? Direction.Up : Direction.Down)
                : (side.x > 0 ? Direction.Right : Direction.Left);
            Direction negative = horizontal ? (side.y > 0 ? Direction.Down : Direction.Up)
                : (side.x > 0 ? Direction.Left : Direction.Right);
            var positiveEdge = FarmerBodyCollision.LeadingEdge(CliffCollisionFacing, positive);
            var negativeEdge = FarmerBodyCollision.LeadingEdge(CliffCollisionFacing, negative);
            bool positiveOpen = true, negativeOpen = true;
            float maxDistance = Mathf.Clamp(cliffSlidePixels, 0, 3) / FarmerPoses.PixelsPerUnit;
            int samples = Mathf.CeilToInt(maxDistance / CliffPixelCollision.MovementStep);
            for (int i = 1; i <= samples; i++)
            {
                float distance = Mathf.Min(i * CliffPixelCollision.MovementStep, maxDistance);
                // 모든 중간 위치를 확인한다. 끝점만 보고 얇은 벽 건너편으로 보정하지 않는다.
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (sign > 0 ? !positiveOpen : !negativeOpen) continue;
                    var candidate = position + side * (distance * sign);
                    var sideEdge = sign > 0 ? positiveEdge : negativeEdge;
                    if (loc.IsBlockedAtWorld(candidate.x, candidate.y, sideEdge))
                    {
                        if (sign > 0) positiveOpen = false;
                        else negativeOpen = false;
                        continue;
                    }
                    var onward = candidate + forward;
                    if (loc.IsBlockedAtWorld(onward.x, onward.y, forwardEdge)) continue;
                    float amount = Mathf.Min(forward.magnitude, CliffPixelCollision.MovementStep);
                    correction = side * (amount * sign);
                    var next = position + correction;
                    // 짧은 마지막 이동도 별도로 확인한다 (초고프레임/작은 입력량).
                    if (!loc.IsBlockedAtWorld(next.x, next.y, sideEdge))
                    {
                        // 대각선으로 벽을 누르는 입력이 다음 소단계에서 보정을 되돌리지 않게 한다.
                        _cliffSlideAway = side * sign;
                        _cliffSlideTangent = forward.normalized;
                        _cliffSlideLocation = loc;
                        return true;
                    }
                }
                if (!positiveOpen && !negativeOpen) break;
            }
            return false;
        }

        /// <summary>
        /// 맵 끝에서 바깥으로 한 발 더 내디뎠을 때 이어진 맵으로 넘어간다.
        /// 막혀서 못 간 자리가 <b>맵 밖</b>일 때만, 그리고 그 변에 이어진 맵이 있을 때만 넘어간다 —
        /// 벽이나 물에 막힌 것과 헷갈리지 않는다. 넘어갈 때는 지금 서 있던 칸을 함께 넘겨서,
        /// 도착한 맵에서 같은 높이(또는 같은 폭)로 들어서게 한다.
        /// </summary>
        private bool TryLeaveThroughEdge(GameLocation loc, float wx, float wy)
        {
            if (loc == null) return false;

            int tx = Mathf.RoundToInt(wx), ty = Mathf.RoundToInt(wy);
            MapSide side;
            if (tx < 0) side = MapSide.Left;
            else if (tx >= loc.width) side = MapSide.Right;
            else if (ty < 0) side = MapSide.Bottom;
            else if (ty >= loc.height) side = MapSide.Top;
            else return false;                      // 맵 안에서 무언가에 막힌 것뿐이다

            if (!loc.TryGetEdgeExit(side, out var target)) return false;

            var from = new Vector2Int(Mathf.RoundToInt(transform.position.x),
                                      Mathf.RoundToInt(transform.position.y));
            _game.ChangeLocationThroughExit(target, from);
            return true;
        }

        private void CheckExits(GameLocation loc)
        {
            int tx = Mathf.RoundToInt(transform.position.x);
            int ty = Mathf.RoundToInt(transform.position.y);

            // 다른 맵으로 이어지는 칸 — "Obj_Exit{위치}" 마커이거나, 빌더가 등록한 가장자리 영역.
            if (loc.TryGetExit(tx, ty, out var exitTarget))
            {
                _game.ChangeLocationThroughExit(exitTarget, new Vector2Int(tx, ty));
                return;
            }
            // FarmHouse door -> Farm1
            if (loc.id == LocationId.FarmHouse && loc.doorExitTile.HasValue)
            {
                if (tx == loc.doorExitTile.Value.x && ty <= loc.doorExitTile.Value.y)
                {
                    _game.ChangeLocationThroughDoor(LocationId.Farm1);
                    return;
                }
            }
        }

        /// <summary>퀵바 10칸에 대응하는 숫자키 (1~9 다음이 0).</summary>
        private static readonly KeyCode[] HotbarKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        private void HandleHotbarKeys()
        {
            // 원형 퀵스킬 메뉴가 열린 동안에는 숫자키가 인벤토리 대신 마법 번호를 고른다.
            QuickSkillHUD.HandleKeyboardInput(_game);

            // 숫자키 1~0: 지금 보고 있는 배낭 페이지에서 그 번째 칸 선택
            for (int i = 0; !QuickSkillHUD.IsRadialMenuOpen && i < HotbarKeys.Length && i < Inventory.HotbarSize; i++)
            {
                if (Input.GetKeyDown(HotbarKeys[i]))
                    _game.SelectHotbarColumn(i);
            }

            // TAB: 증축한 배낭 페이지 전환
            if (Input.GetKeyDown(KeyCode.Tab))
                _game.CycleHotbarPage();

            // Q/E: 원형 퀵스킬을 각각 오른쪽/왼쪽으로 한 칸 바꾼다.
            if (Input.GetKeyDown(KeyCode.Q))
                _game.CycleMagic();
            if (Input.GetKeyDown(KeyCode.E))
                _game.CycleMagic(-1);
        }

        private void HandleInteraction()
        {
            // 낚시 중에는 클릭이 전부 낚시로 간다 (FishingController가 직접 읽는다).
            if (_game.Fishing != null && _game.Fishing.IsActive)
            {
                CancelHolds();
                _indicator?.Hide();
                return;
            }

            HandleMagicInput();
            HandleSeedInput();
            if (_game.Paused) return;

            // E는 퀵스킬 왼쪽 순환에 배정되었으므로, 상호작용은 Space만 쓴다.
            if (Input.GetKeyDown(KeyCode.Space))
                _game.TryContextInteract(this);
        }

        /// <summary>
        /// 좌클릭을 누르는 동안 범위를 충전하고, 놓는 순간 유효한 대상을 동시에 시전한다.
        /// 낚시만 단일 타일을 유지한다. 차징 전용 캐스팅 애니메이션은 추후 연결한다.
        /// </summary>
        private void HandleMagicInput()
        {
            // 원형 메뉴가 열려 있으면 월드 시전보다 방향 클릭을 우선한다.
            if (QuickSkillHUD.ConsumeRadialClick(_game) || QuickSkillHUD.IsRadialMenuOpen)
            {
                CancelHolds();
                return;
            }

            if (_magicHeld && (_heldMagic != _game.CurrentMagic || _chargeLocation != _game.CurrentLocation))
                CancelHolds();

            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI()) return; // 인벤토리/핫바 클릭이 월드 시전으로 새는 것 방지

                _seedHeld = false;
                _heldMagic = _game.CurrentMagic;
                _chargeLocation = _game.CurrentLocation;
                _magicChargeStarted = Time.time;
                _magicHeld = true;
                ShowMagicIndicator();
            }
            else if (_magicHeld && Input.GetMouseButton(0))
            {
                ShowMagicIndicator();
            }

            if (_magicHeld && Input.GetMouseButtonUp(0))
            {
                _magicHeld = false;
                _indicator.Hide();

                if (IsPointerOverUI()) return;
                int stage = MagicCharge.Stage(Time.time - _magicChargeStarted, _heldMagic);
                if (_game.CastMagicOnFacingArea(this, stage))
                    PlayMagicAnimation();
            }
            else if (_magicHeld && !Input.GetMouseButton(0)) CancelHolds();
        }

        /// <summary>
        /// 우클릭 = 인벤토리 아이템 상호작용 키. 오브젝트(작업대·울타리 문·NPC...)가 먼저고,
        /// 그다음 수확, 마지막으로 손에 든 것을 쓴다 — 씨앗은 심고, 울타리·길은 설치한다.
        /// 대지/칼날마법과 동일하게 누르는 동안 범위를 표시하며 이동할 수 있고, 뗄 때 실행한다.
        /// </summary>
        private void HandleSeedInput()
        {
            if (_magicHeld) return;
            bool isSeed = IsSeedSelected();
            bool isPlaceable = _game.SelectedPlaceable != null;

            if (Input.GetMouseButtonDown(1))
            {
                if (IsPointerOverUI()) return; // 인벤토리/핫바 클릭이 월드 상호작용으로 새는 것 방지

                // 1) 오브젝트(NPC·배송함·상점·침대)는 "그 오브젝트가 있는 칸"을 눌러야 반응한다.
                //    바라보는 방향은 상관없고, 오브젝트와 나 사이의 빈 칸을 눌러도 아무 일도 없다.
                if (_game.TryInteractAt(_game.MouseTile())) return;

                // 2) 수확은 조준 없이 즉시 — 바라보는 방향과 상관없이 주변에서 가장 가까운 작물을 캔다.
                //    단, 울타리·길을 들고 있으면 설치가 먼저다 (옆의 작물 때문에 설치가 막히면 답답하다).
                if (!isPlaceable && _game.TryHarvestNearby(this))
                {
                    _anim?.PlayOnce(FarmerAnim.Harvest);   // 허리 굽혀 집어 드는 자세
                    return;
                }

                if (!isSeed && !isPlaceable) return;
                _seedHeld = true;
                ShowSeedIndicator();
            }
            else if (_seedHeld && Input.GetMouseButton(1))
            {
                if (!isSeed && !isPlaceable)
                {
                    _seedHeld = false;
                    _indicator.Hide();
                }
                else
                {
                    ShowSeedIndicator();
                }
            }

            if (_seedHeld && Input.GetMouseButtonUp(1))
            {
                _seedHeld = false;
                _indicator.Hide();
                if (isPlaceable) _game.PlaceSelectedAt(_game.MouseTile());
                else _game.PlantSelectedAt(_game.MouseTile());
                // 씨앗을 뿌리는 것도 울타리를 내려놓는 것도 "손에 든 것을 내려놓는" 동작이다.
                _anim?.PlayOnce(FarmerAnim.Harvest);
            }
        }

        private bool IsSeedSelected()
        {
            var stack = _game.SelectedStack;
            return stack != null && !stack.IsEmpty && stack.Def.type == ItemType.Seed;
        }

        /// <summary>
        /// 충전 단계에 해당하는 모든 칸을 표시한다. 지금 그 칸에 마법이 먹히지 않으면 —
        /// 경작 마스크 밖이거나, 이미 갈아 둔 땅이거나, 벨 나무가 없거나 — 어두운 색으로 바꿔
        /// 클릭해 보기 전에 알 수 있게 한다.
        /// </summary>
        private void ShowMagicIndicator()
        {
            int stage = MagicCharge.Stage(Time.time - _magicChargeStarted, _heldMagic);
            MagicCharge.FillTiles(FacingTile(), facing, stage, _magicTiles);
            _indicator.ShowMagic(_magicTiles, _heldMagic, _game.CurrentLocation);
        }

        /// <summary>
        /// 씨앗 조준은 <b>마우스가 가리키는 칸</b>을 따라간다 (바라보는 방향과 무관).
        /// 주변 8칸 밖이거나 심을 수 없는 자리면 어둡게 표시한다.
        /// </summary>
        private void ShowSeedIndicator()
        {
            var tile = _game.MouseTile();
            bool ok = _game.SelectedPlaceable != null
                ? _game.CanPlaceSelectedAt(tile)
                : _game.CanPlantSelectedAt(tile);
            _indicator.Show(tile, ok ? SeedPreviewColor : MagicSystem.InvalidPreviewColor);
        }

        public Vector2Int FacingTile()
        {
            int tx = Mathf.RoundToInt(transform.position.x);
            int ty = Mathf.RoundToInt(transform.position.y);
            switch (facing)
            {
                case Direction.Up: return new Vector2Int(tx, ty + 1);
                case Direction.Down: return new Vector2Int(tx, ty - 1);
                case Direction.Left: return new Vector2Int(tx - 1, ty);
                case Direction.Right: return new Vector2Int(tx + 1, ty);
            }
            return new Vector2Int(tx, ty);
        }

        // ---------- animation ----------
        /// <summary>
        /// 지금 무엇을 하고 있는지만 애니메이터에 알려 준다. 실제로 어떤 그림이 나올지는
        /// FarmerAnimator가 정한다 (한 번짜리 동작 > 눌러 둔 동작 > 이동 순).
        /// </summary>
        private void UpdateAnimation()
        {
            if (_anim == null) return;

            // 낚시는 여러 초에 걸쳐 단계가 바뀌므로, 그 단계를 그대로 눌러 둔 동작으로 넘긴다.
            var fishing = _game?.Fishing;
            if (fishing != null && fishing.IsActive)
                _anim.SetOverride(FarmerPoses.ForFishing(fishing.State));
            else
                _anim.ClearOverride();

            _anim.SetLocomotion(_moving, _running, facing);
        }

        /// <summary>낚시에 성공했을 때 (FishingController가 부른다).</summary>
        public void PlayFishCatch() => _anim?.PlayOnce(FarmerAnim.FishCaught);

        /// <summary>마법을 성공적으로 썼을 때 그 마법에 맞는 동작을 한 번 재생한다.</summary>
        private void PlayMagicAnimation()
        {
            if (_anim == null || _game == null) return;

            // 낚시마법으로 낚시를 시작했으면 도구 동작이 아니라 던지는 동작이다.
            var fishing = _game.Fishing;
            if (fishing != null && fishing.IsActive)
            {
                _anim.PlayOnce(FarmerAnim.FishCast);
                return;
            }

            _anim.PlayOnce(FarmerPoses.ForMagic(_game.CurrentMagic));
        }
    }
}
