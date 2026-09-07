using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Resources/NPC 폴더의 json을 전부 읽어 NPC 정의를 보관한다.
    /// NPC를 추가하려면 그 폴더에 json 하나를 더 넣기만 하면 되고, 이 파일은 수정할 필요가 없다.
    /// 초상화/대기 스프라이트도 여기서 id 기준으로 찾아 캐시한다.
    /// </summary>
    public static class NpcDatabase
    {
        private const int PortraitCount = 8;
        private const int IdleFrameCount = 4;

        private static readonly Dictionary<string, NpcDefinition> _defs = new Dictionary<string, NpcDefinition>();
        private static readonly Dictionary<string, Sprite[]> _idle = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, Sprite[]> _portraits = new Dictionary<string, Sprite[]>();
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;

            foreach (var asset in Resources.LoadAll<TextAsset>("NPC"))
            {
                NpcDefinition def = null;
                try { def = JsonUtility.FromJson<NpcDefinition>(asset.text); }
                catch (System.Exception e) { Debug.LogError($"[NpcDatabase] {asset.name}.json 파싱 실패: {e.Message}"); }

                if (def == null || string.IsNullOrEmpty(def.id))
                {
                    Debug.LogWarning($"[NpcDatabase] {asset.name}.json 에 id가 없어 건너뜁니다.");
                    continue;
                }
                _defs[def.id] = def;
            }
        }

        public static NpcDefinition Get(string id)
        {
            Init();
            return id != null && _defs.TryGetValue(id, out var d) ? d : null;
        }

        public static IEnumerable<NpcDefinition> All
        {
            get { Init(); return _defs.Values; }
        }

        public static Sprite[] IdleFrames(string npcId)
        {
            if (_idle.TryGetValue(npcId, out var cached)) return cached;

            var list = new List<Sprite>();
            for (int i = 0; i < IdleFrameCount; i++)
            {
                var s = Resources.Load<Sprite>($"Sprites/NPC/{npcId}/Idle_{i}");
                if (s != null) list.Add(s);
            }
            if (list.Count == 0)
                Debug.LogWarning($"[NpcDatabase] Sprites/NPC/{npcId}/Idle_0 을 찾을 수 없습니다.");

            var arr = list.ToArray();
            _idle[npcId] = arr;
            return arr;
        }

        public static Sprite Portrait(string npcId, int emotion)
        {
            if (!_portraits.TryGetValue(npcId, out var arr))
            {
                arr = new Sprite[PortraitCount];
                for (int i = 0; i < PortraitCount; i++)
                    arr[i] = Resources.Load<Sprite>($"Sprites/NPC/{npcId}/Portrait_{i}");
                _portraits[npcId] = arr;
            }

            int idx = Mathf.Clamp(emotion, 0, PortraitCount - 1);
            return arr[idx] != null ? arr[idx] : arr[0];
        }
    }
}
