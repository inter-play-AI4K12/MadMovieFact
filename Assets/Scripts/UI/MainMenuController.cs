using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Small runtime-built menu so the project has a proper entry point without needing
    /// another pile of hand-edited UI objects. Buttons load real scenes from Build Settings.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        Canvas _canvas;

        void Start()
        {
            EnsureCamera();
            EnsureCanvasAndEventSystem();
            BuildMenu();
        }

        void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.CrtBg;
            cam.orthographic = true;
        }

        void EnsureCanvasAndEventSystem()
        {
            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                var cgo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = cgo.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.pixelPerfect = true;
                var scaler = cgo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(960, 540);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        void BuildMenu()
        {
            var bg = UIFactory.Image(_canvas.transform, "Background", Theme.CrtBg);
            UIFactory.Fill(UIFactory.RT(bg.gameObject));

            var storefront = UIFactory.Image(bg.transform, "StorefrontGhost", new Color(1, 1, 1, 0.18f), ArtSprites.StorefrontBackground(), Image.Type.Simple, false);
            storefront.preserveAspect = true;
            UIFactory.Fill(UIFactory.RT(storefront.gameObject));

            var shade = UIFactory.Image(bg.transform, "Shade", new Color(0, 0, 0, 0.58f));
            UIFactory.Fill(UIFactory.RT(shade.gameObject));

            var panel = UIFactory.DialogWindow(bg.transform, "MainMenuPanel", new Color(0.06f, 0.09f, 0.10f, 0.96f));
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620, 430), Vector2.zero);

            var title = UIFactory.Text(panel.transform, "Title", "PELLINGS VIDEO", 30, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 46), new Vector2(0, -42));

            var subtitle = UIFactory.Text(panel.transform, "Subtitle", "recommendation systems, one cursed VHS shop at a time", 13, Theme.CrtAmber, Theme.Typewriter, TextAnchor.MiddleCenter, false);
            UIFactory.Place(UIFactory.RT(subtitle.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 24), new Vector2(0, -86));

            AddButton(panel.transform, 0, "START FULL GAME", ArtSprites.Play(), () => StartFullGame());
            AddButton(panel.transform, 1, "STORE FRONT HUB", ArtSprites.StoreLogo(), () => LoadLevel(0));
            AddButton(panel.transform, 2, "LEVEL 1 · MANUAL", ArtSprites.CustomerPortrait("WENDELL"), () => LoadLevel(1));
            AddButton(panel.transform, 3, "LEVEL 2 · RULES", ArtSprites.Robot(), () => LoadLevel(2));
            AddButton(panel.transform, 4, "LEVEL 3 · CONTENT", ArtSprites.MovieCover(0), () => LoadLevel(3));
            AddButton(panel.transform, 5, "LEVEL 4 · COLLABORATIVE", ArtSprites.Optimize(), () => LoadLevel(4));
            AddButton(panel.transform, 6, "LEVEL 5 · MARKET GAP", ArtSprites.Goal(), () => LoadLevel(5));

            var quit = UIFactory.Button(panel.transform, "Quit", "QUIT", Quit, Theme.Face, 14, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(quit.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(160, 34), new Vector2(0, 20));
            UIFactory.ButtonIcon(quit, ArtSprites.Stop(), 22f);
        }

        void AddButton(Transform parent, int row, string label, Sprite icon, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.Button(parent, "Menu_" + row, label, action, Theme.Cash, 14, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(410, 34), new Vector2(0, -126 - row * 40));
            UIFactory.ButtonIcon(button, icon, 24f);
        }

        void StartFullGame()
        {
            EnsureGameManagers();
            GameManager.I.ResetForNewGame();
            SceneManager.LoadScene(LevelSceneCatalog.FullGame);
        }

        void LoadLevel(int level)
        {
            EnsureGameManagers();
            GameManager.I.PrepareStandaloneLevel(Mathf.Max(1, level));
            SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(level));
        }

        void EnsureGameManagers()
        {
            if (GameManager.I == null) new GameObject("GameManager").AddComponent<GameManager>();
            if (AudioTension.I == null) new GameObject("Audio").AddComponent<AudioTension>();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
