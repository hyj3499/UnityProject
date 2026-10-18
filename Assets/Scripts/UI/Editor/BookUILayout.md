# 책 메뉴 UI

## Canvas에서 생성하고 편집하기

1. 플레이 모드를 종료하고 **Canvas**를 선택합니다.
2. **Tools > Book UI > Build Interactive Menu Book**을 실행합니다. 기존 `MenuBookUI` 뼈대가 있으면 그 안에 UI를 채웁니다.
3. **Preview Inventory / Preview Display Settings / Preview Exit Settings**로 편집할 화면을 표시합니다.
4. Hierarchy의 Image, Text, Button, RectTransform을 직접 수정하고 씬을 저장합니다.

생성기를 다시 실행해도 완성된 UI의 배치·폰트·색을 재작성하지 않습니다. 생성과 미리보기는 Undo를 지원합니다. 기존 빈 뼈대 생성 메뉴와 SHOP 생성기도 유지됩니다. SHOP 구매·제작 기능은 이번 변경 대상이 아닙니다.

`MenuBookUI`의 **BookMenuView**에 모든 페이지·버튼·텍스트·애니메이션 참조가 저장됩니다. 자식 이름을 바꿔도 이 참조는 유지됩니다. 생성 후 새 게임/이어하기를 하면 UIManager의 BookUI가 이 뷰를 사용합니다. 씬에 뷰가 없으면 기존 뼈대를 런타임에 채우거나 같은 기본 UI를 생성합니다.

## 주요 편집 위치

- `Book/Background`: 책 배경. 부모 Book의 Scale은 1.875, 기준 크기는 663 × 418입니다. 실제 크기는 Canvas 배율 적용 전 1243.125 × 783.75로, 이전 크기의 1.5배입니다. 글자·슬롯·아이콘·책갈피·버튼도 부모 배율을 함께 적용받습니다.
- `Book/Pages/Page_Inventory/LeftPage`: PlayerStatus의 초상화·이름·생일·돈·HP·MP, InventorySlots의 10칸 × 3줄.
- `Book/Pages/Page_Inventory/RightPage/ItemDetails`: 확대 아이콘, 이름, 설명, 수량·판매가.
- `Book/Pages/Page_Settings/LeftPage/Categories`: 게임 플레이 / 디스플레이 / 오디오 / 조작 / 종료.
- `Book/Pages/Page_Settings/RightPage/SettingPanels`: 각 분류의 오른쪽 화면.
- `Book/Bookmarks`: 인벤토리 / 스킬 / 주민 / 가축 / 퀘스트 / 지도도감 / 튜토리얼 / 설정.
- `Dim`: 책 뒤 검정색 반투명 배경. Image의 Alpha로 어두운 정도를 조절합니다(기본 0.35).
- `Confirmation`: 종료 확인 창. 평소 비활성입니다. 내부 Card의 Scale은 1.5로, 확인 문구와 버튼까지 함께 확대됩니다.

별도 HUD 버튼에서 페이지를 열려면 OnClick 대상에 씬의 **MenuBookUI / BookMenuView**를 넣고 `OpenInventory`, `OpenSettings`, `OpenPage(int)`를 연결하세요. 페이지 번호는 위 책갈피 순서의 0~7입니다. `Close`로 닫습니다. 에디터 미리보기에서는 Tools 메뉴로 화면을 전환합니다.

## 게임 동작

- I: 인벤토리 열기/닫기. ESC: 설정 열기/닫기. 다른 페이지가 열려 있으면 요청한 페이지로 이동합니다.
- 열기·닫기 4프레임, 좌우 페이지 넘김 8프레임을 원본 이미지의 위치에 맞춰 재생합니다. 애니메이션 속도는 BookMenuView의 Frames Per Second에서 조절합니다.
- 애니메이션이 끝날 때까지 게임은 일시 정지 상태이고 HUD도 숨겨집니다. 원래 비활성이었던 HUD는 닫은 뒤에도 비활성 상태를 유지합니다.
- `HUD`라는 이름의 루트와 기존 QuickInventory / QuickSkill / HPMPBars / DateTime HUD를 자동으로 숨깁니다. 추가 HUD는 BookMenuView의 **Hud Roots**에 등록하세요. 책의 부모를 등록하면 안 됩니다.
- 가방은 기존 BackpackLevel에 따라 10 / 20 / 30칸을 사용합니다. 잠긴 줄은 확장 안내로 표시합니다. 슬롯을 누르면 오른쪽 상세 정보가 갱신됩니다.
- 플레이어 모습은 현재 캐릭터의 외형 레이어를 UI로 표시합니다. FarmerData의 `playerName`, `birthdaySeason`(0~3), `birthdayDay`를 사용하며 생일 0은 미등록입니다. 이름·생일 입력 화면은 아직 없습니다.
- 아이템 설명은 ItemDef.description이 있으면 우선 사용합니다. 없으면 기본 아이템/종류 설명을 표시합니다.
- 디스플레이: 창 모드 / 테두리 없는 전체 화면 / 전체 화면, 지원 해상도, 화면 스케일(기존 게임 카메라 줌). **적용**을 눌러 변경하고 PlayerPrefs에 저장합니다. 창 모드·해상도는 플레이어 빌드에서 실제 반영되며 에디터 Game View 크기는 바꾸지 않습니다.
- 종료: 저장하고 종료 / 바로 종료 모두 확인 창이 있습니다. 저장 실패 시 종료하지 않습니다. 에디터에서는 플레이 모드를 종료합니다.
- 게임 플레이·오디오·조작 설정은 준비 중이며, 나머지 여섯 메뉴 페이지는 빈 페이지입니다.

기존 책과 종료 확인 창의 배율을 적용하려면 **Apply Book Scale 1.875x**를 사용하세요. 반복 실행해도 배율이 누적되지 않습니다. Canvas가 비활성 또는 Scale 0이면 보이지 않으므로 먼저 확인하세요.
