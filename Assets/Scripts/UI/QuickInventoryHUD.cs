using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 씬(Canvas/HUD/QuickInventory)에 만들어 둔 퀵 인벤토리를 채운다.
    /// 배치/크기/색은 전부 유니티 에디터에서 만진다 — 이 스크립트는 스프라이트와 아이템만 바꾼다.
    ///   QuickInventoryPanel      → 가방 번호에 맞는 QuickInventory_{번호} 스프라이트로 교체
    ///   Slot_N / Highlight       → 선택한 칸에만 켜지는 QuickInventory_highlight_{N}
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
        [Tooltip("가방 0, 1, 2번 패널. 비워 두면 Resources의 QuickInventory_0~2를 불러온다.")]
        [SerializeField] private Sprite[] panelSprites = new Sprite[Inventory.BackpackPages];

        [Header("Slots")]
        [SerializeField] private QuickInventorySlot[] slots = new QuickInventorySlot[Inventory.HotbarSize];
        [Tooltip("첫 번째 칸의 중심 (패널 중심 기준, 패널 픽셀 좌표).")]
        [SerializeField] private Vector2 firstSlotCenter = new Vector2(-112f, 0f);
        [Tooltip("칸과 칸 사이 간격 (패널 픽셀).")]
        [SerializeField] private float slotPitch = 25f;
        [SerializeField] private Vector2 slotSize = new Vector2(26f, 26f);
        [Tooltip("칸 번호 0~9에 대응하는 하이라이트 스프라이트. 비워 두면 Resources에서 불러온다.")]
        [SerializeField] private Sprite[] highlightSprites = new Sprite[Inventory.HotbarSize];

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

            int page = _game.HotbarPage;
            if (page != _page)
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
                slot.SetHighlighted(selected);
            }
        }

        private void ApplyPanelSprite(int page)
        {
            if (panelImage == null) return;
            var sprite = page >= 0 && page < panelSprites.Length ? panelSprites[page] : null;
            if (sprite != null) panelImage.sprite = sprite;
        }

        /// <summary>퀵바 N번째 칸이 가리키는 실제 인벤토리 번호 (지금 보는 가방 페이지 기준).</summary>
        public int IndexOf(int slotNumber) => (_game != null ? _game.HotbarPage : 0) * Inventory.HotbarSize + slotNumber;

        // ---------- 슬롯 입력 (QuickInventorySlot이 호출) ----------
        public bool InputBlocked => _game == null || (_game.Events != null && _game.Events.IsRunning);

        public void OnSlotClicked(int slotNumber)
        {
            if (InputBlocked) return;
            _game.SelectHotbar(IndexOf(slotNumber));
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

            bool slotsMissing = slots == null || slots.Length != Inventory.HotbarSize;
            if (!slotsMissing) foreach (var s in slots) if (s == null) { slotsMissing = true; break; }
            if (slotsMissing) BuildSlots();
        }

        private void LoadMissingSprites()
        {
            panelSprites = EnsureLength(panelSprites, Inventory.BackpackPages);
            highlightSprites = EnsureLength(highlightSprites, Inventory.HotbarSize);

            FillFromSheet(panelSprites, "QuickInventory");
            FillFromSheet(highlightSprites, "QuickInventory_highlight");
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
        private void BuildSlots()
        {
            if (panelImage == null) panelImage = GetComponentInChildren<Image>(true);
            if (panelImage == null) { Debug.LogWarning("[QuickInventoryHUD] 패널 Image를 찾지 못했습니다."); return; }
            LoadMissingSprites();

            // 이미 만들어 둔 슬롯은 위치를 유지한 채 재사용한다 (에디터에서 옮긴 값을 덮어쓰지 않도록).
            slots = EnsureLength(slots, Inventory.HotbarSize);
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                if (slots[i] != null) continue;

                var existing = PanelRect.Find("Slot_" + i);
                slots[i] = existing != null ? existing.GetComponent<QuickInventorySlot>() : null;
                if (slots[i] == null) slots[i] = CreateSlot(i);
            }
        }

        private QuickInventorySlot CreateSlot(int i)
        {
            var go = new GameObject("Slot_" + i, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(PanelRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = firstSlotCenter + new Vector2(slotPitch * i, 0f);
            rt.sizeDelta = slotSize;

            // 클릭/드래그를 받는 투명한 판
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var highlight = NewImage("Highlight", rt, highlightSprites[i]);
            highlight.enabled = false;
            highlight.raycastTarget = false;
            Stretch(highlight.rectTransform, 0f);

            var icon = NewImage("Icon", rt, null);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            Stretch(icon.rectTransform, 3f);

            var countGo = new GameObject("Count", typeof(RectTransform));
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
