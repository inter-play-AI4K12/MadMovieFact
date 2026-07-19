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

        void Awake()
        {
            if (_root == null) return;
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

            _result = UIFactory.Text(window.transform, "Result", "", 15, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_result.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(560, 28), new Vector2(18, 52));

            _nextBtn = UIFactory.Button(window.transform, "Next", "NEXT CUSTOMER", NextCustomer, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_nextBtn.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(220, 36), new Vector2(18, 12));
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
            // tab
            var tab = UIFactory.Image(folder.transform, "Tab", Theme.ManilaTab);
            UIFactory.Place(UIFactory.RT(tab.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 22), new Vector2(14, 20));
            var tabT = UIFactory.Text(tab.transform, "TT", "CUSTOMER FILE", 12, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(tabT.gameObject));

            var pf = UIFactory.Bevel(folder.transform, "Portrait", Theme.Manila, sunken: true);
            UIFactory.Place(UIFactory.RT(pf.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, 80), new Vector2(18, -16));
            _portrait = UIFactory.Image(pf.transform, "P", Color.white, Theme.Disc);
            _portrait.preserveAspect = true;
            UIFactory.Fill(UIFactory.RT(_portrait.gameObject), 8, 8, 8, 8);
            _portraitInitial = UIFactory.Text(pf.transform, "PI", "", 30, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_portraitInitial.gameObject));

            _name = UIFactory.Text(folder.transform, "Name", "", 18, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_name.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 24), new Vector2(108, -22));
            _history = UIFactory.Text(folder.transform, "Hist", "", 14, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, false);
            UIFactory.Place(UIFactory.RT(_history.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 22), new Vector2(108, -48));
            _stated = UIFactory.Text(folder.transform, "Stated", "", 14, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, false);
            UIFactory.Place(UIFactory.RT(_stated.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 22), new Vector2(108, -70));

            _quip = UIFactory.Text(folder.transform, "Quip", "", 14, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(_quip.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(268, 56), new Vector2(16, -108));

            var ndlbl = UIFactory.Text(folder.transform, "NotesLbl", "— NOTES —", 12, Theme.ManilaEdge, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(ndlbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(200, 18), new Vector2(16, -170));
            _notes = UIFactory.Text(folder.transform, "Notes", "", 13, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(_notes.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(268, 180), new Vector2(16, -190));
        }

        readonly Vibe[] _qAxis = { Vibe.Funny, Vibe.Spooky, Vibe.Spacey, Vibe.Explosions };
        readonly string[] _qText = { "Looking for a laugh?", "In the mood for a scare?", "Into space & sci-fi?", "Want big explosions?" };

        void BuildQuestions(Transform window)
        {
            var lbl = UIFactory.Text(window.transform, "QLbl", "ASK A CLARIFYING QUESTION", 14, Theme.Ink, Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(lbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, 20), new Vector2(330, -44));
            _qLeft = UIFactory.Text(window.transform, "QLeft", "", 13, Theme.ErrorRed, Theme.SystemSans, TextAnchor.UpperLeft, false);
            UIFactory.Place(UIFactory.RT(_qLeft.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, 18), new Vector2(330, -64));

            for (int i = 0; i < _qText.Length; i++)
            {
                int idx = i;
                var b = UIFactory.Button(window.transform, "Q" + i, _qText[i], () => Ask(idx), Theme.Face, 14, Theme.SystemSans);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(245, 26), new Vector2(330, -86 - i * 30));
                UIFactory.ButtonIcon(b, ArtSprites.VibeIcon((int)_qAxis[i]), 21f);
                var t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft;
                _questionButtons.Add(b);
            }

            var shelfHint = UIFactory.Text(window.transform, "ShelfHint",
                "Then flip through the shelves →\nclick a box to read its features.", 12, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(shelfHint.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(250, 44), new Vector2(330, -212));
        }

        // ---- Flow --------------------------------------------------------
        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            _served = 0;
            NextCustomer();
        }
        public void Close() => _root.SetActive(false);

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
            _qLeft.text = _questionsLeft > 0 ? $"{_questionsLeft} questions remaining" : "No questions left — recommend now";
            if (_questionsLeft <= 0) foreach (var b in _questionButtons) b.interactable = false;
        }

        void Ask(int qi)
        {
            if (_questionsLeft <= 0 || _recommended) return;
            _questionsLeft--;
            _questionsAsked++;
            float w = _cust.TrueVibe[(int)_qAxis[qi]];
            string ans = w > 0.66f ? "“Oh yes, absolutely!”" : w > 0.33f ? "“Eh, it's fine I guess.”" : "“Ugh, no thank you.”";
            _notes.text += $"Q: {_qText[qi]}\n   {ans}\n";
            if (AudioTension.I != null) AudioTension.I.Beep();
            MadFactLokiLogger.Instance?.Log("hint_requested", "Player asked a customer question", new
            {
                interaction_id = _scenario.Id,
                question_id = TelemetryJson.ToSnakeCase(_qAxis[qi].ToString()),
                questions_remaining = _questionsLeft
            });
            UpdateQLeft();
        }

        void Recommend(int mi)
        {
            if (_recommended) return;
            var movie = GameData.Movies[mi];

            // no double-dipping: a customer never takes home the same tape twice
            if (GameManager.I.Run.HasServed(_cust.Name, movie.Title))
            {
                if (AudioTension.I != null) AudioTension.I.Beep();
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, new[]
                    { $"'{movie.Title}'? I already RENTED that one from you. Got anything else?" });
                return;
            }

            _recommended = true;
            float satisfaction = GameData.TrueRating(_cust, movie); // 1..5
            float error = 5f - satisfaction;                        // 0 = perfect

            foreach (var b in _questionButtons) b.interactable = false;
            _browser.SetLocked(true);

            if (AudioTension.I != null) AudioTension.I.Clunk();

            // payout at the register position (top-centre-ish)
            Vector2 pop = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 40);
            var tier = GameManager.I.RecordSale(error, pop);
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
            _result.text = $"{Economy.TierLabel(tier)}  —  {movie.Title}  [{stars}]";
            _result.color = Economy.TierColor(tier);

            _served++;

            // Mr. Pellings breaks in ONCE, after the very first sale, to teach the lesson:
            // taste is a set of features, and matching them is the whole job. Chain into
            // the scenario's own outcome dialogue afterward so the two don't race to show
            // on the CommsBox in the same frame.
            if (!_pellingsExplained)
            {
                _pellingsExplained = true;
                ShowPellingsLesson(movie, tier, () => AfterRecommend(tier));
            }
            else
            {
                AfterRecommend(tier);
            }
        }

        void AfterRecommend(SaleTier tier)
        {
            if (GameManager.I.Money >= GameManager.Level1Goal && !MadFactBootstrap.I.Level1Cleared)
            {
                MadFactBootstrap.I.Level1Cleared = true;
                _nextBtn.gameObject.SetActive(false);
                PlayScenarioOutcome(tier, TriggerUpgrade);
                return;
            }

            PlayScenarioOutcome(tier, () => _nextBtn.gameObject.SetActive(true));
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
                        $"Ha! See that smile? That right there is a PERFECT match, kid.",
                        $"Look at the file: what {_cust.Name} craves most is {vibe} — about {cv} out of 10.",
                        $"And '{movie.Title}' is packed with it — {mv} out of 10. Taste met tape.",
                        "That's the whole job. Match what they LOVE, not just what they say."
                    };
                    break;
                case SaleTier.Close:
                    lines = new[]
                    {
                        $"Not bad — {_cust.Name} paid, but did you see that shrug? They weren't thrilled.",
                        $"Their file says they crave {vibe} at {cv} out of 10.",
                        $"'{movie.Title}' only delivers {mv} out of 10 of it. Close... but close pays five bucks.",
                        "Study the HISTORY, ask a question or two, and hunt for the PERFECT tape."
                    };
                    break;
                default:
                    lines = new[]
                    {
                        $"Hold up, kid. {_cust.Name} stormed out — let me show you what went wrong.",
                        $"Their file says what they crave most is {vibe} — about {cv} out of 10.",
                        $"'{movie.Title}'? It's got {mv} out of 10 of that. Wrong tape, angry customer, refund.",
                        "Read the HISTORY, read what they WANT, ask your questions. THEN match."
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
