using System;
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
        public GameLocation CurrentLocation { get; private set; }
        public PlayerController Player { get; private set; }

        public bool Paused;              // set true while inventory UI open
        public bool InventoryOpen;

        public event Action OnTimeChanged;
        public event Action OnDayChanged;
        public event Action OnHotbarChanged;
        public event Action OnStatsChanged;
        public event Action OnMagicChanged;

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
            RestoreInventory();
            Inventory.OnChanged += () => { SaveInventory(); OnHotbarChanged?.Invoke(); };

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
            foreach (var slot in Data.farmer.slots)
            {
                if (slot.index >= 0 && slot.index < Inventory.slots.Length && !string.IsNullOrEmpty(slot.itemId))
                    Inventory.slots[slot.index] = new ItemStack(slot.itemId, slot.count);
            }
        }

        private void SaveInventory()
        {
            Data.farmer.slots.Clear();
            for (int i = 0; i < Inventory.slots.Length; i++)
            {
                var s = Inventory.slots[i];
                if (s != null && !s.IsEmpty)
                    Data.farmer.slots.Add(new SlotData { index = i, itemId = s.itemId, count = s.count });
            }
            Data.farmer.currentMagic = (int)CurrentMagic;
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

            Data.currentLocation = id;

            // place player
            Vector2 p = spawn;
            if (firstBoot)
                p = new Vector2(Data.farmer.posX, Data.farmer.posY);
            Player.transform.position = new Vector3(p.x, p.y, 0);

            CenterCameraInstant();
        }

        // ---------- update loop ----------
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
                ToggleInventory();

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
        /// 우클릭: 현재 선택된 마법을 바라보는 타일에 시전 시도.
        /// MP가 부족해 시전 자체를 시작할 수 없었으면 false (호출자가 조준/지속시전을 중단하는 데 사용).
        /// </summary>
        public bool CastMagicOnFacingTile(PlayerController pc)
        {
            if (Paused) return false;

            var def = MagicSystem.Get(CurrentMagic);
            if (!TrySpendMp(def.mpCost)) return false;

            var tile = pc.FacingTile();
            if (MagicSystem.TryApply(CurrentMagic, CurrentLocation, Inventory, tile.x, tile.y))
                SpendTime(def.timeCost);
            else
                RefundMp(def.mpCost); // 대상이 없어 아무 일도 없었으면 MP 환불

            return true;
        }

        /// <summary>좌클릭: 선택된 인벤토리 아이템이 씨앗일 때만 바라보는 타일에 심는다.</summary>
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

        /// <summary>E/Space: try to harvest facing tile or sleep in bed.</summary>
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
                {
                    UIManager.Instance?.ShowYesNo("잠들겠습니까?", onYes: Sleep);
                    return;
                }
            }

            // harvest?
            var item = CurrentLocation.HarvestAt(tile.x, tile.y, out int amount);
            if (item != null && amount > 0)
            {
                Inventory.Add(item, amount);
                SpendTime(3);
            }
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

        public void ToggleInventory()
        {
            InventoryOpen = !InventoryOpen;
            Paused = InventoryOpen;
            UIManager.Instance?.SetInventoryOpen(InventoryOpen);
        }

        // manual save hotkey
        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                SaveInventory();
                CurrentLocation.SaveInto(Data.GetLocation(CurrentLocation.id));
                SaveSystem.Save(Data);
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
