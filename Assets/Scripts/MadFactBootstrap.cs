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
        public Level3RatingsTable L3Ratings;
        public Level6ContentBased L3Content;
        public Level3Mainframe L3;
        public Level7Corkboard L4;
        public Level8PosterStudio L8;

        public bool Level1Cleared, Level2Cleared, Level3Cleared, Level4Cleared, Level6Cleared;
        [Tooltip("-1 = normal full-game intro, 0 = storefront hub, 1..8 = start directly in that dedicated level scene.")]
        public int StartPhaseOverride = -1;
        int _currentLevel = 1;
        int _capturedEntryLevel = -1;
        bool _built;

        [SerializeField] Canvas _canvas;
        GameObject _winPanel;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void OnDestroy()
        {
            if (GameManager.I == null) return;
            GameManager.I.OnBankrupt -= OnBankrupt;
            GameManager.I.OnTrustCollapsed -= OnTrustCollapsed;
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

            // Keep scene-authored level instances so prefab and Scene-view edits remain
            // authoritative. The code builders are fallback paths for incomplete scenes.
            EnsureLevels();

            // Authored level roots are intentionally visible in Edit Mode. Normalize
            // runtime state before selecting the one that belongs to this phase.
            CloseAllLevels();
            if (StartPhaseOverride >= 0) StartDedicatedScene(StartPhaseOverride);
            else Intro();
        }

        /// <summary>Keep authored level instances and build only missing scene content.</summary>
        void EnsureLevels()
        {
            bool all = StartPhaseOverride == -1;

            if ((all || StartPhaseOverride == 1) && L1 == null)
                L1 = Level1Counter.Create(_canvas.transform);
            if ((all || StartPhaseOverride == 2) && L2 == null)
                L2 = Level2Robot.Create(_canvas.transform);
            if ((all || StartPhaseOverride == 3) && L3Ratings == null)
                L3Ratings = Level3RatingsTable.Create(_canvas.transform);
            if ((all || StartPhaseOverride == 4 || StartPhaseOverride == 5) && L3 == null)
                L3 = Level3Mainframe.Create(_canvas.transform);
            if ((all || StartPhaseOverride == 6) && L3Content == null)
                L3Content = Level6ContentBased.Create(_canvas.transform);
            if ((all || StartPhaseOverride == 7) && L4 == null)
                L4 = Level7Corkboard.Create(_canvas.transform);
            if ((all || StartPhaseOverride == 8) && L8 == null)
                L8 = Level8PosterStudio.Create(_canvas.transform);

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
            yield return new WaitWhile(() => Comms != null && Comms.IsShowing);
            MadFactLokiLogger.Instance?.Log("level_bankrupt", "Player went bankrupt and the level restarted", new
            {
                level_id = _currentLevel,
                money_before_reset = GameManager.I.Money,
                reset_to = GameManager.I.LevelEntryMoney
            });
            GameManager.I.SetMoney(GameManager.I.LevelEntryMoney);
            Comms.Show(Speaker.OldDude, new[]
            {
                "We ran out of money, so this level will restart with its starting cash.",
                "You will return to the storefront and begin again."
            }, RestartCurrentLevelFromBeginning);
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
            yield return new WaitWhile(() => Comms != null && Comms.IsShowing);
            MadFactLokiLogger.Instance?.Log("level_trust_collapsed", "Trust hit zero and the level restarted", new
            {
                level_id = _currentLevel,
                trust_before_reset = GameManager.I.Trust,
                reset_to = GameManager.I.LevelEntryTrust
            });
            GameManager.I.SetTrust(GameManager.I.LevelEntryTrust);
            Comms.Show(Speaker.OldDude, new[]
            {
                "Trust reached zero, so this level will restart with its starting trust.",
                "You will return to the storefront and begin again."
            }, RestartCurrentLevelFromBeginning);
        }

        void RestartCurrentLevelFromBeginning()
        {
            int level = Mathf.Clamp(_currentLevel, 1, LevelSceneCatalog.MaxPlayableLevel);
            Phase phase = (Phase)((int)Phase.Level1 + level - 1);
            GameManager.I.Run.ResetPhase(
                phase,
                level == 1 ? ScenarioDatabase.Level1Track : null,
                clearFlags: level == 1);

            if (level == 1) Level1Cleared = false;
            if (level == 2) Level2Cleared = false;
            if (level == 3) Level3Cleared = false;
            if (level == 4 || level == 5) Level4Cleared = false;
            if (level == 6) Level6Cleared = false;

            SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(level), LoadSceneMode.Single);
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
                    Storefront.SetEnter("OPEN LEVEL 1 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 1: Serve the line by hand. Match the tape to the taste.");
                    break;
                case 2:
                    Storefront.SetLine(10);
                    Storefront.SetEnter("OPEN LEVEL 2 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 2: The line is huge. Program rules and let the robot serve.");
                    break;
                case 3:
                    Storefront.SetLine(12);
                    Storefront.SetEnter("OPEN LEVEL 3 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 3: Ratings table. Read each customer's zero-to-five-star movie ratings.");
                    break;
                case 4:
                    Storefront.SetLine(16);
                    Storefront.SetEnter("OPEN LEVEL 4 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 4: Collaborative filtering. Predict missing ratings from similar customers.");
                    break;
                case 5:
                    Storefront.SetLine(16);
                    Storefront.SetEnter("OPEN LEVEL 5 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 5: Matrix factorization. Learn hidden taste factors from the ratings.");
                    break;
                case 6:
                    Storefront.SetLine(12);
                    Storefront.SetEnter("OPEN LEVEL 6 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 6: Content-based recommendation. Match item features to stated needs.");
                    break;
                case 7:
                    Storefront.SetLine(0);
                    Storefront.SetEnter("OPEN LEVEL 7 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 7: Market gap research. Build a categorized brief for a missing movie.");
                    break;
                case 8:
                    Storefront.SetLine(0);
                    Storefront.SetEnter("OPEN LEVEL 8 TASKS", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 8: Poster lab. Edit the prompt, choose a style, and compare generated drafts.");
                    break;
            }
            Storefront.SetEnterVisible(true);
        }

        void CloseAllLevels()
        {
            if (L1 != null) L1.Close();
            if (L2 != null) L2.Close();
            if (L3Ratings != null) L3Ratings.Close();
            if (L3Content != null) L3Content.Close();
            if (L3 != null) L3.Close();
            if (L4 != null) L4.Close();
            if (L8 != null) L8.Close();
        }

        void StartDedicatedScene(int sceneLevel)
        {
            _currentLevel = Mathf.Clamp(sceneLevel, 0, LevelSceneCatalog.MaxPlayableLevel);
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
            CaptureLevelEntry();
            Storefront.SetEra(_currentLevel);
            CloseAllLevels();
            GoStorefront();
        }

        void EnterCurrentLevel()
        {
            // The Storefront hub scene deliberately contains no level view. When the player
            // enters from there, hand off to the matching authored level scene — the
            // persistent GameManager carries the active run across the load.
            bool levelIsBuiltHere =
                (_currentLevel == 1 && L1 != null) ||
                (_currentLevel == 2 && L2 != null) ||
                (_currentLevel == 3 && L3Ratings != null) ||
                ((_currentLevel == 4 || _currentLevel == 5) && L3 != null) ||
                (_currentLevel == 6 && L3Content != null) ||
                (_currentLevel == 7 && L4 != null) ||
                (_currentLevel == 8 && L8 != null);

            if (!levelIsBuiltHere)
            {
                GameManager.I.PrepareStandaloneLevel(_currentLevel);
                SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(_currentLevel));
                return;
            }

            if (AudioTension.I != null) AudioTension.I.Whir();
            GameManager.I.PrepareStandaloneLevel(_currentLevel);
            CaptureLevelEntry();
            switch (_currentLevel)
            {
                case 1: GameManager.I.GoTo(Phase.Level1); L1.Open(); break;
                case 2: GameManager.I.GoTo(Phase.Level2); L2.Open(); break;
                case 3: GameManager.I.GoTo(Phase.Level3); L3Ratings.Open(); break;
                case 4: GameManager.I.GoTo(Phase.Level4); L3.Open(); break;
                case 5: GameManager.I.GoTo(Phase.Level5); L3.Open(); break;
                case 6: GameManager.I.GoTo(Phase.Level6); L3Content.Open(); break;
                case 7: GameManager.I.GoTo(Phase.Level7); L4.Open(); break;
                case 8: GameManager.I.GoTo(Phase.Level8); L8.Open(); break;
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

        void CaptureLevelEntry()
        {
            if (_capturedEntryLevel == _currentLevel) return;
            _capturedEntryLevel = _currentLevel;
            GameManager.I.LevelEntryMoney = GameManager.I.Money;
            GameManager.I.LevelEntryTrust = GameManager.I.Trust;
        }

        void BringHudToFront()
        {
            if (Hud != null) Hud.transform.SetAsLastSibling();
        }

        public void OnLevel1Goal()
        {
            LogLevelCompleted(1);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level1GoalOldDude(GameManager.I.Money),
                () => Comms.Show(Speaker.Robot, NarrativeDatabase.Level1GoalRobot,
                    () => FinishLevel(1)));
        }

        public void OnLevel2Goal()
        {
            LogLevelCompleted(2);
            // Level2Robot's requested-rewatch scene already delivers the transition:
            // rigid global rules cannot personalize recommendations. Avoid repeating
            // that lesson with another multi-step dialogue before advancing.
            FinishLevel(2);
        }

        public void OnContentBasedGoal()
        {
            LogLevelCompleted(6);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.ContentBasedCompleteOldDude,
                () => FinishLevel(6));
        }

        public void OnRatingsTableGoal()
        {
            LogLevelCompleted(3);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.RatingsTableCompleteOldDude,
                () => FinishLevel(3));
        }

        public void OnCollaborativeFilteringGoal()
        {
            LogLevelCompleted(4);
            FinishLevel(4);
        }

        public void OnMatrixFactorizationGoal()
        {
            LogLevelCompleted(5);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.Level4GoalOldDude,
                () => FinishLevel(5));
        }

        void FinishLevel(int level)
        {
            GameManager.I.MarkLevelCompleted(level);

            int nextLevel = LevelSceneCatalog.NextLevelInSameDay(level);
            if (nextLevel > 0)
            {
                // Re-arm Continue for the next level while preserving this run's money,
                // trust, logger session, matrix, and choices. The dedicated scene opens
                // on that level's storefront before its tasks.
                GameManager.I.PrepareStandaloneLevel(nextLevel);
                MadFactLokiLogger.Instance?.Log("level_advanced", "Advanced to next level in the same day", new
                {
                    completed_level_id = level,
                    next_level_id = nextLevel
                });
                SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(nextLevel), LoadSceneMode.Single);
                return;
            }

            MenuPauseCoordinator.ReturnToMenu();
        }

        public void OnGreenlit()
        {
            LogLevelCompleted(7);
            Comms.Show(Speaker.OldDude, new[]
            {
                "Great brief. Next, turn those ideas into an editable prompt and create the poster."
            }, () => FinishLevel(7));
        }

        public void OnPosterSelected()
        {
            LogLevelCompleted(8);
            MadFactLokiLogger.Instance?.Log("game_completed", "Player completed MadFact", new
            {
                money = GameManager.I.Money,
                trust = GameManager.I.Trust,
                recommendations = GameManager.I.Run.Recommendations.Count,
                poster_generations = GameManager.I.Run.PosterGenerations.Count,
                selected_poster = GameManager.I.Run.SelectedPosterIndex + 1
            });
            GameManager.I.GoTo(Phase.Win);
            Comms.Show(Speaker.OldDude, NarrativeDatabase.GreenlitOldDude,
                () => FinishLevel(8));
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
