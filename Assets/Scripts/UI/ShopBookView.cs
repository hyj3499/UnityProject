using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>씬에 저장되는 상점 UI. 레이아웃/템플릿은 Inspector에서, 상품은 ShopDatabase에서 편집한다.</summary>
    public sealed class ShopBookView : MonoBehaviour
    {
        public bool configured;
        public RectTransform stage, book, detailPaper;
        public Image background;
        public Sprite idle;
        public Sprite[] opening, closing, flipLeft, flipRight;
        [Range(6, 30)] public float framesPerSecond = 12;
        [Min(.1f)] public float paperDropSeconds = .38f;
        public CanvasGroup pages;
        public Text shopTitle, money, productName, productPrice, description, message, emptyList;
        public Image productIcon;
        public Button buy, close;
        public GameObject quantityControls;
        public Button decrease, increase, cancelQuantity;
        public Text quantity;
        [HideInInspector] public int layoutVersion;
        public ScrollRect productsScroll, descriptionScroll, categoriesScroll;
        public RectTransform products, categories;
        public ShopProductRow rowTemplate;
        public Button categoryTemplate;
        public GameObject inventory;
        public RectTransform inventoryTooltip;
        public Text inventoryTooltipText;
        [Tooltip("책의 기본 중심. 인벤토리가 2~3줄일 때 겹치지 않게 조금 위로 이동합니다.")]
        public Vector2 bookCenter;
        [Header("Editor preview only")]
        public GameObject productPreview, categoryPreview;
        public BookMenuView.Slot[] slots;
        [NonSerialized] public ShopBookController controller;

        public void Close() => controller?.Close();
        public void OpenShop(string shopId) => UIManager.Instance?.OpenShop(shopId);

        private void LateUpdate() => Fit();
        public void Fit()
        {
            if (stage == null) return;
            var size = ((RectTransform)transform).rect.size;
            float scale = Mathf.Min(size.x / 850f, size.y / 620f);
            stage.localScale = new Vector3(scale, scale, 1);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HidePreviews()
        {
            foreach (var view in FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                view.gameObject.SetActive(false);
        }
    }
}
