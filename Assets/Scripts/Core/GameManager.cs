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

        /// <summary>낚시 상태 기계. UIManager가 준비된 뒤 InitFishing에서 만들어진다.</summary>
        public FishingController Fishing { get; private set; }

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
        /// <summary>마우스 커서가 가리키는 칸. 카메라가 없으면 플레이어가 선 칸.</summary>
        public Vector2Int MouseTile()
        {
            if (_cam == null) return PlayerTile();
            var world = _cam.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
        }

        /// <summary>플레이어가 서 있는 칸.</summary>
        public Vector2Int PlayerTile()
            => Player == null ? Vector2Int.zero
             : new Vector2Int(Mathf.RoundToInt(Player.transform.position.x),
                              Mathf.RoundToInt(Player.transform.position.y));

        /// <summary>손이 닿는 범위인지 — 플레이어가 선 칸과 그 둘레 8칸.</summary>
        public bool InReach(Vector2Int tile) => Near(PlayerTile(), tile);

        /// <summary>
        /// 우클릭한 칸에 있는 오브젝트와 상호작용한다. 처리했으면 true.
        ///
        /// 규칙 두 가지:
        ///  - 바라보는 방향은 <b>보지 않는다</b>. 어느 쪽을 보고 있든 그 오브젝트를 누르면 된다.
        ///  - <b>정확히 그 오브젝트가 있는 칸</b>을 눌러야 한다. 오브젝트와 나 사이의 빈 칸을 눌러도
        ///    아무 일도 일어나지 않는다 (예전에는 바라보는 칸이 오브젝트 옆이기만 하면 열렸다).
        /// 손이 닿는 범위(선 칸 + 둘레 8칸)를 벗어난 칸은 무시한다.
        /// </summary>
        public bool TryInteractAt(Vector2Int clicked)
        {
            if (Paused || CurrentLocation == null) return false;
            if (!InReach(clicked)) return false;

            foreach (var actor in _npcActors)
                if (actor != null && actor.Tile == clicked) return InteractNpc(actor);

            if (CurrentLocation.shippingBoxTile == clicked)
            {
                UIManager.Instance?.OpenShippingBox();
                return true;
            }

            if (CurrentLocation.shopTile == clicked)
            {
                UIManager.Instance?.OpenShop();
                return true;
            }

            if (CurrentLocation.workbenchTile == clicked)
            {
                UIManager.Instance?.OpenCrafting();
                return true;
            }

            // 울타리 문은 우클릭 한 번에 바로 열리고, 다시 누르면 닫힌다 (확인 창 없음).
            if (CurrentLocation.HasGate(clicked.x, clicked.y))
            {
                if (!CurrentLocation.ToggleGate(clicked.x, clicked.y))
                    UIManager.Instance?.Toast("문은 두 개를 나란히 놓아야 열립니다");
                return true;
            }

            if (CurrentLocation.bedTile == clicked)
            {
                UIManager.Instance?.ShowYesNo("잠들겠습니까?", onYes: Sleep);
                return true;
            }

            return false;
        }

        /// <summary>
        /// NPC와 상호작용. 손에 아이템을 들고 있으면 선물할지 물어보고, 빈손이면 바로 대화한다.
        /// </summary>
        private bool InteractNpc(NpcActor actor)
        {
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
            FishingZoneDatabase.Init();   // FishDatabase도 함께 초기화된다
            PlaceableDatabase.Init();     // 울타리·길 (FenceDatabase + RoadDatabase)
            RecipeDatabase.Init();        // 작업대 제작표
            AssetLibrary.EnsureLoaded();

            // load or new game
            Data = SaveSystem.Load();
            if (Data == null)
            {
                Data = NewGame();
            }

            Seasons.SetSilently(Data.currentDay);   // 계절은 날짜에서 나온다 (따로 저장하지 않는다)
            AssetLibrary.ApplySeason(Seasons.Current);

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

        /// <summary>
        /// 낚시 준비. 미니게임 바가 UIManager의 캔버스에 붙기 때문에 UI가 만들어진 뒤에 불러야 한다
        /// (GameBootstrap이 ui.Boot 다음에 호출한다).
        /// </summary>
        public void InitFishing(UIManager ui)
        {
            if (Fishing != null || ui == null) return;
            Fishing = FishingController.Create(this, Player, FishingUI.Create(ui));
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
            Fishing?.Cancel();   // 던져 둔 찌는 맵을 옮기면 사라진다

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
                else if (ui != null && ui.IsCraftingOpen)
                    ui.CloseCrafting();
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
            Vector3 target = ClampToMap(new Vector3(Player.transform.position.x, Player.transform.position.y, -10));
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, Time.deltaTime * 6f);
        }

        private void CenterCameraInstant()
        {
            if (_cam == null) return;
            _cam.transform.position = ClampToMap(new Vector3(Player.transform.position.x, Player.transform.position.y, -10));
        }

        /// <summary>
        /// 화면이 맵 바깥(아무것도 칠하지 않은 회색 배경)을 비추지 않도록 카메라 위치를 맵 안으로 밀어 넣는다.
        /// 칸 (x,y)의 중심이 월드 좌표 (x,y)라서 맵이 실제로 차지하는 범위는 -0.5 ~ width-0.5 다.
        /// 맵이 화면보다 작은 축은 밀어 넣을 곳이 없으므로 맵을 화면 가운데에 둔다.
        /// </summary>
        private Vector3 ClampToMap(Vector3 pos)
        {
            var loc = CurrentLocation;
            if (_cam == null || loc == null || !_cam.orthographic) return pos;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            const float edge = 0.5f;   // 칸 중심 기준 좌표라 테두리 칸의 절반이 더 있다
            float minX = -edge, maxX = loc.width - edge;
            float minY = -edge, maxY = loc.height - edge;

            pos.x = (maxX - minX) <= halfW * 2f
                ? (minX + maxX) * 0.5f
                : Mathf.Clamp(pos.x, minX + halfW, maxX - halfW);
            pos.y = (maxY - minY) <= halfH * 2f
                ? (minY + maxY) * 0.5f
                : Mathf.Clamp(pos.y, minY + halfH, maxY - halfH);
            return pos;
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

            // 물마법을 물 타일에 쓰면 물을 주는 대신 낚시를 시작한다.
            // 낚시는 한 판이 여러 초에 걸쳐 진행되므로 즉시 판정하는 TryApply를 타지 않는다.
            var facing = pc.FacingTile();
            if (CurrentMagic == MagicType.Water && Fishing != null
                && FishingController.CanFishAt(CurrentLocation, facing.x, facing.y))
            {
                if (Fishing.IsActive) return true;
                if (!TrySpendMp(def.mpCost)) return false;
                if (!Fishing.TryStartFishing(CurrentLocation, facing)) RefundMp(def.mpCost);
                return true;
            }

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
        /// 지금 든 씨앗을 이 칸에 심을 수 있는지 (실제로 심지는 않는다 — 조준 표시가 쓴다).
        /// </summary>
        public bool CanPlantSelectedAt(Vector2Int tile)
        {
            if (Paused || CurrentLocation == null || !InReach(tile)) return false;

            var stack = SelectedStack;
            if (stack == null || stack.IsEmpty || stack.Def.type != ItemType.Seed) return false;

            var def = stack.Def;
            if (!string.IsNullOrEmpty(def.treeId)) return CurrentLocation.CanPlantTree(tile.x, tile.y);

            var crop = CropDatabase.Get(def.cropId);
            if (crop != null && !Seasons.AllowsNow(crop.seasons)) return false;
            return CurrentLocation.CanPlant(tile.x, tile.y);
        }

        /// <summary>
        /// 미니게임을 성공했을 때 FishingController가 부른다. 인벤토리에 넣고, 자리가 없으면
        /// 발밑에 떨어뜨린다 (다른 획득 경로와 같은 규칙).
        /// </summary>
        public void OnFishCaught(FishDef fish)
        {
            if (fish == null) return;

            SpendTime(FishingTimeCost);

            int left = Inventory.Add(fish.fishId, 1);
            if (left > 0)
                WorldItem.Create(CurrentLocation.FeatureRoot, this, fish.fishId, left,
                                 new Vector2(Player.transform.position.x, Player.transform.position.y));

            UIManager.Instance?.Toast($"{fish.displayName} 을(를) 잡았다! ({fish.RarityLabel})");
        }

        /// <summary>낚시 한 판에 소모되는 게임 내 분.</summary>
        private const int FishingTimeCost = 20;

        /// <summary>
        /// 우클릭: 선택된 인벤토리 아이템이 씨앗일 때 바라보는 타일에 심는다.
        /// 나무 씨앗(treeId가 있는 것)은 빈 땅에, 작물 씨앗은 갈아 둔 밭에 심긴다.
        /// </summary>
        public void PlantSelectedAt(Vector2Int tile)
        {
            if (Paused || CurrentLocation == null) return;
            if (!InReach(tile)) return;   // 주변 8칸 밖에는 못 심는다

            var stack = SelectedStack;
            if (stack == null || stack.IsEmpty || stack.Def.type != ItemType.Seed) return;

            var def = stack.Def;

            if (!string.IsNullOrEmpty(def.cropId))
            {
                var crop = CropDatabase.Get(def.cropId);
                if (crop != null && !Seasons.AllowsNow(crop.seasons))
                {
                    UIManager.Instance?.Toast($"{crop.name}은(는) {Seasons.Name(Seasons.Current)}에 심을 수 없다");
                    return;
                }
            }

            bool planted = !string.IsNullOrEmpty(def.treeId)
                ? CurrentLocation.PlantTree(tile.x, tile.y, def.treeId)
                : CurrentLocation.Plant(tile.x, tile.y, def.cropId);

            if (planted)
            {
                Inventory.ConsumeOne(Data.farmer.equippedHotbarIndex);
                SpendTime(5);
            }
        }

        // ---------- 설치물 (울타리·길) ----------
        /// <summary>지금 든 것이 설치물이면 그 정의를, 아니면 null.</summary>
        public PlaceableDef SelectedPlaceable
        {
            get
            {
                var stack = SelectedStack;
                if (stack == null || stack.IsEmpty) return null;
                return PlaceableDatabase.Get(stack.itemId);
            }
        }

        /// <summary>지금 든 설치물을 이 칸에 놓을 수 있는지 (조준 표시가 쓴다).</summary>
        public bool CanPlaceSelectedAt(Vector2Int tile)
        {
            if (Paused || CurrentLocation == null || !InReach(tile)) return false;
            var def = SelectedPlaceable;
            if (def == null) return false;
            // 서 있는 칸에 막는 것을 세우면 그 자리에 갇힌다.
            if (def.blocks && tile == PlayerTile()) return false;
            return CurrentLocation.CanPlace(tile.x, tile.y, def);
        }

        /// <summary>우클릭: 지금 든 설치물을 이 칸에 놓는다. 하나 놓을 때마다 한 개가 빠진다.</summary>
        public void PlaceSelectedAt(Vector2Int tile)
        {
            if (!CanPlaceSelectedAt(tile)) return;

            var def = SelectedPlaceable;
            if (!CurrentLocation.PlaceAt(tile.x, tile.y, def.id)) return;

            Inventory.ConsumeOne(Data.farmer.equippedHotbarIndex);
            SpendTime(2);
        }

        /// <summary>
        /// 작업대 제작. 재료를 인벤토리에서 빼고 결과물을 가방에 넣는다.
        /// 가방이 꽉 차서 다 못 들어가면 발밑에 떨어뜨린다 (낚시·수확과 같은 규칙).
        /// </summary>
        public bool Craft(CraftingRecipe recipe)
        {
            // Paused를 보면 안 된다 — 제작 창이 열려 있는 동안은 항상 Paused라서 아무것도 못 만든다.
            // (상점의 BuyBackpack도 같은 이유로 Paused를 보지 않는다.)
            if (recipe == null) return false;
            if (!recipe.CanCraft(Inventory))
            {
                UIManager.Instance?.Toast("재료가 부족합니다");
                return false;
            }

            foreach (var cost in recipe.costs)
                Inventory.Remove(cost.itemId, cost.count);

            int left = Inventory.Add(recipe.resultItemId, recipe.resultCount);
            if (left > 0 && CurrentLocation != null)
                WorldItem.Create(CurrentLocation.FeatureRoot, this, recipe.resultItemId, left,
                                 new Vector2(Player.transform.position.x, Player.transform.position.y));

            SpendTime(5);
            UIManager.Instance?.Toast($"{recipe.DisplayName} x{recipe.resultCount} 을(를) 만들었다");
            return true;
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
            if (Paused || CurrentLocation == null) return;

            // 침대 옆에 서 있을 때만. 예전에는 "바라보는 칸이 침대 옆이기만 해도" 열려서,
            // 두 칸 떨어져 침대 쪽을 보기만 해도 잠들 수 있었다.
            if (CurrentLocation.bedTile.HasValue && InReach(CurrentLocation.bedTile.Value))
                UIManager.Instance?.ShowYesNo("잠들겠습니까?", onYes: Sleep);
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

            // 계절이 바뀌면: 그림을 갈아 끼우고, 그 계절에 못 사는 작물을 걷어낸다.
            int withered = 0;
            bool seasonChanged = Seasons.SyncToDay(Data.currentDay);
            if (seasonChanged)
            {
                AssetLibrary.ApplySeason(Seasons.Current);
                withered = WitherOutOfSeasonCrops(Data.farm1)
                         + WitherOutOfSeasonCrops(Data.farm2)
                         + WitherOutOfSeasonCrops(Data.farmHouse);
            }

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
            if (seasonChanged)
            {
                UIManager.Instance?.Toast($"{Seasons.Name(Seasons.Current)}이(가) 되었다"
                                          + (withered > 0 ? $" — 철 지난 작물 {withered}개가 시들었다" : ""));
            }
        }

        /// <summary>
        /// 계절이 바뀌었을 때 그 계절에 못 사는 작물을 저장 데이터에서 걷어낸다. 다 자란 것도 예외 없이
        /// 시든다 — 계절이 끝나기 전에 거두라는 압력이 이 시스템의 핵심이다.
        /// 살아 있는 GameLocation이 아니라 데이터를 고치는 이유: 지금 서 있지 않은 맵도 함께 처리해야 하고,
        /// 바로 뒤에서 위치를 다시 로드하기 때문에 화면은 저절로 맞춰진다.
        /// </summary>
        private static int WitherOutOfSeasonCrops(LocationData loc)
        {
            int withered = 0;
            foreach (var hd in loc.hoeDirts)
            {
                if (!hd.hasCrop) continue;
                var def = CropDatabase.Get(hd.cropId);
                if (def != null && Seasons.AllowsNow(def.seasons)) continue;
                hd.hasCrop = false;
                hd.cropId = null;
                hd.growthStage = 0;
                hd.dayCounter = 0;
                withered++;
            }
            return withered;
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
