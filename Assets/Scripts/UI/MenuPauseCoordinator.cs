using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MadFact
{
    /// <summary>Keeps gameplay loaded beneath the additive menu and owns Escape handling.</summary>
    [DefaultExecutionOrder(-20000)]
    public sealed class MenuPauseCoordinator : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }
        static float _timeScaleBeforePause = 1f;
        static bool _loadingMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            AudioListener.pause = PlayerPrefs.GetInt("madfact.audio_muted", 0) == 1;
            if (Object.FindAnyObjectByType<MenuPauseCoordinator>() == null)
                new GameObject("Menu Pause Coordinator").AddComponent<MenuPauseCoordinator>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void Update()
        {
            if (_loadingMenu || IsPaused || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            OpenPauseMenu();
        }

        /// <summary>
        /// Opens the additive pause menu while leaving the current level loaded underneath.
        /// Shared by the Escape shortcut and the HUD's Back button.
        /// </summary>
        public static void OpenPauseMenu()
        {
            if (_loadingMenu || IsPaused) return;
            if (SceneManager.GetActiveScene().path == LevelSceneCatalog.GameMenu) return;
            if (GameManager.I == null || !GameManager.I.CanContinue) return;
            _loadingMenu = true;
            IsPaused = true;
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            SceneManager.LoadScene(LevelSceneCatalog.GameMenu, LoadSceneMode.Additive);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == LevelSceneCatalog.GameMenu) _loadingMenu = false;
            else if (mode == LoadSceneMode.Single)
            {
                IsPaused = false;
                _loadingMenu = false;
                Time.timeScale = 1f;
            }
        }

        public static void Resume()
        {
            if (!IsPaused) return;
            Scene menu = SceneManager.GetSceneByPath(LevelSceneCatalog.GameMenu);
            IsPaused = false;
            Time.timeScale = _timeScaleBeforePause;
            if (menu.IsValid() && menu.isLoaded) SceneManager.UnloadSceneAsync(menu);
        }

        public static void ReturnToMenu()
        {
            IsPaused = false;
            _loadingMenu = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(LevelSceneCatalog.GameMenu, LoadSceneMode.Single);
        }
    }
}
