# 다국어 관리

게임 로직과 수치·아이템 ID·이벤트 분기는 언어에 관계없이 한 벌만 유지합니다. 번역 문장과 폰트만 언어팩으로 나눕니다. 언어 코드는 `en`(영어), `ko`(한국어), 추후 일본어는 `ja`를 사용합니다.

```text
Assets/
  Scripts/Localization/              # 언어 조회, 변경 알림, TMP/일반 Text 바인딩
  Resources/
    Localization/
      languages.json                # 선택할 언어와 모국어 표시명
      systemfont1.ttf               # 언어와 무관한 BoldsPixels 시스템 폰트
      en/
        Strings/ui.json             # UI·설정·확인창 문구
        Strings/items.json          # 아이템 이름·설명·상점 문구
        Strings/world.json          # 계절·날씨·장소·게임 안내
        Strings/dialogue.json       # NPC 대사·이벤트 문장
        Fonts/default.ttf           # NotJamOldStyle11
        Fonts/bold.ttf              # NotJamBlkltr13
      ko/
        Strings/…                   # 영어와 동일한 키
        Fonts/default.ttf           # neodgm_code
        Fonts/bold.ttf              # BoldDunggeunmo
    NPC/Leona.json                  # 공통 호감도·선물·스케줄·대사 키
    Events/*.json                   # 공통 연출·조건·분기·대사 키
```

최초 실행은 운영체제 언어와 관계없이 영어입니다. 선택한 언어는 `farm_language` PlayerPrefs에 저장되며, 다음 실행에도 유지됩니다. 지원하지 않는 저장값은 영어로 처리합니다. 한국어 문장이 없거나 비어 있으면 영어를 사용하고, 영어에도 없는 키는 `[키]`로 표시합니다.

## 문구 수정

같은 키의 `value`를 각 언어의 JSON에서 수정합니다. 문장을 고쳐도 키는 바꾸지 않습니다.

```json
{
  "entries": [
    { "key": "ui.settings.language", "value": "언어" }
  ]
}
```

코드에서는 `Localization.Get("ui.settings.language")`를 사용합니다. 값이 들어가는 문장은 `Localization.Format("ui.character.day", day)`처럼 전체 문장과 `{0}` 형식을 번역합니다. 단어를 이어 붙이면 언어별 어순을 바꾸기 어렵습니다.

NPC/이벤트 JSON은 `"@npc.Leona.dialogues.any_greet.lines.0"`처럼 `@키`를 담습니다. 대사 순서를 바꾸거나 앞에 새 대사를 넣더라도 이미 사용한 키는 그대로 둡니다. 대사 UI가 표시 시점에 현재 언어로 해석하며, NPC의 행동·선물 호감도·이벤트 분기는 공통 JSON 한 곳에서 관리합니다.

아이템·작물·물고기·시설의 표시 이름도 `@키`로 등록합니다. `NameReference`는 번역 전 참조를 전달하고, `displayName`/`name`은 현재 언어의 표시값을 반환합니다. 씨앗 이름은 공통 `items.seed_name` 형식으로 만들어집니다. 저장 파일에는 기존 아이템 ID를 그대로 사용합니다.

여러 언어를 옆으로 놓고 수정하려면 프로젝트 루트에서 다음 명령을 사용할 수 있습니다.

```powershell
python tools/localization_tables.py export translations.csv
# CSV의 key / en / ko 열을 편집
python tools/localization_tables.py import translations.csv
python tools/localization_tables.py check
```

CSV 가져오기는 누락된 열·중복 키·빈 번역·포맷 인자 불일치를 먼저 확인한 뒤 저장합니다. CSV에서 생략한 기존 키는 삭제하지 않습니다. 신규 키는 `Strings/additional.json`에 들어갑니다. 문장 수정 후 다른 언어의 의미도 맞는지는 번역자가 확인해야 합니다. 자동으로 번역 내용을 갱신하지는 않습니다.

## 폰트 교체

원하는 언어팩의 `Fonts/default.ttf` 또는 `Fonts/bold.ttf` 내용을 교체하면 다음 Play/빌드에서 적용됩니다. **기존 `.meta` 파일은 유지하세요.** 소스 폰트 이름이 달라도 경로와 파일명이 같으면 됩니다. 폰트 파일은 원본 `Resources/Fonts`에서 복사했으며, 기존 참조를 보호하기 위해 원본은 남겨 두었습니다.

