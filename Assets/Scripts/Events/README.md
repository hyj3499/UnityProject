# 농장 이벤트 작성 안내

이벤트 하나는 `Assets/Resources/Events/이름.json` 파일 하나입니다. C#이나 씬을 수정하지 않고 추가할 수 있습니다.
게임 시작 시 자동으로 읽습니다. **실행 중 수정한 JSON은 타이틀로 돌아가 게임을 다시 시작해야 반영됩니다.**

## 시작하기

1. Unity 메뉴 `Farm MVP > Events > Create Event JSON`으로 템플릿을 만듭니다.
2. JSON에서 `id`, `trigger`, `conditions`, `commands`를 작성합니다. 예시 파일을 복제해도 됩니다.
3. `Validate All Events`로 명령, NPC 이름, 분기 목적지, 중복 ID를 검사합니다.
4. 게임에서 조건을 충족시켜 재생합니다. `Manual` 이벤트는 게임 시작 후 Project에서 JSON을 선택하고 `Run Selected Manual Event (Play Mode)`로 실행할 수 있습니다. 이때도 조건·완료 제한은 적용됩니다.
5. `Cancel Running Event (Play Mode)`로 연출을 취소할 수 있습니다. 실패한 이벤트는 오류 반복을 막기 위해 그 게임 세션에서 제외되므로, JSON을 수정하고 타이틀에서 다시 시작합니다.

포함된 예시:

| 파일 | 발생 조건 | 내용 |
| --- | --- | --- |
| intro_arrival.json | 새 게임 | 내레이션 → 플레이어 동작 → 데브 소개 |
| dev_two_hearts.json | 데브 하트 2 이상, 06~18시, Farm1 진입 | 선택지 분기, 호감도 +30, 플래그 |
| farm2_savings.json | 1,000 G 이상 소지하고 Farm2 진입 | 농장 계획 선택, 선택 결과 저장 |
| movement_demo.json | Farm1에서 수동 신호 `demo.movement` | NPC·플레이어 경유지 이동, 방향, 대기, 동작 |

이동 예시의 좌표는 현재 맵에 맞게 조정하세요. 경로에 플레이어/NPC/가구/나무가 있으면 우회하며, 목적지가 막혀 있거나 다른 장소에 있는 NPC를 이동시키면 이벤트를 오류로 중단합니다.

## 이벤트의 구성

```json
{
  "id": "dev.rich_visit",
  "trigger": "EnterLocation",
  "priority": 50,
  "repeat": "Once",
  "conditions": [
    { "type": "Location", "id": "Farm1" },
    { "type": "Money", "min": 2000 },
    { "type": "Hearts", "id": "dev", "min": 2 },
    { "type": "Completed", "id": "dev.two_hearts" }
  ],
  "commands": [
    { "op": "Say", "actor": "dev", "emotion": 1, "lines": ["농장이 많이 성장했네!"] },
    { "op": "Money", "amount": 100 },
    { "op": "Flag", "id": "dev.encouraged" },
    { "op": "End" }
  ]
}
```

`id`는 저장 데이터의 키입니다. 배포 후 변경하면 별개 이벤트로 인식되어 다시 재생됩니다. 파일명은 바꿔도 됩니다. `disabled: true`면 해당 콘텐츠를 비활성화합니다.

`priority`가 높은 이벤트부터 실행하고, 같으면 ID 순서입니다. 한 발생 신호에서 조건을 만족하는 여러 이벤트는 차례로 실행됩니다. 조건은 각 이벤트 시작 직전에 다시 확인합니다. NPC 상호작용은 우선순위가 가장 높은 하나만 재생하고, 해당하는 이벤트가 없으면 기존 대화/선물로 이어집니다.

## 발생 시점과 반복

| trigger | 시점 / triggerId |
| --- | --- |
| NewGame | 새 게임 인트로. Once만 지원 |
| EnterLocation | 장소 로딩 완료 시. 이어하기와 수면 후 맵 재구성도 포함 |
| DayStarted | 게임 시작 시와 다음 날 아침 |
| InteractNpc | NPC에게 상호작용할 때. `triggerId: "dev"` 필수 |
| Manual | 외부 코드/에디터 메뉴가 신호를 보낼 때. `triggerId` 필수 |

```csharp
game.Events.Signal("Manual", "quest.bridge_repaired");
```

