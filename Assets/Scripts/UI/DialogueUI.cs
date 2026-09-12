using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// NPC 대화창. 대사를 한 줄씩 넘기다가 선택지가 있으면 버튼으로 고르게 한다.
    /// 어떤 대사를 보여줄지는 전혀 모르고, 받은 내용을 표시하는 일만 한다.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        private const float PanelW = 720f;
        private const float PanelH = 230f;
        private const float Pad = 20f;
        private const float PortraitSize = 140f;
        private const float ChoiceH = 30f;
        private const float ChoiceGap = 6f;

        // 9-slice 프레임을 아트 3배 크기로 그리기 위한 보정값
        private const float SlicedPpu = 100f / (16f * 3f);
        private static readonly Color InkColor = new Color(0.24f, 0.13f, 0.11f);

        private UIManager _ui;

        private RectTransform _root;
        private RectTransform _panel;
        private Image _portrait;
        private Text _nameText, _bodyText, _nextHint;
        private RectTransform _choiceRoot;
        private readonly List<GameObject> _choiceButtons = new List<GameObject>();

        private string _npcId;
        private string[] _lines;
        private int _lineIndex;
        private DialogueChoice[] _choices;
        private Action<DialogueChoice> _onChoice;
        private Action _onFinished;
        private bool _awaitingChoice;
        private int _openedFrame = -1;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Boot(UIManager ui, RectTransform canvas)
        {
            _ui = ui;
            Build(canvas);
            _root.gameObject.SetActive(false);
        }

        /// <summary>대사를 보여준다. 선택지가 있으면 마지막 줄 뒤에 버튼이 뜬다.</summary>
        public void Show(string npcId, string displayName, int emotion, string[] lines,
            DialogueChoice[] choices, Action<DialogueChoice> onChoice, Action onFinished)
        {
            _npcId = npcId;
            _lines = lines != null && lines.Length > 0 ? lines : new[] { "..." };
            _lineIndex = 0;
            _choices = choices != null && choices.Length > 0 ? choices : null;
            _panel.sizeDelta = new Vector2(PanelW, Mathf.Max(PanelH,
                132f + (_choices == null ? 0 : _choices.Length * (ChoiceH + ChoiceGap)) + Pad));
            _onChoice = onChoice;
            _onFinished = onFinished;
            _awaitingChoice = false;
            // 창을 연 그 클릭이 곧바로 첫 줄을 넘겨 버리지 않도록 한 프레임 무시한다.
            _openedFrame = Time.frameCount;

            _nameText.text = displayName;
            SetEmotion(emotion);
            ClearChoices();

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            ShowCurrentLine();
        }

        /// <summary>대사 도중 표정만 바꾼다 (선택지 반응 등).</summary>
        public void SetEmotion(int emotion)
        {
            _portrait.sprite = NpcDatabase.Portrait(_npcId, emotion);
            _portrait.enabled = _portrait.sprite != null;
        }

        public void Close()
        {
            _onChoice = null;
            _onFinished = null;
            ClearChoices();
            _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen || _awaitingChoice || Time.frameCount == _openedFrame) return;

            bool advance = Input.GetMouseButtonDown(0)
                           || Input.GetKeyDown(KeyCode.Space)
                           || Input.GetKeyDown(KeyCode.Return);
            if (advance) Advance();
        }

        private void Advance()
        {
            _lineIndex++;
            if (_lineIndex < _lines.Length)
            {
                ShowCurrentLine();
                return;
            }

            if (_choices != null)
            {
                BuildChoices();
                return;
            }

            var finished = _onFinished;
            _onFinished = null;
            Close();
            finished?.Invoke();
        }

        private void ShowCurrentLine()
        {
            _bodyText.text = _lines[_lineIndex];
            bool last = _lineIndex >= _lines.Length - 1;
            _nextHint.enabled = !(last && _choices != null);
        }

        // ---------- 선택지 ----------
        private void BuildChoices()
        {
            _awaitingChoice = true;
            _nextHint.enabled = false;

            for (int i = 0; i < _choices.Length; i++)
            {
                var choice = _choices[i];
                var go = new GameObject("Choice_" + i);
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(_choiceRoot, false);
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(0, -(i + 1) * ChoiceH - i * ChoiceGap);
                rt.offsetMax = new Vector2(0, -i * (ChoiceH + ChoiceGap));

                var img = go.AddComponent<Image>();
                img.sprite = AssetLibrary.UiFrame;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = SlicedPpu;

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                var captured = choice;
                btn.onClick.AddListener(() => OnChoicePicked(captured));

                var label = _ui.Label(rt, choice.text, 16, Vector2.zero, TextAnchor.MiddleCenter);
                _ui.Stretch(label.rectTransform, 0, 0, 10, 10);

                _choiceButtons.Add(go);
            }
        }

        private void OnChoicePicked(DialogueChoice choice)
        {
            ClearChoices();
            _awaitingChoice = false;
            _choices = null;

            var handler = _onChoice;
            _onChoice = null;
            handler?.Invoke(choice);
        }

        private void ClearChoices()
        {
            foreach (var go in _choiceButtons) Destroy(go);
            _choiceButtons.Clear();
        }

        // ---------- 뼈대 ----------
        private void Build(RectTransform canvas)
        {
            var rootGo = new GameObject("DialogueRoot");
            _root = rootGo.AddComponent<RectTransform>();
            _root.SetParent(canvas, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var panelGo = new GameObject("Panel");
            var panel = panelGo.AddComponent<RectTransform>();
            _panel = panel;
            panel.SetParent(_root, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0, 24);
            panel.sizeDelta = new Vector2(PanelW, PanelH);

            var bg = panelGo.AddComponent<Image>();
            bg.sprite = AssetLibrary.UiDialoguePanel;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = SlicedPpu;

            // 초상화 (왼쪽)
            var portraitGo = new GameObject("Portrait");
            var prt = portraitGo.AddComponent<RectTransform>();
            prt.SetParent(panel, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0, 1);
            prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(Pad, -Pad);
            prt.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            _portrait = portraitGo.AddComponent<Image>();
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;

            float textLeft = Pad + PortraitSize + 16f;

            _nameText = _ui.Label(panel, "", 20, Vector2.zero, TextAnchor.MiddleLeft);
            Anchor(_nameText.rectTransform, textLeft, Pad, 18f, 28f);
            _nameText.color = new Color(0.45f, 0.24f, 0.14f);

            _bodyText = _ui.Label(panel, "", 18, Vector2.zero, TextAnchor.UpperLeft);
            Anchor(_bodyText.rectTransform, textLeft, Pad, 52f, 74f);
            _bodyText.color = InkColor;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Truncate;

            _choiceRoot = new GameObject("Choices").AddComponent<RectTransform>();
            _choiceRoot.SetParent(panel, false);
            Anchor(_choiceRoot, textLeft, Pad, 132f, PanelH - 132f - Pad);

            _nextHint = _ui.Label(panel, "▼ 클릭 / Space", 14, Vector2.zero, TextAnchor.LowerRight);
            Anchor(_nextHint.rectTransform, textLeft, Pad, PanelH - Pad - 22f, 22f);
            _nextHint.color = new Color(0.6f, 0.45f, 0.35f);
        }

        /// <summary>패널 왼쪽 위 기준으로 자식을 배치한다 (좌/우 여백 + 위에서부터의 거리와 높이).</summary>
        private static void Anchor(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
