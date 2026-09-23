using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 월드에 서 있는 NPC 한 명의 <b>몸</b>. 방향에 맞는 대기/걷기 그림을 고르고 앞뒤 순서를 맞추는
    /// 일만 한다 — <b>어디로 갈지는 전혀 모른다</b>.
    ///
    /// 어디에 있는지는 <see cref="NpcScheduler"/>가 모든 맵에 대해 한꺼번에 굴리고, 그중 지금
    /// 플레이어가 있는 맵의 NPC에게만 이 몸이 붙어 매 프레임 자리를 받는다. 그래서 몸은
    /// "화면에 비추는 창" 이상도 이하도 아니다.
    ///
    /// 걷는 중인지는 <b>실제로 자리가 변했는지</b>로 판단한다. 그래서 이벤트 연출이 transform을
    /// 직접 옮겨도 걷기 애니메이션이 저절로 따라붙는다.
    /// </summary>
    public class NpcActor : MonoBehaviour
    {
        private const float WalkFrameTime = 0.16f;
        private const float IdleFrameTime = 0.45f;

        public NpcDefinition Def { get; private set; }
        public Vector2Int Tile { get; private set; }
        public Direction Facing { get; private set; } = Direction.Down;

        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private string _clip;
        private float _timer;
        private int _frame;
        private Vector3 _lastPosition;

        public static NpcActor Spawn(Transform parent, NpcDefinition def, Vector2Int tile)
        {
            var go = new GameObject($"NPC_{def.id}");
            go.transform.SetParent(parent, false);
            // 플레이어와 같은 32x32 스프라이트라 타일 좌표에 그대로 세운다.
            go.transform.position = new Vector3(tile.x, tile.y, 0);

            var actor = go.AddComponent<NpcActor>();
            actor.Init(def, tile);
            return actor;
        }

        public void SetFacing(Direction direction)
        {
            Facing = direction;
            if (_sr != null) _sr.flipX = direction == Direction.Left;   // 왼쪽은 side를 뒤집어 쓴다
        }

        /// <summary>스케줄이 정한 자리와 방향을 그대로 받는다 (매 프레임).</summary>
        public void Place(Vector2 position, Direction facing, GameLocation location)
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            SetFacing(facing);
            SyncTile(location);
        }

        /// <summary>지금 서 있는 칸을 다시 계산하고, 맵의 "NPC가 막은 칸"을 옮긴다.</summary>
        public void SyncTile(GameLocation location)
        {
            var next = Vector2Int.RoundToInt(transform.position);
            if (next != Tile && location != null) location.MoveNpcBlock(Tile, next);
            Tile = next;
            if (_sr != null) _sr.sortingOrder = Depth.YSort(transform.position.y);
        }

        /// <summary>연출이 끝난 뒤 제자리로 돌려놓을 때 쓰던 이름 (예전 호출부 호환).</summary>
        public void SyncStoryPosition(GameLocation location) => SyncTile(location);

        private void Init(NpcDefinition def, Vector2Int tile)
        {
            Def = def;
            Tile = tile;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = Depth.YSort(tile.y);   // 플레이어·나무와 같은 기준으로 앞뒤를 정한다
            _sr.sprite = NpcDatabase.AnyFrame(def.id);
            _lastPosition = transform.position;
            Apply(NpcDatabase.IdleDown);
        }

        // 스케줄이 Update에서 자리를 옮기므로, 그림 고르기는 그 뒤에 한다.
        private void LateUpdate()
        {
            bool moving = (transform.position - _lastPosition).sqrMagnitude > 1e-8f;
            _lastPosition = transform.position;

            Apply(ClipFor(Facing, moving));
            Animate(moving ? WalkFrameTime : IdleFrameTime);
        }

        /// <summary>그 방향·상태에 맞는 시트 줄 이름. 왼쪽은 side를 뒤집어 쓴다.</summary>
        private static string ClipFor(Direction facing, bool moving)
        {
            switch (facing)
            {
                case Direction.Up: return moving ? NpcDatabase.WalkUp : NpcDatabase.IdleUp;
                case Direction.Left:
                case Direction.Right: return moving ? NpcDatabase.WalkSide : NpcDatabase.IdleSide;
                default: return moving ? NpcDatabase.WalkDown : NpcDatabase.IdleDown;
            }
        }

        private void Apply(string clip)
        {
            if (clip == _clip) return;

            var frames = NpcDatabase.Frames(Def.id, clip);
            // 그 줄을 안 그렸으면 같은 방향의 다른 상태로 대신한다 (대기 한 장만 그린 NPC 대비).
            if (frames.Length == 0) frames = NpcDatabase.Frames(Def.id, Fallback(clip));
            if (frames.Length == 0) return;

            _clip = clip;
            _frames = frames;
            _frame = 0;
            _timer = 0f;
            _sr.sprite = _frames[0];
        }

        private static string Fallback(string clip)
        {
            if (clip == NpcDatabase.WalkUp) return NpcDatabase.IdleUp;
            if (clip == NpcDatabase.WalkSide) return NpcDatabase.IdleSide;
            if (clip == NpcDatabase.WalkDown) return NpcDatabase.IdleDown;
            return NpcDatabase.IdleDown;
        }

        private void Animate(float frameTime)
        {
            if (_frames == null || _frames.Length <= 1) return;

            _timer += Time.deltaTime;
            if (_timer < frameTime) return;

            _timer -= frameTime;
            _frame = (_frame + 1) % _frames.Length;
            _sr.sprite = _frames[_frame];
        }
    }
}
