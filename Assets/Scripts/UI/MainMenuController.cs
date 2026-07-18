using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Binds the scene-authored Main Menu. Its Canvas, panel, labels, icons, and buttons
    /// remain visible and editable in MainMenu.unity; this component only owns behaviour.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Authored Scene References")]
        [SerializeField] Canvas _canvas;
        [SerializeField] Button _startFullGame;
        [SerializeField] Button _storefront;
        [SerializeField] Button _level1;
        [SerializeField] Button _level2;
        [SerializeField] Button _level3;
        [SerializeField] Button _level4;
        [SerializeField] Button _level5;
        [SerializeField] Button _quit;

        void Start()
        {
            if (!ValidateAuthoredMenu()) return;

            RuntimeSkin.Apply(_canvas.transform);
            Bind(_startFullGame, StartFullGame);
            Bind(_storefront, () => LoadLevel(0));
            Bind(_level1, () => LoadLevel(1));
            Bind(_level2, () => LoadLevel(2));
            Bind(_level3, () => LoadLevel(3));
            Bind(_level4, () => LoadLevel(4));
            Bind(_level5, () => LoadLevel(5));
            Bind(_quit, Quit);
            MadFactLokiLogger.Instance?.Log("main_menu_opened", "Main menu opened");
        }

        bool ValidateAuthoredMenu()
        {
            bool valid = _canvas != null && _startFullGame != null && _storefront != null &&
                _level1 != null && _level2 != null && _level3 != null && _level4 != null &&
                _level5 != null && _quit != null;
            if (!valid)
            {
                Debug.LogError("MainMenuController is missing scene-authored UI references. Rebuild the authored Main Menu from the MadFact editor menu.", this);
                enabled = false;
            }
            return valid;
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        void StartFullGame()
        {
            EnsureGameManagers();
            GameManager.I.ResetForNewGame();
            MadFactLokiLogger.Instance?.Log("game_started", "Full game started",
                new { start_mode = "full_game" });
            // The split, authored scenes are now the canonical game flow. Storefront
            // presents the intro and then advances through the dedicated level scenes.
            SceneManager.LoadScene(LevelSceneCatalog.Storefront);
        }

        void LoadLevel(int level)
        {
            EnsureGameManagers();
            GameManager.I.PrepareStandaloneLevel(Mathf.Max(1, level));
            MadFactLokiLogger.Instance?.Log("game_started", "Game started from level select",
                new { start_mode = "level_select", level_id = level });
            SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(level));
        }

        void EnsureGameManagers()
        {
            if (GameManager.I == null) new GameObject("GameManager").AddComponent<GameManager>();
            if (AudioTension.I == null) new GameObject("Audio").AddComponent<AudioTension>();
        }

        void Quit()
        {
            MadFactSessionManager.Instance?.EndSession("main_menu_quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
