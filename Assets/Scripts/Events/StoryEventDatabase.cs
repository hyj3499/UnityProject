using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    public static class StoryEventDatabase
    {
        // 매 게임 시작 때 다시 읽으므로 에디터에서 JSON을 수정한 뒤 타이틀에서 재시작할 수 있다.
        public static List<StoryEventDefinition> Load()
        {
            var result = new List<StoryEventDefinition>();
            var ids = new HashSet<string>();
            var duplicates = new HashSet<string>();
            foreach (var asset in Resources.LoadAll<TextAsset>("Events"))
            {
                try
                {
                    var def = JsonUtility.FromJson<StoryEventDefinition>(asset.text);
                    var errors = StoryEventRules.Validate(def, id => NpcDatabase.Get(id) != null);
                    if (errors.Count > 0)
                    { Debug.LogError($"[StoryEvent] {asset.name}: {string.Join(" / ", errors)}"); continue; }
                    if (!ids.Add(def.id))
                    { duplicates.Add(def.id); Debug.LogError("[StoryEvent] 중복 id (모두 제외): " + def.id); }
                    if (!def.disabled) result.Add(def);
                }
                catch (Exception e) { Debug.LogError($"[StoryEvent] {asset.name}: {e.Message}"); }
            }
            result.RemoveAll(d => duplicates.Contains(d.id));
            result.Sort((a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : string.CompareOrdinal(a.id, b.id));
            return result;
        }
    }
}
