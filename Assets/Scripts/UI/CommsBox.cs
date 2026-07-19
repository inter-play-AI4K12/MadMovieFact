using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    public enum Speaker { OldDude, Robot, System }

    /// <summary>
    /// The "Corporate Comms Box": a heavy beveled OS-alert window used for all narrative.
    ///  - Old Dude  : dithered low-res digitized photo + uneven typewriter font.
    ///  - Robot     : clean 1-bit Mac icon + rigid aliased system font (instant text).
    /// </summary>
    public class CommsBox : MonoBehaviour
    {
        [SerializeField] Image _portrait;
        [SerializeField] Text _name, _body;
        [SerializeField] Image _titleBar;
        [SerializeField] Text _titleText;
        [SerializeField] Button _next;
        [SerializeField] Image _nextIcon;
        [SerializeField] Text _nextLabel;
        Button _skip;

        readonly Queue<string> _queue = new Queue<string>();
        Speaker _speaker;
        Action _onComplete;
        string _full = "";
        float _revealed;
        bool _typing;
        bool _instant;

        static Sprite _oldDude, _robot, _sysIcon;

        void Awake()
        {
            if (_next == null) return;
            _next.onClick.RemoveAllListeners();
            _next.onClick.AddListener(Advance);
            var clicker = GetComponent<Button>();
            if (clicker != null)
            {
                clicker.onClick.RemoveAllListeners();
                clicker.onClick.AddListener(OnBoxClick);
            }
        }

        public static CommsBox Create(Transform parent)
        {
            var root = UIFactory.DialogWindow(parent, "CommsBox", new Color(0.055f, 0.075f, 0.085f, 0.98f));
            var rt = UIFactory.RT(root.gameObject);
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(820, 188);
            rt.anchoredPosition = new Vector2(0, 16);

            var cb = root.gameObject.AddComponent<CommsBox>();
            cb.Build(root.transform);
            root.gameObject.SetActive(false);
            return cb;
        }

        void Build(Transform root)
        {
            // The supplied window sprite owns the title-bar chrome.
            _titleBar = UIFactory.Image(root, "TitleBar", new Color(1, 1, 1, 0));
            var tbr = UIFactory.RT(_titleBar.gameObject);
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0.5f, 1f);
            tbr.sizeDelta = new Vector2(-8, 24); tbr.anchoredPosition = new Vector2(0, -6);
            _titleText = UIFactory.Text(_titleBar.transform, "T", "CORPORATE COMMS", 14, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_titleText.gameObject), 34, 0, 8, 0);
            var reel = UIFactory.Image(_titleBar.transform, "Reel", Color.white, ArtSprites.FilmReel(), Image.Type.Simple, false);
            reel.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(reel.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 22), new Vector2(7, 0));

            // portrait frame (sunken)
            var pf = UIFactory.Bevel(root, "PortraitFrame", Theme.FaceDark, sunken: true);
            UIFactory.Place(UIFactory.RT(pf.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, 118), new Vector2(16, -38));
            _portrait = UIFactory.Image(pf.transform, "Portrait", Color.white);
            _portrait.preserveAspect = true;
            UIFactory.Fill(UIFactory.RT(_portrait.gameObject), 4, 4, 4, 4);

            _name = UIFactory.Text(root, "Name", "", 14, Theme.CrtAmber, Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_name.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(650, 20), new Vector2(144, -40));

            _body = UIFactory.Text(root, "Body", "", 17, Theme.TitleText, Theme.Typewriter, TextAnchor.UpperLeft, true);
            _body.lineSpacing = 1.25f;
            UIFactory.Place(UIFactory.RT(_body.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(640, 92), new Vector2(144, -66));

            _next = UIFactory.Button(root, "Next", "NEXT", Advance, Theme.Face, 14);
            UIFactory.Place(UIFactory.RT(_next.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(124, 34), new Vector2(-16, 14));
            _nextIcon = UIFactory.ButtonIcon(_next, ArtSprites.Next(), 20f);
            _nextLabel = _next.GetComponentInChildren<Text>();

            // "I wanted to skip" — a visible escape hatch that dumps the rest of the
            // current dialogue instantly, for players who don't want to click through
            // every line. Hidden during a choice prompt, which needs an actual answer.
            _skip = UIFactory.Button(root, "Skip", "SKIP »", SkipAll, Theme.FaceDark, 12, Theme.SystemSans, Theme.CommsGray);
            UIFactory.Place(UIFactory.RT(_skip.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(94, 26), new Vector2(-146, 22));

            // whole-box click also advances typing
            var clicker = root.gameObject.AddComponent<Button>();
            clicker.transition = Selectable.Transition.None;
            clicker.onClick.AddListener(OnBoxClick);
        }

        public void Show(Speaker who, string[] lines, Action onComplete = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _speaker = who;
            _onComplete = onComplete;
            _queue.Clear();
            foreach (var l in lines) _queue.Enqueue(l);

            switch (who)
            {
                case Speaker.OldDude:
                    _name.text = "MR. PELLINGS  (former proprietor)";
                    _name.font = Theme.Typewriter; _body.font = Theme.Typewriter;
                    _portrait.sprite = ArtSprites.CustomerPortrait("MR. PELLINGS"); _portrait.color = Color.white;
                    _titleText.text = "INCOMING TRANSMISSION";
                    _instant = false;
                    break;
                case Speaker.Robot:
                    _name.text = "UNIT B-EIGE  (assistant)";
                    _name.font = Theme.SystemSans; _body.font = Theme.SystemSans;
                    _portrait.sprite = ArtSprites.CustomerPortrait("UNIT B-EIGE"); _portrait.color = Color.white;
                    _titleText.text = "SYSTEM MESSAGE";
                    _instant = true;
                    break;
                default:
                    _name.text = "MAD-FACT OS";
                    _name.font = Theme.SystemSans; _body.font = Theme.SystemSans;
                    _portrait.sprite = SystemIcon(); _portrait.color = Color.white;
                    _titleText.text = "NOTICE";
                    _instant = true;
                    break;
            }
            NextLine();
        }

        public void ShowCustomer(CustomerData customer, string[] lines, Action onComplete = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _speaker = Speaker.System;
            _onComplete = onComplete;
            _queue.Clear();
            foreach (var l in lines) _queue.Enqueue(l);

            _name.text = customer.Name + "  (customer)";
            _name.font = Theme.Typewriter;
            _body.font = Theme.Typewriter;
            _portrait.sprite = ArtSprites.CustomerPortrait(customer.Name);
            _portrait.color = Color.white;
            _titleText.text = "CUSTOMER FOLLOW-UP";
            _instant = false;
            NextLine();
        }

        /// <summary>Dialogue from a one-off narrative figure (data broker, filmmaker, angry parent...).</summary>
        public void ShowNamed(string displayName, string titleBar, Sprite portrait, string[] lines, Action onComplete = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _speaker = Speaker.System;
            _onComplete = onComplete;
            _queue.Clear();
            foreach (var l in lines) _queue.Enqueue(l);

            _name.text = displayName;
            _name.font = Theme.Typewriter;
            _body.font = Theme.Typewriter;
            _portrait.sprite = portrait;
            _portrait.color = Color.white;
            _titleText.text = titleBar;
            _instant = false;
            NextLine();
        }

        // ---- Choices / MCQ -------------------------------------------------
        GameObject _choiceRoot;

        /// <summary>
        /// Pose a question with 2-4 answer buttons. Keeps whatever portrait/name is
        /// currently on the box, so call it right after (or from the onComplete of) a
        /// Show/ShowNamed from the same character. The box hides before onPick runs.
        /// </summary>
        public void AskChoice(string question, string[] options, Action<int> onPick)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _queue.Clear();
            _typing = false;
            _full = question;
            _body.text = question;
            // questions read differently from prose: bold, amber, a size up
            _body.fontStyle = FontStyle.Bold;
            _body.color = Theme.CrtAmber;
            _next.gameObject.SetActive(false);
            if (_skip != null) _skip.gameObject.SetActive(false);

            ClearChoices();
            _choiceRoot = UIFactory.Node(transform, "Choices");
            UIFactory.Fill(UIFactory.RT(_choiceRoot));

            int n = Mathf.Min(options.Length, 4);
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 188 + n * 36);

            // body sits above the option stack
            var brt = UIFactory.RT(_body.gameObject);
            brt.anchoredPosition = new Vector2(144, -66);

            for (int i = 0; i < n; i++)
            {
                int pick = i;
                var b = UIFactory.Button(_choiceRoot.transform, "Choice" + i, options[i], () => Pick(pick, onPick),
                    Theme.Face, 14, Theme.SystemSans, Theme.TitleText);
                var brt2 = UIFactory.RT(b.gameObject);
                brt2.anchorMin = new Vector2(0, 0); brt2.anchorMax = new Vector2(1, 0);
                brt2.pivot = new Vector2(0.5f, 0);
                brt2.sizeDelta = new Vector2(-(144 + 16), 32);          // margins: 144 left (portrait), 16 right
                brt2.anchoredPosition = new Vector2((144 - 16) / 2f, 14 + (n - 1 - i) * 36);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(12, label.rectTransform.offsetMin.y);
            }
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        /// <summary>Question with speaker styling in one call.</summary>
        public void AskChoice(Speaker who, string question, string[] options, Action<int> onPick)
        {
            Show(who, new string[0], null);   // sets portrait/name/fonts, queues nothing
            gameObject.SetActive(true);       // Show() hides itself on an empty queue
            AskChoice(question, options, onPick);
        }

        public void AskChoiceNamed(string displayName, string titleBar, Sprite portrait,
            string question, string[] options, Action<int> onPick)
        {
            _name.text = displayName;
            _name.font = Theme.Typewriter;
            _body.font = Theme.Typewriter;
            _portrait.sprite = portrait;
            _portrait.color = Color.white;
            _titleText.text = titleBar;
            AskChoice(question, options, onPick);
        }

        void Pick(int index, Action<int> onPick)
        {
            if (AudioTension.I != null) AudioTension.I.Clunk();
            ClearChoices();
            _next.gameObject.SetActive(true);
            if (_skip != null) _skip.gameObject.SetActive(true);
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 188);
            Hide();
            onPick?.Invoke(index);
        }

        void ClearChoices()
        {
            if (_choiceRoot != null) { Destroy(_choiceRoot); _choiceRoot = null; }
        }

        /// <summary>Instantly ends the current dialogue (not choice prompts) and fires onComplete.</summary>
        void SkipAll()
        {
            if (!gameObject.activeSelf) return;
            _queue.Clear();
            _typing = false;
            Hide();
            _onComplete?.Invoke();
        }

        public void Hide() { gameObject.SetActive(false); }

        void NextLine()
        {
            _body.fontStyle = FontStyle.Normal;
            _body.color = Theme.TitleText;
            if (_queue.Count == 0) { Hide(); _onComplete?.Invoke(); return; }
            _full = _queue.Dequeue();
            _revealed = 0f;
            _typing = !_instant;
            _body.text = _instant ? _full : "";
            if (_instant) _body.text = _full;
            bool done = _queue.Count == 0;
            _nextLabel.text = done ? "DONE" : "NEXT";
            _nextIcon.sprite = done ? ArtSprites.Stop() : ArtSprites.Next();
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void Advance()
        {
            if (_typing) { _typing = false; _body.text = _full; }
            else NextLine();
        }

        void OnBoxClick()
        {
            if (_typing) { _typing = false; _body.text = _full; }
        }

        void Update()
        {
            if (!_typing) return;
            _revealed += Time.unscaledDeltaTime * 42f; // chars/sec
            int n = Mathf.Min(_full.Length, Mathf.FloorToInt(_revealed));
            _body.text = _full.Substring(0, n);
            if (n >= _full.Length) _typing = false;
        }

        // ---- Procedural portraits ----------------------------------------
        static readonly int[,] Bayer =
        {
            { 0, 8, 2,10}, {12, 4,14, 6}, { 3,11, 1, 9}, {15, 7,13, 5}
        };

        static Sprite OldDudeFace()
        {
            if (_oldDude != null) return _oldDude;
            int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Vector2 c = new Vector2(n / 2f, n * 0.46f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // a vague digitized head: oval face, hair cap, eyes, mouth
                    float dx = (x - c.x) / (n * 0.30f);
                    float dy = (y - c.y) / (n * 0.40f);
                    float head = dx * dx + dy * dy;       // <1 inside head
                    float v = 0.25f;                       // dark backdrop
                    if (head < 1f) v = 0.62f - 0.18f * head;          // face shading
                    if (y > n * 0.66f && head < 1.1f) v = 0.30f;      // hair cap
                    // eyes
                    if (Inside(x, y, n * 0.40f, n * 0.52f, 4) || Inside(x, y, n * 0.60f, n * 0.52f, 4)) v = 0.15f;
                    // brow + mustache + mouth
                    if (y > n * 0.55f && y < n * 0.58f && head < 0.8f) v = 0.30f;
                    if (y > n * 0.30f && y < n * 0.34f && Mathf.Abs(x - c.x) < n * 0.16f) v = 0.20f; // mustache
                    if (y > n * 0.24f && y < n * 0.27f && Mathf.Abs(x - c.x) < n * 0.10f) v = 0.18f; // mouth
                    // ordered dither to 2 tones (sepia)
                    float thr = (Bayer[x & 3, y & 3] + 0.5f) / 16f;
                    float lit = v > thr ? 1f : 0.45f;
                    var col = new Color(lit * 0.55f + 0.18f, lit * 0.45f + 0.14f, lit * 0.30f + 0.10f);
                    tex.SetPixel(x, y, col);
                }
            tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
            _oldDude = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return _oldDude;
        }

        static bool Inside(int x, int y, float cx, float cy, float r)
            => (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r;

        static Sprite RobotFace()
        {
            if (_robot != null) return _robot;
            int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color bg = new Color(0.86f, 0.86f, 0.80f), ink = new Color(0.10f, 0.10f, 0.10f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color col = bg;
                    // head outline
                    if (x >= 6 && x <= 25 && y >= 5 && y <= 26) col = (x == 6 || x == 25 || y == 5 || y == 26) ? ink : bg;
                    // antenna
                    if (x == 15 && y >= 27 && y <= 30) col = ink;
                    if (Inside(x, y, 15.5f, 30, 2)) col = ink;
                    // eyes (square)
                    if (x >= 10 && x <= 13 && y >= 16 && y <= 19) col = ink;
                    if (x >= 18 && x <= 21 && y >= 16 && y <= 19) col = ink;
                    // mouth grid
                    if (y >= 9 && y <= 11 && x >= 10 && x <= 21 && (x % 2 == 0)) col = ink;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
            _robot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return _robot;
        }

        static Sprite SystemIcon()
        {
            if (_sysIcon != null) return _sysIcon;
            int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color bg = new Color(0.80f, 0.80f, 0.74f), ink = new Color(0.12f, 0.12f, 0.45f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color col = bg;
                    if (Inside(x, y, 16, 16, 13)) col = new Color(0.95f, 0.85f, 0.30f);
                    if (Inside(x, y, 16, 16, 13) && !Inside(x, y, 16, 16, 11)) col = ink;
                    // exclamation
                    if (x >= 15 && x <= 17 && y >= 12 && y <= 22) col = ink;
                    if (x >= 15 && x <= 17 && y >= 8 && y <= 10) col = ink;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
            _sysIcon = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return _sysIcon;
        }
    }
}
