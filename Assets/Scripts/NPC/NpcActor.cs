using UnityEngine;

namespace FarmMVP
{
    /// <summary>월드에 서 있는 NPC 한 명. 자기 자리에서 대기 애니메이션만 재생한다.</summary>
    public class NpcActor : MonoBehaviour
    {
        private const float FrameTime = 0.35f;

        public NpcDefinition Def { get; private set; }
        public Vector2Int Tile { get; private set; }
        public Direction Facing { get; private set; } = Direction.Down;

        public void SetFacing(Direction direction)
        {
            Facing = direction;
            // 현재 NPC 아트는 Idle만 있다. 좌/우는 반전, 상/하는 상태만 기록한다.
            if (_sr != null) _sr.flipX = direction == Direction.Left;
        }

        public void SyncStoryPosition(GameLocation location)
        {
            var next = Vector2Int.RoundToInt(transform.position);
            if (next != Tile) location.MoveNpcBlock(Tile, next);
            Tile = next;
            if (_sr != null) _sr.sortingOrder = Depth.YSort(transform.position.y);
        }

        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _timer;
        private int _frame;

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

        private void Init(NpcDefinition def, Vector2Int tile)
        {
            Def = def;
            Tile = tile;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = Depth.YSort(tile.y);   // 플레이어·나무와 같은 기준으로 앞뒤를 정한다

            _frames = NpcDatabase.IdleFrames(def.id);
            if (_frames.Length > 0) _sr.sprite = _frames[0];
        }

        private void Update()
        {
            if (_frames == null || _frames.Length <= 1) return;

            _timer += Time.deltaTime;
            if (_timer < FrameTime) return;

            _timer -= FrameTime;
            _frame = (_frame + 1) % _frames.Length;
            _sr.sprite = _frames[_frame];
        }
    }
}
