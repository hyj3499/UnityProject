using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Game Core (design doc §15). Owns game data, current location, player,
    /// time, and coordinates farming / chopping / save / day-end.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameData Data { get; private set; }
        public Inventory Inventory { get; private set; }

        /// <summary>배송함. 여기에 넣어 둔 아이템은 다음 날 아침에 팔려 소지금이 된다.</summary>
        public Inventory ShippingBox { get; private set; }
        public GameLocation CurrentLocation { get; private set; }
        public PlayerController Player { get; private set; }

        public bool Paused;              // 책(인벤토리/설정) 이나 팝업이 열려 있는 동안 true

        public event Action OnTimeChanged;
        public event Action OnDayChanged;
        public event Action OnHotbarChanged;
        public event Action OnStatsChanged;
        public event Action OnMagicChanged;
        public event Action OnMoneyChanged;
        public event Action OnShippingChanged;

        // ---------- NPC ----------
        private const int AffectionPerHeart = 100;
        private const int MaxAffection = 10 * AffectionPerHeart;
        private const int TalkAffection = 12;
        private const int GiftsPerWeek = 2;

        private readonly List<NpcActor> _npcActors = new List<NpcActor>();

        /// <summary>저장된 NPC 관계 상태를 가져온다 (없으면 새로 만든다).</summary>
        public NpcStateData NpcState(string npcId)
        {
            foreach (var s in Data.npcs)
                if (s.npcId == npcId) return s;

            var created = new NpcStateData { npcId = npcId };
            Data.npcs.Add(created);
            return created;
        }

        public int HeartsOf(string npcId) => Mathf.Clamp(NpcState(npcId).affection / AffectionPerHeart, 0, 10);

        private static int WeekOf(int day) => (day - 1) / 7;

        public int GiftsLeftThisWeek(string npcId)
        {
            var st = NpcState(npcId);
            if (st.giftWeek != WeekOf(Data.currentDay)) return GiftsPerWeek;
            return Mathf.Max(0, GiftsPerWeek - st.giftsThisWeek);
        }

        private void ChangeAffection(NpcStateData state, int delta)
        {
            state.affection = Mathf.Clamp(state.affection + delta, 0, MaxAffection);
        }

        /// <summary>바라보는 쪽에 NPC가 있으면 그 NPC를 돌려준다.</summary>
        public NpcActor NpcInFront(PlayerController pc)
        {
            var tile = pc.FacingTile();
            var here = new Vector2Int(Mathf.RoundToInt(pc.transform.position.x), Mathf.RoundToInt(pc.transform.position.y));

            foreach (var actor in _npcActors)
            {
                if (actor == null) continue;
                if (Near(tile, actor.Tile) || Near(here, actor.Tile)) return actor;
            }
            return null;
        }

        /// <summary>
        /// 우클릭으로 NPC와 상호작용한다. 손에 아이템을 들고 있으면 선물할지 물어보고,
        /// 빈손이면 바로 대화한다. 처리했으면 true.
        /// </summary>
        public bool TryInteractNpc(PlayerController pc)
        {
            if (Paused) return false;

            var actor = NpcInFront(pc);
            if (actor == null) return false;

            var def = actor.Def;
            var stack = SelectedStack;
            bool holdingItem = stack != null && !stack.IsEmpty;

            if (holdingItem)
            {
                string itemName = stack.Def != null ? stack.Def.displayName : stack.itemId;
                int slot = Data.farmer.equippedHotbarIndex;
                UIManager.Instance?.ShowYesNo(
                    $"{def.displayName}에게 「{itemName}」을(를) 선물할까요?",
                    onYes: () => GiveGift(def, slot),
                    onNo: () => TalkTo(def));
            }
            else
            {
                TalkTo(def);
            }
            return true;
        }

        /// <summary>하루 한 번 대화. 호감도 구간에 맞는 대사 중 하나를 무작위로 고른다.</summary>
        private void TalkTo(NpcDefinition def)
        {
            var state = NpcState(def.id);

            if (state.lastTalkDay == Data.currentDay)
            {
                UIManager.Instance?.ShowDialogue(def, (int)NpcEmotion.Smile,
                    PickLines(def.alreadyTalkedLines, "오늘은 이미 이야기를 나눴어요."), null, null);
                return;
            }

            state.lastTalkDay = Data.currentDay;
            ChangeAffection(state, TalkAffection);

            var entry = PickDialogue(def, HeartsOf(def.id));
            if (entry == null)
            {
                UIManager.Instance?.ShowDialogue(def, (int)NpcEmotion.Neutral, new[] { "..." }, null, null);
                return;
            }

            UIManager.Instance?.ShowDialogue(def, entry.emotion, entry.lines, entry.choices,
                choice =>
                {
                    ChangeAffection(NpcState(def.id), choice.affection);
                    UIManager.Instance?.ContinueDialogue(def, choice.emotion,
                        PickLines(choice.reply, "그렇군요."));
                });
        }

        /// <summary>일주일에 두 번까지 선물. 아이템 전용 대사가 있으면 그것을, 없으면 등급별 대사를 쓴다.</summary>
        private void GiveGift(NpcDefinition def, int slotIndex)
        {
            var stack = Inventory.GetSlot(slotIndex);
            if (stack == null || stack.IsEmpty) { TalkTo(def); return; }

            var state = NpcState(def.id);
            int week = WeekOf(Data.currentDay);
            if (state.giftWeek != week) { state.giftWeek = week; state.giftsThisWeek = 0; }

            if (state.giftsThisWeek >= GiftsPerWeek)
            {
                UIManager.Instance?.ShowDialogue(def, (int)NpcEmotion.Think,
                    PickLines(def.giftLimitLines, "이번 주엔 벌써 충분히 받았는걸요!"), null, null);
                return;
            }

            string itemId = stack.itemId;
            var tier = def.TierOf(itemId);

            state.giftsThisWeek++;
            ChangeAffection(state, GiftAffection(tier));
            Inventory.ConsumeOne(slotIndex);

            var custom = def.FindItemGiftLine(itemId);
            int emotion = custom != null ? custom.emotion : GiftEmotion(tier);
            string[] lines = custom != null ? custom.lines : PickLines(def.giftLines.For(tier), "고마워요.");

            UIManager.Instance?.ShowDialogue(def, emotion, lines, null, null);
        }

        private static int GiftAffection(GiftTier tier)
        {
            switch (tier)
            {
                case GiftTier.Loved: return 80;
                case GiftTier.Liked: return 45;
                case GiftTier.Disliked: return -20;
                case GiftTier.Hated: return -40;
                default: return 20;
            }
        }

        private static int GiftEmotion(GiftTier tier)
        {
            switch (tier)
            {
                case GiftTier.Loved: return (int)NpcEmotion.Happy;
                case GiftTier.Liked: return (int)NpcEmotion.Smile;
                case GiftTier.Disliked: return (int)NpcEmotion.Sad;
                case GiftTier.Hated: return (int)NpcEmotion.Angry;
                default: return (int)NpcEmotion.Neutral;
            }
        }

        /// <summary>지금 호감도에서 나올 수 있는 대사 중 하나를 무작위로 고른다.</summary>
        private static DialogueEntry PickDialogue(NpcDefinition def, int hearts)
        {
            if (def.dialogues == null || def.dialogues.Length == 0) return null;

            var pool = new List<DialogueEntry>();
            foreach (var e in def.dialogues)
            {
                if (e == null || e.lines == null || e.lines.Length == 0) continue;
                if (hearts < e.minHearts || hearts > e.maxHearts) continue;
                pool.Add(e);
            }
            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        private static string[] PickLines(string[] lines, string fallback)
        {
            if (lines == null || lines.Length == 0) return new[] { fallback };
            return lines;
        }

        /// <summary>이 위치에 사는 NPC들을 배치한다 (위치가 다시 로드될 때마다 호출).</summary>
        private void SpawnNpcs(LocationId locationId)
        {
            _npcActors.Clear();
            foreach (var def in NpcDatabase.All)
            {
                if (def.HomeLocation != locationId) continue;

                var tile = new Vector2Int(def.home.x, def.home.y);
                if (!CurrentLocation.InBounds(tile.x, tile.y)) continue;  // 맵 밖이면 세우지 않는다
                _npcActors.Add(NpcActor.Spawn(CurrentLocation.FeatureRoot, def, tile));
                CurrentLocation.SetNpcBlocked(tile);
            }
        }

        // ---------- 배송함 ----------
        /// <summary>배송함에 담긴 아이템들의 예상 판매 금액 합계.</summary>
        public int ShippingBoxValue
        {
            get
            {
                int total = 0;
                foreach (var s in ShippingBox.slots)
                {
                    if (s == null || s.IsEmpty) continue;
                    var def = s.Def;
                    if (def != null) total += def.sellPrice * s.count;
                }
                return total;
            }
        }

        /// <summary>우클릭으로 배송함을 열어 본다. 바라보는 타일이 배송함이면 UI를 열고 true.</summary>
        public bool TryOpenShippingBox(PlayerController pc)
        {
            if (Paused) return false;
            if (CurrentLocation == null || !CurrentLocation.shippingBoxTile.HasValue) return false;

            var box = CurrentLocation.shippingBoxTile.Value;
            var tile = pc.FacingTile();
            var here = new Vector2Int(Mathf.RoundToInt(pc.transform.position.x), Mathf.RoundToInt(pc.transform.position.y));
            if (!Near(tile, box) && !Near(here, box)) return false;

            UIManager.Instance?.OpenShippingBox();
            return true;
        }

        /// <summary>우클릭으로 상점 수레를 열어 본다. 처리했으면 true.</summary>
        public bool TryOpenShop(PlayerController pc)
        {
            if (Paused) return false;
            if (CurrentLocation == null || !CurrentLocation.shopTile.HasValue) return false;

            var shop = CurrentLocation.shopTile.Value;
            var tile = pc.FacingTile();
            var here = new Vector2Int(Mathf.RoundToInt(pc.transform.position.x), Mathf.RoundToInt(pc.transform.position.y));
            if (!Near(tile, shop) && !Near(here, shop)) return false;

            UIManager.Instance?.OpenShop();
            return true;
        }

        /// <summary>아침이 될 때 배송함을 비우고 판매 대금을 소지금에 더한다. 번 금액을 반환.</summary>
        private int SellShippingBox()
        {
            int income = ShippingBoxValue;
            if (income <= 0) return 0;

            for (int i = 0; i < ShippingBox.slots.Length; i++)
                ShippingBox.slots[i] = null;
            ShippingBox.RaiseChanged();

            AddMoney(income);
            return income;
        }

        // ---------- 소지금 ----------
        public int Money => Data.farmer.money;

        /// <summary>소지금을 더하거나(양수) 뺀다(음수). 0 밑으로는 내려가지 않는다.</summary>
        public void AddMoney(int amount)
        {
            Data.farmer.money = Mathf.Max(0, Data.farmer.money + amount);
            OnMoneyChanged?.Invoke();
        }

        /// <summary>낮(06:00~18:00)이면 true — HUD의 해/달 아이콘에 쓰인다.</summary>
        public bool IsDaytime => Data.currentMinutes >= 6 * 60 && Data.currentMinutes < 18 * 60;

        // ---------- 마법 (스펙/발동 로직은 MagicSystem 참고) ----------
        public MagicType CurrentMagic { get; private set; } = MagicType.Earth;

        /// <summary>Q키로 마법 순환 (대지 → 물 → 칼날 → 대지 ...).</summary>
        public void CycleMagic()
        {
            CurrentMagic = (MagicType)(((int)CurrentMagic + 1) % MagicSystem.Count);
            OnMagicChanged?.Invoke();
        }

        /// <summary>MP가 충분하면 소모하고 true 반환.</summary>
        private bool TrySpendMp(int cost)
        {
            if (Data.farmer.mp < cost)
            {
                UIManager.Instance?.Toast("MP가 부족합니다");
                return false;
            }
            Data.farmer.mp -= cost;
            OnStatsChanged?.Invoke();
            OnTimeChanged?.Invoke(); // HUD의 MP 바 갱신 (RefreshTime이 바를 다시 그림)
            return true;
        }

        private Transform _locationRoot;
        private Camera _cam;
        private float _timeAccum;

        // 1 real second = this many in-game minutes when idle (time passes with actions primarily)
        public float minutesPerRealSecond = 1.0f;

        public void Boot(Camera cam, PlayerController player, Transform locationRoot)
        {
            _cam = cam;
            Player = player;
            _locationRoot = locationRoot;

            ItemDatabase.Init();
            CropDatabase.Init();
            TreeDatabase.Init();
            LootTableDatabase.Init();
            NpcDatabase.Init();
            AssetLibrary.EnsureLoaded();

            // load or new game
            Data = SaveSystem.Load();
            if (Data == null)
            {
                Data = NewGame();
            }

            // 저장된 선택 마법 복원
            CurrentMagic = (MagicType)Mathf.Clamp(Data.farmer.currentMagic, 0, MagicSystem.Count - 1);

            Inventory = new Inventory();
            ShippingBox = new Inventory(Inventory.ShippingSlots);
            ApplyBackpackLevel();
            RestoreInventory();
            Inventory.OnChanged += () => { SaveInventory(); OnHotbarChanged?.Invoke(); };
            ShippingBox.OnChanged += () => { StoreSlots(ShippingBox, Data.shippingBox); OnShippingChanged?.Invoke(); };

            Player.Init(this);
            LoadLocation(Data.currentLocation, new Vector2(Data.farmer.posX, Data.farmer.posY), firstBoot: true);
        }

        private GameData NewGame()
        {
            var d = new GameData
            {
                currentDay = 1,
                currentMinutes = 6 * 60,
                currentLocation = LocationId.Farm1
            };
            d.farmer.posX = 8;
            d.farmer.posY = 7;

            // Starting inventory: 도구는 인벤토리에 넣지 않는다. 딸기 씨앗 5개만 지급.
            d.farmer.slots.Add(new SlotData { index = 0, itemId = "strawberry_seed", count = 5 });
            return d;
        }

        // ---------- inventory persistence ----------
        private void RestoreInventory()
        {
            RestoreSlots(Inventory, Data.farmer.slots);
            RestoreSlots(ShippingBox, Data.shippingBox);
        }

        private void SaveInventory()
        {
            StoreSlots(Inventory, Data.farmer.slots);
            StoreSlots(ShippingBox, Data.shippingBox);
            Data.farmer.currentMagic = (int)CurrentMagic;
        }

        private static void RestoreSlots(Inventory inv, List<SlotData> data)
        {
            foreach (var slot in data)
            {
                if (slot.index >= 0 && slot.index < inv.slots.Length && !string.IsNullOrEmpty(slot.itemId))
                    inv.slots[slot.index] = new ItemStack(slot.itemId, slot.count);
            }
        }

        private static void StoreSlots(Inventory inv, List<SlotData> data)
        {
            data.Clear();
            for (int i = 0; i < inv.slots.Length; i++)
            {
                var s = inv.slots[i];
                if (s != null && !s.IsEmpty)
                    data.Add(new SlotData { index = i, itemId = s.itemId, count = s.count });
            }
        }

        // ---------- location ----------
        public void ChangeLocation(LocationId id, Vector2 spawn)
        {
            // persist current location features before leaving
            if (CurrentLocation != null)
                CurrentLocation.SaveInto(Data.GetLocation(CurrentLocation.id));
            LoadLocation(id, spawn, false);
        }

        /// <summary>
        /// 문으로 드나들 때. 도착지를 좌표로 못 박지 않고, 그 맵을 만든 뒤 문 앞 칸에 내려놓는다 —
        /// 집을 마커로 옮기거나 맵 크기가 바뀌어도 벽 속에 떨어지지 않는다.
        /// </summary>
        public void ChangeLocationThroughDoor(LocationId id)
        {
            if (CurrentLocation != null)
                CurrentLocation.SaveInto(Data.GetLocation(CurrentLocation.id));
            LoadLocation(id, Vector2.zero, false, spawnAtDoor: true);
        }

        /// <summary>
        /// 맵 경계의 출구 칸을 밟아 이동할 때. 도착지에서 "여기로 되돌아가는 출구" 옆에 내려놓기 때문에
        /// 출구를 어디에 칠하든 도착 좌표를 따로 적어 둘 필요가 없다.
        /// </summary>
        public void ChangeLocationThroughExit(LocationId target, Vector2Int fromTile)
        {
            var origin = CurrentLocation != null ? CurrentLocation.id : Data.currentLocation;
            if (CurrentLocation != null)
                CurrentLocation.SaveInto(Data.GetLocation(CurrentLocation.id));
            LoadLocation(target, Vector2.zero, false, entryFrom: origin, entryFromTile: fromTile);
        }

        private void LoadLocation(LocationId id, Vector2 spawn, bool firstBoot, bool spawnAtDoor = false,
                                  LocationId? entryFrom = null, Vector2Int entryFromTile = default)
        {
            if (CurrentLocation != null)
                Destroy(CurrentLocation.gameObject);

            var go = new GameObject($"Location_{id}");
            go.transform.SetParent(_locationRoot, false);
            CurrentLocation = go.AddComponent<GameLocation>();
            CurrentLocation.id = id;
            CurrentLocation.Build(Data);
            RestoreDroppedItems(Data.GetLocation(id));
            SpawnNpcs(id);

            Data.currentLocation = id;

            // place player
            Vector2 p = spawn;
            if (firstBoot)
                p = new Vector2(Data.farmer.posX, Data.farmer.posY);
            else if (spawnAtDoor)
                p = CurrentLocation.DoorEntryTile;
            else if (entryFrom.HasValue)
                p = CurrentLocation.FindEntryFrom(entryFrom.Value, entryFromTile);
            // 맵 크기가 칠한 바닥을 따라가므로, 예전 저장 위치가 벽 속일 수 있다.
            p = CurrentLocation.FindWalkableNear(p);
            Player.transform.position = new Vector3(p.x, p.y, 0);

            CenterCameraInstant();
        }

        /// <summary>이 위치에 저장되어 있던, 아직 줍지 않은 드랍 아이템들을 그대로 되살린다.</summary>
        private void RestoreDroppedItems(LocationData loc)
        {
            foreach (var d in loc.droppedItems)
                WorldItem.CreateAt(CurrentLocation.FeatureRoot, this, d.itemId, d.count, new Vector2(d.x, d.y));
        }

        // ---------- update loop ----------
        private void Update()
        {
            // I = 인벤토리 책, ESC = 설정 책 (같은 페이지를 다시 누르면 닫힌다).
            // 배송함이 열려 있을 때는 두 키 모두 배송함을 닫는다.
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Escape))
            {
                var ui = UIManager.Instance;
                if (ui != null && ui.IsDialogueOpen)
                {
                    // 대화 중에는 다른 창이 겹쳐 열리지 않게 무시한다.
                }
                else if (ui != null && ui.IsShopOpen)
                    ui.CloseShop();
                else if (ui != null && ui.IsShippingOpen)
                    ui.CloseShippingBox();
                else if (Input.GetKeyDown(KeyCode.I))
                    ui?.ToggleBook(BookUI.Page.Inventory);
                else
                    ui?.ToggleBook(BookUI.Page.Settings);
            }

            if (!Paused)
            {
                AdvanceTime(Time.deltaTime * minutesPerRealSecond);
                FollowCamera();
            }

            // sync player pos into data continuously (cheap)
            Data.farmer.posX = Player.transform.position.x;
            Data.farmer.posY = Player.transform.position.y;
            Data.farmer.direction = (int)Player.facing;
        }

        private void AdvanceTime(float minutes)
        {
            _timeAccum += minutes;
            if (_timeAccum >= 1f)
            {
                int add = Mathf.FloorToInt(_timeAccum);
                _timeAccum -= add;
                Data.currentMinutes += add;
                if (Data.currentMinutes >= 24 * 60)
                    Data.currentMinutes = 24 * 60 - 1; // clamp; sleeping resets the day
                OnTimeChanged?.Invoke();
            }
        }

        // ---------- camera ----------
        private void FollowCamera()
        {
            if (_cam == null) return;
            Vector3 target = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, Time.deltaTime * 6f);
        }

        private void CenterCameraInstant()
        {
            if (_cam == null) return;
            _cam.transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
        }

        // ---------- 배낭 / 퀵바 ----------
        /// <summary>배낭 증축 단계별 가격. 인덱스 = 사려는 단계 - 1.</summary>
        public static readonly int[] BackpackPrices = { 300, 500 };

        public int BackpackLevel => Data.farmer.backpackLevel;

        /// <summary>지금 쓸 수 있는 칸 수 (기본 10칸 + 증축 단계마다 10칸).</summary>
        public int UnlockedSlots => (Data.farmer.backpackLevel + 1) * Inventory.HotbarSize;

        /// <summary>증축으로 열린 퀵바 페이지 수.</summary>
        public int HotbarPageCount => Data.farmer.backpackLevel + 1;

        public int HotbarPage => Mathf.Clamp(Data.farmer.hotbarPage, 0, HotbarPageCount - 1);

        /// <summary>다음에 살 수 있는 배낭 가격. 더 살 게 없으면 -1.</summary>
        public int NextBackpackPrice =>
            Data.farmer.backpackLevel < BackpackPrices.Length ? BackpackPrices[Data.farmer.backpackLevel] : -1;

        /// <summary>배낭을 한 단계 증축한다. 성공하면 true.</summary>
        public bool BuyBackpack()
        {
            int price = NextBackpackPrice;
            if (price < 0) return false;
            if (Money < price) return false;

            AddMoney(-price);
            Data.farmer.backpackLevel++;
            ApplyBackpackLevel();
            OnHotbarChanged?.Invoke();
            return true;
        }

        /// <summary>배낭 단계에 맞춰 실제 사용 가능한 칸 수를 인벤토리에 반영한다.</summary>
        private void ApplyBackpackLevel()
        {
            Inventory.unlockedSlots = UnlockedSlots;
            Data.farmer.hotbarPage = HotbarPage; // 범위 보정
        }

        /// <summary>TAB: 증축한 배낭 페이지를 순환한다.</summary>
        public void CycleHotbarPage()
        {
            if (HotbarPageCount <= 1) return;

            int next = (HotbarPage + 1) % HotbarPageCount;
            Data.farmer.hotbarPage = next;

            // 같은 칸 번호를 유지한 채 페이지만 옮긴다.
            int column = Data.farmer.equippedHotbarIndex % Inventory.HotbarSize;
            Data.farmer.equippedHotbarIndex = next * Inventory.HotbarSize + column;

            OnHotbarChanged?.Invoke();
        }

        // ---------- hotbar ----------
        /// <summary>전체 슬롯 번호로 선택 (UI 클릭).</summary>
        public void SelectHotbar(int index)
        {
            Data.farmer.equippedHotbarIndex = Mathf.Clamp(index, 0, UnlockedSlots - 1);
            OnHotbarChanged?.Invoke();
        }

        /// <summary>숫자키 1~0: 지금 보고 있는 페이지의 몇 번째 칸인지로 선택.</summary>
        public void SelectHotbarColumn(int column)
            => SelectHotbar(HotbarPage * Inventory.HotbarSize + column);

        public ItemStack SelectedStack => Inventory.GetSlot(Data.farmer.equippedHotbarIndex);

        // ---------- interaction ----------
        /// <summary>
        /// 좌클릭: 현재 선택된 마법을 바라보는 타일에 시전 시도.
        /// MP가 부족해 시전 자체를 시작할 수 없었으면 false (호출자가 조준/지속시전을 중단하는 데 사용).
        /// </summary>
        public bool CastMagicOnFacingTile(PlayerController pc)
        {
            if (Paused) return false;

            var def = MagicSystem.Get(CurrentMagic);
            if (!TrySpendMp(def.mpCost)) return false;

            var tile = pc.FacingTile();
            if (MagicSystem.TryApply(CurrentMagic, CurrentLocation, tile.x, tile.y, out string dropTableId))
            {
                SpendTime(def.timeCost);
                if (dropTableId != null)
                    ItemDropSpawner.Spawn(CurrentLocation.FeatureRoot, this, tile, dropTableId);
            }
            else
            {
                RefundMp(def.mpCost); // 대상이 없어 아무 일도 없었으면 MP 환불
            }

            return true;
        }

        /// <summary>
        /// 우클릭: 선택된 인벤토리 아이템이 씨앗일 때 바라보는 타일에 심는다.
        /// 나무 씨앗(treeId가 있는 것)은 빈 땅에, 작물 씨앗은 갈아 둔 밭에 심긴다.
        /// </summary>
        public void PlantSelectedOnFacingTile(PlayerController pc)
        {
            if (Paused) return;

            var stack = SelectedStack;
            if (stack == null || stack.IsEmpty || stack.Def.type != ItemType.Seed) return;

            var tile = pc.FacingTile();
            var def = stack.Def;

            bool planted = !string.IsNullOrEmpty(def.treeId)
                ? CurrentLocation.PlantTree(tile.x, tile.y, def.treeId)
                : CurrentLocation.Plant(tile.x, tile.y, def.cropId);

            if (planted)
            {
                Inventory.ConsumeOne(Data.farmer.equippedHotbarIndex);
                SpendTime(5);
            }
        }

        /// <summary>대상이 없어 행동이 무산되면 소모한 MP를 되돌린다.</summary>
        private void RefundMp(int cost)
        {
            Data.farmer.mp = Mathf.Min(Data.farmer.maxMp, Data.farmer.mp + cost);
            OnStatsChanged?.Invoke();
            OnTimeChanged?.Invoke();
        }

        /// <summary>E/Space: 침대와 상호작용해 잠들기.</summary>
        public void TryContextInteract(PlayerController pc)
        {
            if (Paused) return;
            var tile = pc.FacingTile();
            var here = new Vector2Int(Mathf.RoundToInt(pc.transform.position.x), Mathf.RoundToInt(pc.transform.position.y));

            // bed?
            if (CurrentLocation.id == LocationId.FarmHouse && CurrentLocation.bedTile.HasValue)
            {
                var bed = CurrentLocation.bedTile.Value;
                if (Near(tile, bed) || Near(here, bed))
                    UIManager.Instance?.ShowYesNo("잠들겠습니까?", onYes: Sleep);
            }
        }

        /// <summary>
        /// 우클릭: 바라보는 방향과 상관없이 플레이어 주변 1칸(3x3) 안에서 수확 가능한 작물 중
        /// 가장 가까운 것을 수확한다. 수확했으면 true.
        /// </summary>
        public bool TryHarvestNearby(PlayerController pc)
        {
            if (Paused || CurrentLocation == null) return false;

            Vector3 playerPos = pc.transform.position;
            int cx = Mathf.RoundToInt(playerPos.x);
            int cy = Mathf.RoundToInt(playerPos.y);

            var best = Vector2Int.zero;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!CurrentLocation.IsHarvestableAt(x, y)) continue;

                    float dist = (new Vector2(x, y) - (Vector2)playerPos).sqrMagnitude;
                    if (dist >= bestDist) continue;

                    bestDist = dist;
                    best = new Vector2Int(x, y);
                    found = true;
                }
            }

            if (!found) return false;

            var dropTableId = CurrentLocation.HarvestAt(best.x, best.y);
            if (dropTableId == null) return false;

            ItemDropSpawner.Spawn(CurrentLocation.FeatureRoot, this, best, dropTableId);
            SpendTime(3);
            return true;
        }

        private bool Near(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) <= 1 && Mathf.Abs(a.y - b.y) <= 1;

        private void SpendTime(int minutes)
        {
            Data.currentMinutes += minutes;
            if (Data.currentMinutes >= 24 * 60) Data.currentMinutes = 24 * 60 - 1;
            OnTimeChanged?.Invoke();
        }

        // ---------- day / sleep / save ----------
        public void Sleep()
        {
            // persist current location
            CurrentLocation.SaveInto(Data.GetLocation(CurrentLocation.id));

            // advance all locations' crops
            AdvanceAllCrops();

            // 배송함에 넣어 둔 물건은 밤새 팔린다
            int income = SellShippingBox();

            Data.currentDay += 1;
            Data.currentMinutes = 6 * 60; // 06:00

            // 잠을 자면 MP 회복
            Data.farmer.mp = Data.farmer.maxMp;

            OnDayChanged?.Invoke();
            OnTimeChanged?.Invoke();
            OnStatsChanged?.Invoke();

            SaveInventory();
            SaveSystem.Save(Data);

            // reload current location so grown crops render
            LoadLocation(Data.currentLocation, new Vector2(Player.transform.position.x, Player.transform.position.y), false);

            UIManager.Instance?.ShowDayBanner(Data.currentDay);
            if (income > 0) UIManager.Instance?.Toast($"배송함 판매 +{income:N0} G");
        }

        private void AdvanceAllCrops()
        {
            // 살아 있는 위치는 이미 SaveInto로 데이터에 반영된 뒤라 전부 데이터에서 자라게 한다.
            AdvanceCropsInData(Data.farm1);
            AdvanceCropsInData(Data.farm2);
            AdvanceCropsInData(Data.farmHouse);
        }

        private void AdvanceCropsInData(LocationData loc)
        {
            // 나무는 물과 상관없이 하루마다 자란다.
            foreach (var td in loc.trees)
            {
                var tree = new TreeFeature(td.x, td.y, td.treeId, td.growthStage) { dayCounter = td.dayCounter };
                tree.Grow();
                td.growthStage = tree.growthStage;
                td.dayCounter = tree.dayCounter;
            }

            foreach (var hd in loc.hoeDirts)
            {
                if (hd.hasCrop)
                {
                    var crop = new Crop(hd.cropId) { growthStage = hd.growthStage, dayCounter = hd.dayCounter };
                    crop.Grow(hd.watered);
                    hd.growthStage = crop.growthStage;
                    hd.dayCounter = crop.dayCounter;
                }
                hd.watered = false; // water resets each day
            }
        }

        /// <summary>현재 상태를 즉시 저장한다 (F5 단축키와 설정창의 저장 버튼이 공유).</summary>
        public void SaveNow()
        {
            SaveInventory();
            CurrentLocation.SaveInto(Data.GetLocation(CurrentLocation.id));
            SaveSystem.Save(Data);
        }

        // manual save hotkey
        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                SaveNow();
                UIManager.Instance?.Toast("저장됨");
            }
        }

        public string TimeString()
        {
            int h = Data.currentMinutes / 60;
            int m = Data.currentMinutes % 60;
            return $"{h:00}:{m:00}";
        }
    }
}