`repeat`: `Once`(기본, 게임 전체 1회), `Daily`(하루 1회), `Always`(신호마다 1회).
조건이 맞는 동안 매 프레임 반복 실행하지 않습니다. 예를 들어 Farm2 안에서 돈이 1,000 G가 되어도 `EnterLocation`은 다시 들어올 때 판정합니다. 장소나 날짜가 바뀌면 이전 장소/날짜의 대기 이벤트는 버립니다. 창이 열려 있거나 낚시 중이면 실행을 기다립니다.

새 게임 인트로 완료 여부는 저장됩니다. 기존 세이브에는 인트로를 소급 적용하지 않습니다. 인트로 여러 개를 등록할 경우 모두 끝나야 인트로 대기 상태가 해제됩니다. 인트로에는 나중에만 충족 가능한 장소·호감도 조건을 붙이지 마세요.

## 조건

배열의 조건은 모두 만족해야 합니다(AND). `not: true`면 해당 조건을 반전합니다. 수치는 `min` 이상 `max` 이하이며 생략 시 0~int.MaxValue입니다.

| type | 값 |
| --- | --- |
| Money | 소지금 |
| Hearts | `id` NPC의 하트 수. 100 호감도 = 1 하트, 최대 10 |
| Day | 게임 시작 후 날짜, 첫날 1 |
| Time | 자정부터의 분. 06:00=360, 18:00=1080 |
| Location | `id`: Farm1 / Farm2 / FarmHouse |
| Flag | `id` 이름의 플래그가 있음 |
| Completed | `id` 이벤트가 완료됨 |

정확히 1,000 G: `{"type":"Money","min":1000,"max":1000}`.
아직 발생하지 않은 이벤트: `{"type":"Completed","id":"dev.two_hearts","not":true}`.
OR는 `If`를 연속 배치해 같은 label로 분기하거나 발생 조건이 다른 이벤트로 나누면 됩니다.

## 연출 명령

명령은 위에서 아래로 실행합니다. 대사는 입력을, 이동은 도착을 기다린 뒤 다음 명령으로 넘어갑니다. 대소문자를 구분합니다.

| op | 주요 필드 | 동작 |
| --- | --- | --- |
| Say | actor, speaker, emotion, lines | 여러 줄 대화. actor 생략 = 내레이션, player = 주인공 |
| Choice | actor, lines, choices | 2~4개 선택지. 선택한 target label로 이동 |
| Move | actor, path, speed | 경유지마다 장애물을 피해 걷기. speed는 타일/초 |
| Face | actor, direction | Down / Up / Left / Right |
| Animate | actor: player, animation, seconds | 플레이어 동작을 지정 시간 동안 표시 |
| Wait | seconds | 실제 시간 기준 대기 |
| Flag | id, value | 플래그 설정, value 생략=true, false=제거 |
| Money | amount | 돈 증감. 음수=비용; 부족하면 이벤트 실패 |
| Affection | id, amount | NPC 호감도 증감. 0~1000으로 제한 |
| If | conditions, target, otherwise | 참이면 target, 거짓이면 otherwise. otherwise 생략 시 다음 줄 |
| Goto | target | 지정 label로 이동 |
| End | — | 이벤트 정상 종료. 배열 끝에 도달해도 정상 종료 |

모든 명령에 `label`을 붙일 수 있습니다. 같은 이벤트 안에서 label은 고유해야 합니다. 선택지는 `{"text":"좋아!","target":"accept"}`처럼 작성합니다. 한 분기의 끝에는 `Goto`나 `End`를 넣어 다른 분기의 대사가 이어 나오지 않게 하세요.

```json
[
  { "op": "Move", "actor": "dev", "path": [{"x":11,"y":7},{"x":11,"y":8}], "speed": 2 },
  { "op": "Face", "actor": "player", "direction": "Up" },
  { "op": "Animate", "actor": "player", "animation": "Harvest", "seconds": 0.8 },
  { "op": "If", "conditions": [{"type":"Flag","id":"dev.rested_together"}], "target": "friend" },
  { "op": "Say", "actor": "dev", "lines": ["좋은 아침!"] },
  { "op": "End" },
  { "op": "Say", "label": "friend", "actor": "dev", "lines": ["지난번 같이 쉬었던 날, 즐거웠어."] }
]
```

