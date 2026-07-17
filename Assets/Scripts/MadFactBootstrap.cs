using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MadFact
{
    /// <summary>
    /// Coordinates the scene-authored storefront, HUD, dialogue box, and level view.
    /// Production scenes own their visible UI hierarchy; runtime construction is an
    /// explicit compatibility fallback rather than the normal setup path.
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
        [SerializeField, Tooltip("Compatibility only. When enabled, missing scene UI is built at runtime. Production scenes should leave this disabled.")]
        bool _allowRuntimeFallback;
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

            if (_allowRuntimeFallback) CreateMissingRuntimeContent();
            if (!ValidateAuthoredScene()) return;

            RuntimeSkin.Apply(_canvas.transform);

            // Level 1 deliberately keeps the original store. Staging supplied dedicated
            // art for Levels 2-4, which the split scenes now select at runtime.
            Storefront.SetEra(StartPhaseOverride);

            // Dedicated production scenes set StartPhaseOverride so designers can open a
            // level scene and immediately see/play that level in context. The legacy
            // all-in-one scene leaves this at -1 and runs the full intro/hub flow.
            // Authored level roots are intentionally visible in Edit Mode. Normalize their
            // runtime state before selecting the one that belongs to this phase.
            CloseAllLevels();
            if (StartPhaseOverride >= 0) StartDedicatedScene(StartPhaseOverride);
            else Intro();
        }

        void CreateMissingRuntimeContent()
        {
            if (Storefront == null) Storefront = StorefrontView.Create(_canvas.transform);
            if (Hud == null) Hud = Hud.Create(_canvas.transform);
            if (Comms == null) Comms = CommsBox.Create(_canvas.transform);

            // Build only the level required by this scene. The old behaviour created all
            // five levels even when a dedicated scene needed just one of them.
            if ((StartPhaseOverride == -1 || StartPhaseOverride == 1) && L1 == null)
                L1 = Level1Counter.Create(_canvas.transform);
            if ((StartPhaseOverride == -1 || StartPhaseOverride == 2) && L2 == null)
                L2 = Level2Robot.Create(_canvas.transform);
            if ((StartPhaseOverride == -1 || StartPhaseOverride == 3) && L3Content == null)
                L3Content = Level3ContentBased.Create(_canvas.transform);
            if ((StartPhaseOverride == -1 || StartPhaseOverride == 4) && L3 == null)
                L3 = Level3Mainframe.Create(_canvas.transform);
            if ((StartPhaseOverride == -1 || StartPhaseOverride == 5) && L4 == null)
                L4 = Level4Corkboard.Create(_canvas.transform);
        }

        bool ValidateAuthoredScene()
        {
            bool valid = true;
            valid &= Require(Storefront, nameof(Storefront));
            valid &= Require(Hud, nameof(Hud));
            valid &= Require(Comms, nameof(Comms));

            if (StartPhaseOverride == -1 || StartPhaseOverride == 1) valid &= Require(L1, nameof(L1));
            if (StartPhaseOverride == -1 || StartPhaseOverride == 2) valid &= Require(L2, nameof(L2));
            if (StartPhaseOverride == -1 || StartPhaseOverride == 3) valid &= Require(L3Content, nameof(L3Content));
            if (StartPhaseOverride == -1 || StartPhaseOverride == 4) valid &= Require(L3, nameof(L3));
            if (StartPhaseOverride == -1 || StartPhaseOverride == 5) valid &= Require(L4, nameof(L4));

            if (!valid)
            {
                Debug.LogError($"{name} is missing required scene-authored UI. Add the matching prefab to the Canvas and assign it on MadFactBootstrap. Runtime fallback is intentionally disabled for production scenes.", this);
                enabled = false;
            }
            return valid;
        }

        bool Require(Object value, string fieldName)
        {
            if (value != null) return true;
            Debug.LogError($"Missing MadFactBootstrap.{fieldName} in scene '{SceneManager.GetActiveScene().name}'.", this);
            return false;
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
            BringHudToFront();
            GameManager.I.GoTo(Phase.Storefront);
            Storefront.SetEra(_currentLevel);

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
            // The authored dialogue prefab contains preview copy so designers can inspect
            // its layout. Dedicated scenes must hide that preview before gameplay starts.
            Comms.Hide();
            Storefront.SetEnterVisible(false);

            if (_currentLevel == 0)
            {
                if (!GameManager.I.HasActiveRun) GameManager.I.ResetForNewGame();
                _currentLevel = 1;
                Intro();
                return;
            }

            GameManager.I.PrepareStandaloneLevel(_currentLevel);
            CloseAllLevels();
            EnterCurrentLevel();
        }

        void EnterCurrentLevel()
        {
            // The Storefront hub deliberately contains no level view. When the player
            // enters from that scene, hand off to the matching authored level scene.
            // The persistent GameManager carries the active run across the load.
            bool levelIsAuthoredHere =
                (_currentLevel == 1 && L1 != null) ||
                (_currentLevel == 2 && L2 != null) ||
                (_currentLevel == 3 && L3Content != null) ||
                (_currentLevel == 4 && L3 != null) ||
                (_currentLevel == 5 && L4 != null);

            if (!levelIsAuthoredHere)
            {
                GameManager.I.PrepareStandaloneLevel(_currentLevel);
                SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(_currentLevel));
                return;
            }

            if (AudioTension.I != null) AudioTension.I.Whir();
            switch (_currentLevel)
            {
                case 1: GameManager.I.GoTo(Phase.Level1); L1.Open(); break;
                case 2: GameManager.I.GoTo(Phase.Level2); L2.Open(); break;
                case 3: GameManager.I.GoTo(Phase.Level3); L3Content.Open(); break;
                case 4: GameManager.I.GoTo(Phase.Level4); L3.Open(); break;
                case 5: GameManager.I.GoTo(Phase.Level5); L4.Open(); break;
            }
            BringHudToFront();
        }

        void BringHudToFront()
        {
            if (Hud != null) Hud.transform.SetAsLastSibling();
        }

        public void OnLevel1Goal()
        {
            _currentLevel = 2;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level1GoalOldDude(GameManager.I.Money),
                () => Comms.Show(Speaker.Robot, NarrativeDatabase.Level1GoalRobot, ContinueAfterLevelGoal));
        }

        public void OnLevel2Goal()
        {
            _currentLevel = 3;
            Comms.Show(Speaker.Robot, NarrativeDatabase.Level2GoalRobot,
                () => Comms.Show(Speaker.OldDude, NarrativeDatabase.Level2GoalOldDude, ContinueAfterLevelGoal));
        }

        public void OnContentBasedGoal()
        {
            _currentLevel = 4;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.ContentBasedCompleteOldDude, ContinueAfterLevelGoal);
        }

        public void OnLevel4Goal()
        {
            _currentLevel = 5;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level4GoalOldDude,
                ContinueAfterLevelGoal);
        }

        void ContinueAfterLevelGoal()
        {
            // Directly-played production levels advance through production scenes. The
            // legacy full-game scene keeps its original storefront interstitials.
            if (StartPhaseOverride > 0)
                SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(_currentLevel));
            else
                GoStorefront();
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
