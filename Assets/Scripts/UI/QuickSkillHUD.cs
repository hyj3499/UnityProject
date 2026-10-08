using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 씬(Canvas/HUD/QuickSkill)의 장착 마법 아이콘과 Ctrl 홀드 원형 퀵스킬 선택 메뉴를 관리한다.
    /// 씬에 만든 프레임/키 그림은 유지하고, 이 스크립트는 아이콘과 원형 메뉴만 채운다.
    /// </summary>
    public class QuickSkillHUD : MonoBehaviour
    {
        private const string SpriteFolder = "Sprites/UI/QuickSkills/";
        private const float HoldSeconds = 1f;
        private const float IdleSeconds = 10f;
        private const float Radius = 92f;
        private const float MinClickDistance = 28f;
        private const float HighlightFrameSeconds = 0.16f;

        private static QuickSkillHUD _instance;
        public static bool IsRadialMenuOpen => _instance != null && _instance._radialOpen;

        private GameManager _game;
        private CanvasGroup _group;
        private readonly List<Image> _equippedIcons = new List<Image>();
        private Sprite[] _icons;
        private Sprite[] _indexFrames;
        private Sprite[] _highlightFrames;
        private RectTransform _hudRoot;
        private RectTransform _radialRoot;
        private Image[] _radialFrames;
        private Vector2[] _radialPositions;
        private Image _radialHighlight;
        private float _ctrlHeldSeconds;
        private float _idleSeconds;
        private float _highlightFrameTimer;
        private int _hoveredSkillIndex = -1;
        private bool _radialOpen;

        /// <summary>씬의 QuickSkill 패널에 컴포넌트를 자동 연결한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToScene()
        {
            if (FindObjectOfType<QuickSkillHUD>(true) != null) return;

            foreach (var canvas in FindObjectsOfType<Canvas>(true))
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "QuickSkill")
                {
                    rect.gameObject.AddComponent<QuickSkillHUD>();
                    return;
                }
        }

        private void Awake()
        {
            _instance = this;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _hudRoot = transform.parent as RectTransform;
            FindEquippedIcons();
            LoadSprites();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_game != null) _game.OnMagicChanged -= Refresh;
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null)
            {
                if (_group.alpha > 0f) SetVisible(false);
                var found = FindObjectOfType<GameManager>();
                if (found != null && found.Data != null) Bind(found);
                return;
            }

            if (!_radialOpen) return;

            // 메뉴는 화면의 한 지점이 아니라 플레이어 월드 위치에 붙는다.
            PositionRadialAtPlayer();
            RefreshRadialFocus();

            // Ctrl을 계속 누르고 있거나 이동/조작 중이면 메뉴를 유지한다.
            if (AnyActivityThisFrame()) _idleSeconds = 0f;
            else _idleSeconds += Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.Escape) || _idleSeconds >= IdleSeconds)
                HideRadial();
        }

        private void Bind(GameManager game)
        {
            _game = game;
            _game.OnMagicChanged += Refresh;
            Refresh();
            SetVisible(true);
        }

        private void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = false;
        }

        private void Refresh()
        {
            if (_game == null) return;
            var icon = IconFor(_game.CurrentMagic);
            foreach (var equippedIcon in _equippedIcons)
            {
                if (equippedIcon == null) continue;
                equippedIcon.sprite = icon;
                equippedIcon.enabled = icon != null;
            }

            if (_radialFrames != null)
            {
                int selected = MagicSystem.QuickSkillIndex(_game.CurrentMagic);
                for (int i = 0; i < _radialFrames.Length; i++)
                    if (_radialFrames[i] != null)
                        _radialFrames[i].color = i == selected ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
            }
        }

        /// <summary>PlayerController가 매 프레임 호출한다. Ctrl 1초 홀드 및 숫자 1~7 선택을 처리한다.</summary>
        public static void HandleKeyboardInput(GameManager game)
        {
            if (_instance == null || _instance._game != game) return;
            _instance.HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!_radialOpen)
            {
                _ctrlHeldSeconds = ctrl ? _ctrlHeldSeconds + Time.unscaledDeltaTime : 0f;
                if (_ctrlHeldSeconds >= HoldSeconds) ShowRadial();
                return;
            }

            for (int i = 0; i < MagicSystem.Count; i++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha1 + i);
                if (!Input.GetKeyDown(key)) continue;
                Select(MagicSystem.QuickSkillAt(i));
                return;
            }
        }

        /// <summary>원형 메뉴가 열린 상태의 좌클릭을 가로채고, 마우스 방향의 마법을 고른다.</summary>
        public static bool ConsumeRadialClick(GameManager game)
        {
            if (_instance == null || _instance._game != game || !_instance._radialOpen || !Input.GetMouseButtonDown(0))
                return false;

            _instance.SelectByMouse();
            return true; // 메뉴가 열렸을 때의 좌클릭은 월드 마법으로 새지 않는다.
        }

        private void SelectByMouse()
        {
            int index = SkillIndexAtPointer();
            if (index >= 0) Select(MagicSystem.QuickSkillAt(index));
        }

        private void Select(MagicType magic)
        {
            _game.SelectMagic(magic);
            // 원형 선택은 즉시 현재 시전 마법에도 반영된다. 다음 저장 전에도 세이브 데이터가 같은 값을 가진다.
            if (_game.Data != null) _game.Data.farmer.currentMagic = (int)magic;
            HideRadial();
        }

        private void ShowRadial()
        {
            _ctrlHeldSeconds = 0f;
            _idleSeconds = 0f;
            BuildRadialIfNeeded();
            PositionRadialAtPlayer();
            _radialRoot.gameObject.SetActive(true);
            _radialOpen = true;
            RefreshRadialFocus();
        }

        private void HideRadial()
        {
            _ctrlHeldSeconds = 0f;
            _radialOpen = false;
            _hoveredSkillIndex = -1;
            if (_radialRoot != null) _radialRoot.gameObject.SetActive(false);
        }

        private void PositionRadialAtPlayer()
        {
            if (_hudRoot == null || _game?.Player == null) return;
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = Camera.main;
            var screen = camera != null ? camera.WorldToScreenPoint(_game.Player.transform.position) : Input.mousePosition;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, screen, eventCamera, out var local))
                _radialRoot.anchoredPosition = local;
        }

        private void BuildRadialIfNeeded()
        {
            if (_radialRoot != null) return;
            if (_hudRoot == null) _hudRoot = transform.parent as RectTransform;
            if (_hudRoot == null) return;

            var root = new GameObject("QuickSkillRadial", typeof(RectTransform));
            root.layer = gameObject.layer;
            _radialRoot = (RectTransform)root.transform;
            _radialRoot.SetParent(_hudRoot, false);
            _radialRoot.anchorMin = _radialRoot.anchorMax = new Vector2(.5f, .5f);
            _radialRoot.pivot = new Vector2(.5f, .5f);
            _radialRoot.sizeDelta = Vector2.zero;
            _radialRoot.gameObject.SetActive(false);

            _radialFrames = new Image[MagicSystem.Count];
            _radialPositions = new Vector2[MagicSystem.Count];
            for (int i = 0; i < MagicSystem.Count; i++)
            {
                float radians = -i * Mathf.PI * 2f / MagicSystem.Count + Mathf.PI * .5f;
                var position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Radius;
                _radialPositions[i] = position;
                _radialFrames[i] = CreateRadialSlot(i, position);
            }

            // 마우스가 가리키는 칸 하나에만 올라가는 두 프레임 애니메이션.
            _radialHighlight = NewImage("QuickSkillsFrameHighlight", _radialRoot, null);
            _radialHighlight.preserveAspect = false; // base frame·번호 프레임과 같은 52x52 영역을 정확히 덮는다.
            _radialHighlight.raycastTarget = false;
            _radialHighlight.rectTransform.anchorMin = _radialHighlight.rectTransform.anchorMax = new Vector2(.5f, .5f);
            _radialHighlight.rectTransform.pivot = new Vector2(.5f, .5f);
            _radialHighlight.rectTransform.sizeDelta = new Vector2(52f, 52f);
            _radialHighlight.gameObject.SetActive(false);
        }

        private Image CreateRadialSlot(int index, Vector2 position)
        {
            var slot = new GameObject("Skill_" + (index + 1), typeof(RectTransform));
            slot.layer = gameObject.layer;
            var rt = (RectTransform)slot.transform;
            rt.SetParent(_radialRoot, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(52f, 52f);

            var frame = slot.AddComponent<Image>();
            frame.sprite = LoadFirst("QuickSkillsFrame");
            frame.preserveAspect = false;
            frame.raycastTarget = false;

            var icon = NewImage("QuickSkillIcon", rt, IconFor(MagicSystem.QuickSkillAt(index)));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Stretch(icon.rectTransform, 9f);

            var number = NewImage("QuickSkillsFrameindex", rt, IndexFor(index));
            number.preserveAspect = false;
            number.raycastTarget = false;
            // Frameindex 스프라이트의 투명 여백까지 프레임과 같은 크기로 겹쳐야 숫자 테두리가 정확히 맞는다.
            Stretch(number.rectTransform, 0f);
            return frame;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void FindEquippedIcons()
        {
            _equippedIcons.Clear();

            // 사용자가 QuickSkill/QuickSkillsIcons 같은 별도 컨테이너를 만들어도 그 안의 Image를 모두 찾는다.
            var explicitRoot = transform.Find("QuickSkillsIcons") ?? transform.Find("QuickSkillIcon");
            if (explicitRoot != null)
                foreach (var image in explicitRoot.GetComponentsInChildren<Image>(true)) AddEquippedIcon(image);

            var named = transform.Find("Image");
            if (named != null) AddEquippedIcon(named.GetComponent<Image>());

            foreach (var image in GetComponentsInChildren<Image>(true))
                if (image != GetComponent<Image>()
                    && (image.gameObject.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
                        || image.gameObject.name.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0))
                    AddEquippedIcon(image);
        }

        private void AddEquippedIcon(Image image)
        {
            // 프레임 자체와 Q/E/Ctrl 키 안내 이미지는 절대 아이콘으로 바꾸지 않는다.
            if (image == null || image == GetComponent<Image>() || image.gameObject.name.EndsWith("_key", StringComparison.OrdinalIgnoreCase)
                || image.gameObject.name == "press_key" || _equippedIcons.Contains(image)) return;
            _equippedIcons.Add(image);
        }

        private void LoadSprites()
        {
            _icons = LoadNumbered("QuickSkillsIcons", MagicSystem.Count);
            _indexFrames = LoadNumbered("QuickSkillsFrameindex", MagicSystem.Count);
            _highlightFrames = BuildHighlightFrames();
        }

        private static Sprite[] LoadNumbered(string sheet, int count)
        {
            var result = new Sprite[count];
            foreach (var sprite in Resources.LoadAll<Sprite>(SpriteFolder + sheet))
            {
                int underscore = sprite.name.LastIndexOf('_');
                if (underscore < 0 || !int.TryParse(sprite.name.Substring(underscore + 1), out int index)) continue;
                if (index >= 0 && index < count) result[index] = sprite;
            }
            return result;
        }

        private Sprite IconFor(MagicType magic)
        {
            int index = MagicSystem.QuickSkillIndex(magic);
            return _icons != null && index >= 0 && index < _icons.Length ? _icons[index] : null;
        }

        private Sprite IndexFor(int skillIndex)
        {
            // Frameindex 시트의 0번은 숫자 1이므로 마법 순번 0~6에 그대로 대응한다.
            return _indexFrames != null && skillIndex >= 0 && skillIndex < _indexFrames.Length ? _indexFrames[skillIndex] : null;
        }

        private Sprite[] BuildHighlightFrames()
        {
            var texture = Resources.Load<Texture2D>(SpriteFolder + "QuickSkillsFramehighlight_animation");
            if (texture == null) return Array.Empty<Sprite>();

            // 원본은 96x48 시트에 32x32 프레임 두 장이 놓여 있다. 자동 슬라이스된 모서리 조각이 아니라
            // 각 32x32 영역을 하나의 스프라이트로 만들어야 프레임이 온전하게 애니메이션 된다.
            return new[]
            {
                Sprite.Create(texture, new Rect(8f, 8f, 32f, 32f), new Vector2(.5f, .5f), 16f),
                Sprite.Create(texture, new Rect(52f, 8f, 32f, 32f), new Vector2(.5f, .5f), 16f),
            };
        }

        private void RefreshRadialFocus()
        {
            if (_radialHighlight == null) return;

            int index = SkillIndexAtPointer();
            if (index < 0 || _highlightFrames == null || _highlightFrames.Length == 0)
            {
                _radialHighlight.gameObject.SetActive(false);
                _hoveredSkillIndex = -1;
                return;
            }

            if (index != _hoveredSkillIndex)
            {
                _hoveredSkillIndex = index;
                _highlightFrameTimer = 0f;
                _radialHighlight.rectTransform.anchoredPosition = _radialPositions[index];
                _radialHighlight.gameObject.SetActive(true);
            }

            _highlightFrameTimer += Time.unscaledDeltaTime;
            int frame = Mathf.FloorToInt(_highlightFrameTimer / HighlightFrameSeconds) % _highlightFrames.Length;
            _radialHighlight.sprite = _highlightFrames[frame];
        }

        private int SkillIndexAtPointer()
        {
            if (_radialRoot == null) return -1;
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_radialRoot, Input.mousePosition, camera, out var local)
                || local.magnitude < MinClickDistance) return -1;

            // 위쪽이 0번이고 시계방향으로 1~6번이다. Mathf.Repeat로 하단 왼쪽의 음수 각도도 올바르게 감싼다.
            float clockwiseFromUp = Mathf.Repeat(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg, 360f);
            float sector = 360f / MagicSystem.Count;
            return Mathf.FloorToInt(clockwiseFromUp / sector + .5f) % MagicSystem.Count;
        }

        private static Sprite LoadFirst(string sheet)
        {
            var all = Resources.LoadAll<Sprite>(SpriteFolder + sheet);
            return all != null && all.Length > 0 ? all[0] : null;
        }

        private static bool AnyActivityThisFrame()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                   || Input.anyKeyDown
                   || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                   || Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
        }
    }
}
