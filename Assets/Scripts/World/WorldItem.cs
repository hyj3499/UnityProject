using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 월드 바닥에 떨어져 있는 아이템 더미 하나. 플레이어가 일정 거리 안으로 들어오면
    /// 자석처럼 끌려가 인벤토리에 들어가고, 인벤토리에 자리가 없으면 들어가지 못한 만큼
    /// 플레이어 반대쪽으로 튕겨나가 바닥에 남는다 (나중에 자리가 생기면 다시 시도한다).
    /// 어떤 아이템을 얼마나 드랍할지는 이 클래스가 전혀 모른다 — 그건 LootTable/
    /// ItemDropSpawner가 결정하고, WorldItem은 오직 "이미 정해진 (itemId, count) 하나를
    /// 어떻게 줍게 만들지"만 담당한다.
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        private const float MagnetRadius = 1.6f;
        private const float PickupRadius = 0.25f;
        private const float MagnetSpeed = 6f;
        private const float SpawnGraceTime = 0.25f; // 스폰 직후 잠깐은 끌려가지 않는다 (튀어나오는 느낌)
        private const float RejectCooldown = 0.6f;  // 인벤토리가 가득 차 튕겨난 뒤 재시도까지 대기
        private const float BounceDistance = 0.6f;

        private GameManager _game;
        private SpriteRenderer _sr;
        private string _itemId;
        private int _count;
        private float _grace;
        private float _rejectTimer;

        public string ItemId => _itemId;
        public int Count => _count;

        /// <summary>지정한 타일 근처에 아이템 하나를 스폰한다 (여러 개면 여러 번 호출). 여러 개가
        /// 한 번에 떨어질 때 서로 겹치지 않도록 위치를 살짝 흩뿌린다.</summary>
        public static WorldItem Create(Transform parent, GameManager game, string itemId, int count, Vector2 tile)
        {
            Vector2 scatter = Random.insideUnitCircle * 0.3f;
            return CreateAt(parent, game, itemId, count, tile + scatter);
        }

        /// <summary>저장된 위치를 그대로 복원할 때 사용 (추가 흩뿌림 없음).</summary>
        public static WorldItem CreateAt(Transform parent, GameManager game, string itemId, int count, Vector2 exactPosition)
        {
            var go = new GameObject($"drop_{itemId}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(exactPosition.x, exactPosition.y, 0);

            var item = go.AddComponent<WorldItem>();
            item.Init(game, itemId, count);
            return item;
        }

        private void Init(GameManager game, string itemId, int count)
        {
            _game = game;
            _itemId = itemId;
            _count = count;
            _grace = SpawnGraceTime;

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ItemDatabase.Get(itemId)?.GetDropSprite();
            _sr = sr;
            sr.sortingOrder = Depth.YSort(transform.position.y, Depth.DroppedItemBias);
        }

        private void LateUpdate()
        {
            // 플레이어에게 끌려오며 움직이므로 앞뒤도 따라가야 한다.
            if (_sr != null) _sr.sortingOrder = Depth.YSort(transform.position.y, Depth.DroppedItemBias);
        }

        private void Update()
        {
            if (_game == null || _game.Player == null) return;

            if (_grace > 0f) { _grace -= Time.deltaTime; return; }
            if (_rejectTimer > 0f) { _rejectTimer -= Time.deltaTime; return; }

            Vector3 playerPos = _game.Player.transform.position;
            float dist = Vector3.Distance(transform.position, playerPos);

            if (dist <= PickupRadius)
            {
                TryCollect(playerPos);
                return;
            }

            if (dist <= MagnetRadius)
                transform.position = Vector3.MoveTowards(transform.position, playerPos, MagnetSpeed * Time.deltaTime);
        }

        private void TryCollect(Vector3 playerPos)
        {
            int leftover = _game.Inventory.Add(_itemId, _count);
            if (leftover <= 0)
            {
                Destroy(gameObject);
                return;
            }

            // 다 들어가지 못한 만큼만 남기고, 플레이어 반대 방향으로 튕겨나간다.
            _count = leftover;
            Vector3 away = transform.position - playerPos;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
            transform.position += away.normalized * BounceDistance;
            _rejectTimer = RejectCooldown;
        }
    }
}
