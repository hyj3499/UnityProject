using System;
using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>상점별 판매 설정. UI 참조 없이 C#에서 작성한다.</summary>
    public sealed class ShopDefinition
    {
        public string id;
        public string title;
        public bool allowSelling = true;
        // 예전 상점 정의와 소스 호환. 이제 인벤토리 표시와 판매 기능은 같은 설정이다.
        public bool showInventory { get => allowSelling; set => allowSelling = value; }
        public bool useCategories = true;
        public readonly List<ShopProduct> products = new List<ShopProduct>();

        public ShopDefinition(string id, string title) { this.id = id; this.title = title; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("상점 id와 title을 입력하세요.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in products)
            {
                if (product == null || string.IsNullOrWhiteSpace(product.id) || !ids.Add(product.id))
                    throw new ArgumentException(id + ": 상품 id는 비어 있지 않고 상점 안에서 고유해야 합니다.");
                if (product.price < 0 || product.backpackLevel < 0 || product.backpackLevel > GameManager.BackpackPrices.Length)
                    throw new ArgumentException(id + ": 잘못된 가격 또는 배낭 단계: " + product.id);
                if (product.backpackLevel == 0 && product.Def == null)
                    throw new ArgumentException(id + ": ItemDatabase에 없는 itemId: " + product.itemId);
                if (product.backpackLevel > 0 && product.price != GameManager.BackpackPrices[product.backpackLevel - 1])
                    throw new ArgumentException("배낭 가격은 GameManager.BackpackPrices와 같아야 합니다.");
            }
        }
    }

    public sealed class ShopProduct
    {
        public string id;
        public string itemId;
        public int price;
        public string category;
        public string displayName;
        public string description;
        public string note;
        public int backpackLevel; // 0: 일반 상품, 1 이상: 해당 단계 배낭 확장
        public bool allowQuantity = true; // false: 한 거래에 하나씩 구매. 배낭 확장은 항상 단일 구매.
        public bool SupportsQuantity => backpackLevel == 0 && allowQuantity;
        internal string buybackId, buybackShopId;
        public bool IsBuyback => !string.IsNullOrEmpty(buybackId);
        public ItemDef Def => string.IsNullOrWhiteSpace(itemId) ? null : ItemDatabase.Get(itemId);
        public string Name => !string.IsNullOrWhiteSpace(displayName) ? displayName : Def?.displayName ?? id;
        // 공통 설명은 ItemDef, 상점 전용 설명은 description으로 덮어쓴다.
        public string Description => !string.IsNullOrWhiteSpace(description) ? description : Def?.Description ?? "";
    }
}
