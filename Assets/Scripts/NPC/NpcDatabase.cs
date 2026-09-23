using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Resources/NPC 폴더의 json을 전부 읽어 NPC 정의를 보관한다.
    /// NPC를 추가하려면 그 폴더에 json 하나를 더 넣기만 하면 되고, 이 파일은 수정할 필요가 없다.
    ///
    /// 그림도 여기서 id 기준으로 찾아 캐시한다.
    ///   걷기  Sprites/NPC/{id}/{id}_sprites.png 를 잘라 둔 조각 "{id}_{동작}_{번호}"
    ///         (동작: idle_down/idle_side/idle_up/walk_down/walk_side/walk_up — 왼쪽은 side를 뒤집어 쓴다)
    ///   초상화 Sprites/NPC/{id}/Portraits/ 안의 그림들. <b>파일 이름이 곧 대화에서 부르는 이름</b>이다.
    /// 조각 자르기는 tools/build_npc_metas.py 가 만들어 둔 .meta 에 들어 있다.
    /// </summary>
    public static class NpcDatabase
    {
        /// <summary>시트의 줄 이름. build_npc_metas.py 의 ROWS 와 같아야 한다.</summary>
        public const string IdleDown = "idle_down", IdleSide = "idle_side", IdleUp = "idle_up";
        public const string WalkDown = "walk_down", WalkSide = "walk_side", WalkUp = "walk_up";

        private static readonly Dictionary<string, NpcDefinition> _defs = new Dictionary<string, NpcDefinition>();
        private static readonly Dictionary<string, Dictionary<string, Sprite[]>> _clips
            = new Dictionary<string, Dictionary<string, Sprite[]>>();
        private static readonly Dictionary<string, Dictionary<string, Sprite>> _portraits
            = new Dictionary<string, Dictionary<string, Sprite>>();
        private static readonly Sprite[] _none = new Sprite[0];
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
                if (def.schedule == null) def.schedule = new NpcSchedule();
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

        // ---------- 걷기 시트 ----------
        /// <summary>그 동작의 프레임들. 시트에 없으면 빈 배열 (부르는 쪽이 다른 동작으로 대신한다).</summary>
        public static Sprite[] Frames(string npcId, string clip)
        {
            return Clips(npcId).TryGetValue(clip, out var frames) ? frames : _none;
        }

        /// <summary>서 있는 모습 한 장 — 스폰 직후나 시트가 모자랄 때의 기본 그림.</summary>
        public static Sprite AnyFrame(string npcId)
        {
            var clips = Clips(npcId);
            foreach (var clip in new[] { IdleDown, WalkDown, IdleSide, IdleUp })
                if (clips.TryGetValue(clip, out var f) && f.Length > 0) return f[0];
            foreach (var pair in clips)
                if (pair.Value.Length > 0) return pair.Value[0];
            return null;
        }

        private static Dictionary<string, Sprite[]> Clips(string npcId)
        {
            if (_clips.TryGetValue(npcId, out var cached)) return cached;

            var buckets = new Dictionary<string, List<Sprite>>();
            string path = $"Sprites/NPC/{npcId}/{npcId}_sprites";
            foreach (var sprite in Resources.LoadAll<Sprite>(path))
            {
                // "{id}_{동작}_{번호}" 에서 동작만 떼어낸다.
                string name = sprite.name;
                int last = name.LastIndexOf('_');
                if (last <= npcId.Length) continue;
                string clip = name.Substring(npcId.Length + 1, last - npcId.Length - 1);
                if (!buckets.TryGetValue(clip, out var list)) buckets[clip] = list = new List<Sprite>();
                list.Add(sprite);
            }

            var result = new Dictionary<string, Sprite[]>();
            foreach (var pair in buckets)
            {
                // Resources.LoadAll 의 순서는 보장되지 않는다 — 이름 끝의 번호로 순서를 맞춘다.
                pair.Value.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
                result[pair.Key] = pair.Value.ToArray();
            }
            if (result.Count == 0)
                Debug.LogWarning($"[NpcDatabase] {path}.png 에서 조각을 찾지 못했습니다. " +
                                 "python tools/build_npc_metas.py 로 .meta를 만들었는지 확인하세요.");

            _clips[npcId] = result;
            return result;
        }

        /// <summary>"{id}_{동작}_{번호}" 의 번호. 없으면 0.</summary>
        private static int FrameIndex(string spriteName)
        {
            int last = spriteName.LastIndexOf('_');
            return last >= 0 && int.TryParse(spriteName.Substring(last + 1), out int i) ? i : 0;
        }

        // ---------- 초상화 ----------
        /// <summary>
        /// 이름으로 초상화를 찾는다. 이름은 Portraits 폴더의 <b>파일 이름</b>이고,
        /// "Leona_happy" 처럼 다 적어도 되고 "happy" 처럼 줄여 적어도 된다.
        /// 찾지 못하면 def.defaultPortrait, 그것도 없으면 폴더의 첫 장을 쓴다.
        /// </summary>
        public static Sprite Portrait(string npcId, string name)
        {
            // 내레이션("actor" 없는 대사)은 말하는 사람이 없다 — 초상화 칸을 비워 둔다.
            if (string.IsNullOrEmpty(npcId)) return null;

            var all = Portraits(npcId);
            if (all.Count == 0) return null;

            var found = Find(all, npcId, name);
            if (found != null) return found;

            var def = Get(npcId);
            if (def != null) found = Find(all, npcId, def.defaultPortrait);
            if (found != null) return found;

            foreach (var pair in all) return pair.Value;   // 아무거나 한 장
            return null;
        }

        private static Sprite Find(Dictionary<string, Sprite> all, string npcId, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (all.TryGetValue(name, out var exact)) return exact;
            return all.TryGetValue($"{npcId}_{name}", out var prefixed) ? prefixed : null;
        }

        /// <summary>그 NPC가 가진 초상화 이름들 (에디터 검사·디버그용).</summary>
        public static IEnumerable<string> PortraitNames(string npcId) => Portraits(npcId).Keys;

        private static Dictionary<string, Sprite> Portraits(string npcId)
        {
            if (_portraits.TryGetValue(npcId, out var cached)) return cached;

            var map = new Dictionary<string, Sprite>();
            foreach (var sprite in Resources.LoadAll<Sprite>($"Sprites/NPC/{npcId}/Portraits"))
                map[sprite.name] = sprite;

            if (map.Count == 0)
                Debug.LogWarning($"[NpcDatabase] Sprites/NPC/{npcId}/Portraits 에 초상화가 없습니다.");

            _portraits[npcId] = map;
            return map;
        }
    }
}
