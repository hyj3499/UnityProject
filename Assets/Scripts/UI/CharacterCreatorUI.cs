using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 새 게임을 시작할 때 뜨는 캐릭터 만들기 화면.
    ///
    /// 왼쪽에 살아 움직이는 미리보기(서 있는 자세를 네 방향으로 돌려 볼 수 있다), 오른쪽에
    /// 항목별 &lt; 현재 값 &gt; 줄이 놓인다. 항목과 선택지는 AppearanceCatalog가 들고 있으므로
    /// 머리 모양이나 옷을 추가해도 이 파일은 손대지 않는다.
    ///
    /// 미리보기는 게임과 <b>같은 PlayerAnimator</b>를 쓴다 — 화면 전용 그리기 코드를 따로 두면
    /// 실제 모습과 어긋나기 때문에, 카메라 앞에 진짜 캐릭터를 하나 세워 두고 보여 준다.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(0.96f, 0.92f, 0.84f);
        private static readonly Color AccentColor = new Color(1f, 0.86f, 0.45f);

        private Font _font;
        private Canvas _canvas;
        private Action<PlayerAppearance> _onDone;
        private Action _onCancel;

        private PlayerAppearance _appearance = new PlayerAppearance();

        private GameObject _previewGo;
        private PlayerAnimator _preview;
        private Camera _previewCam;
        private RenderTexture _previewTexture;
        private RawImage _previewImage;
        private Direction _previewFacing = Direction.Down;

        /// <summary>한 줄짜리 선택 항목 — 무엇을 고르는지와, 고른 값을 어디에 넣을지.</summary>
        private class Row
        {
            public string label;
            public AppearanceOption[] options;
            public Func<PlayerAppearance, string> get;
            public Action<PlayerAppearance, string> set;
            public Text valueText;
        }

        private readonly List<Row> _rows = new List<Row>();

        /// <summary>
        /// 화면을 만들어 띄운다. 만들다 실패하면 <b>null을 돌려준다</b> — 부르는 쪽이 타이틀로
        /// 되돌려야 아무것도 못 하는 빈 화면에 갇히지 않는다.
        /// </summary>
        public static CharacterCreatorUI Create(Font font, PlayerAppearance start,
                                                Action<PlayerAppearance> onDone, Action onCancel)
        {
            var go = new GameObject("CharacterCreator");
            var ui = go.AddComponent<CharacterCreatorUI>();
            try
            {
                ui.Boot(font, start, onDone, onCancel);
                Debug.Log("[캐릭터 만들기] 화면을 띄웠습니다.");
                return ui;
            }
            catch (Exception e)
            {
                Debug.LogError($"[캐릭터 만들기] 화면을 만들지 못했습니다: {e}");
                Destroy(go);
                return null;
            }
        }

        private void Boot(Font font, PlayerAppearance start,
                          Action<PlayerAppearance> onDone, Action onCancel)
        {
            _font = font;
            _appearance = start != null ? start.Clone() : new PlayerAppearance();
            _onDone = onDone;
            _onCancel = onCancel;

            BuildRows();
            BuildCanvas();
            BuildPreview();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_previewGo != null) Destroy(_previewGo);
            if (_previewCam != null) Destroy(_previewCam.gameObject);
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_previewTexture != null) { _previewTexture.Release(); Destroy(_previewTexture); }
        }

        // ---------- 항목 ----------
        private void BuildRows()
        {
            void Add(string label, AppearanceOption[] options,
                     Func<PlayerAppearance, string> get, Action<PlayerAppearance, string> set)
                => _rows.Add(new Row { label = label, options = options, get = get, set = set });

            Add("피부", AppearanceCatalog.Skins, a => a.skin, (a, v) => a.skin = v);
            Add("눈매", AppearanceCatalog.EyeSets, a => a.eyeSet, (a, v) => a.eyeSet = v);
            Add("눈 색", AppearanceCatalog.EyeColors, a => a.eyeColor, (a, v) => a.eyeColor = v);
            Add("머리", AppearanceCatalog.HairStyles, a => a.hairStyle, (a, v) => a.hairStyle = v);
            Add("머리색", AppearanceCatalog.HairColors, a => a.hairColor, (a, v) => a.hairColor = v);
            Add("옷", AppearanceCatalog.Clothes, a => a.clothes, (a, v) => a.clothes = v);
            Add("장식", AppearanceCatalog.Accessories, a => a.acc, (a, v) => a.acc = v);
        }

        /// <summary>한 항목을 앞뒤로 넘긴다 (목록 끝에서 반대쪽으로 돌아간다).</summary>
        private void Step(Row row, int delta)
        {
            int i = AppearanceCatalog.IndexOf(row.options, row.get(_appearance));
            int n = row.options.Length;
            i = ((i + delta) % n + n) % n;
            row.set(_appearance, row.options[i].value);

            row.valueText.text = row.options[i].label;
            _preview?.SetAppearance(_appearance);
        }

        private void RefreshAll()
        {
            foreach (var row in _rows)
            {
                int i = AppearanceCatalog.IndexOf(row.options, row.get(_appearance));
                row.valueText.text = row.options[i].label;
            }
            _preview?.SetAppearance(_appearance);
        }

        /// <summary>고를 때마다 무작위로 한 벌 뽑아 준다 — 처음 고르기가 막막할 때 쓴다.</summary>
        private void Randomize()
        {
            foreach (var row in _rows)
                row.set(_appearance, row.options[UnityEngine.Random.Range(0, row.options.Length)].value);
            RefreshAll();
        }

        // ---------- 미리보기 ----------
        /// <summary>
        /// 실제 캐릭터를 화면 밖 빈자리에 세워 두고 전용 카메라로 찍어 RenderTexture에 담은 뒤,
        /// 그것을 화면 왼쪽에 붙인다. UI 이미지로 흉내 내지 않고 게임과 <b>같은 PlayerAnimator</b>를
        /// 쓰기 때문에, 여기서 보이는 모습이 곧 게임에서의 모습이다.
        ///
        /// (카메라를 화면에 바로 비추지 않는 이유: 이 화면의 캔버스가 ScreenSpaceOverlay라
        /// 카메라가 그린 것 위에 덮여 버린다. 찍어서 붙이면 순서 문제가 없다.)
        /// </summary>
        private void BuildPreview()
        {
            _previewTexture = new RenderTexture(240, 300, 16) { filterMode = FilterMode.Point };
            _previewTexture.Create();

            var camGo = new GameObject("PreviewCamera");
            _previewCam = camGo.AddComponent<Camera>();
            _previewCam.orthographic = true;
            _previewCam.orthographicSize = 1.5f;
            _previewCam.backgroundColor = new Color(0.13f, 0.17f, 0.15f);
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.targetTexture = _previewTexture;
            // 세워 둔 캐릭터가 세상 어디와도 겹치지 않도록 멀리 떨어뜨려 둔다.
            camGo.transform.position = new Vector3(500f, 500f, -10f);

            _previewGo = new GameObject("PreviewCharacter");
            _previewGo.transform.position = new Vector3(500f, 499.7f, 0f);
            _preview = _previewGo.AddComponent<PlayerAnimator>();
            _preview.Init(_appearance);
            _preview.SetSortingOrder(0);
            _preview.SetLocomotion(false, false, false, _previewFacing);

            var go = new GameObject("Preview");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.23f, 0.52f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240, 300);
            _previewImage = go.AddComponent<RawImage>();
            _previewImage.texture = _previewTexture;
        }

        private void TurnPreview(int delta)
        {
            // 보여 주는 순서: 앞 → 오른쪽 → 뒤 → 왼쪽 (제자리에서 한 바퀴 도는 느낌)
            var order = new[] { Direction.Down, Direction.Right, Direction.Up, Direction.Left };
            int i = System.Array.IndexOf(order, _previewFacing);
            if (i < 0) i = 0;
            int n = order.Length;
            _previewFacing = order[((i + delta) % n + n) % n];
            _preview?.SetLocomotion(false, false, false, _previewFacing);
        }

        // ---------- 화면 ----------
        private void BuildCanvas()
        {
            var go = new GameObject("CharacterCreatorCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = UILayers.CharacterCreator;   // 타이틀 화면 위에 뜬다
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            go.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(_canvas.transform, false);
            bg.color = new Color(0.10f, 0.14f, 0.12f, 1f);
            Stretch(bg.rectTransform);

            var title = Label(_canvas.transform as RectTransform, "캐릭터 만들기", 30, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.offsetMin = new Vector2(0, -70);
            title.rectTransform.offsetMax = new Vector2(0, -18);
            title.color = AccentColor;

            BuildTurnButtons();
            BuildRowList();
            BuildFooter();
        }

        private void BuildTurnButtons()
        {
            var hint = Label(_canvas.transform as RectTransform, "↻ 방향 돌려보기", 15, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.23f, 0.16f);
            hint.rectTransform.sizeDelta = new Vector2(220, 24);
            hint.color = new Color(0.75f, 0.72f, 0.68f);

            MakeButton(_canvas.transform as RectTransform, "◀", new Vector2(0.14f, 0.11f),
                       new Vector2(52, 36), () => TurnPreview(-1));
            MakeButton(_canvas.transform as RectTransform, "▶", new Vector2(0.32f, 0.11f),
                       new Vector2(52, 36), () => TurnPreview(1));
        }

        private void BuildRowList()
        {
            const float rowH = 46f;
            float top = -96f;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];

                var holder = new GameObject("Row_" + row.label);
                var rt = holder.AddComponent<RectTransform>();
                rt.SetParent(_canvas.transform, false);
                rt.anchorMin = new Vector2(0.44f, 1f);
                rt.anchorMax = new Vector2(0.96f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0, top - rowH);
                rt.offsetMax = new Vector2(0, top);
                top -= rowH + 6f;

                var name = Label(rt, row.label, 18, TextAnchor.MiddleLeft);
                name.rectTransform.anchorMin = new Vector2(0, 0);
                name.rectTransform.anchorMax = new Vector2(0.32f, 1);
                name.rectTransform.offsetMin = new Vector2(8, 0);
                name.rectTransform.offsetMax = Vector2.zero;
                name.color = TextColor;

                var captured = row;
                MakeButtonIn(rt, "◀", new Vector2(0.34f, 0.5f), new Vector2(38, 32),
                             () => Step(captured, -1));

                row.valueText = Label(rt, "", 17, TextAnchor.MiddleCenter);
                row.valueText.rectTransform.anchorMin = new Vector2(0.40f, 0);
                row.valueText.rectTransform.anchorMax = new Vector2(0.86f, 1);
                row.valueText.rectTransform.offsetMin = Vector2.zero;
                row.valueText.rectTransform.offsetMax = Vector2.zero;
                row.valueText.color = AccentColor;

                MakeButtonIn(rt, "▶", new Vector2(0.92f, 0.5f), new Vector2(38, 32),
                             () => Step(captured, 1));
            }
        }

        private void BuildFooter()
        {
            MakeButton(_canvas.transform as RectTransform, "무작위", new Vector2(0.56f, 0.11f),
                       new Vector2(140, 44), Randomize);

            MakeButton(_canvas.transform as RectTransform, "이 모습으로 시작", new Vector2(0.76f, 0.11f),
                       new Vector2(200, 44), () =>
                       {
                           var result = _appearance.Clone();
                           var done = _onDone;
                           Destroy(gameObject);
                           done?.Invoke(result);
                       });

            MakeButton(_canvas.transform as RectTransform, "뒤로", new Vector2(0.09f, 0.93f),
                       new Vector2(110, 38), () =>
                       {
                           var cancel = _onCancel;
                           Destroy(gameObject);
                           cancel?.Invoke();
                       });
        }

        // ---------- 작은 위젯들 ----------
        private Button MakeButton(RectTransform parent, string text, Vector2 anchor, Vector2 size,
                                  UnityEngine.Events.UnityAction onClick)
        {
            var btn = MakeButtonIn(parent, text, anchor, size, onClick);
            return btn;
        }

        private Button MakeButtonIn(RectTransform parent, string text, Vector2 anchor, Vector2 size,
                                    UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + text);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.26f, 0.23f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var label = Label(rt, text, 17, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.color = TextColor;
            return btn;
        }

        private Text Label(RectTransform parent, string text, int size, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(200, 30);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