`speaker`는 표시 이름을 덮어씁니다. NPC 표정은 기존 `NpcEmotion` 번호: 0 기본, 1 미소, 2 행복, 3 생각, 4 화남, 5 슬픔, 6 놀람, 7 충격입니다.

플레이어 `animation`: Idle, Walk, Run, Carry, Harvest, Tool, Watering, Melee, FishCast, FishWait, FishReel, FishCaught. `Animate`는 시각 연출이며 실제 농사·낚시 효과는 발생시키지 않습니다. 동작의 실제 그림은 기존 FarmerPoses/에셋에 따릅니다.

NPC는 현재 Idle 스프라이트만 있어서 이동 중에도 대기 그림을 사용하며, Left는 좌우 반전합니다. Up/Down 전용 그림과 걷기 프레임은 아직 없습니다. `NpcActor.SetFacing`/`Update`에 방향별 애니메이션을 연결하면 같은 이벤트 파일을 계속 사용할 수 있습니다.

## 실행과 저장 규칙

- 연출 중에는 시간, 플레이어 직접 조작, 인벤토리 조작, 메뉴, F5 저장, 수면, 장소 전환이 잠깁니다. 카메라는 플레이어 이동을 따라갑니다.
- 연출이 끝나거나 취소되면 NPC와 플레이어는 시작 위치/방향으로 복원됩니다. 이벤트 동선은 연출용이며 NPC의 일상 시간표를 바꾸지 않습니다. 출구 타일은 경로에서 제외합니다.
- 돈·호감도·플래그 변경은 이벤트 안의 임시 상태에 반영합니다. 이후 `If`도 이 상태를 보며, 정상 종료할 때만 실제 게임 데이터에 반영합니다.
- 취소/실패 시 보상과 완료 기록을 남기지 않습니다. 무한 분기는 4,096명령에서 중단하며, 오류 로그에 이벤트 ID와 원인을 출력합니다.
- 완료 기록은 기존 F5/수면 저장에 포함됩니다. 이벤트 끝마다 자동 저장하지 않으며 중간 재개는 지원하지 않습니다. 저장 전 종료하면 마지막 세이브 상태로 돌아갑니다.
- 신규 명령을 추가할 때는 StoryCommand 필드 → StoryEventRules 검증 → StoryEventDirector.Execute 실행을 함께 확장합니다. 타임라인이나 외부 대화 플러그인 없이 동작합니다.

현재 기반은 한 이벤트 안의 순차 연출을 담당합니다. NPC의 하루 시간표, 여러 배우 동시 이동, 카메라 컷·페이드, 이벤트 도중 맵 전환은 별도 확장 지점입니다.

## 검증

`Farm MVP > Events > Run Regression Checks`는 조건 경계, 반복 제한, JSON 기본값, 기존 세이브 호환, 상태 저장 왕복, 잘못된 분기, 장애물 경로를 검사합니다. 실제 저장 파일은 수정하지 않습니다.

명령줄에서도 실행할 수 있습니다:

```text
Unity.exe -batchmode -nographics -projectPath <프로젝트 절대경로> -executeMethod FarmMVP.Editor.StoryEventChecks.Run -quit -logFile <로그 경로>
```

에디터 실행이 불가능한 환경에서는 저장소 루트에서 `./tools/Check-StoryEvents.ps1`을 실행합니다. 설치된 Unity 컴파일러로 전체 C#을 컴파일하고, 28개 순수 로직 검사와 이벤트 JSON 검증을 실행합니다. 경로가 다르면 `-UnityData`로 Editor/Data 폴더를 지정합니다. 이 검사는 프로젝트가 한 번 임포트되어 Library/ScriptAssemblies의 UI·Tilemap DLL이 있어야 합니다. Unity JsonUtility와 실제 화면/코루틴 재생 검사는 에디터에서 별도로 해야 합니다.

플레이 모드 확인: 새 게임 인트로 → 마지막 대사 이후 조작 복구 → 저장/이어하기에서 인트로 미재생 → 하트 2 상태로 Farm1 재진입 → 두 선택지 각각 확인 → 1,000 G로 Farm2 진입 → movement_demo 수동 실행 및 취소 → 가구로 목적지를 막고 실패 시 잠금/위치 복구 확인.
