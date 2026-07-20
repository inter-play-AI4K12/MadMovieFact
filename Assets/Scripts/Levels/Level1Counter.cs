using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 1 — The Manual Era. The player reviews a physical customer file, may ask a
    /// limited number of clarifying questions (action economy), then walks the shelves:
    /// a genre carousel of poster thumbnails, each opening to box art and feature bars,
    /// exactly like a clerk flipping a box over before recommending it.
    /// After the very first recommendation, Mr. Pellings breaks in to explain WHY the
    /// customer reacted the way they did — feature matching, said out loud.
    /// The growing line proves manual labour can't scale.
    /// </summary>
    public class Level1Counter : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] Text _name, _history, _stated, _quip, _notes, _qLeft, _result;
        [SerializeField] List<Button> _questionButtons = new List<Button>();
        [SerializeField] Button _nextBtn;
        [SerializeField] Image _portrait;
        [SerializeField] Text _portraitInitial;
        PosterBrowser _browser;

        LevelScenario _scenario;
        CustomerVisit _visit;
        CustomerData _cust;
        int _questionsLeft;
        int _questionsAsked;
        int _served;
        bool _recommended;
        bool _pellingsExplained;   // the one-time "here's why they liked/hated it" lesson
        bool _openedOnce;
        bool _tutorialShown;

        void Awake()
        {
            if (_root == null) return;
            _browser = GetComponentInChildren<PosterBrowser>(true);
            if (_browser != null) _browser.OnRecommend = Recommend;
            _nextBtn.onClick.RemoveAllListeners();
            _nextBtn.onClick.AddListener(NextCustomer);
            var leave = UIFactory.FindDeep<Button>(transform, "Leave");
            if (leave != null) { leave.onClick.RemoveAllListeners(); leave.onClick.AddListener(() => MadFactBootstrap.I.GoStorefront()); }
            for (int i = 0; i < _questionButtons.Count; i++)
            {
                int index = i;
                _questionButtons[i].onClick.RemoveAllListeners();
                _questionButtons[i].onClick.AddListener(() => Ask(index));
            }
        }

        public static Level1Counter Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level1");
            UIFactory.Fill(UIFactory.RT(go));
            var lvl = go.AddComponent<Level1Counter>();
            lvl.Build(go.transform);
            lvl._root.SetActive(false);
            return lvl;
        }

        void Build(Transform parent)
        {
            _root = UIFactory.Image(parent, "Level1Counter", new Color(0, 0, 0, 0.35f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            var window = UIFactory.DialogWindow(_root.transform, "Window", Theme.Face);
            UIFactory.Place(UIFactory.RT(window.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900, 470), new Vector2(0, 10));

            // title bar
            var tb = UIFactory.Image(window.transform, "TitleBar", new Color(0.06f, 0.09f, 0.10f, 1f));
            var tbr = UIFactory.RT(tb.gameObject); tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(-8, 26); tbr.anchoredPosition = new Vector2(0, -6);
            var tt = UIFactory.Text(tb.transform, "T", "THE COUNTER  ·  MANUAL RECOMMENDATION", 12, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(tt.gameObject), 8, 0, 8, 0);

            BuildFile(window.transform);
            BuildQuestions(window.transform);

            // the shelf browser (genre carousel + posters + detail), per the design sketch
            _browser = PosterBrowser.Create(window.transform, "Shelf");
            UIFactory.Place(UIFactory.RT(_browser.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(292, 384), new Vector2(-12, -40));
            _browser.OnRecommend = Recommend;

            // Keep the outcome light on the artwork: only a crisp text stroke, without
            // a banner or rectangle. Its centre matches the button below.
            _result = UIFactory.Text(window.transform, "Result", "", 15, Theme.Ink,
                Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_result.gameObject), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(560, 32), new Vector2(18, 50));
            var resultStroke = _result.gameObject.AddComponent<Outline>();
            resultStroke.effectColor = new Color(0, 0, 0, 0.95f);
            resultStroke.effectDistance = new Vector2(1, -1);
            resultStroke.useGraphicAlpha = true;

            _nextBtn = UIFactory.Button(window.transform, "Next", "NEXT CUSTOMER", NextCustomer, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_nextBtn.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(220, 36), new Vector2(188, 10));
            UIFactory.ButtonIcon(_nextBtn, ArtSprites.Next(), 24f);
            _nextBtn.gameObject.SetActive(false);

            var leave = UIFactory.Button(window.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-8, -7));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);
        }

        void BuildFile(Transform window)
        {
            var folder = UIFactory.Image(window.transform, "Folder", Theme.Manila);
            UIFactory.Place(UIFactory.RT(folder.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, 380), new Vector2(16, -40));
            // Keep the folder tab inside the body. The old upward offset placed it over
            // the window title bar and hid part of "THE COUNTER".
            var tab = UIFactory.Image(folder.transform, "Tab", Theme.ManilaTab);
            UIFactory.Place(UIFactory.RT(tab.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 22), new Vector2(14, -4));
            var tabT = UIFactory.Text(tab.transform, "TT", "CUSTOMER FILE", 12, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(tabT.gameObject));

            var pf = UIFactory.Bevel(folder.transform, "Portrait", Theme.Manila, sunken: true);
            UIFactory.Place(UIFactory.RT(pf.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, 80), new Vector2(18, -34));
            pf.gameObject.AddComponent<RectMask2D>();
            _portrait = UIFactory.Image(pf.transform, "P", Color.white, Theme.Disc);
            UIFactory.Fill(UIFactory.RT(_portrait.gameObject), 8, 8, 8, 8);
            _portraitInitial = UIFactory.Text(pf.transform, "PI", "", 30, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_portraitInitial.gameObject));

            _name = UIFactory.Text(folder.transform, "Name", "", 18, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_name.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 24), new Vector2(108, -40));
            _history = UIFactory.Text(folder.transform, "Hist", "", 14, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, false);
            UIFactory.Place(UIFactory.RT(_history.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 22), new Vector2(108, -66));
            _stated = UIFactory.Text(folder.transform, "Stated", "", 14, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, false);
            UIFactory.Place(UIFactory.RT(_stated.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 22), new Vector2(108, -88));

            _quip = UIFactory.Text(folder.transform, "Quip", "", 14, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(_quip.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(268, 56), new Vector2(16, -126));

            var ndlbl = UIFactory.Text(folder.transform, "NotesLbl", "NOTES", 12, Theme.ManilaEdge, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(ndlbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(200, 18), new Vector2(16, -188));
            _notes = UIFactory.Text(folder.transform, "Notes", "", 13, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(_notes.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(268, 160), new Vector2(16, -208));
        }

        readonly struct QuestionDef
        {
            public readonly string Id;
            public readonly string Label;
            public readonly Vibe? VibeAxis;
            public readonly Genre? GenreAxis;
            public readonly string Strong;
            public readonly string Medium;
            public readonly string Weak;

            public QuestionDef(string id, string label, Vibe axis, string strong, string medium, string weak)
            {
                Id = id;
                Label = label;
                VibeAxis = axis;
                GenreAxis = null;
                Strong = strong;
                Medium = medium;
                Weak = weak;
            }

            public QuestionDef(string id, string label, Genre genre, string strong, string medium, string weak)
            {
                Id = id;
                Label = label;
                VibeAxis = null;
                GenreAxis = genre;
                Strong = strong;
                Medium = medium;
                Weak = weak;
            }

            public float Score(CustomerData customer) =>
                VibeAxis.HasValue
                    ? customer.TrueVibe[(int)VibeAxis.Value]
                    : customer.GenreTaste[(int)GenreAxis.Value];

            public string PreferenceKey =>
                VibeAxis.HasValue ? VibeAxis.Value.ToString() : GenreAxis.Value.ToString();
        }

        static readonly QuestionDef[] Questions =
        {
            new QuestionDef("funny", "SHOULD IT BE FUNNY?", Vibe.Funny,
                "“Yes. Make me laugh the whole way through.”", "“A few laughs would be nice.”", "“No jokes, please.”"),
            new QuestionDef("scary", "HOW SCARY SHOULD IT BE?", Vibe.Spooky,
                "“Make it really scary.”", "“A little suspense is fine.”", "“Nothing scary, please.”"),
            new QuestionDef("space", "SPACE OR EARTH?", Vibe.Spacey,
                "“Yes. Take me to another world.”", "“Space is fine if the story is good.”", "“No. Keep it on Earth.”"),
            new QuestionDef("action", "LOTS OF ACTION?", Vibe.Explosions,
                "“Yes. Make it fast and exciting.”", "“Some action is fine.”", "“No. I want a quieter story.”"),
            new QuestionDef("true_story", "TRUE STORY OR MADE-UP?", Genre.Documentary,
                "“A true story, please. I want real people and real events.”", "“Either is fine if it feels believable.”", "“Made-up is fine. I am not looking for a documentary.”"),
            new QuestionDef("serious", "LIGHT OR SERIOUS?", Genre.Drama,
                "“Serious. I want a story that stays with me.”", "“Some serious moments are fine.”", "“Keep it light. Nothing heavy.”"),
            new QuestionDef("romance", "ROMANCE OR NO ROMANCE?", Genre.Romance,
                "“Yes. The love story should matter.”", "“A little romance is fine.”", "“No romance for me.”"),
            new QuestionDef("animation", "CARTOON OR LIVE ACTION?", Genre.Animation,
                "“A cartoon, please.”", "“Either one is fine.”", "“Live action, please.”")
        };

        public static Sprite QuestionIcon(int index)
        {
            if (index < 0 || index >= Questions.Length) return null;
            var question = Questions[index];
            return question.VibeAxis.HasValue
                ? ArtSprites.VibeIcon((int)question.VibeAxis.Value)
                : ArtSprites.GenreIcon(question.GenreAxis.Value);
        }

        void BuildQuestions(Transform window)
        {
            // This is a layout group, not a form field. Keep it transparent so the
            // question controls sit directly on the window instead of inside a gray box.
            var panel = UIFactory.Node(window, "QuestionPanel");
            UIFactory.Place(UIFactory.RT(panel), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(256, 252), new Vector2(328, -40));

            var lbl = UIFactory.Text(panel.transform, "QLbl", "ASK FOR A CLUE", 13, Theme.Ink,
                Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(lbl.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(126, 22), new Vector2(12, -9));
            _qLeft = UIFactory.Text(panel.transform, "QLeft", "", 11, Theme.ErrorRed,
                Theme.SystemSans, TextAnchor.MiddleRight, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_qLeft.gameObject), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(106, 22), new Vector2(-12, -9));

            for (int i = 0; i < Questions.Length; i++)
            {
                int idx = i;
                float y = -42f - i * 25.5f;
                var question = Questions[i];
                var b = UIFactory.Button(panel.transform, "Q" + i, question.Label, () => Ask(idx),
                    Theme.Face, 12, Theme.SystemSans);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(232, 23), new Vector2(0, y));
                UIFactory.ButtonIcon(b, QuestionIcon(i), 17f);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 10;
                t.resizeTextMaxSize = 12;
                _questionButtons.Add(b);
            }

            var shelfHint = UIFactory.Text(window.transform, "ShelfHint",
                "Use the answers as clues. Then check the feature bars on each box →",
                12, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(shelfHint.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(250, 44), new Vector2(330, -300));
        }

        // ---- Flow --------------------------------------------------------
        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            if (_openedOnce) return;
            _openedOnce = true;
            _served = 0;
            NextCustomer();
            ShowFirstOpenTutorial();
        }
        public void Close() => _root.SetActive(false);

        void ShowFirstOpenTutorial()
        {
            if (_tutorialShown) return;
            _tutorialShown = true;

            RectTransform folder = UIFactory.FindDeep<RectTransform>(_root.transform, "Folder");
            RectTransform questions = UIFactory.FindDeep<RectTransform>(_root.transform, "QuestionPanel");
            RectTransform shelf = _browser != null ? UIFactory.RT(_browser.gameObject) : null;
            MadFactBootstrap.I.Comms.ShowFocused(Speaker.System, new[]
            {
                "Start with the CUSTOMER FILE. Read what they rented, what they want, and any notes.",
                "You may ask up to TWO clues. Use them when the request is not clear.",
                "Browse the shelves, select a poster to inspect its details, then recommend the best match."
            }, new[] { folder, questions, shelf });
        }

        void NextCustomer()
        {
            _recommended = false;
            _questionsLeft = 2;
            _questionsAsked = 0;
            _result.text = "";
            _nextBtn.gameObject.SetActive(false);

            // Scenarios own the authored task; CustomerData owns the persistent person.
            // This is the first step away from random POC customers toward returning,
            // consequence-bearing customer arcs.
            _scenario = ScenarioDatabase.NextLevel1Manual(GameManager.I.Run);
            _visit = _scenario.Visit;
            _cust = _visit.Customer;
            MadFactLokiLogger.Instance?.Log("interaction_started", "Customer interaction started", new
            {
                interaction_id = _scenario.Id,
                level_id = 1,
                customer_id = _cust.Name,
                return_visit = _visit.ReturnVisit
            });

            _name.text = _cust.Name;
            _history.text = "RENTS: " + GenreInfo.Name(_visit.HistoryGenre);
            _stated.text = "WANTS: " + GenreInfo.Name(_visit.StatedGenre);
            _quip.text = "“" + _visit.DemandLine + "”";
            _notes.text = BuildInitialNotes(_visit);
            var portrait = ArtSprites.CustomerPortrait(_cust.Name);
            bool hasPortrait = portrait != Theme.Disc;
            _portrait.sprite = portrait;
            _portrait.color = hasPortrait ? Color.white : _cust.Shirt;
            UIFactory.CoverFit(_portrait);
            _portraitInitial.gameObject.SetActive(!hasPortrait);
            _portraitInitial.text = _cust.Name.Substring(0, 1);

            foreach (var b in _questionButtons) b.interactable = true;
            if (_browser != null)
            {
                _browser.SetLocked(false);
                _browser.SetGenre(_visit.StatedGenre);   // open the shelf they asked about
            }
            UpdateQLeft();

            // the line keeps growing — the bottleneck
            MadFactBootstrap.I.Storefront.SetLine(Mathf.Clamp(2 + _served, 2, 16));
            MadFactBootstrap.I.Storefront.SetSubtitle($"The line is {2 + _served} deep and growing...");
        }

        string BuildInitialNotes(CustomerVisit visit)
        {
            string notes = "- AGE: " + visit.Customer.Age + "\n";
            if (visit.ReturnVisit)
            {
                int visits = GameManager.I.Run.VisitsFor(visit.Customer.Name);
                if (visits > 0)
                    notes += $"- RETURN VISIT: last satisfaction {GameManager.I.Run.LastSatisfactionFor(visit.Customer.Name):0.0}/5\n";
                else
                    notes += "- RETURN VISIT: claims to know the store\n";
            }
            if (!string.IsNullOrEmpty(visit.FileNote)) notes += "- " + visit.FileNote + "\n";
            return notes;
        }

        void UpdateQLeft()
        {
            _qLeft.text = _questionsLeft > 0 ? $"{_questionsLeft} LEFT" : "CHOOSE A TAPE";
            if (_questionsLeft <= 0) foreach (var b in _questionButtons) b.interactable = false;
        }

        void Ask(int qi)
        {
            if (_questionsLeft <= 0 || _recommended) return;
            if (qi < 0 || qi >= Questions.Length) return;
            _questionsLeft--;
            _questionsAsked++;
            if (qi < _questionButtons.Count) _questionButtons[qi].interactable = false;
            var question = Questions[qi];
            float w = question.Score(_cust);
            string ans = w > 0.66f ? question.Strong : w > 0.33f ? question.Medium : question.Weak;
            _notes.text += $"Q: {question.Label}\n   {ans}\n";
            if (AudioTension.I != null) AudioTension.I.Beep();
            MadFactLokiLogger.Instance?.Log("hint_requested", "Player asked a customer question", new
            {
                interaction_id = _scenario.Id,
                question_id = question.Id,
                preference_axis = TelemetryJson.ToSnakeCase(question.PreferenceKey),
                questions_remaining = _questionsLeft
            });
            UpdateQLeft();
            MadFactBootstrap.I.Comms.ShowCustomer(_cust, new[] { ans });
        }

        void Recommend(int mi)
        {
            if (_recommended) return;
            var movie = GameData.Movies[mi];

            // no double-dipping: a repeat is allowed, but it tanks the sale and costs
            // a little trust rather than silently no-op'ing — the customer notices.
            bool repeat = GameManager.I.Run.HasServed(_cust.Name, movie.Title);

            _recommended = true;
            float satisfaction = repeat ? 1f : GameData.TrueRating(_cust, movie); // 1..5
            float error = 5f - satisfaction;                        // 0 = perfect

            foreach (var b in _questionButtons) b.interactable = false;
            _browser.SetLocked(true);

            if (AudioTension.I != null) AudioTension.I.Clunk();

            // payout at the register position (top-centre-ish)
            Vector2 pop = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 40);
            var tier = GameManager.I.RecordSale(error, pop);
            if (repeat) GameManager.I.AddTrust(-5);
            GameManager.I.Run.RecordRecommendation(_scenario, movie, tier, satisfaction, _questionsAsked);
            MadFactLokiLogger.Instance?.Log("movie_recommended", "Player recommended a movie to a customer", new
            {
                interaction_id = _scenario.Id,
                level_id = 1,
                customer_id = _cust.Name,
                movie_id = movie.Title,
                satisfaction,
                sale_tier = tier.ToString(),
                questions_asked = _questionsAsked,
                money_after = GameManager.I.Money
            });
            MadFactLokiLogger.Instance?.Log("interaction_completed", "Customer interaction completed", new
            {
                interaction_id = _scenario.Id,
                outcome = tier.ToString(),
                satisfaction
            });
            string stars = new string('★', Mathf.RoundToInt(satisfaction)) + new string('·', 5 - Mathf.RoundToInt(satisfaction));
            _result.text = repeat ? $"ALREADY RENTED: {movie.Title}  [{stars}]" : $"{Economy.TierLabel(tier)}: {movie.Title}  [{stars}]";
            _result.color = repeat ? Theme.ErrorRed : Economy.TierColor(tier);

            _served++;

            if (repeat)
            {
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, new[]
                    { $"'{movie.Title}'? I already RENTED that one from you. Come on, I want something NEW." },
                    () =>
                    {
                        _pellingsExplained = true;
                        if (GameManager.I.Money < 0) FinishRecommendation(tier);
                        else ShowPellingsLesson(movie, tier, () => FinishRecommendation(tier));
                    });
                return;
            }

            if (tier == SaleTier.Terrible)
            {
                PlayMistakeFeedback(movie, tier, () => FinishRecommendation(tier));
            }
            else
            {
                PlaySuccessfulFeedback(movie, tier, () => FinishRecommendation(tier));
            }
        }

        void PlaySuccessfulFeedback(MovieData movie, SaleTier tier, System.Action onComplete)
        {
            var outcome = _scenario != null ? _scenario.OutcomeFor(tier) : null;

            // Some authored outcomes contain only Mr. Pellings' lesson. Give the
            // customer the first word so the conversation still follows the same order.
            if (outcome != null && outcome.Target == DialogueTarget.OldDude)
            {
                string customerLine = tier == SaleTier.Perfect
                    ? "That sounds right for me. Thanks!"
                    : "That is close enough. I will give it a try.";

                MadFactBootstrap.I.Comms.ShowCustomer(_cust, new[] { customerLine }, () =>
                {
                    _pellingsExplained = true;
                    PlayScenarioOutcome(tier, onComplete);
                });
                return;
            }

            PlayScenarioOutcome(tier, () =>
            {
                if (_pellingsExplained)
                {
                    onComplete?.Invoke();
                    return;
                }

                _pellingsExplained = true;
                ShowPellingsLesson(movie, tier, onComplete);
            });
        }

        void PlayMistakeFeedback(MovieData movie, SaleTier tier, System.Action onComplete)
        {
            var outcome = _scenario != null ? _scenario.OutcomeFor(tier) : null;

            System.Action showOwner = () =>
            {
                // Bankruptcy/trust recovery supplies the owner's response after the
                // customer finishes, so do not stack another owner dialogue before it.
                if (GameManager.I.Money < 0 || GameManager.I.Trust <= 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                _pellingsExplained = true;
                if (outcome != null && outcome.Target == DialogueTarget.OldDude)
                    PlayScenarioOutcome(tier, onComplete);
                else
                    ShowPellingsLesson(movie, tier, onComplete);
            };

            if (outcome != null && outcome.Target == DialogueTarget.Customer)
                PlayScenarioOutcome(tier, showOwner);
            else
                MadFactBootstrap.I.Comms.ShowCustomer(_cust,
                    new[] { "That is not what I asked for. I want a different tape." },
                    showOwner);
        }

        void FinishRecommendation(SaleTier tier)
        {
            if (GameManager.I.Money >= GameManager.Level1Goal && !MadFactBootstrap.I.Level1Cleared)
            {
                MadFactBootstrap.I.Level1Cleared = true;
                _nextBtn.gameObject.SetActive(false);
                TriggerUpgrade();
                return;
            }

            _nextBtn.gameObject.SetActive(true);
        }

        void PlayScenarioOutcome(SaleTier tier, System.Action onComplete)
        {
            var outcome = _scenario != null ? _scenario.OutcomeFor(tier) : null;
            if (outcome == null || outcome.Lines == null || outcome.Lines.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            GameManager.I.Run.SetFlag(outcome.FlagToSet);
            switch (outcome.Target)
            {
                case DialogueTarget.Customer:
                    MadFactBootstrap.I.Comms.ShowCustomer(_cust, outcome.Lines, onComplete);
                    break;
                case DialogueTarget.OldDude:
                    MadFactBootstrap.I.Comms.Show(Speaker.OldDude, outcome.Lines, onComplete);
                    break;
                case DialogueTarget.Robot:
                    MadFactBootstrap.I.Comms.Show(Speaker.Robot, outcome.Lines, onComplete);
                    break;
                default:
                    MadFactBootstrap.I.Comms.Show(Speaker.System, outcome.Lines, onComplete);
                    break;
            }
        }

        /// <summary>Explain, in Pellings' voice, why this customer reacted the way they did.</summary>
        void ShowPellingsLesson(MovieData movie, SaleTier tier, System.Action onComplete)
        {
            // find the customer's strongest craving and how much of it the tape delivers
            int axis = 0;
            for (int d = 1; d < Latent.Dim; d++) if (_cust.TrueVibe[d] > _cust.TrueVibe[axis]) axis = d;
            int cv = Mathf.RoundToInt(Mathf.Clamp01(_cust.TrueVibe[axis]) * 10f);
            int mv = Mathf.RoundToInt(Mathf.Clamp01(movie.Vibe[axis]) * 10f);
            string vibe = Latent.Names[axis];

            string[] lines;
            switch (tier)
            {
                case SaleTier.Perfect:
                    lines = new[]
                    {
                        $"Perfect match! {_cust.Name} likes {vibe} about {cv} out of 10, and '{movie.Title}' has {mv} out of 10.",
                        "Match what customers truly like, not only the first thing they say."
                    };
                    break;
                case SaleTier.Close:
                    lines = new[]
                    {
                        $"Close match. {_cust.Name} likes {vibe} at {cv} out of 10, but '{movie.Title}' has {mv}.",
                        "Read the HISTORY, ask questions, and look for a better tape."
                    };
                    break;
                default:
                    lines = new[]
                    {
                        $"That match failed. {_cust.Name} likes {vibe} about {cv} out of 10, but '{movie.Title}' has only {mv}.",
                        "Read the HISTORY, check what they WANT, and ask questions before you choose."
                    };
                    break;
            }
            MadFactBootstrap.I.Comms.Show(Speaker.OldDude, lines, onComplete);
        }

        void TriggerUpgrade()
        {
            if (AudioTension.I != null) AudioTension.I.Silence();
            Close();
            MadFactBootstrap.I.OnLevel1Goal();
        }
    }
}
