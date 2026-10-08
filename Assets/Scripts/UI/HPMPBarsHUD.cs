using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 씬(Canvas/HUD/HPMPBars)에 만들어 둔 MP 바를 채운다.
    /// 배치/크기는 전부 유니티 에디터에서 만진다 — 이 스크립트는 MP 바의 채움 양만 바꾼다.
    ///   MPBar/Fill → MP 비율만큼 왼쪽부터 채워지는 바
    /// 자식 오브젝트는 컴포넌트 우클릭 메뉴 "바 생성"으로 만들 수 있고, 만들어 두지 않았으면 실행 시 자동 생성된다.
    /// 게임(GameManager)은 타이틀에서 시작을 누른 뒤에야 생기므로, 없는 동안은 숨긴다.
    /// </summary>
    public class HPMPBarsHUD : MonoBehaviour
    {
        private const string SpriteFolder = "Sprites/UI/Bars/";

        [Header("MP (바)")]
        [Tooltip("Image Type이 Filled(Horizontal)인 채움 이미지. MP 비율이 Fill Amount로 들어간다.")]
        [SerializeField] private Image mpFill;

        [Header("처음 만들 때의 배치")]
        [Tooltip("자식이 하나도 없을 때 \"바 생성\"이 HPMPBars에 주는 배율 (스프라이트 1픽셀 = 화면 N픽셀).")]
        [SerializeField] private float pixelScale = 3f;
        [Tooltip("화면 좌측 상단에서 떨어진 거리.")]
        [SerializeField] private Vector2 screenMargin = new Vector2(16f, 16f);

        private GameManager _game;
        private CanvasGroup _group;
        private MPLiquidBar _mpLiquid;

        /// <summary>씬의 "HPMPBars" 오브젝트에 컴포넌트를 붙여 두지 않았어도 자동으로 붙인다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToScene()
        {
            if (FindObjectOfType<HPMPBarsHUD>(true) != null) return;
            var root = FindOrCreateRoot();
            if (root != null) root.gameObject.AddComponent<HPMPBarsHUD>();
        }

        /// <summary>씬의 "HPMPBars"를 찾고, 없으면 Canvas/HUD 아래에 새로 만든다.</summary>
        private static RectTransform FindOrCreateRoot()
        {
            RectTransform hud = null;
            foreach (var canvas in FindObjectsOfType<Canvas>(true))
                foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "HPMPBars") return rt;
                    if (hud == null && rt.name == "HUD") hud = rt;
                }
            if (hud == null) return null;

            var go = new GameObject("HPMPBars", typeof(RectTransform));
            go.layer = hud.gameObject.layer;
            go.transform.SetParent(hud, false);
            return (RectTransform)go.transform;
        }

#if UNITY_EDITOR
        /// <summary>플레이하지 않고도 씬에 MP 바를 만들어 두고 위치를 만질 수 있게 한다.</summary>
        [UnityEditor.MenuItem("Farm MVP/UI/HP·MP 바 만들기")]
        private static void CreateInScene()
        {
            var root = FindOrCreateRoot();
            if (root == null) { Debug.LogWarning("[HPMPBarsHUD] Canvas/HUD를 찾지 못했습니다."); return; }

            var hud = root.GetComponent<HPMPBarsHUD>();
            if (hud == null) hud = root.gameObject.AddComponent<HPMPBarsHUD>();
            hud.Build(); // 이미 있는 자식은 건드리지 않는다

            root.gameObject.SetActive(true);
            UnityEditor.EditorUtility.SetDirty(hud);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            UnityEditor.Selection.activeGameObject = root.gameObject;
        }
