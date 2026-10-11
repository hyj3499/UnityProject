using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>해상도/창 모드/게임 화면 배율을 선택한 뒤 적용한다.</summary>
    public static class BookDisplaySettings
    {
        private const string Prefix = "farm_display_";
        public static readonly FullScreenMode[] Modes =
        {
            FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen
        };
        public static readonly string[] ModeNames = { "창 모드", "테두리 없는 전체 화면", "전체 화면" };

        public static List<Vector2Int> Resolutions()
        {
            var sizes = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).ToList();
            sizes.Add(new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)));
            // 일부 플랫폼과 에디터는 지원 해상도 목록을 반환하지 않는다.
            if (Screen.resolutions.Length == 0)
                sizes.AddRange(new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) });
            return sizes.Distinct().OrderBy(s => s.x).ThenBy(s => s.y).ToList();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Restore()
        {
            if (!PlayerPrefs.HasKey(Prefix + "width")) return;
            ApplyScreen(PlayerPrefs.GetInt(Prefix + "width", 1280), PlayerPrefs.GetInt(Prefix + "height", 720),
                Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "mode", 0), 0, Modes.Length - 1));
        }

        public static void Apply(Vector2Int size, int mode)
        {
            mode = Mathf.Clamp(mode, 0, Modes.Length - 1);
            ApplyScreen(size.x, size.y, mode);
            PlayerPrefs.SetInt(Prefix + "width", size.x);
            PlayerPrefs.SetInt(Prefix + "height", size.y);
            PlayerPrefs.SetInt(Prefix + "mode", mode);
            PlayerPrefs.Save();
        }

        private static void ApplyScreen(int width, int height, int mode)
        {
            // Game View의 크기를 강제로 바꾸지 않는다. 실제 창 변경은 플레이어 빌드에서 적용된다.
            if (!Application.isEditor) Screen.SetResolution(Mathf.Max(320, width), Mathf.Max(240, height), Modes[mode]);
        }
    }
}
