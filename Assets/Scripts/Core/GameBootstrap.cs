using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Single entry point. Attached to one GameObject in the scene (or spawned
    /// automatically). Shows the title screen (MainMenuController) first; once
    /// 새 게임/이어하기 is chosen, StartGame() creates the camera, player,
    /// GameManager and UIManager and wires them together, so the project runs
    /// with zero manual scene setup.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            // Ensure a bootstrap exists even if the scene is empty.
            if (FindObjectOfType<GameBootstrap>() == null)
            {
                var go = new GameObject("GameBootstrap");
                go.AddComponent<GameBootstrap>();
            }
        }

        private void Awake()
        {
            // Avoid duplicate bootstraps
            var all = FindObjectsOfType<GameBootstrap>();
            if (all.Length > 1)
            {
                for (int i = 1; i < all.Length; i++)
                    if (all[i] != this) Destroy(all[i].gameObject);
            }
            DontDestroyOnLoad(gameObject);
        }

        // StartGame()이 만든 오브젝트들 — 타이틀로 돌아갈 때 통째로 정리한다.
        private GameObject _playerGo, _worldGo, _gameManagerGo, _uiManagerGo;

        private void Start()
        {
            AssetLibrary.EnsureLoaded();
            ShowTitle();
        }

        private void ShowTitle()
        {
            // 타이틀 화면을 먼저 띄우고, 새 게임/이어하기를 고르면 StartGame()이 실제 게임을 만든다.
            var menuGo = new GameObject("MainMenu");
            var menu = menuGo.AddComponent<MainMenuController>();
            menu.Boot(this);
        }

        /// <summary>설정창의 "타이틀로 나가기": 진행 중인 게임을 정리하고 타이틀 화면으로 되돌아간다.</summary>
        public void ReturnToTitle()
        {
            if (_uiManagerGo != null) Destroy(_uiManagerGo);
            if (_gameManagerGo != null) Destroy(_gameManagerGo);
            if (_playerGo != null) Destroy(_playerGo);
            if (_worldGo != null) Destroy(_worldGo);
            _uiManagerGo = _gameManagerGo = _playerGo = _worldGo = null;

            ShowTitle();
        }

        /// <summary>Called by MainMenuController once 새 게임/이어하기 is chosen.</summary>
        public void StartGame()
        {
            AssetLibrary.EnsureLoaded();

            // Camera
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = new Color(0.15f, 0.18f, 0.20f);
            cam.transform.position = new Vector3(8, 7, -10);

            // Player
            var playerGo = new GameObject("Player");
            var pc = playerGo.AddComponent<PlayerController>();
            var psr = playerGo.AddComponent<SpriteRenderer>();
            psr.sprite = AssetLibrary.IdleDown.Length > 0 ? AssetLibrary.IdleDown[0] : null;
            psr.sortingOrder = 1000;

            // Location root
            var locationRoot = new GameObject("World").transform;

            // Managers
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();

            var uiGo = new GameObject("UIManager");
            var ui = uiGo.AddComponent<UIManager>();

            _playerGo = playerGo;
            _worldGo = locationRoot.gameObject;
            _gameManagerGo = gmGo;
            _uiManagerGo = uiGo;

            // Boot order: game first (creates Inventory/Data), then UI reads them.
            gm.Boot(cam, pc, locationRoot);
            ui.Boot(gm);
            gm.InitFishing(ui);   // 미니게임 바가 UI 캔버스에 붙으므로 UI 다음에

            ui.ShowDayBanner(gm.Data.currentDay);

            Debug.Log("[GameBootstrap] Farm MVP started. Controls: WASD move, LeftClick use tool, " +
                      "E/Space interact/harvest/sleep, 1-9 & Q select tool, I inventory, F5 save.");
        }
    }
}
