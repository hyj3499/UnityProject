using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>씬에서 편집한 BookMenuView를 구동한다. UI 배치는 런타임에 재작성하지 않는다.</summary>
    public class BookUI : MonoBehaviour
    {
        public enum Page { Inventory, Skills, Residents, Livestock, Quests, MapEncyclopedia, Tutorial, Settings, Help = Tutorial }
        private UIManager _ui;
        private GameManager _game;
        private BookMenuView _view;
        private Page _current;
        private bool _open, _closing;
        private Coroutine _animation;
        private Coroutine _detailAnimation;
        private int _selectedSlot = -1;
        private int _shownSlot = -1;
        private Vector2 _iconHolderHome;
        private readonly List<GameObject> _hiddenHuds = new List<GameObject>();
        private readonly List<Action> _removeListeners = new List<Action>();
        private List<Vector2Int> _resolutions;
        private int _mode, _resolution;
        private float _zoom;
        private bool _saveBeforeQuit;
        public bool IsOpen => _open;
        public BookMenuView View => _view;

        public void Boot(UIManager ui, GameManager game, RectTransform canvas)
        {
            _ui = ui;
            _game = game;
            AudioListener.volume = PlayerPrefs.GetFloat("farm_mvp_volume", 1f);
            _view = FindObjectsByType<BookMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.configured && v.gameObject.scene == game.gameObject.scene);
            if (_view == null)
            {
                var placeholder = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(r => r.name == "MenuBookUI" && r.gameObject.scene == game.gameObject.scene);
                var root = placeholder != null ? placeholder : BookMenuLayout.Node(canvas, "MenuBookUI", 0, 0, 1, 1);
                _view = BookMenuLayout.Build(root);
            }
            BookMenuLayout.UpgradeInventory(_view);
            if (_view.iconHolder != null) _iconHolderHome = _view.iconHolder.anchoredPosition;
            _view.controller = this;
            _view.gameObject.SetActive(false);
            _view.confirmation.SetActive(false);
            Listen(_view.close, Close);
            for (int i = 0; i < _view.tabs.Length; i++)
            {
                int index = i;
                Listen(_view.tabs[i], () => Open((Page)index));
            }
            for (int i = 0; i < _view.slots.Length; i++)
            {
                int index = i;
                Listen(_view.slots[i].button, () => SelectItem(index));
            }
            for (int i = 0; i < _view.categories.Length; i++)
            {
                int index = i;
                Listen(_view.categories[i], () => _view.PreviewSettings(index));
            }
            Listen(_view.previousMode, () => { _mode = (_mode + 2) % 3; RefreshDisplayLabels(); });
            Listen(_view.nextMode, () => { _mode = (_mode + 1) % 3; RefreshDisplayLabels(); });
            Listen(_view.previousResolution, () => StepResolution(-1));
            Listen(_view.nextResolution, () => StepResolution(1));
            Listen(_view.applyDisplay, ApplyDisplay);
            Listen(_view.saveAndQuit, () => AskQuit(true));
            Listen(_view.quit, () => AskQuit(false));
            Listen(_view.confirmNo, () => _view.confirmation.SetActive(false));
            Listen(_view.confirmYes, ConfirmQuit);
            UnityEngine.Events.UnityAction<float> scaleChanged = value => { _zoom = value; RefreshDisplayLabels(); };
            _view.scale.onValueChanged.AddListener(scaleChanged);
            _removeListeners.Add(() => { if (_view != null) _view.scale.onValueChanged.RemoveListener(scaleChanged); });
            _game.Inventory.OnChanged += RefreshStatus;
            _game.OnStatsChanged += RefreshStatus;
            _game.OnMoneyChanged += RefreshStatus;
        }

        private void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.AddListener(action);
            _removeListeners.Add(() => { if (button != null) button.onClick.RemoveListener(action); });
        }

        public void Toggle(Page page)
        {
            if (_view.confirmation.activeSelf) { _view.confirmation.SetActive(false); return; }
            if (_open && !_closing && _current == page) Close();
            else Open(page);
        }

        // 외부 HUD Button의 OnClick에서도 사용할 수 있는 진입점.
        public void OpenInventory() => Open(Page.Inventory);
        public void OpenSettings() => Open(Page.Settings);
        public void OpenPage(int index) => Open((Page)Mathf.Clamp(index, 0, 7));

        public void Open(Page page)
        {
            if (_view == null || _game == null || (int)page < 0 || (int)page >= 8) return;
            if (_view.confirmation.activeSelf) return;
            bool opening = !_open || _closing;
            if (!opening && _current == page) return;
            bool right = (int)page > (int)_current;
            _current = page;
            if (page == Page.Inventory)
            {
                _selectedSlot = -1;
                _shownSlot = -1;
                HideItemDetails();
            }
            _closing = false;
            _open = true;
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
            HideHuds();
            RefreshStatus();
            if (page == Page.Inventory) RefreshPortrait();
            if (page == Page.Settings) LoadDisplay();
            _ui?.SyncPaused();
            BeginAnimation(opening ? _view.opening : right ? _view.flipRight : _view.flipLeft, false);
        }

        public void Close()
        {
            if (!_open || _closing) return;
            _view.confirmation.SetActive(false);
            _closing = true;
            BeginAnimation(_view.closing, true);
        }

        private void BeginAnimation(Sprite[] frames, bool closing)
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(Animate(frames, closing));
        }

        private IEnumerator Animate(Sprite[] frames, bool closing)
        {
            _view.content.alpha = 0;
            _view.content.blocksRaycasts = false;
            _view.bookmarks.SetActive(false);
            _view.close.gameObject.SetActive(false);
            foreach (var sprite in frames ?? Array.Empty<Sprite>())
            {
                if (sprite == null) continue;
                SetFrame(sprite);
                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1, _view.framesPerSecond));
            }
            SetFrame(_view.idle);
            _view.PreviewPage((int)_current);
            _view.content.alpha = 1;
            _view.content.blocksRaycasts = true;
            _view.bookmarks.SetActive(true);
            _view.close.gameObject.SetActive(true);
            _animation = null;
            if (closing) FinishClose();
        }

        private void SetFrame(Sprite sprite)
        {
            // 프레임의 원본 텍스처 좌표로 배치하여 자동 트리밍된 프레임도 제본 위치가 흔들리지 않는다.
            var rect = _view.background.rectTransform;
            Vector2 ratio = _view.book.rect.size / _view.idle.rect.size;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = Vector2.Scale(sprite.rect.size, ratio);
            rect.anchoredPosition = Vector2.Scale(sprite.rect.center - _view.idle.rect.center, ratio);
            _view.background.sprite = sprite;
            _view.background.preserveAspect = false;
        }

        private void FinishClose()
        {
            _open = _closing = false;
            if (_view != null) _view.gameObject.SetActive(false);
            RestoreHuds();
            _ui?.SyncPaused();
        }

        private void LateUpdate()
        {
            if (_open && (_view == null || !_view.gameObject.activeInHierarchy))
            {
                if (_animation != null) StopCoroutine(_animation);
                _animation = null;
                FinishClose();
            }
        }

        private void HideHuds()
        {
            var roots = new HashSet<GameObject>(_view.hudRoots.Where(h => h != null));
            foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rect.name == "HUD") roots.Add(rect.gameObject);
            foreach (var hud in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hud is QuickInventoryHUD || hud is QuickSkillHUD || hud is HPMPBarsHUD || hud is DateTimeHUD)
                    roots.Add(hud.gameObject);
            foreach (var root in roots)
            {
                if (!root.activeSelf || _view.transform.IsChildOf(root.transform) || transform.IsChildOf(root.transform)) continue;
                // 이미 HUD 조상을 숨긴 경우에도 activeSelf를 기록하여 원래 상태를 보존한다.
                if (!_hiddenHuds.Contains(root)) _hiddenHuds.Add(root);
                root.SetActive(false);
            }
        }

        private void RestoreHuds()
        {
            foreach (var root in _hiddenHuds) if (root != null) root.SetActive(true);
            _hiddenHuds.Clear();
        }

        public void RefreshStatus()
        {
            if (_view == null || _game == null || _game.Data == null) return;
            var farmer = _game.Data.farmer;
            _view.playerName.text = Localization.Get("ui.bookui.name") + (string.IsNullOrWhiteSpace(farmer.playerName) ? Localization.Get("ui.bookui.farmer") : farmer.playerName);
            string[] seasons = { Localization.Get("ui.bookui.spring"), Localization.Get("ui.bookui.summer"), Localization.Get("ui.bookui.fall"), Localization.Get("ui.bookui.winter") };
            _view.birthday.text = farmer.birthdayDay > 0
                ? Localization.Format("ui.character.birthday", seasons[Mathf.Clamp(farmer.birthdaySeason, 0, 3)], farmer.birthdayDay)
                : Localization.Get("ui.bookui.birthday") + Localization.Get("ui.bookui.not_set");
            _view.money.text = Localization.Format("ui.bookui.gold_0_n0_g", _game.Money);
            _view.hp.text = Localization.Format("ui.bookui.hp_0_1", farmer.hp, farmer.maxHp);
            _view.mp.text = $"MP  {farmer.mp} / {farmer.maxMp}";
            int unlocked = Mathf.Clamp(_game.UnlockedSlots, 0, 30);
            BookInventoryLayout.ApplyMenu(_view, unlocked);
            for (int i = 0; i < _view.slots.Length; i++)
            {
                var slot = _view.slots[i];
                var stack = _game.Inventory.GetSlot(i);
                bool filled = i < unlocked && stack != null && !stack.IsEmpty;
                slot.button.interactable = i < unlocked;
                slot.icon.sprite = filled ? stack.Def?.GetSprite() : null;
                slot.icon.enabled = slot.icon.sprite != null;
                slot.count.text = filled && stack.count > 1 ? stack.count.ToString() : "";
                BookInventoryLayout.SetSelected(slot, filled && _selectedSlot == i);
            }
            if (_selectedSlot >= unlocked) _selectedSlot = -1;
            ShowItem();
        }

        private void SelectItem(int index)
        {
            if (index >= _game.UnlockedSlots) return;
            _selectedSlot = index;
            _shownSlot = -1;
            RefreshStatus();
            if (_selectedSlot == index)
                BookInventoryLayout.SetSelected(_view.slots[index], _view.slots[index].icon.enabled, true);
        }

        private void ShowItem()
        {
            var item = _game.Inventory.GetSlot(_selectedSlot);
            var def = item != null && !item.IsEmpty ? item.Def : null;
            if (def == null)
            {
                _shownSlot = -1;
                HideItemDetails();
                return;
            }
            _view.itemIcon.sprite = def?.GetSprite();
            _view.itemIcon.enabled = _view.itemIcon.sprite != null;
            _view.itemName.text = def.displayName;
            _view.itemDescription.text = def.Description;
            _view.itemFacts.text = Localization.Format("ui.bookui.owned_0", item.count) + (def.sellPrice > 0 ? Localization.Format("ui.bookui.sell_price_0_n0_g_each", def.sellPrice) : Localization.Get("ui.bookui.this_item_cannot_be_sold"));
            if (_shownSlot != _selectedSlot)
            {
                _shownSlot = _selectedSlot;
                if (_detailAnimation != null) StopCoroutine(_detailAnimation);
                _detailAnimation = StartCoroutine(DropItemDetails());
            }
        }

        private void HideItemDetails()
        {
            if (_detailAnimation != null) StopCoroutine(_detailAnimation);
            _detailAnimation = null;
            BookMenuLayout.SetItemDetailsVisible(_view, false);
            if (_view != null && _view.iconHolder != null) _view.iconHolder.anchoredPosition = _iconHolderHome;
        }

        private IEnumerator DropItemDetails()
        {
            if (_view.itemHeader != null) _view.itemHeader.transform.parent.gameObject.SetActive(true);
            if (_view.iconHolder != null)
            {
                _view.iconHolder.gameObject.SetActive(true);
                _view.iconHolder.anchoredPosition = _iconHolderHome + Vector2.up * 115;
            }
            if (_view.detailReveal != null)
            {
                _view.detailReveal.gameObject.SetActive(true);
                _view.detailReveal.alpha = 0;
            }
            float seconds = Mathf.Max(.1f, _view.itemDropSeconds);
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                // 살짝 지나쳤다가 제자리로 돌아오는 착지 곡선.
                float c = 1.70158f;
                float eased = 1 + (c + 1) * Mathf.Pow(t - 1, 3) + c * Mathf.Pow(t - 1, 2);
                if (_view.iconHolder != null)
                    _view.iconHolder.anchoredPosition = Vector2.LerpUnclamped(_iconHolderHome + Vector2.up * 115, _iconHolderHome, eased);
                if (_view.detailReveal != null)
                    _view.detailReveal.alpha = Mathf.InverseLerp(.68f, 1, t);
                yield return null;
            }
            if (_view.iconHolder != null) _view.iconHolder.anchoredPosition = _iconHolderHome;
            if (_view.detailReveal != null) _view.detailReveal.alpha = 1;
            _detailAnimation = null;
        }

        private void RefreshPortrait()
        {
            var parent = _view.portrait;
            foreach (Transform child in parent)
                if (child.name.StartsWith("Appearance_")) Destroy(child.gameObject);
            var player = _game.Player;
            var animator = player != null ? player.GetComponentInChildren<FarmerAnimator>() : null;
            var renderers = animator != null ? animator.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.enabled && r.sprite != null).OrderBy(r => r.sortingOrder).ToArray() : Array.Empty<SpriteRenderer>();
            var placeholder = parent.Find("Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(renderers.Length == 0);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Canvas.ForceUpdateCanvases();
            float scale = Mathf.Min(parent.rect.width*.88f/Mathf.Max(.01f, bounds.size.x), parent.rect.height*.88f/Mathf.Max(.01f, bounds.size.y));
            for (int i = 0; i < renderers.Length; i++)
            {
                var source = renderers[i];
                var go = new GameObject("Appearance_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = Vector2.one*.5f;
                rect.anchoredPosition = (Vector2)(source.bounds.center - bounds.center)*scale;
                rect.sizeDelta = Vector2.Scale(source.sprite.rect.size/source.sprite.pixelsPerUnit,
                    new Vector2(Mathf.Abs(source.transform.lossyScale.x), Mathf.Abs(source.transform.lossyScale.y)))*scale;
                rect.localScale = new Vector3(source.flipX ? -1 : 1, source.flipY ? -1 : 1, 1);
                var image = go.GetComponent<Image>();
                image.sprite = source.sprite;
                image.color = source.color;
                image.raycastTarget = false;
            }
        }

        private void LoadDisplay()
        {
            _resolutions = BookDisplaySettings.Resolutions();
            _resolution = _resolutions.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
            if (_resolution < 0) _resolution = 0;
            _mode = Mathf.Max(0, Array.IndexOf(BookDisplaySettings.Modes, Screen.fullScreenMode));
            _zoom = _game.ZoomMultiplier;
            _view.scale.SetValueWithoutNotify(_zoom);
            _view.displayMessage.text = "";
            RefreshDisplayLabels();
        }

        private void StepResolution(int direction)
        {
            _resolution = (_resolution + direction + _resolutions.Count) % _resolutions.Count;
            RefreshDisplayLabels();
        }

        private void RefreshDisplayLabels()
        {
            _view.modeLabel.text = BookDisplaySettings.ModeNames[_mode];
            if (_resolutions != null)
            {
                var size = _resolutions[_resolution];
                _view.resolutionLabel.text = $"{size.x} × {size.y}";
            }
            _view.scaleLabel.text = Localization.Format("ui.bookui.game_zoom_0_0", _zoom*100);
        }

        private void ApplyDisplay()
        {
            _game.SetZoom(_zoom);
            BookDisplaySettings.Apply(_resolutions[_resolution], _mode);
            _view.displayMessage.text = Application.isEditor ? Localization.Get("ui.bookui.saved_screen_settings_apply_in_the_game") : Localization.Get("ui.bookui.settings_applied");
        }

        private void AskQuit(bool save)
        {
            _saveBeforeQuit = save;
            _view.confirmationText.text = save ? Localization.Get("ui.bookui.save_and_exit_the_game") : Localization.Get("ui.bookui.exit_without_saving_unsaved_progress_will_be");
            _view.confirmYes.interactable = true;
            _view.confirmation.SetActive(true);
        }

        private void ConfirmQuit()
        {
            if (_saveBeforeQuit && !_game.TrySaveNow())
            {
                _view.confirmationText.text = Localization.Get("ui.bookui.could_not_save_cancel_and_try_again");
                _view.confirmYes.interactable = false;
                return;
            }
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_detailAnimation != null) StopCoroutine(_detailAnimation);
            if (_game != null)
            {
                _game.Inventory.OnChanged -= RefreshStatus;
                _game.OnStatsChanged -= RefreshStatus;
                _game.OnMoneyChanged -= RefreshStatus;
            }
            foreach (var remove in _removeListeners) remove();
            if (_view != null)
            {
                _view.controller = null;
                _view.gameObject.SetActive(false);
            }
            RestoreHuds();
            _open = _closing = false;
            _ui?.SyncPaused();
        }
    }
}
