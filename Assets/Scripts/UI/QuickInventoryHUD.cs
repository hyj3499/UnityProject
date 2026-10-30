using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 씬(Canvas/HUD/QuickInventory)에 만들어 둔 퀵 인벤토리를 채운다.
    /// 가방 번호에 맞는 슬롯 그림을 같은 간격으로 10개 배치한다.
    ///   Slot_N / Highlight       → Book과 같은 네 모서리 선택 애니메이션 (한 번 재생)
    ///   Slot_N / Icon, Count     → 아이템 그림과 개수
    /// 슬롯 오브젝트는 컴포넌트 우클릭 메뉴 "슬롯 생성"으로 만들 수 있고, 만들어 두지 않았으면 실행 시 자동 생성된다.
    /// 게임(GameManager)은 타이틀에서 시작을 누른 뒤에야 생기므로, 없는 동안은 숨긴다.
    /// </summary>
    public class QuickInventoryHUD : MonoBehaviour
    {
        private const string SpriteFolder = "Sprites/UI/QuickInventory/";

        [Header("Panel")]
        [Tooltip("비워 두면 자식에서 Image를 찾는다 (QuickInventoryPanel).")]
        [SerializeField] private Image panelImage;
        [Tooltip("첫 번째~세 번째 가방의 개별 슬롯 그림.")]
        [SerializeField] private Sprite[] panelSprites = new Sprite[Inventory.BackpackPages];

        [Header("Slots")]
        [SerializeField] private QuickInventorySlot[] slots = new QuickInventorySlot[Inventory.HotbarSize];
        [Min(0f), SerializeField] private float slotGap = 3f;
        [SerializeField] private Vector2 slotSize = new Vector2(27f, 27f);
        [Min(0f), SerializeField] private float selectedLift = 3f;
        [SerializeField] private Color normalTint = new Color(.8f, .8f, .8f, 1f);
        [SerializeField] private Color selectedTint = Color.white;
        private Sprite[] _selectorFrames;

        private GameManager _game;
        private CanvasGroup _group;
        private int _page = -1;

        public RectTransform PanelRect => panelImage != null ? panelImage.rectTransform : null;
        public Transform CanvasRoot => GetComponentInParent<Canvas>()?.rootCanvas.transform;

        /// <summary>씬의 "QuickInventory" 오브젝트에 컴포넌트를 붙여 두지 않았어도 자동으로 붙인다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToScene()
        {
            if (FindObjectOfType<QuickInventoryHUD>(true) != null) return;
            foreach (var canvas in FindObjectsOfType<Canvas>(true))
                foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
                    if (rt.name == "QuickInventory")
                    {
                        rt.gameObject.AddComponent<QuickInventoryHUD>();
                        return;
                    }
        }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            ResolveReferences();
            SetVisible(false);
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            // 타이틀로 나가면 GameManager가 파괴되고, 새 게임을 시작하면 새로 생긴다.
            if (_game == null)
            {
                if (_group.alpha > 0f) SetVisible(false);
                var found = FindObjectOfType<GameManager>();
                if (found != null && found.Data != null && found.Inventory != null) Bind(found);
                return;
            }
        }

        // ---------- 연결 ----------
        private void Bind(GameManager game)
        {
            _game = game;
            _game.OnHotbarChanged += Refresh;
            _page = -1;
            Refresh();
            SetVisible(true);
        }

        private void Unbind()
        {
            if (_game == null) return;
            _game.OnHotbarChanged -= Refresh;
            _game = null;
        }

        private void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
        }

        // ---------- 갱신 ----------
        private void Refresh()
        {
            if (_game == null || _game.Inventory == null) return;

            // 개방한 가방 수(BackpackLevel)가 아니라 현재 Tab으로 보고 있는 가방의 색을 쓴다.
            int page = _game.HotbarPage;
            bool pageChanged = page != _page;
            if (pageChanged)
            {
                _page = page;
                ApplyPanelSprite(page);
            }

            int equipped = _game.Data.farmer.equippedHotbarIndex;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                int index = IndexOf(i);
                bool selected = index == equipped;
                slot.Show(_game.Inventory.GetSlot(index));
                slot.SetHighlighted(selected, pageChanged);
            }
        }

        private void ApplyPanelSprite(int page)
        {
            var sprite = page >= 0 && page < panelSprites.Length ? panelSprites[page] : null;
            foreach (var slot in slots) if (slot != null) slot.SetBackground(sprite);
        }

        /// <summary>퀵바 N번째 칸이 가리키는 실제 인벤토리 번호 (지금 보는 가방 페이지 기준).</summary>
        public int IndexOf(int slotNumber) => (_game != null ? _game.HotbarPage : 0) * Inventory.HotbarSize + slotNumber;

        // ---------- 슬롯 입력 (QuickInventorySlot이 호출) ----------
        public bool InputBlocked => _game == null || (_game.Events != null && _game.Events.IsRunning);

        public void OnSlotClicked(int slotNumber)
        {
            if (InputBlocked) return;
            _game.SelectHotbar(IndexOf(slotNumber));
            slots[slotNumber].SetHighlighted(true, true);
        }

        public void OnSlotDropped(int fromSlot, int toSlot)
        {
            if (InputBlocked) return;
            _game.Inventory.MoveSlot(IndexOf(fromSlot), IndexOf(toSlot));
        }

        // ---------- 에디터에서 쓸 오브젝트 만들기 ----------
        private void Reset() => ResolveReferences();

        private void ResolveReferences()
        {
            if (panelImage == null) panelImage = GetComponentInChildren<Image>(true);
            LoadMissingSprites();
            BuildSlots();
        }

        private void LoadMissingSprites()
        {
            panelSprites = EnsureLength(panelSprites, Inventory.BackpackPages);
            FillFromSheet(panelSprites, "QuickInventory");
            if (_selectorFrames == null || _selectorFrames.Length != 8)
            {
                _selectorFrames = new Sprite[8];
                FillFromSheet(_selectorFrames, "Inventory Selector");
            }
        }

        private static void FillFromSheet(Sprite[] target, string sheet)
        {
            bool missing = false;
            foreach (var s in target) if (s == null) { missing = true; break; }
            if (!missing) return;

            var all = Resources.LoadAll<Sprite>(SpriteFolder + sheet);
            for (int i = 0; i < target.Length; i++)
            {
                if (target[i] != null) continue;
                string name = sheet + "_" + i;
                foreach (var sp in all) if (sp.name == name) { target[i] = sp; break; }
            }
        }

        private static T[] EnsureLength<T>(T[] array, int length)
        {
            if (array != null && array.Length == length) return array;
            var result = new T[length];
            if (array != null) Array.Copy(array, result, Math.Min(array.Length, length));
            return result;
        }

        [ContextMenu("슬롯 생성")]
        public void BuildSlots()
        {
            if (panelImage == null) panelImage = GetComponentInChildren<Image>(true);
            if (panelImage == null) { Debug.LogWarning("[QuickInventoryHUD] 패널 Image를 찾지 못했습니다."); return; }
            LoadMissingSprites();
            panelImage.sprite = null;
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
            float pitch = slotSize.x + slotGap;
            PanelRect.sizeDelta = new Vector2(pitch * Inventory.HotbarSize - slotGap, slotSize.y);
            slots = EnsureLength(slots, Inventory.HotbarSize);
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                if (slots[i] == null)
                {
                    var existing = PanelRect.Find("Slot_" + i);
                    slots[i] = existing != null ? existing.GetComponent<QuickInventorySlot>() : null;
                }
                if (slots[i] == null) slots[i] = CreateSlot(i);
                var center = new Vector2((i - (Inventory.HotbarSize - 1) * .5f) * pitch, 0f);
                slots[i].ConfigurePresentation(center, slotSize, selectedLift, normalTint, selectedTint, _selectorFrames);
            }
            ApplyPanelSprite(_game != null ? _game.HotbarPage : 0);
        }

        private QuickInventorySlot CreateSlot(int i)
        {
            var go = new GameObject("Slot_" + i, typeof(RectTransform));
            go.layer = PanelRect.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(PanelRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = slotSize;

            // 슬롯 배경과 클릭/드래그 입력 영역.
            var hit = go.AddComponent<Image>();
            hit.color = normalTint;

            var highlight = NewImage("Highlight", rt, null);
            highlight.enabled = false;
            highlight.raycastTarget = false;
            Stretch(highlight.rectTransform, 0f);

            var icon = NewImage("Icon", rt, null);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            Stretch(icon.rectTransform, 3f);

            var countGo = new GameObject("Count", typeof(RectTransform));
            countGo.layer = rt.gameObject.layer;
            var crt = (RectTransform)countGo.transform;
            crt.SetParent(rt, false);
            Stretch(crt, 1f);
            var count = countGo.AddComponent<TextMeshProUGUI>();
            count.fontSize = 8f;
            count.alignment = TextAlignmentOptions.BottomRight;
            count.color = Color.white;
            count.raycastTarget = false;
            count.text = "";

            var slot = go.AddComponent<QuickInventorySlot>();
            slot.Setup(this, i, highlight, icon, count);
            return slot;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            return img;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
