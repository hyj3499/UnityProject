using System;
using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>Unity 씬 없이도 검사 가능한 조건/데이터 검증. 잘못된 콘텐츠는 실행 전에 제외한다.</summary>
    public static class StoryEventRules
    {
        public static bool Matches(StoryCondition[] conditions, GameData data, StoryEventState state,
            int money, Func<string, int> hearts)
        {
            if (conditions == null) return true;
            foreach (var c in conditions)
            {
                bool result;
                switch (c.type)
                {
                    case "Money": result = InRange(money, c); break;
                    case "Hearts": result = InRange(hearts(c.id), c); break;
                    case "Day": result = InRange(data.currentDay, c); break;
                    case "Time": result = InRange(data.currentMinutes, c); break;
                    case "Location": result = data.currentLocation.ToString() == c.id; break;
                    case "Flag": result = state.flags.Contains(c.id); break;
                    case "Completed": result = state.LastDay(c.id) >= 0; break;
                    default: return false;
                }
                if (c.not ? result : !result) return false;
            }
            return true;
        }

        private static bool InRange(int value, StoryCondition c) => value >= c.min && value <= c.max;

        public static bool CanRepeat(StoryEventDefinition def, StoryEventState state, int day)
        {
            int last = state.LastDay(def.id);
            return def.repeat == "Always" || last < 0 || (def.repeat == "Daily" && last != day);
        }

        public static bool NamedEnum<T>(string value) where T : struct
            => value != null && Array.IndexOf(Enum.GetNames(typeof(T)), value) >= 0;

        public static List<string> Validate(StoryEventDefinition def, Func<string, bool> npcExists)
        {
            var errors = new List<string>();
            if (def == null) { errors.Add("이벤트가 비어 있습니다."); return errors; }
            if (string.IsNullOrWhiteSpace(def.id)) errors.Add("id가 필요합니다.");
            if (!OneOf(def.trigger, "NewGame", "EnterLocation", "DayStarted", "InteractNpc", "Manual"))
                errors.Add("알 수 없는 trigger: " + def.trigger);
            if (!OneOf(def.repeat, "Once", "Daily", "Always")) errors.Add("repeat는 Once/Daily/Always입니다.");
            if (def.trigger == "NewGame" && def.repeat != "Once") errors.Add("NewGame은 Once만 지원합니다.");
            if (def.trigger == "InteractNpc" && !npcExists(def.triggerId)) errors.Add("triggerId NPC가 없습니다.");
            if (def.trigger == "Manual" && string.IsNullOrWhiteSpace(def.triggerId)) errors.Add("Manual triggerId가 필요합니다.");
            CheckConditions(def.conditions, errors, npcExists);
            if (def.commands == null || def.commands.Length == 0)
            { errors.Add("commands가 필요합니다."); return errors; }
            var labels = new HashSet<string>();
            foreach (var c in def.commands)
                if (c != null && !string.IsNullOrEmpty(c.label) && !labels.Add(c.label)) errors.Add("중복 label: " + c.label);
            for (int i = 0; i < def.commands.Length; i++)
            {
                var c = def.commands[i];
                string at = "commands[" + i + "]: ";
                if (c == null) { errors.Add(at + "빈 명령"); continue; }
                if (!OneOf(c.op, "Say", "Choice", "Move", "Face", "Animate", "Wait", "Flag", "Money", "Affection", "If", "Goto", "End"))
                    errors.Add(at + "알 수 없는 op: " + c.op);
                if (OneOf(c.op, "Say", "Choice") && (c.lines == null || c.lines.Length == 0)) errors.Add(at + "lines 필요");
                if (OneOf(c.op, "Move", "Face", "Animate") && string.IsNullOrEmpty(c.actor)) errors.Add(at + "actor 필요");
                if (!string.IsNullOrEmpty(c.actor) && c.actor != "player" && !npcExists(c.actor)) errors.Add(at + "NPC 없음: " + c.actor);
                if (c.op == "Move" && (c.path == null || c.path.Length == 0 || Array.Exists(c.path, p => p == null)
                    || !Finite(c.speed) || c.speed <= 0)) errors.Add(at + "유효한 path와 양수 speed 필요");
                if (!Finite(c.seconds) || c.seconds < 0) errors.Add(at + "seconds는 유한한 0 이상 값 필요");
                if (c.op == "Face" && !NamedEnum<Direction>(c.direction)) errors.Add(at + "잘못된 direction");
                if (c.op == "Animate" && (c.actor != "player" || !NamedEnum<FarmerAnim>(c.animation))) errors.Add(at + "Animate는 player와 FarmerAnim 이름을 사용");
                if (c.op == "Affection" && !npcExists(c.id)) errors.Add(at + "호감도 NPC 없음");
                if (c.op == "Flag" && string.IsNullOrWhiteSpace(c.id)) errors.Add(at + "flag id 필요");
                if (c.op == "Choice")
                {
                    if (c.choices == null || c.choices.Length < 2 || c.choices.Length > 4) errors.Add(at + "선택지는 2~4개 필요");
                    else foreach (var choice in c.choices)
                    {
                        if (choice == null || string.IsNullOrWhiteSpace(choice.text)) errors.Add(at + "빈 선택지");
                        CheckTarget(choice?.target, labels, errors, at);
                    }
                }
                if (c.op == "Goto" || c.op == "If") CheckTarget(c.target, labels, errors, at);
                if (c.op == "If")
                {
                    CheckConditions(c.conditions, errors, npcExists);
                    if (!string.IsNullOrEmpty(c.otherwise)) CheckTarget(c.otherwise, labels, errors, at);
                }
            }
            return errors;
        }

        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool OneOf(string value, params string[] options) => Array.IndexOf(options, value) >= 0;
        private static void CheckTarget(string target, HashSet<string> labels, List<string> errors, string at)
        {
            if (string.IsNullOrEmpty(target) || !labels.Contains(target)) errors.Add(at + "목적지 label 없음: " + target);
        }
        private static void CheckConditions(StoryCondition[] conditions, List<string> errors, Func<string, bool> npcExists)
        {
            if (conditions == null) return;
            foreach (var c in conditions)
            {
                if (c == null) { errors.Add("빈 조건"); continue; }
                if (!OneOf(c.type, "Money", "Hearts", "Day", "Time", "Location", "Flag", "Completed")) errors.Add("알 수 없는 조건: " + c.type);
                if (c.min > c.max) errors.Add("조건 min > max");
                if (c.type == "Location" && !NamedEnum<LocationId>(c.id)) errors.Add("장소 없음: " + c.id);
                if (c.type == "Hearts" && !npcExists(c.id)) errors.Add("조건 NPC 없음: " + c.id);
                if (OneOf(c.type, "Flag", "Completed") && string.IsNullOrWhiteSpace(c.id)) errors.Add("조건 id 필요");
            }
        }
    }
}
