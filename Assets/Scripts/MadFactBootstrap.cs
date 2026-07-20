using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Coordinates the scene-authored storefront, HUD, dialogue box, and level views.
    /// Scenes own the environment shell (storefront, HUD, comms); the level panels
    /// themselves are rebuilt from code at startup so their layout and wiring always
    /// match the current source, even when a scene's authored instance has drifted.
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

            RuntimeSkin.Apply(_canvas.transform);

            // The environment shell may be scene-authored; anything missing is built here.
            if (Storefront == null) Storefront = StorefrontView.Create(_canvas.transform);
            if (Hud == null) Hud = Hud.Create(_canvas.transform);
            if (Comms == null) Comms = CommsBox.Create(_canvas.transform);

            // Levels are iterating quickly in code. Rebuild the ones this scene needs from
            // script — authored instances are art shells whose layout/wiring may predate
            // the current source (poster browser, engine picks, scenario beats...).
            RebuildLevels();

            // Authored level roots are intentionally visible in Edit Mode. Normalize
            // runtime state before selecting the one that belongs to this phase.
            CloseAllLevels();
            if (StartPhaseOverride >= 0) StartDedicatedScene(StartPhaseOverride);
            else Intro();
        }

        /// <summary>Destroy authored level instances and build only what this scene needs.</summary>
        void RebuildLevels()
        {
            bool all = StartPhaseOverride == -1;

            if (L1 != null) Destroy(L1.gameObject);
            if (L2 != null) Destroy(L2.gameObject);
            if (L3Content != null) Destroy(L3Content.gameObject);
            if (L3 != null) Destroy(L3.gameObject);
            if (L4 != null) Destroy(L4.gameObject);

            L1 = (all || StartPhaseOverride == 1) ? Level1Counter.Create(_canvas.transform) : null;
            L2 = (all || StartPhaseOverride == 2) ? Level2Robot.Create(_canvas.transform) : null;
            L3Content = (all || StartPhaseOverride == 3) ? Level3ContentBased.Create(_canvas.transform) : null;
            L3 = (all || StartPhaseOverride == 4) ? Level3Mainframe.Create(_canvas.transform) : null;
            L4 = (all || StartPhaseOverride == 5) ? Level4Corkboard.Create(_canvas.transform) : null;

            BringHudToFront();
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
            GameManager.I.OnBankrupt -= OnBankrupt;
            GameManager.I.OnBankrupt += OnBankrupt;
            GameManager.I.OnTrustCollapsed -= OnTrustCollapsed;
            GameManager.I.OnTrustCollapsed += OnTrustCollapsed;
        }

        void OnBankrupt() => StartCoroutine(BankruptcyFlow());

        /// <summary>
        /// Waits a frame so whichever coroutine pushed Money negative (a batch run, a sale)
        /// finishes its own synchronous work first, then rolls back to this level's entry
        /// balance and restarts it fresh.
        /// </summary>
        IEnumerator BankruptcyFlow()
        {
            yield return null;
            MadFactLokiLogger.Instance?.Log("level_bankrupt", "Player went bankrupt and the level restarted", new
            {
                level_id = _currentLevel,
                money_before_reset = GameManager.I.Money,
                reset_to = GameManager.I.LevelEntryMoney
            });
            GameManager.I.SetMoney(GameManager.I.LevelEntryMoney);
            Comms.Show(Speaker.OldDude, new[]
            {
                "Whoa — hold it. We just went BANKRUPT, kid. Negative dollars. That's not a real number of dollars to have.",
                "Deep breath. We're resetting the till back to where you walked in and running this level again.",
                "Same problem, clean slate. Go get 'em."
            }, () => { CloseAllLevels(); EnterCurrentLevel(); });
        }

        void OnTrustCollapsed() => StartCoroutine(TrustCollapseFlow());

        /// <summary>
        /// Mirrors BankruptcyFlow for the trust meter: waits a frame so whichever coroutine
        /// dropped Trust to zero finishes its own synchronous work first, then rolls back to
        /// this level's entry trust and restarts it fresh.
        /// </summary>
        IEnumerator TrustCollapseFlow()
        {
            yield return null;
            MadFactLokiLogger.Instance?.Log("level_trust_collapsed", "Trust hit zero and the level restarted", new
            {
                level_id = _currentLevel,
                trust_before_reset = GameManager.I.Trust,
                reset_to = GameManager.I.LevelEntryTrust
            });
            GameManager.I.SetTrust(GameManager.I.LevelEntryTrust);
            Comms.Show(Speaker.OldDude, new[]
            {
                "Whoa — hold it. Trust just hit ZERO, kid. Nobody in this town believes a word we say anymore.",
                "Deep breath. We're resetting trust back to where you walked in and running this level again.",
                "Same problem, clean slate. Watch the customers this time."
            }, () => { CloseAllLevels(); EnterCurrentLevel(); });
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
            Storefront.SetEra(_currentLevel);
            CloseAllLevels();
            EnterCurrentLevel();
        }

        void EnterCurrentLevel()
        {
            // The Storefront hub scene deliberately contains no level view. When the player
            // enters from there, hand off to the matching authored level scene — the
            // persistent GameManager carries the active run across the load.
            bool levelIsBuiltHere =
                (_currentLevel == 1 && L1 != null) ||
                (_currentLevel == 2 && L2 != null) ||
                (_currentLevel == 3 && L3Content != null) ||
                (_currentLevel == 4 && L3 != null) ||
                (_currentLevel == 5 && L4 != null);

            if (!levelIsBuiltHere)
            {
                GameManager.I.PrepareStandaloneLevel(_currentLevel);
                SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(_currentLevel));
                return;
            }

            if (AudioTension.I != null) AudioTension.I.Whir();
            GameManager.I.LevelEntryMoney = GameManager.I.Money;
            GameManager.I.LevelEntryTrust = GameManager.I.Trust;
            switch (_currentLevel)
            {
                case 1: GameManager.I.GoTo(Phase.Level1); L1.Open(); break;
                case 2: GameManager.I.GoTo(Phase.Level2); L2.Open(); break;
                case 3: GameManager.I.GoTo(Phase.Level3); L3Content.Open(); break;
                case 4: GameManager.I.GoTo(Phase.Level4); L3.Open(); break;
                case 5: GameManager.I.GoTo(Phase.Level5); L4.Open(); break;
            }
            MadFactLokiLogger.Instance?.Log("level_started", "Level started", new
            {
                level_id = _currentLevel,
                phase = GameManager.I.Current.ToString(),
                money = GameManager.I.Money,
                trust = GameManager.I.Trust
            });
            BringHudToFront();
        }

        void BringHudToFront()
        {
            if (Hud != null) Hud.transform.SetAsLastSibling();
        }

        public void OnLevel1Goal()
        {
            LogLevelCompleted(1);
            _currentLevel = 2;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level1GoalOldDude(GameManager.I.Money),
                () => Comms.Show(Speaker.Robot, NarrativeDatabase.Level1GoalRobot, ContinueAfterLevelGoal));
        }

        public void OnLevel2Goal()
        {
            LogLevelCompleted(2);
            _currentLevel = 3;
            Comms.Show(Speaker.Robot, NarrativeDatabase.Level2GoalRobot,
                () => Comms.Show(Speaker.OldDude, NarrativeDatabase.Level2GoalOldDude, ContinueAfterLevelGoal));
        }

        public void OnContentBasedGoal()
        {
            LogLevelCompleted(3);
            _currentLevel = 4;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.ContentBasedCompleteOldDude, ContinueAfterLevelGoal);
        }

        public void OnLevel4Goal()
        {
            LogLevelCompleted(4);
            _currentLevel = 5;
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level4GoalOldDude, ContinueAfterLevelGoal);
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
            LogLevelCompleted(5);
            MadFactLokiLogger.Instance?.Log("game_completed", "Player completed MadFact", new
            {
                money = GameManager.I.Money,
                trust = GameManager.I.Trust,
                recommendations = GameManager.I.Run.Recommendations.Count
            });
            GameManager.I.GoTo(Phase.Win);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.GreenlitOldDude, ShowWin);
        }

        void LogLevelCompleted(int level)
        {
            MadFactLokiLogger.Instance?.Log("level_completed", "Level completed", new
            {
                level_id = level,
                money = GameManager.I.Money,
                trust = GameManager.I.Trust,
                recommendations = GameManager.I.Run.Recommendations.Count
            });
        }

        void ShowWin()
        {
            if (_winPanel != null) { _winPanel.SetActive(true); return; }
            var dim = UIFactory.Image(_canvas.transform, "Win", new Color(0, 0, 0, 0.65f));
            UIFactory.Fill(UIFactory.RT(dim.gameObject));
            _winPanel = dim.gameObject;
            var card = UIFactory.DialogWindow(dim.transform, "Card", Theme.Face);
            UIFactory.Place(UIFactory.RT(card.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 380), Vector2.zero);
            UIFactory.Place(UIFactory.RT(UIFactory.Text(card.transform, "t", "★  BLOCKBUSTER GREENLIT  ★", 22, Theme.TitleText, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 32), new Vector2(0, -6));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(card.transform, "b",
                "You inherited a failing store and rebuilt it with math.\n\nManual  →  Rules  →  Content  →  Collaborative Filtering  →  Insight\n\nYou didn't just compute the error. You FELT it.",
                16, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperCenter, true).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 130), new Vector2(0, -46));

            // this IS the end of the run — no button back to a playable hub, just the
            // numbers the whole game was building toward.
            var statsBox = UIFactory.Bevel(card.transform, "Stats", Theme.FaceShade, sunken: true);
            UIFactory.Place(UIFactory.RT(statsBox.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(500, 110), new Vector2(0, 20));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(statsBox.transform, "h", "FINAL RESULTS", 12, Theme.InkSoft, Theme.SystemSans, TextAnchor.UpperCenter, false, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(460, 18), new Vector2(0, -8));
            // UIFactory.Place takes ONE anchor point (used for both min/max) plus a FIXED
            // size — it doesn't do stretch regions. Three columns need three fixed-width
            // boxes centered at explicit x-offsets, not a fractional anchor split.
            UIFactory.Place(UIFactory.RT(UIFactory.Text(statsBox.transform, "trust", $"TRUST\n{GameManager.I.Trust}", 18, Theme.TitleText, Theme.Typewriter, TextAnchor.MiddleCenter, true, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 60), new Vector2(-165, -6));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(statsBox.transform, "money", $"NET MONEY\n${GameManager.I.Money}", 18, Theme.Cash, Theme.Typewriter, TextAnchor.MiddleCenter, true, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 60), new Vector2(0, -6));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(statsBox.transform, "recs", $"RECS SERVED\n{GameManager.I.Run.Recommendations.Count}", 18, Theme.InkSoft, Theme.Typewriter, TextAnchor.MiddleCenter, true, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 60), new Vector2(165, -6));
        }
    }
}
