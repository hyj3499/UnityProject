/*
Settings Guide
=============
이 파일은 프로젝트 루트의 공용 설정 가이드 겸 C# 예제입니다. Assets 밖이므로 게임에 컴파일되지 않습니다.
상점뿐 아니라 MenuBookUI, ShopBookUi 및 월드 상호작용처럼 코드로 조정하는 기능의 위치를 기록합니다.

0. 공용 UI / 설정 위치
   Canvas/MenuBookUI: 인벤토리·설정 책. BookMenuView가 씬 UI 참조를 보관합니다.
   Canvas/ShopBookUi: 상점 책. ShopBookView가 상점 UI 참조를 보관합니다.
   MenuBookUI 편집 메뉴: Tools > Book UI > Build Interactive Menu Book
   ShopBookUi 편집 메뉴: Tools > Book UI > Make Shop Book Editable In Scene
   위 메뉴는 프리팹 연결 해제, 비활성 UI/부모 Canvas 켜기, 미리보기 복원, Scene 뷰 초점을 함께 처리합니다.
   이미 프리팹을 풀었어도 다시 실행하면 미리보기가 켜집니다. Play 모드를 종료하고 사용하세요.
   Canvas 아래의 자식 텍스트·이미지·RectTransform을 직접 수정한 뒤 씬을 저장합니다.
   설정 화면의 페이지/버튼 배치: Assets/Scripts/UI/BookMenuLayout.cs
   설정 화면 동작: Assets/Scripts/UI/BookUI.cs
   상점 화면 배치/동작: Assets/Scripts/UI/ShopBookLayout.cs, ShopBookController.cs

   월드 상점 그림: Assets/Resources/Sprites/UtilityObjects/test_shop.png
   Farm의 기존 상점은 실행할 때 test_shop 그림으로 자동 표시됩니다.
   새 상점은 Palette에 test_shop.png을 넣은 뒤 Fixed_Farm 타일맵에 바로 칠하면 됩니다.
   해당 칸 가까이에서 우클릭하면 기본 Farmer's shop을 엽니다. 마커 타일은 필요하지 않습니다.

1. ShopBookUi 사용 가이드
--------------------------
아래 예제를 사용할 때는 필요한 클래스를 Assets/Scripts/Items/에 복사하세요.

1. 기본 UI 편집
   Main 씬 > Canvas > ShopBookUi. 처음에는 다른 UI와 겹치지 않도록 비활성화되어 있습니다.
   활성화하면 기본 배치를 볼 수 있습니다. 플레이 시에는 자동으로 숨겨지고 상점을 열 때 나타납니다.
   DeskStage/Desk: Book Desk/5.png
   DeskStage/Book/Background: MenuBookUI와 동일한 Inventory Book 이미지와 열기/닫기 프레임
   DeskStage/Book/Pages/LeftPage: 상점 이름, 굵은 소지금, 상품 목록
   DeskStage/Book/Pages/CategoryBookmarks: 왼쪽 카테고리 책갈피 (많으면 휠/드래그)
   DeskStage/Book/Pages/DetailPaper: 위에서 내려오는 종이, 아이콘, 이름, 설명, 구매
   DeskStage/Inventory: 화면 아래 중앙 인벤토리 (아이템 호버 설명, 우클릭 판매)
   DeskStage/InventoryTooltip: 아이템 이름·개당 판매가·설명. 마우스 입력을 가리지 않습니다.
   ESC 닫기/현재 인벤토리 용량 라벨은 제거했습니다. ESC 키와 우측 닫기 버튼은 계속 동작합니다.
   책은 기본적으로 가운데에 놓고, 인벤토리가 2~3줄일 때만 겹치지 않게 조금 올립니다.
   기본 중심은 ShopBookView.bookCenter에서 조절합니다.
   ProductRowTemplate / CategoryTemplate: 실행 중 복제하는 디자인 원본. 비활성 상태가 정상입니다.
   EditorPreview: 배치 확인용 가짜 행/책갈피. 실행 시 숨겨지며 실제 판매 목록과 무관합니다.
   ShopBookView의 프레임 속도와 paperDropSeconds로 연출 속도를 조정할 수 있습니다.
   책갈피 변경: 방향에 맞게 페이지 넘기기 → 새 상품 목록 → 오른쪽 페이퍼 내려오기 순서입니다.
   DetailPaper/QuantityControls: Minus, QuantityBox/Value, Plus. CancelQuantity는 수량 초기화 버튼입니다.
   PlusMinus.png는 왼쪽부터 16×16씩 +, + 눌림, −, − 눌림으로 분리합니다.
   수량 버튼은 누르는 즉시 1회, 0.4초 후부터 0.075초마다 반복합니다. 최소 수량은 1입니다.
   인벤토리는 한 줄 10칸, 최대 3줄입니다. 잠긴 슬롯은 숨기고 배낭 확장 시 해당 줄을 표시합니다.
   DeskStage는 화면 크기에 맞춰 자동 축소/확대합니다. 책 위치는 내부 RectTransform에서 편집하세요.
   프리팹: Assets/Prefabs/UI/ShopBookUi.prefab (여러 씬에서 재사용 가능)
   생성 메뉴: Tools > Book UI > Build Interactive Shop Book
   생성 메뉴는 이미 configured인 UI를 다시 배치하지 않으므로 Inspector에서 꾸민 내용을 유지합니다.

2. 상점 데이터 작성 위치
   Assets/Scripts/Items/ShopDatabase.cs: 상점 ID 등록, 기본 잡화점과 재료점 예제
   Assets/Scripts/Items/ShopDefinition.cs: 상점/판매 상품 모델
   Assets/Scripts/Items/ShopPurchaseService.cs: 잔액/가방/배낭 확장 검증 및 구매
   Assets/Scripts/Items/ShopSellService.cs: 아이템 판매, 상점별 재구매 재고, 다음 월요일 만료
   Assets/Scripts/Core/GameData.cs: shopBuybacks에 재구매 기록 저장
   Assets/Scripts/UI/ShopBookController.cs: 데이터 바인딩, 선택, 애니메이션
   Assets/Scripts/UI/ShopBookView.cs: 씬의 UI 참조
   Assets/Scripts/UI/ShopBookLayout.cs: 기본 레이아웃 생성

   데이터 -> 구매 서비스 -> UI를 분리했습니다. 새 상점을 위해 UI 스크립트를 복제할 필요가 없습니다.
   기존 잡화점은 작물/풀/나무 데이터베이스에서 씨앗을 자동으로 가져오며 배낭 확장도 판매합니다.
   기존 상점 패널 ShopUI/ShopRoot와 옛 ShopBookUI 뼈대는 새 ShopBookUi로 교체했습니다.
   제작/배송 UI와 MenuBookUI는 독립된 기능으로 유지됩니다.

3. 필드 설명
   ShopDefinition.id: 상점 고유 ID. 예: "forest". Register의 ID와 동일해야 합니다.
   title: 화면에 그대로 표시할 이름. 예: "Mina's shop" 또는 "미나의 상점"
   allowSelling: true이면 인벤토리 판매·재구매와 인벤토리/판매 책갈피를 모두 표시합니다.
                 false이면 해당 기능과 인벤토리/판매 책갈피를 모두 숨깁니다.
   useCategories: 일반 상품의 카테고리 표시 설정. allowSelling=true일 때 전체/판매 책갈피는 항상 표시합니다.
   products: 이 상점에서 판매할 상품 목록. 작성 순서대로 표시됩니다.

   ShopProduct.id: 이 상점 안에서 고유한 판매 항목 ID. itemId와 달라도 됩니다.
   itemId: ItemDatabase에 등록된 실제 아이템 ID. 이름을 지어 쓰면 검증 오류가 납니다.
   price: 1개 가격. 0은 무료, 음수는 금지
   category: 책갈피 이름. 빈 category는 분류 사용 시 "기타"로 표시
   displayName: 상점 전용 표시 이름. 생략하면 ItemDef.displayName 사용
   description: 이 상점에서 보여줄 상세 설명. 생략하면 ItemDef.Description 사용
   note: 설명 아래 덧붙이는 안내. 예: 성장 일수, 사용 조건
   backpackLevel: 일반 상품은 0. 기본 배낭 확장은 1, 2
   allowQuantity: 기본 true. 일반 상품의 수량 선택을 켭니다. false이면 한 거래에 하나씩 구매합니다.
   배낭 확장은 allowQuantity와 무관하게 구매 버튼 하나만 표시하고, 기존 저장된 배낭 단계로 재구매를 막습니다.
   allowQuantity=false 자체는 영구적인 1회 구매 제한이 아닙니다.
   배낭 가격은 기존 GameManager.BackpackPrices에서 관리합니다. 임의 가격 변경은 검증으로 막습니다.

4. 상품 설명 관리
   모든 상점/인벤토리에 공통인 설명: ItemDatabase의 ItemDef.description
   특정 상점에서만 다른 설명: ShopProduct.description
   설명이 많으면 아래 ForestShopText처럼 별도 C# 파일로 나누어 관리하세요.
   줄바꿈은 \n. 긴 설명은 상세 종이 내부에서 휠/드래그로 스크롤합니다.
   마크업은 해석하지 않으므로 <b> 같은 태그를 넣지 마세요.

5. 상점 열기
   UIManager.Instance.OpenShop();             // 기존 상점: general
   UIManager.Instance.OpenShop("materials");  // 재료점 예제: 인벤토리/카테고리 숨김
   UIManager.Instance.OpenShop(ForestShop.Create()); // 등록 없이 직접 정의 전달

   ID 방식으로 등록하려면 게임 초기화 중 아래 한 줄을 실행하거나 ShopDatabase의 Factories에 추가:
   ShopDatabase.Register("forest", ForestShop.Create);
   이후 NPC/건물 상호작용에서 UIManager.Instance.OpenShop("forest");
   UIManager.Boot 완료 후 호출하세요. 스토리/다른 주요 메뉴 중에는 상점 열기가 제한됩니다.
   Canvas/ShopBookUi의 ShopBookView.OpenShop(string)은 Unity Button OnClick 연결에도 사용 가능합니다.
   기존 상점 수레의 OpenShop() 호출은 그대로 새 UI로 이어집니다.
   판매 상품에 적힌 itemId가 실제로 아이템 DB에 있어야 하며, 잘못된 ID/중복 상품 ID/가격은 검증 오류로 알려줍니다.

6. 구매 규칙
   목록 클릭은 상품 선택만 합니다. 오른쪽 [구매] 버튼으로 구매합니다.
   일반 상품은 − / 수량 / + 아래에 취소·구매 버튼을 표시합니다. 최초 선택 수량은 1입니다.
   수량이 2 이상이면 취소가 켜지고, 취소 시 1로 초기화되며 다시 흐려집니다.
   상품을 바꾸거나 구매에 성공하면 수량은 1로 돌아갑니다. 가격은 선택한 수량의 합계입니다.
   구매 버튼은 선택 수량만큼 요청합니다. 돈이나 가방 공간이 부족하면 실제 들어간 수량만 결제합니다.
   구매/외부 소지금 변경/인벤토리 변경 시 화면이 갱신됩니다.
   배낭은 순서대로 1회만 구매 가능. 닫기 버튼 또는 기존 ESC 입력으로 닫습니다.
   게임이 일시 정지되어도 책/종이 애니메이션은 진행됩니다.
   기본 판매 상품은 무제한입니다. 플레이어가 판 재구매 상품은 실제 남은 수량만 구매할 수 있습니다.

7. 판매 기능 ON / OFF
   Assets/Scripts/Items/ShopDatabase.cs에서 각 상점 생성 메서드를 수정합니다.
   예: new ShopDefinition("general", "Farmer's shop") { allowSelling = true, useCategories = true };
   ON:  allowSelling = true;  // 인벤토리 + 판매 책갈피 + 우클릭 판매 + 재구매
   OFF: allowSelling = false; // 인벤토리/판매 책갈피 숨김, 판매/재구매 불가 (기본 상품 구매는 가능)
   기본 Farmer's shop은 ON, 예제 Robin's shop(materials)은 OFF입니다.
   옛 showInventory 설정은 allowSelling에 연결되는 호환용 이름입니다. 새 코드는 allowSelling만 사용하세요.

   아이템에 마우스를 올리면 이름·개당 판매가·상세 설명 툴팁이 나옵니다.
   우클릭 한 번에 해당 슬롯의 아이템 1개를 팔고 즉시 돈을 받습니다. sellPrice가 0인 아이템은 팔 수 없습니다.
   판매 가격/설명: ItemDef.sellPrice와 ItemDef.Description. 작물·나무·물고기는 각각의 데이터베이스에서 설정합니다.
   판매한 상품은 해당 상점의 전체/판매 책갈피에 나타납니다. 판매 당시 개당 가격으로 남은 수량만 재구매합니다.
   같은 상점에서 같은 주에 같은 가격으로 판 동일 아이템은 수량을 합칩니다. 가격이 다르면 별도 항목입니다.
   재구매하면 그 수량만큼 재고가 줄고, 모두 사면 목록에서 사라집니다.
   게임 날짜 기준 다음 월요일 아침에 전 상점의 지난주 재구매 재고가 정리됩니다.
   예: 월요일 판매 → 다음 주 월요일, 일요일 판매 → 다음 날 월요일 만료.
   상점이 닫혀 있어도 만료됩니다. 재구매 기록은 일반 게임 저장/불러오기와 함께 유지됩니다.
   상점 id가 저장 키이므로 운영 중인 상점의 id를 바꾸면 그 상점의 기존 재구매 목록과 연결되지 않습니다.

8. 검증
   tools/Check-ShopBook.ps1로 별도의 Unity 검증 프로젝트에서 실행합니다. 원본 씬/세이브에는 영향을 주지 않습니다.
   tools/ShopBookChecks.cs는 격리된 Unity 프로젝트용 편집/플레이 검증 코드입니다.
   정상 구매, 부족한 돈, 가방 포화/부분 수용, 배낭 단계, 무료/잘못된 가격,
   카테고리와 종이 연출 순서, 길게 누르기, 수량 초기화, 잠긴 인벤토리 숨김,
   편집 메뉴의 비활성 UI 복원, 긴 목록/설명, 빈 상점, 연출 중 닫기/재열기를 검증합니다.
*/

using FarmMVP;

// 복사할 때 ForestShop.cs와 ForestShopText.cs로 나누어도 됩니다.
public static class ForestShop
{
    public static ShopDefinition Create()
    {
        var shop = new ShopDefinition("forest", "Mina's shop")
        {
            allowSelling = true,
            useCategories = true
        };
        shop.products.Add(new ShopProduct
        {
            id = "forest_wood", itemId = "wood", price = 25,
            category = "재료", displayName = "숲의 목재", description = ForestShopText.Wood
        });
        shop.products.Add(new ShopProduct
        {
            id = "forest_stone", itemId = "stone", price = 18,
            category = "재료", description = ForestShopText.Stone
        });
        return shop;
    }
}

public static class ForestShopText
{
    public const string Wood = "숲에서 구한 단단한 목재입니다.\n\n가구나 시설을 만들 때 사용하세요.";
    public const string Stone = "깨끗하게 골라 놓은 돌입니다.\n\n다양한 제작에 사용할 수 있습니다.";
}