TMP는 TTF를 직접 지정할 수 없어, 런타임에 해당 TTF에서 동적 TMP Font Asset을 생성하고 언어·역할별로 재사용합니다. 필요한 글자만 아틀라스에 추가하며 여러 아틀라스를 허용하므로 한글 전체를 미리 생성할 필요가 없습니다.

`default SDF.asset` / `bold SDF.asset`은 에디터·프리팹 미리보기용입니다. 런타임에는 TTF에서 새로 생성하므로 예전 SDF 때문에 교체한 폰트가 무시되지 않습니다. 폰트 교체 후 편집 화면의 미리보기도 바꾸려면 `Tools > Localization > Rebuild Font Previews`를 실행합니다.

Canvas의 TextMeshPro 또는 기존 Text에는 `LocalizedText` 컴포넌트를 사용합니다.

- `Key`: 번역 키. 숫자·아이템 설명·플레이어 이름처럼 다른 코드가 채우는 텍스트는 비워 둡니다.
- `Font Role`: `Default`는 일반 본문, `Bold`는 제목·소제목입니다. 폰트 파일 자체를 바꾸므로 합성 Bold 효과에 의존하지 않습니다.
- `System1`: `Localization/systemfont1.ttf`를 사용하는 고정 역할입니다. 우측 상단 DateTimePanel의 날짜·시간, 퀵 인벤토리·메뉴 인벤토리·상점 인벤토리의 수량에 적용했습니다. 언어를 바꿔도 이 폰트는 유지됩니다.
- `Inventory Count`: 수량 표시용 흰 글자·검정 외곽선입니다. TMP는 공유 외곽선 머티리얼, 기존 Text는 Outline을 사용합니다. 글자 크기와 슬롯 위치는 각 기존 UI 설정을 따릅니다.
- `Font Language`: 보통 비워 둡니다. 특별히 해당 언어 폰트가 필요한 경우만 `ko`처럼 지정합니다.

영어 TMP 폰트에는 한국어 폰트를 fallback으로 연결하여 `한국어` 선택 항목과 한글 플레이어 이름을 표시합니다. 기존 Text 기반 이름 입력창·저장 캐릭터 이름에는 한글과 영문을 함께 지원하는 한국어 일반 폰트를 사용합니다.

새 Canvas 텍스트는 `LocalizedText`를 붙여 키와 역할을 명시하는 것이 좋습니다. 기존 텍스트를 일괄 연결할 때는 `Tools > Localization > Bind Text In Active Scene`을 사용합니다. 이 도구는 기존 문구가 테이블에 있는 경우 키를 찾아 연결하며, 다른 코드가 값을 관리하는 필드는 폰트만 연결합니다. 같은 문구라도 문맥이 다르면 Inspector에서 올바른 키를 직접 선택하세요.

## 언어 추가

1. `en`과 같은 구조로 새 언어의 `Strings`와 `Fonts`를 만듭니다.
2. 모든 영어 키를 번역하고 기본·제목 폰트를 넣습니다.
3. `languages.json`에 `{"code":"ja","nativeName":"日本語"}`처럼 등록합니다.
4. 검증 명령과 실제 UI에서 긴 문장·글자 지원 여부를 확인합니다. 새 문자권의 모국어 표시명을 영어 UI에서도 표시하려면 해당 폰트를 TMP fallback 목록에 추가합니다.

## 설정과 검증

시작 메뉴의 설정은 음량, 해상도, 언어를 제공합니다. 콤보박스는 요청한 `combobox_white`, `DropdownList_icon_white`, `Radiobutton_checked_white`, `Radiobutton_unchecked_white`를 사용합니다. 펼친 목록은 행별 배경·구분선·선택 강조를 가지며, 옅은 스크롤바를 항상 표시합니다. 스크롤 손잡이 크기는 전체 항목 중 보이는 비율을 나타냅니다. 선택 즉시 적용·저장합니다. 해상도 저장은 기존 책 메뉴의 `BookDisplaySettings`와 공유하며, 실제 창 크기 변경은 게임 빌드에서만 수행합니다.

```powershell
python tools/localization_tables.py check
powershell -File tools/Check-Localization.ps1
# 이전 Unity 검증 프로세스가 끝난 뒤 실행
powershell -File tools/Check-Localization.ps1 -Play
```

Unity 검증은 `Library/LocalizationChecks`의 별도 프로젝트와 별도 PlayerPrefs를 사용합니다. 본 게임의 저장 파일·설정을 변경하지 않습니다. 결과는 `Output/result.txt`, `Output/play-result.txt`, 화면 PNG에 남습니다.
