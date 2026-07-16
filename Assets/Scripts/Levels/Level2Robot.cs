using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
        bool _running;
        bool _complainedOnce;   // the robot's "my rules are rigid" speech plays only once
        bool _incidentPause;    // batch is frozen while the rated-R scene plays out

        const string FlagKidIncident = "kid_incident_done";
        const string FlagAgeRule = "age_rule_learned";

        void Awake()
        {
            if (_root == null) return;
            Bind("GSel", () => CycleGenre(1));
            Bind("MSel", () => CycleMovie(1));
            Bind("Add", AddRule);
            Bind("Clr", ClearRules);
            Bind("Run", RunBatch);
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
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

            var head = UIFactory.Text(chassis.transform, "Head", "UNIT B-EIGE  ·  RULE PROGRAMMER v2.1", 16, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(head.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(500, 24), new Vector2(18, -14));

            // ---- Rule builder (left) ----
            var builder = UIFactory.Bevel(chassis.transform, "Builder", Theme.Face, sunken: true);
            UIFactory.Place(UIFactory.RT(builder.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, 150), new Vector2(18, -46));

            UIFactory.Place(UIFactory.RT(UIFactory.Text(builder.transform, "i1", "IF  CUSTOMER WANTS:", 14, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold).gameObject),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 22), new Vector2(12, -12));
            var gSel = UIFactory.Button(builder.transform, "GSel", "", () => CycleGenre(1), Theme.Plastic, 14, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(gSel.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 28), new Vector2(210, -10));
            _genreIcon = UIFactory.ButtonIcon(gSel, ArtSprites.GenreIcon(_selGenre), 22f);
            _genreSel = gSel.GetComponentInChildren<Text>();

            UIFactory.Place(UIFactory.RT(UIFactory.Text(builder.transform, "i2", "THEN  RECOMMEND TAPE:", 14, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold).gameObject),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 22), new Vector2(12, -48));
            var mSel = UIFactory.Button(builder.transform, "MSel", "", () => CycleMovie(1), Theme.Plastic, 11, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(mSel.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 28), new Vector2(210, -46));
            _movieIcon = UIFactory.ButtonIcon(mSel, ArtSprites.MovieCover(_selMovie), 22f);
            _movieSel = mSel.GetComponentInChildren<Text>();

            var addBtn = UIFactory.Button(builder.transform, "Add", "ADD RULE", AddRule, Theme.Cash, 14, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(addBtn.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, 30), new Vector2(12, -90));
            UIFactory.ButtonIcon(addBtn, ArtSprites.Add(), 22f);
            var clrBtn = UIFactory.Button(builder.transform, "Clr", "CLEAR", ClearRules, Theme.Face, 14, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(clrBtn.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, 30), new Vector2(210, -90));
            UIFactory.ButtonIcon(clrBtn, ArtSprites.Clear(), 22f);

            // ---- Rules list ----
            var rulesPanel = UIFactory.Bevel(chassis.transform, "RulesPanel", new Color(0.06f, 0.10f, 0.06f), sunken: true);
            UIFactory.Place(UIFactory.RT(rulesPanel.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, 200), new Vector2(18, -204));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(rulesPanel.transform, "rl", "PROGRAM:", 13, Theme.CrtGreenDim, Theme.Typewriter, TextAnchor.UpperLeft, false).gameObject),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(200, 18), new Vector2(10, -6));
            _rulesText = UIFactory.Text(rulesPanel.transform, "Rules", "", 13, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(_rulesText.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, 170), new Vector2(10, -26));

            // ---- DOS output (right) ----
            var crt = UIFactory.Bevel(chassis.transform, "CRT", new Color(0.04f, 0.09f, 0.05f), sunken: true);
            UIFactory.Place(UIFactory.RT(crt.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(420, 358), new Vector2(-18, -46));
            _log = UIFactory.Text(crt.transform, "Log", "C:\\STORE> _\n", 13, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Fill(UIFactory.RT(_log.gameObject), 12, 10, 12, 52); // bottom gap = summary strip
            _summary = UIFactory.Text(crt.transform, "Sum", "", 13, Theme.CrtAmber, Theme.Typewriter, TextAnchor.LowerLeft, true, FontStyle.Bold);
            var srt = UIFactory.RT(_summary.gameObject);
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
            srt.sizeDelta = new Vector2(-24, 44); srt.anchoredPosition = new Vector2(0, 6);

            // ---- bottom buttons ----
            _runBtn = UIFactory.Button(chassis.transform, "Run", "RUN BATCH", RunBatch, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_runBtn.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(420, 38), new Vector2(18, 14));
            UIFactory.ButtonIcon(_runBtn, ArtSprites.Play(), 28f);

            var leave = UIFactory.Button(chassis.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-10, -10));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);

            RefreshSelectors();
            RefreshRules();
        }

        void CycleGenre(int d) { _selGenre = (Genre)(((int)_selGenre + d + GenreInfo.Count) % GenreInfo.Count); RefreshSelectors(); }
        void CycleMovie(int d) { _selMovie = (_selMovie + d + GameData.Movies.Count) % GameData.Movies.Count; RefreshSelectors(); }

        void RefreshSelectors()
        {
            var m = GameData.Movies[_selMovie];
            _genreSel.text = GenreInfo.Name(_selGenre);
            _movieSel.text = m.Title + " [" + GenreInfo.RatingLabel(m.Rating) + "]";
            if (_genreIcon != null) _genreIcon.sprite = ArtSprites.GenreIcon(_selGenre);
            if (_movieIcon != null) _movieIcon.sprite = ArtSprites.MovieCover(_selMovie);
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void AddRule()
        {
            _rules.Add(new Rule { Stated = _selGenre, Movie = _selMovie });
            RefreshRules();
        }
        void ClearRules() { _rules.Clear(); RefreshRules(); }

        void RefreshRules()
        {
            var sb = new StringBuilder();
            if (GameManager.I != null && GameManager.I.Run.HasFlag(FlagAgeRule))
                sb.AppendLine("00 IF AGE < RATING THEN SWAP FOR SAFE TAPE");
            if (_rules.Count == 0 && sb.Length == 0) { _rulesText.text = "<no rules — robot will refund everyone>"; return; }
            for (int i = 0; i < _rules.Count; i++)
            {
                var m = GameData.Movies[_rules[i].Movie];
                sb.AppendLine($"{i + 1:00} IF WANTS={GenreInfo.Name(_rules[i].Stated)} THEN '{m.Title}' [{GenreInfo.RatingLabel(m.Rating)}]");
            }
            _rulesText.text = sb.ToString();
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            RefreshRules();
            MadFactBootstrap.I.Storefront.SetLine(16);
        }
        public void Close() { _root.SetActive(false); if (AudioTension.I != null) AudioTension.I.Silence(); }

        void RunBatch()
        {
            if (_running) return;
            StartCoroutine(RunBatchRoutine());
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
            _summary.text = "";
            _log.text = "C:\\STORE> RUN AUTOSERVE.BAT\n";

            int batch = GameManager.I.TrustScaledCustomers(12);
            if (batch < 12)
                _log.text += $"<color=#E0C266>trust is low — only {batch} customers in line</color>\n";

            var pool = BatchPool();
            var rng = new System.Random();
            int earned = 0, perfect = 0, close = 0, terrible = 0;
            float potential = 0f;

            // The trust lesson: the first time a horror rule exists that would hand an
            // R-rated tape to a kid, Timmy is guaranteed to walk in and take it home.
            bool kidPrimed = !GameManager.I.Run.HasFlag(FlagKidIncident) && FindRatedRHorrorRule() >= 0;
            int kidSlot = kidPrimed ? Mathf.Min(2, batch - 1) : -1;

            for (int n = 0; n < batch; n++)
            {
                CustomerData cust = (n == kidSlot)
                    ? GameData.CustomerByName("TIMMY")
                    : pool[rng.Next(pool.Count)];
                potential += Economy.PerfectPay;

                int matchRule = -1;
                for (int r = 0; r < _rules.Count; r++)
                    if (_rules[r].Stated == cust.StatedGenre) { matchRule = r; break; }

                string line;
                if (matchRule < 0)
                {
                    terrible++; earned += Economy.Refund;
                    if (AudioTension.I != null) AudioTension.I.Buzzer();
                    line = $"> <color=#F05A66>✕</color> {cust.Name}: wants {GenreInfo.Name(cust.StatedGenre)} — NO RULE {Money(Economy.Refund)}";
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
                            _log.text += $"> <color=#E0C266>⚠ AGE CHECK: no safe {GenreInfo.Name(cust.StatedGenre)} tape for {cust.Name} (age {cust.Age}) — politely declined</color>\n";
                            yield return new WaitForSecondsRealtime(0.18f);
                            continue;
                        }
                    }

                    // the incident: an R-rated tape goes home with a nine-year-old
                    if (n == kidSlot && !GameData.AgeOk(cust, movie))
                    {
                        _log.text += $"> <color=#F05A66>⚠</color> {cust.Name} (age {cust.Age}): wants {GenreInfo.Name(cust.StatedGenre)} — rule {matchRule + 1:00} hands over '{movie.Title}' [{GenreInfo.RatingLabel(movie.Rating)}]\n";
                        yield return new WaitForSecondsRealtime(0.7f);
                        yield return KidIncident(movie);
                        // the mother's refund + fine
                        earned += -25; terrible++;
                        _log.text += $"> <color=#F05A66>✕ REFUND + FINE (inappropriate rental) {Money(-25)}</color>\n";
                        yield return new WaitForSecondsRealtime(0.3f);
                        continue;
                    }

                    float sat = GameData.TrueRating(cust, movie);
                    float err = 5f - sat;
                    var tier = Economy.Tier(err);
                    int pay = Economy.Pay(tier);
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
                            $"earned ${earned} of ${potential:0} possible — {acc * 100f:0}% effective";
            _log.text += "\nC:\\STORE> _\n";

            _running = false;
            _runBtn.interactable = true;

            // verdict
            if (_rules.Count > 0)
            {
                if (GameManager.I.Money >= GameManager.Level2Goal && !MadFactBootstrap.I.Level2Cleared)
                {
                    MadFactBootstrap.I.Level2Cleared = true;
                    yield return new WaitForSecondsRealtime(0.8f);
                    Close();
                    MadFactBootstrap.I.OnLevel2Goal();
                }
                else if (terrible >= 3 && !_complainedOnce)
                {
                    _complainedOnce = true;
                    yield return new WaitForSecondsRealtime(0.6f);
                    MadFactBootstrap.I.Comms.Show(Speaker.Robot, new[]
                    {
                        "ANALYSIS: " + terrible + " CUSTOMERS COULD NOT BE SERVED.",
                        "MY RULES ARE RIGID. TASTE IS NOT. THIS WILL NOT SCALE.",
                        "RECOMMENDATION: ADJUST THE RULES. OR ACQUIRE A REAL ALGORITHM."
                    });
                }
            }
        }

        int FindRatedRHorrorRule()
        {
            for (int r = 0; r < _rules.Count; r++)
                if (_rules[r].Stated == Genre.Horror &&
                    GameData.Movies[_rules[r].Movie].Rating == AgeRating.R) return r;
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
                "It's rated R! He slept in our bed for a week the last time he saw a COMMERCIAL for one of these.",
                "I want a refund. And I'm telling every parent on the block."
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
                            "A RULE THAT ONLY READS THE REQUEST IS BLIND TO THE PERSON MAKING IT."
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
                "Change nothing — the rule matched correctly"
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
                            "A HUMAN NOTICED WHAT MY RULES COULD NOT. LOGGED FOR THE RECORD."
                        }, () => comms.Show(Speaker.OldDude, new[]
                        {
                            "THAT'S the lesson, kid. A machine does exactly what you say — nothing more.",
                            "If nobody's watching what it does, it'll do the wrong thing very, very fast.",
                            "That's why you never let a computer make decisions without human oversight."
                        }, ResumeBatch));
                        break;
                    case 1:
                        comms.Show(Speaker.Robot, new[]
                        {
                            "ANALYSIS: KIDS RENT 30% OF ALL CARTOONS. BANNING THEM BANS THEIR MONEY.",
                            "ALSO: TIMMY DID NOTHING WRONG. THE RULE DID. RECONSIDER."
                        }, AskWhatToDo);
                        break;
                    default:
                        comms.Show(Speaker.Robot, new[]
                        {
                            "IF NOTHING CHANGES, THE SAME INPUT PRODUCES THE SAME REFUND.",
                            "THAT IS NOT A PREDICTION. THAT IS A RULE. RECONSIDER."
                        }, AskWhatToDo);
                        break;
                }
            });
        }

        void ResumeBatch() { _incidentPause = false; }
    }
}
