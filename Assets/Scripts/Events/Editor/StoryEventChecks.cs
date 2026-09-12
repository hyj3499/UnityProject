using System;
using UnityEditor;
using UnityEngine;

namespace FarmMVP.Editor
{
    /// <summary>외부 테스트 패키지 없이 실행하는 회귀 검사. 실제 세이브 파일은 읽거나 쓰지 않는다.</summary>
    public static class StoryEventChecks
    {
        private static int _checks;

        [MenuItem("Farm MVP/Events/Run Regression Checks")]
        public static void Run()
        {
            StoryEventTools.ValidateAll();
            RunPure();
            CheckSerialization();
            Debug.Log($"[StoryEventChecks] PASS ({_checks} assertions)");
        }

        // Unity 네이티브 런타임/실제 저장 파일 없이 실행할 수 있는 검사.
        public static int RunPure()
        {
            _checks = 0;
            var data = new GameData { currentDay = 3, currentMinutes = 600, currentLocation = LocationId.Farm1 };
            var state = new StoryEventState();
            var conditions = new[] {
                new StoryCondition { type = "Money", min = 1000, max = 2000 },
                new StoryCondition { type = "Location", id = "Farm1" },
                new StoryCondition { type = "Hearts", id = "dev", min = 2 },
                new StoryCondition { type = "Time", min = 360, max = 1080 }
            };
            Check(!StoryEventRules.Matches(conditions, data, state, 999, _ => 2), "소지금 하한 직전");
            Check(StoryEventRules.Matches(conditions, data, state, 1000, _ => 2), "소지금/하트 하한 포함");
            Check(StoryEventRules.Matches(conditions, data, state, 2000, _ => 2), "소지금 상한 포함");
            Check(!StoryEventRules.Matches(conditions, data, state, 2001, _ => 2), "소지금 상한 초과");
            Check(!StoryEventRules.Matches(conditions, data, state, 1000, _ => 1), "호감도 미달");
            data.currentLocation = LocationId.Farm2;
            Check(!StoryEventRules.Matches(conditions, data, state, 1000, _ => 2), "다른 장소");
            data.currentLocation = LocationId.Farm1;
            data.currentMinutes = 1081;
            Check(!StoryEventRules.Matches(conditions, data, state, 1000, _ => 2), "시간 범위 밖");

            var definition = new StoryEventDefinition { id = "test", repeat = "Once" };
            Check(StoryEventRules.CanRepeat(definition, state, 3), "처음에는 실행 가능");
            state.Complete("test", 3);
            Check(!StoryEventRules.CanRepeat(definition, state, 4), "일회성 완료 후 차단");
            definition.repeat = "Daily";
            Check(!StoryEventRules.CanRepeat(definition, state, 3), "당일 재실행 차단");
            Check(StoryEventRules.CanRepeat(definition, state, 4), "다음 날 재실행 허용");
            definition.repeat = "Always";
            Check(StoryEventRules.CanRepeat(definition, state, 3), "Always 허용");
            state.Complete("test", 4);
            Check(state.completed.Count == 1 && state.LastDay("test") == 4, "완료 기록 중복 없이 갱신");
            state.flags.Add("accepted");
            var branch = new[] { new StoryCondition { type = "Flag", id = "accepted" }, new StoryCondition { type = "Completed", id = "test" } };
            Check(StoryEventRules.Matches(branch, data, state, 0, _ => 0), "플래그/완료 AND");
            branch[0].not = true;
            Check(!StoryEventRules.Matches(branch, data, state, 0, _ => 0), "조건 반전");

            var invalid = new StoryEventDefinition { id = "bad", commands = new[] { new StoryCommand { op = "Goto", target = "missing" } } };
            Check(StoryEventRules.Validate(invalid, _ => true).Count > 0, "잘못된 분기 감지");
            invalid.commands[0] = new StoryCommand { op = "Move", actor = "player", speed = 0, path = new[] { new StoryPoint() } };
            Check(StoryEventRules.Validate(invalid, _ => true).Count > 0, "이동 속도 0 감지");
            invalid.commands[0] = new StoryCommand { op = "Face", actor = "player", direction = "999" };
            Check(StoryEventRules.Validate(invalid, _ => true).Count > 0, "숫자 enum 오타 감지");
            invalid.commands = new[] { new StoryCommand { op = "End", label = "same" }, new StoryCommand { op = "End", label = "same" } };
            Check(StoryEventRules.Validate(invalid, _ => true).Count > 0, "중복 label 감지");
            invalid.commands = new[] { new StoryCommand { op = "Typo" } };
            Check(StoryEventRules.Validate(invalid, _ => true).Count > 0, "알 수 없는 명령 감지");

            Func<Vector2Int, bool> blocked = p => p.x < 0 || p.y < 0 || p.x > 4 || p.y > 4 || p == new Vector2Int(1, 0);
            var path = StoryPathfinder.Find(Vector2Int.zero, new Vector2Int(2, 0), blocked);
            Check(path != null && path.Count == 5, "장애물 우회");
            for (int i = 1; i < path.Count; i++)
                Check(!blocked(path[i]) && (path[i] - path[i - 1]).sqrMagnitude == 1, "경로가 벽을 뚫지 않고 4방향 이동");
            Check(StoryPathfinder.Find(Vector2Int.zero, new Vector2Int(1, 0), blocked) == null, "막힌 목적지 실패");
            Check(StoryPathfinder.Find(Vector2Int.zero, new Vector2Int(2, 0), p => p != Vector2Int.zero) == null, "도달 불가능 경로 실패");
            Check(StoryPathfinder.Find(Vector2Int.zero, Vector2Int.zero, blocked).Count == 1, "제자리 이동");
            return _checks;
        }

        private static void CheckSerialization()
        {
            var data = new GameData();
            data.story.Complete("test", 4);
            data.story.flags.Add("accepted");
            var loaded = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
            Check(loaded.story.LastDay("test") == 4 && loaded.story.flags.Contains("accepted"), "세이브 왕복");
            var legacy = JsonUtility.FromJson<GameData>("{\"currentDay\": 12}");
            if (legacy.story == null) legacy.story = new StoryEventState();
            legacy.story.Normalize();
            Check(!legacy.story.introPending, "기존 세이브에 인트로 재생 안 함");
            Check(StoryEventRules.CanRepeat(new StoryEventDefinition { id = "future" }, legacy.story, 12), "기존 세이브에 새 콘텐츠 허용");
            var defaults = JsonUtility.FromJson<StoryEventDefinition>("{\"id\":\"defaults\",\"commands\":[{\"op\":\"End\"}],\"conditions\":[{\"type\":\"Money\",\"min\":1000}]}");
            Check(defaults.repeat == "Once" && defaults.conditions[0].max == int.MaxValue, "JSON 생략 필드 기본값");
        }

        private static void Check(bool value, string message)
        {
            _checks++;
            if (!value) throw new InvalidOperationException("[StoryEventChecks] FAIL: " + message);
        }
    }
}
