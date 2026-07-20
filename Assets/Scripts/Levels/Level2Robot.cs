using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 2 — The Automation Era. The player programs the beige Robot Assistant with rigid
    /// IF (wants = genre) THEN (recommend tape) rules on an MS-DOS terminal, then runs a batch.
    /// Hardcoded rules are brittle: customers with contradictory tastes tank the revenue —
    /// and a rule that blindly hands an R-rated tape to a nine-year-old teaches, the hard way,
    /// why a computer must not make decisions without human oversight.
    /// </summary>
    public class Level2Robot : MonoBehaviour
    {
        struct Rule { public Genre Stated; public int Movie; }

        [SerializeField] GameObject _root;
        readonly List<Rule> _rules = new List<Rule>();
        Genre _selGenre = Genre.SciFi;
        int _selMovie = 0;

        [SerializeField] Text _genreSel, _movieSel, _rulesText, _log, _summary;
        [SerializeField] Image _genreIcon, _movieIcon;
        [SerializeField] Button _runBtn;
        [SerializeField] ScrollRect _logScroll;
        [SerializeField] Image _previewPoster;
        [SerializeField] Text _previewTitle, _previewRating;
        [SerializeField] Image[] _previewBars = new Image[GenreInfo.Count];
        [SerializeField] PosterBrowser _moviePicker;
        [SerializeField] GameObject _pickerOverlay;
        bool _running;
        bool _complainedOnce;   // the robot's "my rules are rigid" speech plays only once
        bool _incidentPause;    // batch is frozen while a rule-learning scene plays out

        const string FlagKidIncident = "kid_incident_done";
        const string FlagAgeRule = "age_rule_learned";
        const string FlagFirstBatchComplete = "level_2_first_batch_complete";
        const string FlagRepeatIncident = "repeat_tape_incident_done";
        const string FlagRepeatRule = "repeat_tape_rule_learned";
        const string FlagRewatchException = "rewatch_exception_seen";
        const string RepeatComplainantPrefix = "repeat_complainant_";
        const string FlagTutorial = "level_2_rule_tutorial";

        void Awake()
        {
            if (_root == null) return;
            ReconnectAuthoredView();
            Bind("GPrev", () => CycleGenre(-1));
            Bind("GNext", () => CycleGenre(1));
            Bind("MSel", OpenMoviePicker);
            Bind("Add", AddRule);
            Bind("Clr", ClearRules);
            Bind("Run", RunBatch);
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
            Bind("Close", ClosePicker);
        }

        /// <summary>
        /// Builder-time assignments are not reliable after this UI is saved as a prefab.
        /// Reconnect every Level 2-only reference so the authored scene behaves exactly like
        /// the code-created fallback, including the movie picker and the tape preview.
        /// </summary>
        void ReconnectAuthoredView()
        {
            if (_previewPoster == null) _previewPoster = UIFactory.FindDeep<Image>(transform, "PvPoster");
            if (_previewTitle == null) _previewTitle = UIFactory.FindDeep<Text>(transform, "PvTitle");
            if (_previewRating == null) _previewRating = UIFactory.FindDeep<Text>(transform, "PvRating");
            if (_logScroll == null) _logScroll = UIFactory.FindDeep<ScrollRect>(transform, "LogViewport");
            if (_pickerOverlay == null)
                _pickerOverlay = UIFactory.FindDeep<Transform>(transform, "PickerOverlay")?.gameObject;
            if (_moviePicker == null)
                _moviePicker = UIFactory.FindDeep<PosterBrowser>(transform, "MoviePicker");

            if (_previewBars == null || _previewBars.Length != GenreInfo.Count)
                _previewBars = new Image[GenreInfo.Count];
            for (int g = 0; g < GenreInfo.Count; g++)
                if (_previewBars[g] == null)
                    _previewBars[g] = UIFactory.FindDeep<Image>(transform, "pvf" + g);

            if (_moviePicker != null)
            {
                _moviePicker.RecommendLabel = "USE THIS TAPE";
                _moviePicker.OnRecommend = OnMoviePicked;
                _moviePicker.ApplySimpleNavigationStyle();
            }

            ApplySimplePickerChrome();
        }

        void ApplySimplePickerChrome()
        {
            var pickerWindow = UIFactory.FindDeep<Transform>(transform, "PickerWindow");
            if (pickerWindow == null) return;

            var title = UIFactory.FindDeep<Text>(pickerWindow, "T");
            if (title != null) title.color = Theme.Ink;

            var close = UIFactory.FindDeep<Button>(pickerWindow, "Close");
            if (close == null) return;
            var icon = UIFactory.FindDeep<Image>(close.transform, "Icon");
            if (icon != null) icon.gameObject.SetActive(false);
            var label = close.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "CLOSE";
                label.color = Theme.Ink;
                label.rectTransform.offsetMin = new Vector2(4, 2);
                label.rectTransform.offsetMax = new Vector2(-4, -2);
            }
            close.GetComponent<RectTransform>().sizeDelta = new Vector2(66, 26);
        }

        void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level2Robot Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level2");
            UIFactory.Fill(UIFactory.RT(go));
            var lvl = go.AddComponent<Level2Robot>();
            lvl.Build(go.transform);
            lvl._root.SetActive(false);
            return lvl;
        }

        void Build(Transform parent)
        {
            // translucent overlay: the era backdrop lives on the storefront behind this
            _root = UIFactory.Image(parent, "Level2Robot", new Color(0, 0, 0, 0.4f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            // beige plastic terminal chassis
            var chassis = UIFactory.DialogWindow(_root.transform, "Chassis", Theme.Plastic);
            UIFactory.Place(UIFactory.RT(chassis.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900, 470), new Vector2(0, 10));

            var head = UIFactory.Text(chassis.transform, "Head", "UNIT B-EIGE  ·  RULE PROGRAMMER v2.1",
                16, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(head.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(500, 24), new Vector2(18, -14));

            // ---- Rule builder (left) ----
            var builder = UIFactory.Bevel(chassis.transform, "Builder", Theme.Face, sunken: true);
            UIFactory.Place(UIFactory.RT(builder.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, 150), new Vector2(18, -46));

            UIFactory.Place(UIFactory.RT(UIFactory.Text(builder.transform, "i1", "IF THEY WANT:", 13, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold).gameObject),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 22), new Vector2(12, -12));
            var gPrev = UIFactory.Button(builder.transform, "GPrev", "", () => CycleGenre(-1), Theme.Face, 12);
            UIFactory.Place(UIFactory.RT(gPrev.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, 28), new Vector2(184, -10));
            UIFactory.ButtonIcon(gPrev, ArtSprites.Back(), 16f, true).color = Theme.Ink;
            var gSel = UIFactory.Bevel(builder.transform, "GSel", Theme.Plastic, sunken: true, raycast: false);
            UIFactory.Place(UIFactory.RT(gSel.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(160, 28), new Vector2(212, -10));
            _genreIcon = UIFactory.Image(gSel.transform, "Icon", Color.white, ArtSprites.GenreIcon(_selGenre), Image.Type.Simple, false);
            _genreIcon.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(_genreIcon.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 20), new Vector2(6, 0));
            _genreSel = UIFactory.Text(gSel.transform, "T", "", 12, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_genreSel.gameObject), 28, 2, 4, 2);
            var gNext = UIFactory.Button(builder.transform, "GNext", "", () => CycleGenre(1), Theme.Face, 12);
            UIFactory.Place(UIFactory.RT(gNext.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, 28), new Vector2(376, -10));
            UIFactory.ButtonIcon(gNext, ArtSprites.Next(), 16f, true).color = Theme.Ink;

            UIFactory.Place(UIFactory.RT(UIFactory.Text(builder.transform, "i2", "THEN HAND OUT:", 13, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold).gameObject),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, 22), new Vector2(12, -48));
            // A 47-title catalog is unbrowsable with prev/next arrows, so this chip opens
            // a full scrollable picker (genre carousel + poster grid + detail) instead.
            var mSel = UIFactory.Button(builder.transform, "MSel", "", OpenMoviePicker, Theme.Plastic, 12, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(mSel.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(260, 30), new Vector2(142, -45));
            _movieIcon = UIFactory.Image(mSel.transform, "Icon", Color.white, ArtSprites.MovieCover(_selMovie), Image.Type.Simple, false);
            _movieIcon.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(_movieIcon.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 26), new Vector2(6, 0));
            _movieSel = UIFactory.Text(mSel.transform, "T", "", 12, Theme.TitleText, Theme.SystemSans,
                TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_movieSel.gameObject), 32, 2, 28, 2);
            var browseIcon = UIFactory.Image(mSel.transform, "Browse", Color.white, ArtSprites.Next(), Image.Type.Simple, false);
            browseIcon.preserveAspect = true;
            browseIcon.color = Theme.TitleText;
            UIFactory.Place(UIFactory.RT(browseIcon.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(16, 16), new Vector2(-6, 0));

            var addBtn = UIFactory.Button(builder.transform, "Add", "ADD RULE", AddRule, Theme.Cash, 14, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(addBtn.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 30), new Vector2(12, -90));
            UIFactory.ButtonIcon(addBtn, ArtSprites.Add(), 22f);
            var clrBtn = UIFactory.Button(builder.transform, "Clr", "CLEAR", ClearRules, Theme.Face, 14, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(clrBtn.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, 30), new Vector2(210, -90));
            UIFactory.ButtonIcon(clrBtn, ArtSprites.Clear(), 22f);

            // ---- Rules list (scrollable — a full ruleset can run past the visible area) ----
            UIFactory.Place(UIFactory.RT(UIFactory.Text(chassis.transform, "rl", "PROGRAM:", 13, Theme.CrtGreenDim, Theme.Typewriter, TextAnchor.UpperLeft, false).gameObject),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(200, 18), new Vector2(28, -208));
            var (rulesScroll, rulesContent) = UIFactory.VScroll(chassis.transform, "RulesPanel", new Color(0.06f, 0.10f, 0.06f));
            UIFactory.Place(UIFactory.RT(rulesScroll.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, 178), new Vector2(18, -226));
            _rulesText = UIFactory.Text(rulesContent.transform, "Rules", "", 13, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);

            // ---- Tape preview (right, top): what the selected rule would hand out ----
            var preview = UIFactory.Bevel(chassis.transform, "Preview", Theme.Face, sunken: true);
            UIFactory.Place(UIFactory.RT(preview.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(420, 168), new Vector2(-18, -46));
            _previewPoster = UIFactory.Image(preview.transform, "PvPoster", Color.white, null, Image.Type.Simple, false);
            _previewPoster.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(_previewPoster.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, 128), new Vector2(28, -20));
            _previewTitle = UIFactory.Text(preview.transform, "PvTitle", "", 12, Theme.Ink, Theme.SystemSans, TextAnchor.UpperLeft, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_previewTitle.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 34), new Vector2(128, -22));
            _previewRating = UIFactory.Text(preview.transform, "PvRating", "", 10, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_previewRating.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 16), new Vector2(128, -56));
            // 8 compact genre-fit bars, two columns of four
            for (int g = 0; g < GenreInfo.Count; g++)
            {
                int col = g / 4, row = g % 4;
                float x = 128 + col * 138;
                float y = -78 - row * 20;
                var lbl = UIFactory.Text(preview.transform, "pvl" + g, GenreInfo.Names[g], 10, Theme.InkSoft, Theme.SystemSans, TextAnchor.UpperLeft, false);
                UIFactory.Place(UIFactory.RT(lbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, 14), new Vector2(x, y));
                var bg = UIFactory.Image(preview.transform, "pvb" + g, new Color(0, 0, 0, 0.22f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, 8), new Vector2(x + 68, y - 3));
                var fill = UIFactory.Image(bg.transform, "pvf" + g, GenreInfo.Colors[g], null, Image.Type.Simple, false);
                var frt = UIFactory.RT(fill.gameObject);
                frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                _previewBars[g] = fill;
            }

            // ---- DOS output (right, bottom): the batch run's money and ratings ----
            var crt = UIFactory.Bevel(chassis.transform, "CRT", new Color(0.04f, 0.09f, 0.05f), sunken: true);
            UIFactory.Place(UIFactory.RT(crt.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(420, 184), new Vector2(-18, -220));

            var logViewport = UIFactory.Image(crt.transform, "LogViewport", new Color(0, 0, 0, 0.001f));
            UIFactory.Fill(UIFactory.RT(logViewport.gameObject), 12, 8, 12, 46); // bottom gap = summary strip
            logViewport.gameObject.AddComponent<RectMask2D>();
            _logScroll = logViewport.gameObject.AddComponent<ScrollRect>();
            _logScroll.horizontal = false;
            _logScroll.vertical = true;
            _logScroll.movementType = ScrollRect.MovementType.Clamped;
            _logScroll.scrollSensitivity = 28f;
            _logScroll.viewport = UIFactory.RT(logViewport.gameObject);

            _log = UIFactory.Text(logViewport.transform, "Log", "C:\\STORE> _\n", 11,
                Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);
            var logRt = UIFactory.RT(_log.gameObject);
            logRt.anchorMin = new Vector2(0, 1);
            logRt.anchorMax = new Vector2(1, 1);
            logRt.pivot = new Vector2(0.5f, 1);
            logRt.offsetMin = Vector2.zero;
            logRt.offsetMax = Vector2.zero;
            logRt.sizeDelta = new Vector2(0, 24);
            var logFitter = _log.gameObject.AddComponent<ContentSizeFitter>();
            logFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            logFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _logScroll.content = logRt;

            _summary = UIFactory.Text(crt.transform, "Sum", "", 12, Theme.CrtAmber, Theme.Typewriter, TextAnchor.LowerLeft, true, FontStyle.Bold);
            var srt = UIFactory.RT(_summary.gameObject);
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
            srt.sizeDelta = new Vector2(-24, 40); srt.anchoredPosition = new Vector2(0, 4);

            // ---- bottom buttons ----
            _runBtn = UIFactory.Button(chassis.transform, "Run", "RUN BATCH", RunBatch, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_runBtn.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(420, 38), new Vector2(18, 14));
            UIFactory.ButtonIcon(_runBtn, ArtSprites.Play(), 28f);

            var leave = UIFactory.Button(chassis.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-10, -10));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);

            BuildMoviePickerOverlay(_root.transform);

            RefreshSelectors();
            RefreshRules();
        }

        /// <summary>Full scrollable genre/poster/detail browser for picking a rule's tape.</summary>
        void BuildMoviePickerOverlay(Transform root)
        {
            _pickerOverlay = UIFactory.Image(root, "PickerOverlay", new Color(0, 0, 0, 0.6f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_pickerOverlay));

            var window = UIFactory.DialogWindow(_pickerOverlay.transform, "PickerWindow", Theme.Face);
            UIFactory.Place(UIFactory.RT(window.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 460), Vector2.zero);

            var title = UIFactory.Text(window.transform, "T", "CHOOSE A TAPE FOR THIS RULE", 15,
                Theme.Ink, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(400, 24), new Vector2(-34, -14));

            _moviePicker = PosterBrowser.Create(window.transform, "MoviePicker");
            UIFactory.Place(UIFactory.RT(_moviePicker.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500, 386), new Vector2(0, -16));
            _moviePicker.RecommendLabel = "USE THIS TAPE";
            _moviePicker.OnRecommend = OnMoviePicked;

            var close = UIFactory.Button(window.transform, "Close", "CLOSE", ClosePicker, Theme.Face, 11,
                Theme.SystemSans, Theme.Ink);
            UIFactory.Place(UIFactory.RT(close.gameObject), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(66, 26), new Vector2(-10, -10));

            _pickerOverlay.SetActive(false);
        }

        void OpenMoviePicker()
        {
            if (_pickerOverlay == null || _moviePicker == null)
            {
                ReconnectAuthoredView();
                if (_pickerOverlay == null || _moviePicker == null) return;
            }
            _pickerOverlay.SetActive(true);
            _pickerOverlay.transform.SetAsLastSibling();
            _moviePicker.SetGenre(_selGenre);   // convenience default; still fully browsable
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void ClosePicker()
        {
            _pickerOverlay.SetActive(false);
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void OnMoviePicked(int index)
        {
            _selMovie = index;
            RefreshSelectors();
            ClosePicker();
        }

        void CycleGenre(int d) { _selGenre = (Genre)(((int)_selGenre + d + GenreInfo.Count) % GenreInfo.Count); RefreshSelectors(); }

        void RefreshSelectors()
        {
            var m = GameData.Movies[_selMovie];
            _genreSel.text = GenreInfo.Name(_selGenre);
            _movieSel.text = m.Short;
            if (_genreIcon != null) _genreIcon.sprite = ArtSprites.GenreIcon(_selGenre);
            if (_movieIcon != null) _movieIcon.sprite = ArtSprites.MovieCover(_selMovie);
            RefreshPreview(m);
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        /// <summary>The right-hand preview: poster, rating sticker, and genre-fit bars.</summary>
        void RefreshPreview(MovieData m)
        {
            if (_previewPoster == null) return;
            _previewPoster.sprite = ArtSprites.MovieCover(_selMovie);
            _previewTitle.text = m.TitleWithYear;
            _previewRating.text = "RATED " + GenreInfo.RatingLabel(m.Rating);
            _previewRating.color = m.Rating >= AgeRating.PG13 ? Theme.ErrorRed : Theme.InkSoft;
            for (int g = 0; g < GenreInfo.Count; g++)
            {
                if (_previewBars[g] == null) continue;
                var frt = UIFactory.RT(_previewBars[g].gameObject);
                frt.anchorMax = new Vector2(Mathf.Clamp01(m.Features[g]), 1f);
            }
        }

        void AddRule()
        {
            _rules.Add(new Rule { Stated = _selGenre, Movie = _selMovie });
            MadFactLokiLogger.Instance?.Log("rule_configured", "Player added a recommendation rule", new
            {
                level_id = 2,
                stated_genre = GenreInfo.Name(_selGenre),
                movie_id = GameData.Movies[_selMovie].Title,
                rule_count = _rules.Count
            });
            RefreshRules();
        }
        void ClearRules()
        {
            int removed = _rules.Count;
            _rules.Clear();
            MadFactLokiLogger.Instance?.Log("rule_set_cleared", "Player cleared recommendation rules",
                new { level_id = 2, rules_removed = removed });
            RefreshRules();
        }

        void RefreshRules()
        {
            var sb = new StringBuilder();
            int guardNumber = 0;
            if (GameManager.I != null && GameManager.I.Run.HasFlag(FlagAgeRule))
                sb.AppendLine($"{guardNumber++:00} IF AGE < RATING THEN SWAP FOR SAFE TAPE");
            if (GameManager.I != null && GameManager.I.Run.HasFlag(FlagRepeatRule))
                sb.AppendLine($"{guardNumber++:00} IF ALREADY RENTED THEN SWAP FOR UNSEEN TAPE");
            if (_rules.Count == 0 && sb.Length == 0) { _rulesText.text = "<no rules; robot will refund everyone>"; return; }
            for (int i = 0; i < _rules.Count; i++)
            {
                var m = GameData.Movies[_rules[i].Movie];
                int ruleNumber = i + Mathf.Max(1, guardNumber);
                sb.AppendLine($"{ruleNumber:00} IF WANTS={GenreInfo.Name(_rules[i].Stated)} THEN '{m.Title}' [{GenreInfo.RatingLabel(m.Rating)}]");
            }
            _rulesText.text = sb.ToString();
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            ReconnectAuthoredView();
            RefreshSelectors();
            RefreshRules();
            MadFactBootstrap.I.Storefront.SetLine(16);
            ShowFirstOpenTutorial();
        }
        public void Close() { _root.SetActive(false); if (AudioTension.I != null) AudioTension.I.Silence(); }

        void ShowFirstOpenTutorial()
        {
            if (GameManager.I == null || GameManager.I.Run.HasFlag(FlagTutorial)) return;
            GameManager.I.Run.SetFlag(FlagTutorial);

            RectTransform builder = UIFactory.FindDeep<RectTransform>(_root.transform, "Builder");
            RectTransform movieSelector = UIFactory.FindDeep<RectTransform>(_root.transform, "MSel");
            RectTransform run = _runBtn != null ? UIFactory.RT(_runBtn.gameObject) : null;
            MadFactBootstrap.I.Comms.ShowFocused(Speaker.Robot, new[]
            {
                "BUILD A RULE: IF a customer asks for a genre, THEN the robot hands out the tape you choose.",
                "Open the tape selector. Browse the posters and choose a tape that fits that genre.",
                "Add your rules, then select RUN BATCH to test them on a line of customers."
            }, new[] { builder, movieSelector, run });
        }

        void RunBatch()
        {
            if (_running) return;
            StartCoroutine(RunBatchRoutine());
        }

        void LateUpdate()
        {
            // Follow new terminal output while the batch is running. Once it finishes,
            // stop forcing the position so the player can freely review older results.
            if (!_running || _logScroll == null) return;
            Canvas.ForceUpdateCanvases();
            _logScroll.verticalNormalizedPosition = 0f;
        }

        /// <summary>Rich-text coloured payout token for the CRT log (+$ green, -$ red).</summary>
        static string Money(int amount)
            => amount >= 0 ? $"<color=#66E07A>+${amount}</color>" : $"<color=#F05A66>-${-amount}</color>";

        /// <summary>Random adult pool; kids only join the line once the robot can handle them.</summary>
        List<CustomerData> BatchPool()
        {
            bool kidsSafe = GameManager.I.Run.HasFlag(FlagAgeRule);
            var pool = new List<CustomerData>();
            foreach (var c in GameData.Customers)
                if (c.Age >= 13 || kidsSafe) pool.Add(c);
            return pool;
        }

        IEnumerator RunBatchRoutine()
        {
            _running = true;
            _runBtn.interactable = false;
            MadFactLokiLogger.Instance?.Log("interaction_started", "Robot recommendation batch started",
                new { interaction_id = "level_2_batch", level_id = 2, rule_count = _rules.Count });
            _summary.text = "";
            _log.text = "C:\\STORE> RUN AUTOSERVE.BAT\n";
            if (_logScroll != null) _logScroll.verticalNormalizedPosition = 1f;

            if (_rules.Count == 0)
            {
                // No rules means the robot refuses every single order — that's already
                // reflected in the per-customer refunds below, but it should ALSO cost
                // trust: "the rules aren't enough" is a lesson about the till, not just it.
                _log.text += "<color=#F05A66>NO RULES PROGRAMMED. Every order was refused.</color>\n";
                GameManager.I.AddTrust(-10);
            }

            int batch = GameManager.I.TrustScaledCustomers(12);
            if (batch < 12)
                _log.text += $"<color=#E0C266>Trust is low. Only {batch} customers are in line.</color>\n";

            var pool = BatchPool();
            int earned = 0, perfect = 0, close = 0, terrible = 0;
            float potential = 0f;

            // The trust lesson: the first time a horror rule exists that would hand an
            // R-rated tape to a kid, Timmy is guaranteed to walk in and take it home.
            bool kidPrimed = !GameManager.I.Run.HasFlag(FlagKidIncident) && FindUnsafeHorrorRule() >= 0;
            int kidSlot = kidPrimed ? Mathf.Min(2, batch - 1) : -1;

            // Let the first batch play normally. Starting with the second batch, force
            // one eligible adult to visit twice so the repeat lesson cannot be missed.
            CustomerData repeatCustomer = GameManager.I.Run.HasFlag(FlagFirstBatchComplete) &&
                                          !GameManager.I.Run.HasFlag(FlagRepeatIncident)
                ? FindRepeatLessonCustomer()
                : null;
            bool repeatPrimed = repeatCustomer != null;
            int repeatFirstSlot = repeatPrimed ? 0 : -1;
            int repeatComplaintSlot = repeatPrimed ? batch - 1 : -1;

            // Once the store is close to its goal, a different customer deliberately
            // asks for a favorite again. This exposes the limit of the new global rule.
            bool nearGoal = GameManager.I.Money >= GameManager.Level2Goal - Economy.PerfectPay * 4;
            CustomerData rewatchCustomer = GameManager.I.Run.HasFlag(FlagRepeatRule) &&
                                           !GameManager.I.Run.HasFlag(FlagRewatchException) &&
                                           nearGoal
                ? FindRewatchLessonCustomer()
                : null;
            bool rewatchPrimed = rewatchCustomer != null && !repeatPrimed;
            int rewatchFirstSlot = rewatchPrimed ? 0 : -1;
            int rewatchRequestSlot = rewatchPrimed ? batch - 1 : -1;

            for (int n = 0; n < batch; n++)
            {
                // UnityEngine.Random (not System.Random with its default TickCount seed,
                // which can collide across rapid successive batch runs and make "random"
                // customers repeat identically batch after batch) — genuinely re-rolled
                // every call, so re-running the batch actually re-randomizes who shows up.
                CustomerData cust = (n == kidSlot)
                    ? GameData.CustomerByName("TIMMY")
                    : (n == repeatFirstSlot || n == repeatComplaintSlot)
                        ? repeatCustomer
                        : (n == rewatchFirstSlot || n == rewatchRequestSlot)
                            ? rewatchCustomer
                            : RandomCustomerExcept(pool, repeatCustomer, rewatchCustomer);
                potential += Economy.PerfectPay;

                int matchRule = -1;
                for (int r = 0; r < _rules.Count; r++)
                    if (_rules[r].Stated == cust.StatedGenre) { matchRule = r; break; }

                string line;
                if (matchRule < 0)
                {
                    terrible++; earned += Economy.Refund;
                    if (AudioTension.I != null) AudioTension.I.Buzzer();
                    line = $"> <color=#F05A66>✕</color> {cust.Name}: wants {GenreInfo.Name(cust.StatedGenre)}. NO RULE {Money(Economy.Refund)}";
                }
                else
                {
                    var movie = GameData.Movies[_rules[matchRule].Movie];

                    // age guard, once learned: swap unsafe tapes for the customer
                    if (GameManager.I.Run.HasFlag(FlagAgeRule) && !GameData.AgeOk(cust, movie))
                    {
                        int safe = FindSafeAlternative(cust, _rules[matchRule].Stated);
                        if (safe >= 0)
                        {
                            var swapped = GameData.Movies[safe];
                            _log.text += $"> <color=#E0C266>⚠ AGE CHECK: '{movie.Title}' [{GenreInfo.RatingLabel(movie.Rating)}] blocked for {cust.Name} (age {cust.Age}) → '{swapped.Title}' [{GenreInfo.RatingLabel(swapped.Rating)}]</color>\n";
                            movie = swapped;
                        }
                        else
                        {
                            _log.text += $"> <color=#E0C266>⚠ AGE CHECK: no safe {GenreInfo.Name(cust.StatedGenre)} tape for {cust.Name} (age {cust.Age}). Politely declined.</color>\n";
                            yield return new WaitForSecondsRealtime(0.18f);
                            continue;
                        }
                    }

                    // the incident: an R-rated tape goes home with a nine-year-old
                    if (n == kidSlot && !GameData.AgeOk(cust, movie))
                    {
                        _log.text += $"> <color=#F05A66>⚠</color> {cust.Name} (age {cust.Age}): wants {GenreInfo.Name(cust.StatedGenre)}. Rule {matchRule + 1:00} hands over '{movie.Title}' [{GenreInfo.RatingLabel(movie.Rating)}]\n";
                        yield return new WaitForSecondsRealtime(0.7f);
                        yield return KidIncident(movie);
                        // the mother's refund + fine
                        earned += -25; terrible++;
                        _log.text += $"> <color=#F05A66>✕ REFUND + FINE (inappropriate rental) {Money(-25)}</color>\n";
                        yield return new WaitForSecondsRealtime(0.3f);
                        continue;
                    }

                    bool alreadyRented = GameManager.I.Run.HasServed(cust.Name, movie.Title);
                    bool isRepeatComplaint = n == repeatComplaintSlot && cust == repeatCustomer && alreadyRented;
                    bool isRewatchRequest = n == rewatchRequestSlot && cust == rewatchCustomer && alreadyRented;
                    bool allowRequestedRepeat = false;

                    if (isRepeatComplaint)
                    {
                        _log.text += $"> <color=#F05A66>!</color> {cust.Name}: received '{movie.Title}' again and asks for a different tape.\n";
                        yield return new WaitForSecondsRealtime(0.45f);
                        yield return RepeatTapeIncident(cust, movie);
                        terrible++; earned += Economy.Refund;
                        GameManager.I.AddTrust(-5);
                        if (AudioTension.I != null) AudioTension.I.Buzzer();
                        line = $"> <color=#F05A66>✕</color> {cust.Name}: repeat tape returned. REFUND {Money(Economy.Refund)}";
                        _log.text += line + "\n";
                        yield return new WaitForSecondsRealtime(0.18f);
                        continue;
                    }

                    if (isRewatchRequest)
                    {
                        _log.text += $"> <color=#E0C266>!</color> {cust.Name}: asks for '{movie.Title}' again on purpose.\n";
                        yield return new WaitForSecondsRealtime(0.45f);
                        yield return RewatchException(cust, movie);
                        allowRequestedRepeat = true;
                        _log.text += $"> <color=#66E07A>PERSONAL EXCEPTION: '{movie.Title}' approved for {cust.Name}.</color>\n";
                    }

                    // The learned global condition checks rental history before handing
                    // over the rule's tape. The later favorite-movie scene deliberately
                    // overrides it to show why people sometimes need personal exceptions.
                    if (alreadyRented && GameManager.I.Run.HasFlag(FlagRepeatRule) && !allowRequestedRepeat)
                    {
                        int unseen = FindUnseenAlternative(cust, _rules[matchRule].Stated);
                        if (unseen >= 0)
                        {
                            var swapped = GameData.Movies[unseen];
                            _log.text += $"> <color=#E0C266>⚠ HISTORY CHECK: {cust.Name} already rented '{movie.Title}' → '{swapped.Title}'</color>\n";
                            MadFactLokiLogger.Instance?.Log("recommendation_rule_applied", "Repeat-tape guard selected an unseen movie", new
                            {
                                interaction_id = "level_2_batch",
                                level_id = 2,
                                condition_id = "already_rented",
                                customer_id = cust.Name,
                                blocked_movie_id = movie.Title,
                                replacement_movie_id = swapped.Title
                            });
                            movie = swapped;
                        }
                        else
                        {
                            terrible++; earned += Economy.Refund;
                            _log.text += $"> <color=#F05A66>✕ HISTORY CHECK: no unseen matching tape for {cust.Name}. REFUND {Money(Economy.Refund)}</color>\n";
                            yield return new WaitForSecondsRealtime(0.18f);
                            continue;
                        }
                    }

                    float sat = GameData.TrueRating(cust, movie);
                    float err = 5f - sat;
                    var tier = Economy.Tier(err);
                    int pay = Economy.Pay(tier);
                    GameManager.I.Run.RecordRecommendation("level_2_batch", cust.Name, Phase.Level2, movie, tier, sat);
                    MadFactLokiLogger.Instance?.Log("movie_recommended", "Robot recommended a movie to a customer", new
                    {
                        interaction_id = "level_2_batch",
                        level_id = 2,
                        customer_id = cust.Name,
                        movie_id = movie.Title,
                        satisfaction = sat,
                        sale_tier = tier.ToString(),
                        automated = true
                    });
                    earned += pay;
                    if (tier == SaleTier.Perfect) perfect++; else if (tier == SaleTier.Close) close++; else terrible++;
                    if (AudioTension.I != null)
                    {
                        if (tier == SaleTier.Perfect) AudioTension.I.Coin(); else if (tier == SaleTier.Terrible) AudioTension.I.Buzzer();
                    }
                    string mark = tier == SaleTier.Perfect ? "<color=#66E07A>✓</color>"
                                : tier == SaleTier.Close ? "<color=#E0C266>~</color>"
                                : "<color=#F05A66>✕</color>";
                    line = $"> {mark} {cust.Name}: '{movie.Title}' [{Mathf.RoundToInt(sat)}★] {Money(pay)}";
                    _log.text += line + "\n";
                    yield return new WaitForSecondsRealtime(0.18f);
                    continue;
                }
                _log.text += line + "\n";
                yield return new WaitForSecondsRealtime(0.18f);
            }

            GameManager.I.AddMoney(earned);
            if (AudioTension.I != null) AudioTension.I.Silence();

            float acc = (perfect * 1f + close * 0.4f) / batch;
            _summary.text = $"BATCH: {Money(earned)}  ·  <color=#66E07A>{perfect} perfect</color> / <color=#E0C266>{close} close</color> / <color=#F05A66>{terrible} refunds</color>\n" +
                            $"earned ${earned} of ${potential:0} possible; {acc * 100f:0}% effective";
            _log.text += "\nC:\\STORE> _\n";

            _running = false;
            _runBtn.interactable = true;
            MadFactLokiLogger.Instance?.Log("interaction_completed", "Robot recommendation batch completed", new
            {
                interaction_id = "level_2_batch",
                level_id = 2,
                customers = batch,
                earned,
                perfect,
                close,
                terrible,
                effectiveness = acc
            });
            GameManager.I.Run.SetFlag(FlagFirstBatchComplete);

            // verdict
            if (_rules.Count > 0)
            {
                if (GameManager.I.Money >= GameManager.Level2Goal &&
                    GameManager.I.Run.HasFlag(FlagRewatchException) &&
                    !MadFactBootstrap.I.Level2Cleared)
                {
                    MadFactBootstrap.I.Level2Cleared = true;
                    yield return new WaitForSecondsRealtime(0.8f);
                    Close();
                    MadFactBootstrap.I.OnLevel2Goal();
                }
                else if (GameManager.I.Money >= GameManager.Level2Goal &&
                         GameManager.I.Run.HasFlag(FlagRepeatRule) &&
                         !GameManager.I.Run.HasFlag(FlagRewatchException))
                {
                    yield return new WaitForSecondsRealtime(0.6f);
                    MadFactBootstrap.I.Comms.Show(Speaker.Robot, new[]
                    {
                        "THE MONEY GOAL IS MET. I NEED ONE FINAL TEST OF THE NEW HISTORY RULE."
                    });
                }
                else if (terrible >= 3 && !_complainedOnce)
                {
                    _complainedOnce = true;
                    yield return new WaitForSecondsRealtime(0.6f);
                    MadFactBootstrap.I.Comms.Show(Speaker.Robot, new[]
                    {
                        "RESULT: " + terrible + " CUSTOMERS DID NOT GET A GOOD MATCH.",
                        "MY RULES ARE TOO STRICT. FIX THEM OR USE A SMARTER SET OF STEPS."
                    });
                }
            }
        }

        int FindUnsafeHorrorRule()
        {
            // any horror rule whose tape a nine-year-old shouldn't take home (PG-13+)
            var timmy = GameData.CustomerByName("TIMMY");
            for (int r = 0; r < _rules.Count; r++)
                if (_rules[r].Stated == Genre.Horror &&
                    !GameData.AgeOk(timmy, GameData.Movies[_rules[r].Movie])) return r;
            return -1;
        }

        int FindSafeAlternative(CustomerData cust, Genre stated)
        {
            // the robot's idea of "safe": any age-appropriate tape whose box lists the
            // stated genre highest among the safe options
            int best = -1; float bestF = 0f;
            for (int i = 0; i < GameData.Movies.Count; i++)
            {
                var m = GameData.Movies[i];
                if (!GameData.AgeOk(cust, m)) continue;
                float f = m.Features[(int)stated];
                if (f > bestF) { bestF = f; best = i; }
            }
            return bestF > 0.2f ? best : -1;
        }

        int FindUnseenAlternative(CustomerData cust, Genre stated)
        {
            int best = -1;
            float bestFeature = 0f;
            for (int i = 0; i < GameData.Movies.Count; i++)
            {
                var movie = GameData.Movies[i];
                if (GameManager.I.Run.HasServed(cust.Name, movie.Title)) continue;
                if (GameManager.I.Run.HasFlag(FlagAgeRule) && !GameData.AgeOk(cust, movie)) continue;
                float feature = movie.Features[(int)stated];
                if (feature > bestFeature)
                {
                    bestFeature = feature;
                    best = i;
                }
            }
            return bestFeature > 0.2f ? best : -1;
        }

        CustomerData FindRepeatLessonCustomer()
        {
            foreach (var customer in GameData.Customers)
            {
                if (customer.Age < 13) continue;
                if (RuleIndexFor(customer.StatedGenre) >= 0) return customer;
            }
            return null;
        }

        CustomerData FindRewatchLessonCustomer()
        {
            CustomerData best = null;
            float bestRating = float.MinValue;
            foreach (var customer in GameData.Customers)
            {
                if (customer.Age < 13 || WasRepeatComplainant(customer)) continue;
                int ruleIndex = RuleIndexFor(customer.StatedGenre);
                if (ruleIndex < 0) continue;
                float rating = GameData.TrueRating(customer, GameData.Movies[_rules[ruleIndex].Movie]);
                if (rating > bestRating)
                {
                    bestRating = rating;
                    best = customer;
                }
            }
            return best;
        }

        int RuleIndexFor(Genre genre)
        {
            for (int i = 0; i < _rules.Count; i++)
                if (_rules[i].Stated == genre) return i;
            return -1;
        }

        bool WasRepeatComplainant(CustomerData customer)
            => GameManager.I.Run.HasFlag(RepeatComplainantPrefix + customer.Name);

        static CustomerData RandomCustomerExcept(List<CustomerData> pool, CustomerData first, CustomerData second)
        {
            if (pool.Count == 1) return pool[0];
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var customer = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (customer != first && customer != second) return customer;
            }
            foreach (var customer in pool)
                if (customer != first && customer != second) return customer;
            return pool[0];
        }

        IEnumerator RepeatTapeIncident(CustomerData customer, MovieData tape)
        {
            _incidentPause = true;
            GameManager.I.Run.SetFlag(FlagRepeatIncident);
            GameManager.I.Run.SetFlag(RepeatComplainantPrefix + customer.Name);
            MadFactLokiLogger.Instance?.Log("dialogue_seen", "Customer complaint introduced the repeat-tape condition", new
            {
                dialogue_id = "level_2_repeat_tape_complaint",
                level_id = 2,
                customer_id = customer.Name,
                movie_id = tape.Title
            });

            var comms = MadFactBootstrap.I.Comms;
            comms.ShowCustomer(customer, new[]
            {
                $"Again? Your robot keeps handing me '{tape.Title}'. I already watched it. I want something new."
            }, () => comms.Show(Speaker.Robot, new[]
            {
                "THE GENRE RULE MATCHED, BUT I DID NOT CHECK THIS CUSTOMER'S RENTAL HISTORY."
            }, () => comms.Show(Speaker.OldDude, new[]
            {
                "Add a history check. If someone already rented a tape, offer an unseen one instead."
            }, () =>
            {
                GameManager.I.Run.SetFlag(FlagRepeatRule);
                RefreshRules();
                ResumeBatch();
            })));

            yield return new WaitUntil(() => !_incidentPause);
        }

        IEnumerator RewatchException(CustomerData customer, MovieData tape)
        {
            _incidentPause = true;
            GameManager.I.Run.SetFlag(FlagRewatchException);
            MadFactLokiLogger.Instance?.Log("dialogue_seen", "A requested rewatch exposed the limit of a global no-repeat rule", new
            {
                dialogue_id = "level_2_requested_rewatch_exception",
                level_id = 2,
                customer_id = customer.Name,
                movie_id = tape.Title
            });

            var comms = MadFactBootstrap.I.Comms;
            comms.ShowCustomer(customer, new[]
            {
                $"Wait. I asked for '{tape.Title}' on purpose. I love it, and I want to watch it again."
            }, () => comms.Show(Speaker.Robot, new[]
            {
                "REQUEST BLOCKED. THE GLOBAL RULE SAYS NO REPEATS. I AM FOLLOWING THE RULES."
            }, () => comms.Show(Speaker.OldDude, new[]
            {
                "One customer wanted something new, but this customer wants a favorite again. One rule cannot fit everyone. We need to learn what each person likes."
            }, ResumeBatch)));

            yield return new WaitUntil(() => !_incidentPause);
        }

        /// <summary>
        /// The Trustworthy-RS scene: mother storms in, B-EIGE asks why money was lost,
        /// then asks what it should do about it. The batch resumes afterwards.
        /// </summary>
        IEnumerator KidIncident(MovieData tape)
        {
            _incidentPause = true;
            GameManager.I.Run.SetFlag(FlagKidIncident);
            GameManager.I.AddTrust(-10);
            if (AudioTension.I != null) AudioTension.I.Buzzer();

            var comms = MadFactBootstrap.I.Comms;
            var mom = ArtSprites.CustomerPortrait("TIMMY'S MOM");

            comms.ShowNamed("TIMMY'S MOM  (furious)", "INCOMING COMPLAINT", mom, new[]
            {
                $"Excuse me. EXCUSE ME. Your machine rented '{tape.Title}' to my NINE-YEAR-OLD.",
                $"It is rated {GenreInfo.RatingLabel(tape.Rating)}. I want a refund, and I am warning other parents."
            }, AskWhy);

            yield return new WaitUntil(() => !_incidentPause);
        }

        void AskWhy()
        {
            var comms = MadFactBootstrap.I.Comms;
            comms.Show(Speaker.Robot, new[] { "BOSS. WHY DID WE LOSE MONEY? THE RULE MATCHED. I DID EXACTLY WHAT YOU SAID." }, () =>
                comms.AskChoice(Speaker.Robot, "QUERY: WHAT WENT WRONG?", new[]
                {
                    "The tape was not what they asked for",
                    "The tape was not appropriate for them"
                }, why =>
                {
                    if (why == 0)
                        comms.Show(Speaker.Robot, new[]
                        {
                            "NEGATIVE. TIMMY ASKED FOR HORROR. THE TAPE WAS HORROR. THE MATCH WAS CORRECT.",
                            "AND YET: REFUND. FINE. SHOUTING. THE PROBLEM IS NOT THE MATCH."
                        }, AskWhatToDo);
                    else
                        comms.Show(Speaker.Robot, new[]
                        {
                            "CONFIRMED. THE TAPE MATCHED THE REQUEST BUT NOT THE CUSTOMER.",
                            "A RULE THAT ONLY READS THE REQUEST DOES NOT THINK ABOUT THE PERSON WHO ASKED."
                        }, AskWhatToDo);
                }));
        }

        void AskWhatToDo()
        {
            var comms = MadFactBootstrap.I.Comms;
            comms.AskChoice(Speaker.Robot, "QUERY: WHAT SHOULD I DO DIFFERENTLY?", new[]
            {
                "Check the customer's age against the rating",
                "Ban every kid from entering the store",
                "Change nothing because the rule matched correctly"
            }, pick =>
            {
                switch (pick)
                {
                    case 0:
                        GameManager.I.Run.SetFlag(FlagAgeRule);
                        GameManager.I.AddTrust(15);
                        RefreshRules();
                        comms.Show(Speaker.Robot, new[]
                        {
                            "UPDATING: I WILL CHECK CUSTOMER AGE AGAINST THE RATING ON THE BOX.",
                            "A HUMAN SAW A PROBLEM THAT MY RULES MISSED. I WILL REMEMBER IT."
                        }, () => comms.Show(Speaker.OldDude, new[]
                        {
                            "That is the lesson. A machine follows the rules you give it and nothing more.",
                            "If no person checks its work, it can repeat a bad choice very quickly.",
                            "People must watch computer decisions and step in when something is wrong."
                        }, ResumeBatch));
                        break;
                    case 1:
                        comms.Show(Speaker.Robot, new[]
                        {
                            "RESULT: KIDS RENT 30% OF ALL CARTOONS. BANNING THEM WOULD HURT THE STORE.",
                            "TIMMY DID NOTHING WRONG. THE RULE WAS WRONG. TRY AGAIN."
                        }, AskWhatToDo);
                        break;
                    default:
                        comms.Show(Speaker.Robot, new[]
                        {
                            "IF WE CHANGE NOTHING, THE SAME CHOICE WILL CAUSE THE SAME REFUND.",
                            "A RULE WILL KEEP REPEATING. TRY AGAIN."
                        }, AskWhatToDo);
                        break;
                }
            });
        }

        void ResumeBatch() { _incidentPause = false; }
    }
}
