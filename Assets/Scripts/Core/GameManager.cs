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
            CurrentMagic = (MagicType)(((int)CurrentMagic + 1) % 3);
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
            LootTableDatabase.Init();
            AssetLibrary.EnsureLoaded();

            // load or new game
            Data = SaveSystem.Load();
            if (Data == null)
            {
                Data = NewGame();
            }

            // 저장된 선택 마법 복원
            CurrentMagic = (MagicType)Mathf.Clamp(Data.farmer.currentMagic, 0, 2);

            Inventory = new Inventory();
            ShippingBox = new Inventory(Inventory.ShippingSlots);
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

        private void LoadLocation(LocationId id, Vector2 spawn, bool firstBoot)
        {
            if (CurrentLocation != null)
                Destroy(CurrentLocation.gameObject);

            var go = new GameObject($"Location_{id}");
            go.transform.SetParent(_locationRoot, false);
            CurrentLocation = go.AddComponent<GameLocation>();
            CurrentLocation.id = id;
            CurrentLocation.Build(Data);
            RestoreDroppedItems(Data.GetLocation(id));

            Data.currentLocation = id;

            // place player
            Vector2 p = spawn;
            if (firstBoot)
                p = new Vector2(Data.farmer.posX, Data.farmer.posY);
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
                if (ui != null && ui.IsShippingOpen)
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

        // ---------- hotbar ----------
        public void SelectHotbar(int index)
        {
            Data.farmer.equippedHotbarIndex = Mathf.Clamp(index, 0, Inventory.HotbarSize - 1);
            OnHotbarChanged?.Invoke();
        }

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

        /// <summary>우클릭: 선택된 인벤토리 아이템이 씨앗일 때만 바라보는 타일에 심는다.</summary>
        public void PlantSelectedOnFacingTile(PlayerController pc)
        {
            if (Paused) return;

            var stack = SelectedStack;
            if (stack == null || stack.IsEmpty || stack.Def.type != ItemType.Seed) return;

            var tile = pc.FacingTile();
            if (CurrentLocation.Plant(tile.x, tile.y, stack.Def.cropId))
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
        /// 우클릭: 바라보는 타일에 수확 가능한 작물이 있으면 수확한다 (인벤토리 아이템 상호작용과
        /// 같은 키). 수확했으면 true — 호출자는 이게 false일 때만 씨앗 심기를 시도하면 된다.
        /// </summary>
        public bool HarvestOnFacingTile(PlayerController pc)
        {
            if (Paused) return false;
            var tile = pc.FacingTile();
            var dropTableId = CurrentLocation.HarvestAt(tile.x, tile.y);
            if (dropTableId == null) return false;

            ItemDropSpawner.Spawn(CurrentLocation.FeatureRoot, this, tile, dropTableId);
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
            // for the location we're in, use the live one; others via data
            AdvanceCropsInData(Data.farm1);
            AdvanceCropsInData(Data.farm2);
            AdvanceCropsInData(Data.farmHouse);
        }

        private void AdvanceCropsInData(LocationData loc)
        {
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
