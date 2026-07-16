using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace MadFact
{
    /// <summary>
    /// The conductor. Drop this on a single empty GameObject in an empty scene and it builds
    /// the entire MadFact game: managers, audio, camera, canvas, all five learning levels,
    /// and the narrative flow that ties them together.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MadFactBootstrap : MonoBehaviour
    {
        public static MadFactBootstrap I { get; private set; }

        public StorefrontView Storefront;
        public Hud Hud;
        public CommsBox Comms;
        public Level1Counter L1;
        public Level2Robot L2;
        public Level3ContentBased L3Content;
        public Level3Mainframe L3;
        public Level4Corkboard L4;

        public bool Level1Cleared, Level2Cleared, Level3Cleared, Level4Cleared;
        [Tooltip("-1 = normal full-game intro, 0 = storefront hub, 1..5 = start directly in that dedicated level scene.")]
        public int StartPhaseOverride = -1;
        int _currentLevel = 1;
        bool _built;

        [SerializeField] Canvas _canvas;
        GameObject _winPanel;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void Start()
        {
            if (_built) return;
            _built = true;

            EnsureCamera();
            EnsureManagers();
            EnsureCanvasAndEventSystem();

            // Production scenes provide serialized prefab instances. The factory path is
            // retained as a safety net for empty test scenes and for newly-added levels.
            bool hasAuthoredContent = Storefront != null || Hud != null || Comms != null ||
                L1 != null || L2 != null || L3Content != null || L3 != null || L4 != null;
            if (hasAuthoredContent) RuntimeSkin.Apply(_canvas.transform);

            // Every level is iterating quickly in code right now. Rebuild them all from
            // script so layout, backgrounds, and button wiring always match the current
            // source even when a scene's authored instance has drifted out of date
            // (e.g. a Next button serialized half off-screen, or a stage backdrop the
            // prefab never knew about).
            if (L1 != null) Destroy(L1.gameObject);
            if (L2 != null) Destroy(L2.gameObject);
            if (L3Content != null) Destroy(L3Content.gameObject);
            if (L3 != null) Destroy(L3.gameObject);
            if (L4 != null) Destroy(L4.gameObject);
            L1 = Level1Counter.Create(_canvas.transform);
            L2 = Level2Robot.Create(_canvas.transform);
            L3Content = Level3ContentBased.Create(_canvas.transform);
            L3 = Level3Mainframe.Create(_canvas.transform);
            L4 = Level4Corkboard.Create(_canvas.transform);

            if (Storefront == null) Storefront = StorefrontView.Create(_canvas.transform);
            if (Hud == null) Hud = Hud.Create(_canvas.transform);
            if (Comms == null) Comms = CommsBox.Create(_canvas.transform);

            // Dedicated production scenes set StartPhaseOverride so designers can open a
            // level scene and immediately see/play that level in context. The legacy
            // all-in-one scene leaves this at -1 and runs the full intro/hub flow.
            if (StartPhaseOverride >= 0) StartDedicatedScene(StartPhaseOverride);
            else Intro();
        }

        // ---- Scene plumbing ----------------------------------------------
        void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Lino;
            cam.orthographic = true;
            if (cam.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
        }

        void EnsureManagers()
        {
            if (GameManager.I == null) new GameObject("GameManager").AddComponent<GameManager>();
            if (AudioTension.I == null) new GameObject("Audio").AddComponent<AudioTension>();
            if (MusicManager.I == null) new GameObject("Music").AddComponent<MusicManager>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) AudioTension.I.gameObject.AddComponent<AudioListener>();
        }

        void EnsureCanvasAndEventSystem()
        {
            if (_canvas == null) _canvas = Object.FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                var cgo = new GameObject("MadFactCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = cgo.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.pixelPerfect = true;
                var scaler = cgo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(960, 540);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>(); // project uses the new Input System
            }
        }

        // ---- Narrative flow ----------------------------------------------
        void Intro()
        {
            if (!GameManager.I.HasActiveRun) GameManager.I.ResetForNewGame();
            GameManager.I.GoTo(Phase.Storefront);
            Storefront.SetLine(2);
            Storefront.SetEnterVisible(false);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.IntroOldDude, () =>
            {
                Storefront.SetEnterVisible(true);
                GoStorefront();
            });
        }

        public void GoStorefront()
        {
            CloseAllLevels();
            GameManager.I.GoTo(Phase.Storefront);
            Storefront.SetEra(_currentLevel);   // the shop itself upgrades between levels

            switch (_currentLevel)
            {
                case 1:
                    Storefront.SetLine(4);
                    Storefront.SetEnter("APPROACH THE COUNTER", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 1 — serve the line by hand. Match the tape to the taste.");
                    break;
                case 2:
                    Storefront.SetLine(10);
                    Storefront.SetEnter("BOOT THE ROBOT ASSISTANT", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 2 — the line is huge. Program rules and let the robot serve.");
                    break;
                case 3:
                    Storefront.SetLine(12);
                    Storefront.SetEnter("SORT BY MOVIE FEATURES", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 3 — content-based recommendation. Match item features to stated needs.");
                    break;
                case 4:
                    Storefront.SetLine(16);
                    Storefront.SetEnter("POWER ON THE MAINFRAME", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 4 — collaborative filtering. Learn hidden taste from the matrix.");
                    break;
                case 5:
                    Storefront.SetLine(0);
                    Storefront.SetEnter("GO TO THE CORKBOARD", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 5 — market gap research. Make the movie people are starving for.");
                    break;
            }
            Storefront.SetEnterVisible(true);
        }

        void CloseAllLevels()
        {
            if (L1 != null) L1.Close();
            if (L2 != null) L2.Close();
            if (L3Content != null) L3Content.Close();
            if (L3 != null) L3.Close();
            if (L4 != null) L4.Close();
        }

        void StartDedicatedScene(int sceneLevel)
        {
            _currentLevel = Mathf.Clamp(sceneLevel, 0, 5);
            Storefront.SetEnterVisible(false);

            if (_currentLevel == 0)
            {
                GameManager.I.PrepareStandaloneLevel(1);
                _currentLevel = 1;
                GoStorefront();
                return;
            }

            GameManager.I.PrepareStandaloneLevel(_currentLevel);
            Storefront.SetEra(_currentLevel);
            CloseAllLevels();
            EnterCurrentLevel();
        }

        void EnterCurrentLevel()
        {
            if (AudioTension.I != null) AudioTension.I.Whir();
            switch (_currentLevel)
            {
                case 1: GameManager.I.GoTo(Phase.Level1); L1.Open(); break;
                case 2: GameManager.I.GoTo(Phase.Level2); L2.Open(); break;
                case 3: GameManager.I.GoTo(Phase.Level3); L3Content.Open(); break;
                case 4: GameManager.I.GoTo(Phase.Level4); L3.Open(); break;
                case 5: GameManager.I.GoTo(Phase.Level5); L4.Open(); break;
            }
        }

        public void OnLevel1Goal()
        {
            _currentLevel = 2;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level1GoalOldDude(GameManager.I.Money),
                () => Comms.Show(Speaker.Robot, NarrativeDatabase.Level1GoalRobot, GoStorefront));
        }

        public void OnLevel2Goal()
        {
            _currentLevel = 3;
            Comms.Show(Speaker.Robot, NarrativeDatabase.Level2GoalRobot,
                () => Comms.Show(Speaker.OldDude, NarrativeDatabase.Level2GoalOldDude, GoStorefront));
        }

        public void OnContentBasedGoal()
        {
            _currentLevel = 4;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.ContentBasedCompleteOldDude, GoStorefront);
        }

        public void OnLevel4Goal()
        {
            _currentLevel = 5;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level4GoalOldDude,
                () => { GoStorefront(); L4.Open(); GameManager.I.GoTo(Phase.Level5); });
        }

        public void OnGreenlit()
        {
            GameManager.I.GoTo(Phase.Win);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.GreenlitOldDude, ShowWin);
        }

        void ShowWin()
        {
            if (_winPanel != null) { _winPanel.SetActive(true); return; }
            var dim = UIFactory.Image(_canvas.transform, "Win", new Color(0, 0, 0, 0.65f));
            UIFactory.Fill(UIFactory.RT(dim.gameObject));
            _winPanel = dim.gameObject;
            var card = UIFactory.DialogWindow(dim.transform, "Card", Theme.Face);
            UIFactory.Place(UIFactory.RT(card.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 320), Vector2.zero);
            UIFactory.Place(UIFactory.RT(UIFactory.Text(card.transform, "t", "★  BLOCKBUSTER GREENLIT  ★", 22, Theme.TitleText, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 32), new Vector2(0, -6));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(card.transform, "b",
                $"You inherited a failing store and rebuilt it with math.\n\nFinal balance:  ${GameManager.I.Money}\n\nManual  →  Rules  →  Content  →  Collaborative Filtering  →  Insight\n\nYou didn't just compute the error. You FELT it.",
                16, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperCenter, true).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 190), new Vector2(0, -54));
            var back = UIFactory.Button(card.transform, "Back", "RETURN TO STORE", () => { _winPanel.SetActive(false); _currentLevel = 5; GoStorefront(); }, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(back.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(220, 40), new Vector2(0, 24));
            UIFactory.ButtonIcon(back, ArtSprites.Back(), 28f);
        }
    }
}
