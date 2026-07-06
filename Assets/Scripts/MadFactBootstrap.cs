using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace MadFact
{
    /// <summary>
    /// The conductor. Drop this on a single empty GameObject in an empty scene and it builds
    /// the entire MadFact game: managers, audio, camera, canvas, all four levels, and the
    /// narrative flow that ties them together.
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
        public Level3Mainframe L3;
        public Level4Corkboard L4;

        public bool Level1Cleared, Level2Cleared, Level3Cleared;
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
            // retained as a safety net for empty test scenes and rapid prototyping.
            bool authored = Storefront != null && Hud != null && Comms != null &&
                L1 != null && L2 != null && L3 != null && L4 != null;
            if (authored) RuntimeSkin.Apply(_canvas.transform);
            if (!authored)
            {
                Storefront = StorefrontView.Create(_canvas.transform);
                Hud = Hud.Create(_canvas.transform);
                L1 = Level1Counter.Create(_canvas.transform);
                L2 = Level2Robot.Create(_canvas.transform);
                L3 = Level3Mainframe.Create(_canvas.transform);
                L4 = Level4Corkboard.Create(_canvas.transform);
                Comms = CommsBox.Create(_canvas.transform);
            }

            Intro();
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
            GameManager.I.GoTo(Phase.Storefront);
            Storefront.SetLine(2);
            Storefront.SetEnterVisible(false);
            Comms.Show(Speaker.OldDude, new[]
            {
                "So. You actually showed up to claim the place. PELLINGS VIDEO. My life's work.",
                "Forty years I matched folks to tapes by hand. My back's done. The shop's yours now, kid.",
                "Problem is... the line never stops growing, and nobody can guess what people want.",
                "Figure it out. Match the customer to the tape. Make me proud. And make some money."
            }, () =>
            {
                Storefront.SetEnterVisible(true);
                GoStorefront();
            });
        }

        public void GoStorefront()
        {
            L1.Close(); L2.Close(); L3.Close(); L4.Close();
            GameManager.I.GoTo(Phase.Storefront);

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
                    Storefront.SetLine(16);
                    Storefront.SetEnter("POWER ON THE MAINFRAME", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 3 — rules failed. Boot the Matrix Factorization mainframe.");
                    break;
                case 4:
                    Storefront.SetLine(0);
                    Storefront.SetEnter("GO TO THE CORKBOARD", EnterCurrentLevel);
                    Storefront.SetSubtitle("LEVEL 4 — you found a market gap. Go make the movie.");
                    break;
            }
            Storefront.SetEnterVisible(true);
        }

        void EnterCurrentLevel()
        {
            if (AudioTension.I != null) AudioTension.I.Whir();
            switch (_currentLevel)
            {
                case 1: GameManager.I.GoTo(Phase.Level1); L1.Open(); break;
                case 2: GameManager.I.GoTo(Phase.Level2); L2.Open(); break;
                case 3: GameManager.I.GoTo(Phase.Level3); L3.Open(); break;
                case 4: GameManager.I.GoTo(Phase.Level4); L4.Open(); break;
            }
        }

        public void OnLevel1Goal()
        {
            _currentLevel = 2;
            Comms.Show(Speaker.OldDude, new[]
            {
                $"${GameManager.I.Money}! Look at you. But your hand's cramping and the line's out the door.",
                "My nephew left a robot assistant in the back. Beige thing. Talks funny.",
                "Teach it some rules. Let IT do the matching. That's called AUTOMATION, kid."
            }, () => Comms.Show(Speaker.Robot, new[]
            {
                "GREETINGS PROPRIETOR. I AM UNIT B-EIGE.",
                "PROVIDE ME WITH IF/THEN RULES. I WILL SERVE THE LINE WITHOUT REST.",
                "WARNING: I DO EXACTLY WHAT YOU SAY. NOTHING MORE."
            }, GoStorefront));
        }

        public void OnLevel2Goal()
        {
            _currentLevel = 3;
            Comms.Show(Speaker.Robot, new[]
            {
                "PROPRIETOR. MY RULES ARE TOO RIGID FOR REAL PEOPLE.",
                "TASTE IS CONTINUOUS. RULES ARE NOT. I HAVE REACHED MY LIMIT."
            }, () => Comms.Show(Speaker.OldDude, new[]
            {
                "There's an old mainframe in the basement. Cost me a fortune in '91.",
                "It doesn't use rules. It learns hidden 'vibes' — numbers behind the taste.",
                "They call it MATRIX FACTORIZATION. Go on. Boot it up."
            }, GoStorefront));
        }

        public void OnLevel3Goal()
        {
            _currentLevel = 4;
            Comms.Show(Speaker.OldDude, new[]
            {
                "You see that cluster? Rates EVERYTHING we stock a one or a two.",
                "Look at the math — their vibe is high SPOOKY and high FUNNY. Spook-comedy!",
                "We never stocked a single one. That's not a problem, kid. That's a GOLDMINE.",
                "We've got the budget. Go to the corkboard and MAKE the movie they're starving for."
            }, () => { GoStorefront(); L4.Open(); GameManager.I.GoTo(Phase.Level4); });
        }

        public void OnGreenlit()
        {
            GameManager.I.GoTo(Phase.Win);
            Comms.Show(Speaker.OldDude, new[]
            {
                "THAT'S IT. That's the one. Spooky AND funny — exactly what the numbers screamed for.",
                "You went from matching tapes by hand to PRODUCING the blockbuster the data predicted.",
                "From manual, to rules, to the algorithm. You learned to feel the math, kid. Proud of you."
            }, ShowWin);
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
                $"You inherited a failing store and rebuilt it with math.\n\nFinal balance:  ${GameManager.I.Money}\n\nManual  →  Rules  →  Matrix Factorization  →  Insight\n\nYou didn't just compute the error. You FELT it.",
                16, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperCenter, true).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 190), new Vector2(0, -54));
            var back = UIFactory.Button(card.transform, "Back", "RETURN TO STORE", () => { _winPanel.SetActive(false); _currentLevel = 4; GoStorefront(); }, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(back.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(220, 40), new Vector2(0, 24));
            UIFactory.ButtonIcon(back, ArtSprites.Back(), 28f);
        }
    }
}
