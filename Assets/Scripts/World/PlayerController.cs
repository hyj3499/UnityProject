using UnityEngine;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    /// <summary>
    /// Handles WASD movement, 4-directional idle/walk animation, and left-click
    /// tool/interaction on the facing tile (design doc §3).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 4f;
        public Direction facing = Direction.Down;

        private SpriteRenderer _sr;
        private GameManager _game;
        private float _animTimer;
        private int _animFrame;
        private bool _moving;

        // ---------- 마법(좌클릭) / 씨앗(우클릭) 조준 상태 ----------
        private static readonly Color SeedPreviewColor = new Color(0.35f, 0.85f, 0.35f, 0.45f);

        private TargetIndicator _indicator;
        private bool _magicHeld;
        private float _waterTickTimer;
        private bool _seedHeld;

        public void Init(GameManager game)
        {
            _game = game;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 1000;
            AssetLibrary.EnsureLoaded();

            _indicator = TargetIndicator.Create();
        }

        private void Update()
        {
            if (_game == null || _game.Paused)
            {
                CancelHolds();
                UpdateAnimation(0);
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

        private void HandleMovement()
        {
            float h = 0, v = 0;
            if (Input.GetKey(KeyCode.W)) v += 1;
            if (Input.GetKey(KeyCode.S)) v -= 1;
            if (Input.GetKey(KeyCode.A)) h -= 1;
            if (Input.GetKey(KeyCode.D)) h += 1;

            var move = new Vector2(h, v);
            _moving = move.sqrMagnitude > 0.001f;

            if (_moving)
            {
                // update facing (prioritise vertical for animation rows like the sheets)
                if (Mathf.Abs(h) > Mathf.Abs(v))
                    facing = h > 0 ? Direction.Right : Direction.Left;
                else
                    facing = v > 0 ? Direction.Up : Direction.Down;

                move = move.normalized * moveSpeed * Time.deltaTime;
                TryMove(move);
            }

            UpdateAnimation(Time.deltaTime);
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

            // Farm1 right -> Farm2
            if (loc.id == LocationId.Farm1 && loc.rightExit.HasValue)
            {
                var r = loc.rightExit.Value;
                if (tx >= loc.width - 1 && ty >= r.yMin && ty < r.yMax)
                {
                    _game.ChangeLocation(LocationId.Farm2, new Vector2(1, ty));
                    return;
                }
            }
            // Farm2 left -> Farm1
            if (loc.id == LocationId.Farm2 && loc.leftExit.HasValue)
            {
                var l = loc.leftExit.Value;
                if (tx <= 0 && ty >= l.yMin && ty < l.yMax)
                {
                    _game.ChangeLocation(LocationId.Farm1, new Vector2(loc.width - 2, ty));
                    return;
                }
            }
            // Farm1 house door -> FarmHouse
            if (loc.id == LocationId.Farm1 && loc.doorExitTile.HasValue)
            {
                if (tx == loc.doorExitTile.Value.x && ty == loc.doorExitTile.Value.y)
                {
                    _game.ChangeLocation(LocationId.FarmHouse, new Vector2(6, 2));
                    return;
                }
            }
            // FarmHouse door -> Farm1
            if (loc.id == LocationId.FarmHouse && loc.doorExitTile.HasValue)
            {
                if (tx == loc.doorExitTile.Value.x && ty <= loc.doorExitTile.Value.y)
                {
                    // Return just below the Farm1 house door tile (door at y=Farm1.height-5=10).
                    _game.ChangeLocation(LocationId.Farm1, new Vector2(4, 9));
                    return;
                }
            }
        }

        private void HandleHotbarKeys()
        {
            // 숫자키 1~9: 인벤토리(핫바) 아이템 선택 (씨앗 등)
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    _game.SelectHotbar(i);
            }

            // Q키: 마법 변경 (대지 → 물 → 칼날)
            if (Input.GetKeyDown(KeyCode.Q))
                _game.CycleMagic();
        }

        private void HandleInteraction()
        {
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
                }

                _magicHeld = true;
                _indicator.Show(FacingTile(), MagicSystem.Get(_game.CurrentMagic).previewColor);
            }
            else if (_magicHeld && Input.GetMouseButton(0))
            {
                _indicator.Show(FacingTile(), MagicSystem.Get(_game.CurrentMagic).previewColor);

                if (_game.CurrentMagic == MagicType.Water)
                {
                    _waterTickTimer += Time.deltaTime;
                    float interval = MagicSystem.Get(_game.CurrentMagic).tickInterval;
                    if (_waterTickTimer >= interval)
                    {
                        _waterTickTimer -= interval;
                        if (!_game.CastMagicOnFacingTile(this))
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
                if (_game.CurrentMagic != MagicType.Water)
                    _game.CastMagicOnFacingTile(this);
            }
        }

        /// <summary>
        /// 우클릭 = 인벤토리에서 씨앗이 선택되어 있을 때만 씨앗 심기.
        /// 대지/칼날마법과 동일하게 누르는 동안 범위를 표시하며 이동할 수 있고, 뗄 때 심는다.
        /// </summary>
        private void HandleSeedInput()
        {
            bool isSeed = IsSeedSelected();

            if (Input.GetMouseButtonDown(1))
            {
                if (IsPointerOverUI()) return; // 인벤토리/핫바 클릭이 월드 파종으로 새는 것 방지
                if (!isSeed) return;
                _seedHeld = true;
                _indicator.Show(FacingTile(), SeedPreviewColor);
            }
            else if (_seedHeld && Input.GetMouseButton(1))
            {
                if (!isSeed)
                {
                    _seedHeld = false;
                    _indicator.Hide();
                }
                else
                {
                    _indicator.Show(FacingTile(), SeedPreviewColor);
                }
            }

            if (_seedHeld && Input.GetMouseButtonUp(1))
            {
                _seedHeld = false;
                _indicator.Hide();
                if (isSeed) _game.PlantSelectedOnFacingTile(this);
            }
        }

        private bool IsSeedSelected()
        {
            var stack = _game.SelectedStack;
            return stack != null && !stack.IsEmpty && stack.Def.type == ItemType.Seed;
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
        private void UpdateAnimation(float dt)
        {
            Sprite[] frames = GetFrames();
            if (frames == null || frames.Length == 0) return;

            if (_moving)
            {
                _animTimer += dt;
                if (_animTimer >= 0.12f)
                {
                    _animTimer = 0;
                    _animFrame = (_animFrame + 1) % frames.Length;
                }
            }
            else
            {
                _animFrame = 0;
            }

            _sr.sprite = frames[_animFrame % frames.Length];
            // flip for left (side sheet faces right)
            _sr.flipX = facing == Direction.Left;
        }

        private Sprite[] GetFrames()
        {
            if (_moving)
            {
                switch (facing)
                {
                    case Direction.Up: return AssetLibrary.WalkUp;
                    case Direction.Down: return AssetLibrary.WalkDown;
                    default: return AssetLibrary.WalkSide;
                }
            }
            else
            {
                switch (facing)
                {
                    case Direction.Up: return AssetLibrary.IdleUp;
                    case Direction.Down: return AssetLibrary.IdleDown;
                    default: return AssetLibrary.IdleSide;
                }
            }
        }
    }
}
