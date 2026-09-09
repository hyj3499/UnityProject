using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어 시트를 <b>런타임에 잘라</b> 방향별 프레임 배열로 내주는 곳.
    ///
    /// 왜 스프라이트 에디터로 미리 자르지 않는가: 그림이 1200장이 넘고 층(피부·옷·눈·머리·장식·도구)마다
    /// 같은 규격이라, 여기서 한 번 자르면 새 머리 모양이나 옷을 넣어도 png만 떨어뜨리면 그대로 쓰인다.
    ///
    /// 시트 규격은 그림에서 읽는다 — 손으로 적어 둘 것이 없다:
    ///   · 한 프레임은 정사각형이고 <b>세로 길이가 곧 프레임 크기</b> (몸은 32px, 낚싯대는 64px)
    ///   · 가로로 한 줄이며 <b>Down → Up → Right → Left</b> 순서로 네 등분된다
    ///     (세 번째 칸이 오른쪽, 네 번째 칸이 왼쪽이다 — 눈동자·머리카락 위치를 재 보면
    ///     확인된다. Left/Right로 넘겨짚으면 A/D가 정반대로 보이는 버그가 생긴다.)
    ///   · 그래서 방향당 프레임 수 = 가로 / 프레임 크기 / 4
    /// </summary>
    public static class PlayerSpriteLibrary
    {
        private const string Root = "Sprites/Player/";
        private const float PixelsPerUnit = 16f;

        /// <summary>방향 순서. 시트가 이 순서로 그려져 있다 (세 번째=오른쪽, 네 번째=왼쪽).</summary>
        private static readonly Direction[] Order =
        { Direction.Down, Direction.Up, Direction.Right, Direction.Left };

        // "동작폴더/상대경로" -> 방향별 프레임. 시트 하나당 한 번만 자른다.
        private static readonly Dictionary<string, Sprite[][]> _cache = new Dictionary<string, Sprite[][]>();

        // Resources에 없는 경로를 계속 다시 찾지 않도록 기억해 둔다 (없는 장식/도구가 흔하다).
        private static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>
        /// 한 동작·한 층의 프레임을 방향별로 가져온다. 그림이 없으면 null —
        /// 부르는 쪽은 그 층을 그냥 비워 두면 된다 (장식이 없는 동작이 있기 때문).
        /// </summary>
        /// <param name="allowSibling">
        /// 딱 맞는 색이 없을 때 같은 폴더의 다른 색으로라도 그릴지. 몸을 이루는 층(눈·머리·옷)은
        /// 색 하나가 빠졌다고 <b>없는 채로</b> 그리면 눈이 사라져 버리므로 true로 쓴다
        /// (실제로 Fishing - Reel 에는 초록 눈만 들어 있다).
        /// 장식과 도구는 없으면 없는 것이 맞으므로 false.
        /// </param>
        public static Sprite[] Frames(string animFolder, string relativePath, Direction dir,
                                      bool allowSibling = false)
        {
            var byDir = Sheet(animFolder, relativePath);
            if (byDir == null && allowSibling) byDir = SiblingSheet(animFolder, relativePath);
            return byDir == null ? null : byDir[(int)dir];
        }

        // 색 하나가 빠졌을 때 대신 쓸 형제 그림. 경로당 한 번만 찾는다.
        private static readonly Dictionary<string, Sprite[][]> _siblings = new Dictionary<string, Sprite[][]>();

        /// <summary>같은 폴더에 있는 아무 그림이나 하나 (예: Eyes/Male 안의 다른 색).</summary>
        private static Sprite[][] SiblingSheet(string animFolder, string relativePath)
        {
            string key = animFolder + "/" + relativePath;
            if (_siblings.TryGetValue(key, out var cached)) return cached;

            Sprite[][] result = null;
            int slash = relativePath.LastIndexOf('/');
            if (slash > 0)
            {
                string folder = relativePath.Substring(0, slash);
                var all = Resources.LoadAll<Texture2D>(Root + animFolder + "/" + folder);
                if (all != null && all.Length > 0)
                {
                    result = Sheet(animFolder, folder + "/" + all[0].name);
                    if (result != null)
                        Debug.Log($"[PlayerSprites] {key}: 그림이 없어 같은 폴더의 " +
                                  $"\"{all[0].name}\"(으)로 대신 그립니다.");
                }
            }
            _siblings[key] = result;
            return result;
        }

        /// <summary>이 동작의 방향당 프레임 수. 몸(Skins)을 기준으로 잰다.</summary>
        public static int FrameCount(string animFolder)
        {
            var byDir = Sheet(animFolder, "Skins/1");
            return byDir == null ? 0 : byDir[0].Length;
        }

        private static Sprite[][] Sheet(string animFolder, string relativePath)
        {
            if (string.IsNullOrEmpty(animFolder) || string.IsNullOrEmpty(relativePath)) return null;

            string key = animFolder + "/" + relativePath;
            if (_cache.TryGetValue(key, out var cached)) return cached;
            if (_missing.Contains(key)) return null;

            var tex = Load(Root + key);
            if (tex == null)
            {
                _missing.Add(key);
                return null;
            }

            var sliced = Slice(tex);
            if (sliced == null)
            {
                Debug.LogWarning($"[PlayerSprites] {key}: {tex.width}x{tex.height} — 네 방향으로 나눌 수 없는 시트라 건너뜁니다.");
                _missing.Add(key);
                return null;
            }

            _cache[key] = sliced;
            return sliced;
        }

        /// <summary>
        /// 시트 한 장을 네 방향 × 프레임으로 자른다. Texture2D에서 바로 만들기 때문에
        /// 텍스처의 Read/Write를 켜 둘 필요가 없다 (픽셀을 읽지는 않는다).
        /// </summary>
        private static Sprite[][] Slice(Texture2D tex)
        {
            int frameSize = tex.height;
            if (frameSize <= 0) return null;

            int total = tex.width / frameSize;
            if (total < Order.Length || total % Order.Length != 0) return null;

            int perDir = total / Order.Length;
            var byDir = new Sprite[4][];
            var pivot = new Vector2(0.5f, 0.5f);

            for (int d = 0; d < Order.Length; d++)
            {
                var frames = new Sprite[perDir];
                for (int f = 0; f < perDir; f++)
                {
                    var rect = new Rect((d * perDir + f) * frameSize, 0, frameSize, frameSize);
                    frames[f] = Sprite.Create(tex, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    frames[f].name = $"{tex.name}_{Order[d]}_{f}";
                }
                byDir[(int)Order[d]] = frames;
            }
            return byDir;
        }

        /// <summary>
        /// Resources에서 텍스처 하나. 대소문자가 어긋난 파일이 섞여 있어서
        /// ("Santa hat" / "Santa Hat") 곧이곧대로 찾아 실패하면 같은 폴더를 훑어 다시 맞춰 본다.
        /// </summary>
        private static Texture2D Load(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return tex;

            int slash = path.LastIndexOf('/');
            if (slash < 0) return null;

            string folder = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            foreach (var candidate in Resources.LoadAll<Texture2D>(folder))
                if (string.Equals(candidate.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return candidate;

            return null;
        }
    }
}