#endif

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
                if (found != null && found.Data != null) Bind(found);
            }
        }

        // ---------- 연결 ----------
        private void Bind(GameManager game)
        {
            _game = game;
            _game.OnStatsChanged += Refresh;
            if (_mpLiquid != null)
                _mpLiquid.SetMp(game.Data.farmer.mp, game.Data.farmer.maxMp, true);
            Refresh();
            SetVisible(true);
        }

        private void Unbind()
        {
            if (_game == null) return;
            _game.OnStatsChanged -= Refresh;
            _game = null;
        }

        private void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = false; // 보기만 하는 HUD — 클릭을 가로채지 않는다
        }

        // ---------- 갱신 ----------
        private void Refresh()
        {
            if (_game == null || _game.Data == null) return;
            var f = _game.Data.farmer;

            if (_mpLiquid != null)
                _mpLiquid.SetMp(f.mp, f.maxMp);
            else if (mpFill != null)
                mpFill.fillAmount = f.maxMp > 0 ? Mathf.Clamp01((float)f.mp / f.maxMp) : 0f;
        }

        // ---------- 에디터에서 쓸 오브젝트 만들기 ----------
        private void Reset() => ResolveReferences();

        private void ResolveReferences()
        {
            if (mpFill == null)
            {
                var fill = transform.Find("MPBar/Fill");
                if (fill != null) mpFill = fill.GetComponent<Image>();
            }

            if (mpFill == null) Build();
            ResolveMpLiquid();
        }

        private void ResolveMpLiquid()
        {
            var bar = transform.Find("MPBar");
            if (bar == null) return;
            _mpLiquid = bar.GetComponent<MPLiquidBar>();
            if (_mpLiquid == null) _mpLiquid = bar.gameObject.AddComponent<MPLiquidBar>();
            _mpLiquid.Configure();
        }

        // 스프라이트 모드가 Multiple이라 시트 안의 첫 조각을 꺼낸다.
        private static Sprite LoadSprite(string sheet)
        {
            var all = Resources.LoadAll<Sprite>(SpriteFolder + sheet);
            if (all.Length == 0) all = Resources.LoadAll<Sprite>(SpriteFolder + "백업/" + sheet);
            return all.Length > 0 ? all[0] : null;
        }

        [ContextMenu("바 생성")]
        private void Build()
        {
            var root = (RectTransform)transform;

            // 이미 만들어 둔 것은 위치를 유지한 채 재사용한다 (에디터에서 옮긴 값을 덮어쓰지 않도록).
            if (root.childCount == 0)
            {
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
                root.anchoredPosition = new Vector2(screenMargin.x, -screenMargin.y);
                root.sizeDelta = new Vector2(114f, 34f);
                root.localScale = new Vector3(pixelScale, pixelScale, 1f);
            }

            // MP 바 (배경 → 채움 → 테두리 순으로 겹친다)
            var bar = root.Find("MPBar") as RectTransform;
            if (bar == null)
            {
                bar = NewRect("MPBar", root, Vector2.zero, new Vector2(96f, 16f));
                NewBarLayer("Background", bar, LoadSprite("mp_background_96x16"));
                var fill = NewBarLayer("Fill", bar, LoadSprite("mp_fill_96x16"));
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 1f;
                NewBarLayer("Frame", bar, LoadSprite("mp_frame_96x16"));
            }
            if (mpFill == null)
            {
                var fill = bar.Find("Fill");
                if (fill != null) mpFill = fill.GetComponent<Image>();
            }
            ResolveMpLiquid();
        }

        /// <summary>부모의 좌측 상단을 기준으로 놓는 빈 칸.</summary>
        private static RectTransform NewRect(string name, RectTransform parent, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// 바의 한 겹. 배경·채움·테두리는 같은 96x16 그림에서 잘라 낸 조각이라,
        /// 원본 그림 안에서의 위치(sprite.rect)에 그대로 놓으면 서로 정확히 맞물린다.
        /// </summary>
        private static Image NewBarLayer(string name, RectTransform bar, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            if (sprite != null)
            {
                rt.anchoredPosition = sprite.rect.position;
                rt.sizeDelta = sprite.rect.size;
            }
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }
    }
}
