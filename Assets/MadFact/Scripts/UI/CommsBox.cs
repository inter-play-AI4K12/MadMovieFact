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
        Image _portrait;
        Text _name, _body;
        Image _titleBar;
        Text _titleText;
        Button _next;
        Text _nextLabel;

        readonly Queue<string> _queue = new Queue<string>();
        Speaker _speaker;
        Action _onComplete;
        string _full = "";
        float _revealed;
        bool _typing;
        bool _instant;

        static Sprite _oldDude, _robot, _sysIcon;

        public static CommsBox Create(Transform parent)
        {
            var root = UIFactory.Bevel(parent, "CommsBox", Theme.CommsGray);
            var rt = UIFactory.RT(root.gameObject);
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(720, 168);
            rt.anchoredPosition = new Vector2(0, 18);

            var cb = root.gameObject.AddComponent<CommsBox>();
            cb.Build(root.transform);
            root.gameObject.SetActive(false);
            return cb;
        }

        void Build(Transform root)
        {
            // title bar (win95 navy)
            _titleBar = UIFactory.Image(root, "TitleBar", Theme.TitleBar);
            var tbr = UIFactory.RT(_titleBar.gameObject);
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0.5f, 1f);
            tbr.sizeDelta = new Vector2(-8, 24); tbr.anchoredPosition = new Vector2(0, -6);
            _titleText = UIFactory.Text(_titleBar.transform, "T", "CORPORATE COMMS", 14, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_titleText.gameObject), 8, 0, 8, 0);

            // portrait frame (sunken)
            var pf = UIFactory.Bevel(root, "PortraitFrame", Theme.Face, sunken: true);
            UIFactory.Place(UIFactory.RT(pf.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, 96), new Vector2(14, -36));
            _portrait = UIFactory.Image(pf.transform, "Portrait", Color.white);
            UIFactory.Fill(UIFactory.RT(_portrait.gameObject), 4, 4, 4, 4);

            _name = UIFactory.Text(root, "Name", "", 14, Theme.Ink, Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_name.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(560, 20), new Vector2(122, -36));

            _body = UIFactory.Text(root, "Body", "", 16, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(_body.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(566, 78), new Vector2(122, -58));

            _next = UIFactory.Button(root, "Next", "NEXT  ▶", Advance, Theme.Face, 14);
            UIFactory.Place(UIFactory.RT(_next.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(110, 30), new Vector2(-14, 14));
            _nextLabel = _next.GetComponentInChildren<Text>();

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
                    _portrait.sprite = OldDudeFace(); _portrait.color = new Color(0.92f, 0.86f, 0.74f);
                    _titleText.text = "INCOMING TRANSMISSION";
                    _instant = false;
                    break;
                case Speaker.Robot:
                    _name.text = "UNIT B-EIGE  (assistant)";
                    _name.font = Theme.SystemSans; _body.font = Theme.SystemSans;
                    _portrait.sprite = RobotFace(); _portrait.color = Color.white;
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

        public void Hide() { gameObject.SetActive(false); }

        void NextLine()
        {
            if (_queue.Count == 0) { Hide(); _onComplete?.Invoke(); return; }
            _full = _queue.Dequeue();
            _revealed = 0f;
            _typing = !_instant;
            _body.text = _instant ? _full : "";
            if (_instant) _body.text = _full;
            _nextLabel.text = _queue.Count == 0 ? "DONE  ■" : "NEXT  ▶";
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
