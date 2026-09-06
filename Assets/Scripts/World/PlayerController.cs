using UnityEngine;

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

        public void Init(GameManager game)
        {
            _game = game;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 1000;
            AssetLibrary.EnsureLoaded();
        }

        private void Update()
        {
            if (_game == null || _game.Paused) { UpdateAnimation(0); return; }

            HandleMovement();
            HandleHotbarKeys();
            HandleInteraction();
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
            if (Input.GetMouseButtonDown(0))
                _game.UseSelectedOnFacingTile(this);

            // interact key (e / space) for bed and generic
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                _game.TryContextInteract(this);
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
