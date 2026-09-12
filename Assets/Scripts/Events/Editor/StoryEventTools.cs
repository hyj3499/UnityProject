using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmMVP.Editor
{
    public static class StoryEventTools
    {
        [MenuItem("Farm MVP/Events/Create Event JSON")]
        public static void CreateTemplate()
        {
            Directory.CreateDirectory("Assets/Resources/Events");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/Events/new_event.json");
            string id = Path.GetFileNameWithoutExtension(path).Replace(" ", "_");
            File.WriteAllText(path, "{\n  \"id\": \"" + id + "\",\n  \"trigger\": \"Manual\",\n  \"triggerId\": \"" + id
                + "\",\n  \"repeat\": \"Once\",\n  \"conditions\": [],\n  \"commands\": [\n"
                + "    { \"op\": \"Say\", \"speaker\": \"내레이션\", \"lines\": [\"여기에 대사를 작성하세요.\"] },\n"
                + "    { \"op\": \"End\" }\n  ]\n}\n");
            AssetDatabase.ImportAsset(path);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Farm MVP/Events/Validate All Events")]
        public static void ValidateAll()
        {
            var ids = new HashSet<string>();
            var definitions = new List<StoryEventDefinition>();
            int errors = 0;
            foreach (var asset in Resources.LoadAll<TextAsset>("Events"))
            {
                try
                {
                    var def = JsonUtility.FromJson<StoryEventDefinition>(asset.text);
                    var found = StoryEventRules.Validate(def, id => NpcDatabase.Get(id) != null);
                    if (def != null && !string.IsNullOrEmpty(def.id) && !ids.Add(def.id)) found.Add("중복 id: " + def.id);
                    foreach (string error in found) Debug.LogError($"[StoryEvent] {asset.name}: {error}", asset);
                    errors += found.Count;
                    if (def != null) definitions.Add(def);
                }
                catch (Exception e) { errors++; Debug.LogError($"[StoryEvent] {asset.name}: {e.Message}", asset); }
            }
            foreach (var def in definitions)
            {
                errors += ValidateReferences(def.conditions, ids, def.id);
                if (def.commands != null)
                    foreach (var command in def.commands)
                        if (command != null) errors += ValidateReferences(command.conditions, ids, def.id);
            }
            if (errors > 0) throw new InvalidOperationException($"이벤트 검증 실패: {errors}개 오류");
            Debug.Log($"[StoryEvent] {definitions.Count}개 이벤트 검증 통과");
        }

        private static int ValidateReferences(StoryCondition[] conditions, HashSet<string> ids, string owner)
        {
            int errors = 0;
            if (conditions != null) foreach (var c in conditions)
                if (c != null && c.type == "Completed" && !ids.Contains(c.id))
                { errors++; Debug.LogError($"[StoryEvent] {owner}: 완료 조건의 이벤트 id 없음: {c.id}"); }
            return errors;
        }

        [MenuItem("Farm MVP/Events/Run Selected Manual Event (Play Mode)")]
        public static void RunSelected()
        {
            var game = UnityEngine.Object.FindObjectOfType<GameManager>();
            var asset = Selection.activeObject as TextAsset;
            if (!Application.isPlaying || game == null || game.Events == null || asset == null)
            { Debug.LogWarning("게임 시작 후 Project에서 Manual 이벤트 JSON을 선택하세요."); return; }
            var def = JsonUtility.FromJson<StoryEventDefinition>(asset.text);
            if (def.trigger != "Manual") { Debug.LogWarning("Manual 이벤트만 수동 실행할 수 있습니다."); return; }
            game.Events.Signal("Manual", def.triggerId);
        }

        [MenuItem("Farm MVP/Events/Cancel Running Event (Play Mode)")]
        public static void Cancel() => UnityEngine.Object.FindObjectOfType<GameManager>()?.Events?.Cancel();
    }
}
