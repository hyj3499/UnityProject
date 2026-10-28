# StartScreen UI 편집

`Assets/Scenes/Main.unity`의 `Canvas/StartScreenUI`에서 편집합니다. 실행 시 이 계층의 참조를 그대로 사용하므로, RectTransform 위치·크기, 텍스트, 버튼 이미지 수정이 유지됩니다.

- `Design/MainMenu`: StartScreen.png 이미지와 NEW GAME / CONTINUE / SETTING / EXIT.
- `Design/CharacterCreator`: 공통 제목, 닫기, StandingPanel, 회전, 다음/이전/게임 시작 버튼.
- `CharacterCreator/BasicSettings`: NameInput, EditName, Male/Female, 계절·날짜.
- `CharacterCreator/CustomSettings/PartsScroll/Viewport/Content`: Skin, Eyes, Hair, Top, Bottom, Outfit, Shoes, Accessory. 각 행에는 색상 견본, H/S/V 슬라이더, 추천색 버튼이 있습니다.
- `Design/PartPicker`: 어두운 슬롯 패널. `SlotsScroll/Viewport/Content/SlotTemplate`은 실행 중 복제하는 편집용 원본입니다. GridLayoutGroup에서 열 개수·슬롯 간격·표시 크기를 바꿉니다. 얼굴 텍스처 자체는 16×16입니다.
- `Design/Settings`: 음량 및 돌아가기.
- `Design/ContinuePanel`: 저장 데이터 선택. `StandingPanel/Character`는 마지막 저장 시점의 정면 외형, `GameDataInfoPanel`은 현재 데이터 번호·이름·생일·날짜·소지금·플레이시간입니다. `PreviousSave`/`NextSave`, `StartSave`/`DeleteSave`, `Close`의 RectTransform과 이미지를 직접 편집할 수 있습니다.
- `Confirmation`: 전체 화면 입력 차단과 예/아니오 확인창.

Unity 메뉴 `Tools > Start Screen > Preview Main Menu / Preview Basic Settings / Preview Custom Settings / Preview Part Picker / Preview Continue`로 편집할 패널을 켤 수 있습니다. 플레이하면 항상 메인 메뉴부터 시작합니다. `Create UI In Canvas`는 기존 UI가 있으면 선택만 하며 기존 수정을 덮어쓰지 않습니다.

`StartScreenView`의 Parts 배열에서 추천색, 옵션 및 UI 참조를 관리합니다. 추천색의 색상 값과 추천 버튼 하위 Color 이미지의 색을 함께 수정하세요. 현재 지원하는 눈은 `Base/eyes#0`, `Base/eyes#1`이며 미제작 파츠는 빈 문자열 하나입니다. 미래 파츠 그림을 추가할 때에는 게임 캐릭터 렌더러와 미리보기 합성도 함께 확장해야 합니다.

`Design`의 StartScreenFit은 1920×1080 기준 전체 화면 맞춤만 담당합니다. 개별 패널 위치는 변경하지 않습니다. 행과 슬롯 내부는 Unity LayoutGroup이 배치하므로 그 내부 배치는 LayoutGroup 설정으로 수정합니다.

패널·캐릭터 미리보기·글꼴은 `Assets/Resources/Sprites/StartScreen` 안에 있습니다. 버튼은 `Assets/Resources/Sprites/UI/Button`의 `Btn_White.png`, `Close_btn.png`, `Arrow_btn.png`를 사용합니다. `CharacterPreview`에는 기존 캐릭터의 몸·눈·앵커 시트 사본이 있고, `Fonts`에는 기존 한글 폰트 사본이 있습니다. 게임 캐릭터 원본을 변경하면 이 사본도 갱신하세요.

이름 기본값은 Ros, 성별은 미선택, 생일은 봄 1일입니다. 이름은 edit 버튼으로 수정하며 최대 20자입니다. 성별은 선택 후 하나만 활성화됩니다. 계절과 날짜는 양쪽 끝에서 순환합니다.

슬롯 클릭은 미리보기이고 `설정` 버튼으로 확정합니다. 다른 파츠를 열거나, 다른 파츠의 색을 바꾸거나, 닫기·이전·게임 시작을 누르면 미확정 슬롯 선택을 취소합니다. 색은 즉시 적용됩니다. 취소 후 다시 NEW GAME을 누르면 새 초안으로 시작합니다.

이름·성별·생일과 확정한 외형은 FarmerData에 전달됩니다. 새 게임은 별도의 캐릭터 데이터로 생성되어 시작 시 최초 저장됩니다. 피부/눈 색은 실제 게임 캐릭터에도 동일한 픽셀 색상 변환으로 반영됩니다.

세이브는 최대 5개입니다. 캐릭터마다 고정된 `GameData.saveId`가 있고 F5·설정 저장·수면 저장 모두 그 ID의 진행 상황 하나를 갱신합니다. 동일한 캐릭터의 별도 복제 슬롯은 만들지 않습니다. 5개가 차면 NEW GAME에서 안내하며 Continue에서 삭제하여 공간을 확보할 수 있습니다. 삭제는 확인창에서 확정한 선택 데이터만 지웁니다.

저장 파일은 `Application.persistentDataPath/Saves/<saveId>.json`이며, 기존 `farm_save.json`도 하나의 독립 세이브로 인식하고 같은 파일에 이어 저장합니다. Continue는 정상적으로 읽을 수 있는 데이터가 없으면 딤드 처리됩니다. 목록은 마지막 저장 시각 내림차순이며 좌우 끝에서 순환하고, 하나만 남으면 화살표가 비활성화됩니다. 플레이시간은 타이틀 화면을 제외하고 게임에 포커스가 있는 동안의 실제 시간을 누적합니다. 기존 파일의 플레이시간은 기록이 없으므로 0부터 시작합니다.

기본 UI의 재사용 프리팹은 `Assets/Resources/UI/StartScreenUI.prefab`입니다. Main 씬은 독립된 편집 가능한 계층을 사용합니다. 빈 씬에서는 프리팹을 사용하므로, 다른 씬에서도 수정본을 쓰려면 프리팹에도 반영하세요.

검증: `tools/Check-StartScreen.ps1`은 `Temp/StartScreenChecks`에 격리된 Unity 프로젝트를 만들고 컴파일, 프리팹 직렬화, 입력/전환/선택 확정, 저장 데이터 및 렌더링을 검사합니다. 해당 프로세스 종료 후 `tools/Check-StartScreen.ps1 -Play`로 실제 레이캐스트 클릭·이름 입력 포커스·스크롤·확인창 입력 차단도 검사할 수 있습니다. 결과와 화면은 해당 폴더의 `Output`에 저장됩니다.

Continue 검증: `tools/Check-Continue.ps1`, 종료 후 `-VerifyInstalled`, 종료 후 `-Play`를 실행합니다. `Temp/ContinueChecks/Output`에 결과와 렌더링이 생성되며 실제 플레이어의 저장 파일은 사용하지 않습니다. `tools/Install-ContinueScene.py`는 검증된 Continue 계층만 기존 씬/프리팹에 한 번 추가하는 설치 도구이며, 이미 설치된 계층이나 기존 편집을 덮어쓰지 않습니다.
