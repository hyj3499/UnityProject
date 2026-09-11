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

        public Direction facing = Direction.Down;

        private GameManager _game;
        private FarmerAnimator _anim;
        private bool _moving;
        private bool _running;

        // ---------- 마법(좌클릭) / 씨앗·수확(우클릭) 조준 상태 ----------
        private static readonly Color SeedPreviewColor = new Color(0.35f, 0.85f, 0.35f, 0.45f);

        private TargetIndicator _indicator;
        private bool _magicHeld;
        private float _waterTickTimer;
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
            if (_game == null || _game.Paused)
            {
                CancelHolds();
                _moving = false;
                UpdateAnimation();
                return;
            }

            HandleMovement();
            HandleHotbarKeys();
            HandleInteraction();
        }

        /// <summary>인벤토리를 열거나 팝업이 뜨는 등 입력이 막히면 진행 중인 조준을 취소한다.</summary>
        private void CancelHolds()
        {
            if (!_magicHeld && !_seedHeld) return;
            _magicHeld = false;
            _seedHeld = false;
            _indicator?.Hide();
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

            // 밟고 선 칸이 바뀌는 순간에만 작물을 스친다 (매 프레임 흔들면 계속 떨린다)
            var tile = new Vector2Int(Mathf.RoundToInt(transform.position.x),
                                      Mathf.RoundToInt(transform.position.y));
            if (tile == _lastTile) return;
            _lastTile = tile;
            _game?.CurrentLocation?.BrushCropAt(tile);
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

            UpdateAnimation();
        }

        private void TryMove(Vector2 delta)
        {
            var loc = _game.CurrentLocation;
            Vector3 target = transform.position + (Vector3)delta;

            // axis-separated collision against tile grid using a small footprint
            float cx = transform.position.x, cy = transform.position.y;

            // X
            float nx = cx + delta.x;
            if (!IsSolidAtWorld(nx, cy, loc)) cx = nx;
            // Y
            float ny = cy + delta.y;
            if (!IsSolidAtWorld(cx, ny, loc)) cy = ny;

            transform.position = new Vector3(cx, cy, 0);
            CheckExits(loc);
        }

        private bool IsSolidAtWorld(float wx, float wy, GameLocation loc)
        {
            int tx = Mathf.RoundToInt(wx);
            int ty = Mathf.RoundToInt(wy);
            return loc.IsBlocked(tx, ty);
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
            // Farm1 house door -> FarmHouse
            if (loc.id == LocationId.Farm1 && loc.doorExitTile.HasValue)
            {
                if (tx == loc.doorExitTile.Value.x && ty == loc.doorExitTile.Value.y)
                {
                    _game.ChangeLocationThroughDoor(LocationId.FarmHouse);
                    return;
                }
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
            // 숫자키 1~0: 지금 보고 있는 배낭 페이지에서 그 번째 칸 선택
            for (int i = 0; i < HotbarKeys.Length && i < Inventory.HotbarSize; i++)
            {
                if (Input.GetKeyDown(HotbarKeys[i]))
                    _game.SelectHotbarColumn(i);
            }

            // TAB: 증축한 배낭 페이지 전환
            if (Input.GetKeyDown(KeyCode.Tab))
                _game.CycleHotbarPage();

            // Q키: 마법 변경 (대지 → 물 → 칼날)
            if (Input.GetKeyDown(KeyCode.Q))
                _game.CycleMagic();
        }

        private void HandleInteraction()
        {
            // 낚시 중에는 클릭이 전부 낚시로 간다 (FishingController가 직접 읽는다).
            if (_game.Fishing != null && _game.Fishing.IsActive)
            {
                CancelHolds();   // 물마법은 꾹 누르면 반복 시전이라, 낚시 중에는 홀드를 풀어 둔다
                _indicator?.Hide();
                return;
            }

            HandleMagicInput();
            HandleSeedInput();

            // interact key (e / space) for bed and generic
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                _game.TryContextInteract(this);
        }

        /// <summary>
        /// 좌클릭 = 마법 사용.
        /// 대지/칼날마법: 누르는 즉시 범위를 표시하고, 키를 뗄 때 실제로 시전한다
        /// (짧게 누르면 거의 즉시 떼어지므로 바로 시전한 것처럼 보이고, 길게 누르면 그동안 이동하며 조준할 수 있다).
        /// 물마법: 누르는 즉시 한 번 시전하고, 계속 누르고 있으면 일정 간격마다 반복 시전한다.
        /// </summary>
        private void HandleMagicInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI()) return; // 인벤토리/핫바 클릭이 월드 시전으로 새는 것 방지

                _waterTickTimer = 0f;

                if (_game.CurrentMagic == MagicType.Water)
                {
                    if (!_game.CastMagicOnFacingTile(this)) return; // MP 부족 - 조준 시작조차 하지 않음
                    PlayMagicAnimation();
                }

                _magicHeld = true;
                ShowMagicIndicator();
            }
            else if (_magicHeld && Input.GetMouseButton(0))
            {
                ShowMagicIndicator();

                if (_game.CurrentMagic == MagicType.Water)
                {
                    _waterTickTimer += Time.deltaTime;
                    float interval = MagicSystem.Get(_game.CurrentMagic).tickInterval;
                    if (_waterTickTimer >= interval)
                    {
                        _waterTickTimer -= interval;
                        if (_game.CastMagicOnFacingTile(this))
                        {
                            PlayMagicAnimation();
                        }
                        else
                        {
                            _magicHeld = false;
                            _indicator.Hide();
                        }
                    }
                }
            }

            if (_magicHeld && Input.GetMouseButtonUp(0))
            {
                _magicHeld = false;
                _indicator.Hide();

                // 물마법은 누르는 동안 이미 시전했으므로 뗄 때 추가로 시전하지 않는다.
                if (_game.CurrentMagic != MagicType.Water && _game.CastMagicOnFacingTile(this))
                    PlayMagicAnimation();
            }
        }

        /// <summary>
        /// 우클릭 = 인벤토리 아이템 상호작용 키. 오브젝트(작업대·울타리 문·NPC...)가 먼저고,
        /// 그다음 수확, 마지막으로 손에 든 것을 쓴다 — 씨앗은 심고, 울타리·길은 설치한다.
        /// 대지/칼날마법과 동일하게 누르는 동안 범위를 표시하며 이동할 수 있고, 뗄 때 실행한다.
        /// </summary>
        private void HandleSeedInput()
        {
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
        /// 조준 사각형을 바라보는 칸에 띄운다. 지금 그 칸에 마법이 먹히지 않으면 —
        /// 경작 마스크 밖이거나, 이미 갈아 둔 땅이거나, 벨 나무가 없거나 — 어두운 색으로 바꿔
        /// 클릭해 보기 전에 알 수 있게 한다.
        /// </summary>
        private void ShowMagicIndicator()
        {
            var tile = FacingTile();
            bool ok = MagicSystem.CanApply(_game.CurrentMagic, _game.CurrentLocation, tile.x, tile.y);
            _indicator.Show(tile, ok ? MagicSystem.Get(_game.CurrentMagic).previewColor
                                     : MagicSystem.InvalidPreviewColor);
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

            _anim.SetCarriedItem(CarriedSprite());
            _anim.SetLocomotion(_moving, _running, facing);
        }

        /// <summary>
        /// 지금 퀵바에서 고른 아이템의 그림 (도구는 제외). 이것이 있으면 머리 위로 들고 다니는
        /// 자세가 되고, 그 그림이 머리 위에 그려진다.
        /// </summary>
        private Sprite CarriedSprite()
        {
            var stack = _game?.SelectedStack;
            if (stack == null || stack.IsEmpty) return null;

            var def = stack.Def;
            if (def == null || def.type == ItemType.Tool) return null;
            return def.GetSprite();
        }

        /// <summary>낚시에 성공했을 때 (FishingController가 부른다).</summary>
        public void PlayFishCatch() => _anim?.PlayOnce(FarmerAnim.FishCaught);

        /// <summary>마법을 성공적으로 썼을 때 그 마법에 맞는 동작을 한 번 재생한다.</summary>
        private void PlayMagicAnimation()
        {
            if (_anim == null || _game == null) return;

            // 물마법으로 낚시를 시작했으면 물주기가 아니라 던지는 동작이다.
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
