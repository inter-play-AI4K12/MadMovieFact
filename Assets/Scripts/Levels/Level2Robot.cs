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
    /// Hardcoded rules are brittle: customers with contradictory tastes tank the revenue.
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
            var mSel = UIFactory.Button(builder.transform, "MSel", "", () => CycleMovie(1), Theme.Plastic, 13, Theme.SystemSans);
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
            _rulesText = UIFactory.Text(rulesPanel.transform, "Rules", "", 14, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(_rulesText.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, 170), new Vector2(10, -26));

            // ---- DOS output (right) ----
            var crt = UIFactory.Bevel(chassis.transform, "CRT", new Color(0.04f, 0.09f, 0.05f), sunken: true);
            UIFactory.Place(UIFactory.RT(crt.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(420, 358), new Vector2(-18, -46));
            _log = UIFactory.Text(crt.transform, "Log", "C:\\STORE> _\n", 14, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(_log.gameObject), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 320), new Vector2(0, -10));
            UIFactory.RT(_log.gameObject).offsetMin = new Vector2(12, 30);
            UIFactory.RT(_log.gameObject).offsetMax = new Vector2(-12, -10);
            _summary = UIFactory.Text(crt.transform, "Sum", "", 14, Theme.CrtAmber, Theme.Typewriter, TextAnchor.LowerLeft, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_summary.gameObject), new Vector2(0, 0), new Vector2(1, 0), new Vector2(-24, 22), new Vector2(0, 8));

            // ---- bottom buttons ----
            _runBtn = UIFactory.Button(chassis.transform, "Run", "RUN BATCH (12 customers)", RunBatch, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_runBtn.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(420, 38), new Vector2(18, 14));
            UIFactory.ButtonIcon(_runBtn, ArtSprites.Play(), 28f);

            var leave = UIFactory.Button(chassis.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-10, -10));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);

            RefreshSelectors();
            RefreshRules();
        }

        void CycleGenre(int d) { _selGenre = (Genre)(((int)_selGenre + d + 4) % 4); RefreshSelectors(); }
        void CycleMovie(int d) { _selMovie = (_selMovie + d + GameData.Movies.Count) % GameData.Movies.Count; RefreshSelectors(); }

        void RefreshSelectors()
        {
            _genreSel.text = _selGenre.ToString().ToUpper();
            _movieSel.text = GameData.Movies[_selMovie].Title;
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
            if (_rules.Count == 0) { _rulesText.text = "<no rules — robot will refund everyone>"; return; }
            var sb = new StringBuilder();
            for (int i = 0; i < _rules.Count; i++)
                sb.AppendLine($"{i + 1:00} IF WANTS={_rules[i].Stated.ToString().ToUpper()} THEN '{GameData.Movies[_rules[i].Movie].Title}'");
            _rulesText.text = sb.ToString();
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            MadFactBootstrap.I.Storefront.SetLine(16);
        }
        public void Close() { _root.SetActive(false); if (AudioTension.I != null) AudioTension.I.Silence(); }

        void RunBatch()
        {
            if (_running) return;
            StartCoroutine(RunBatchRoutine());
        }

        IEnumerator RunBatchRoutine()
        {
            _running = true;
            _runBtn.interactable = false;
            _summary.text = "";
            _log.text = "C:\\STORE> RUN AUTOSERVE.BAT\n";

            int batch = 12;
            var rng = new System.Random();
            int earned = 0, perfect = 0, close = 0, terrible = 0;
            float potential = 0f;

            for (int n = 0; n < batch; n++)
            {
                var cust = GameData.Customers[rng.Next(GameData.Customers.Count)];
                potential += Economy.PerfectPay;

                int matchRule = -1;
                for (int r = 0; r < _rules.Count; r++)
                    if (_rules[r].Stated == cust.StatedGenre) { matchRule = r; break; }

                string line;
                if (matchRule < 0)
                {
                    terrible++; earned += Economy.Refund;
                    if (AudioTension.I != null) { AudioTension.I.SetError(3f); AudioTension.I.Buzzer(); }
                    line = $"> {cust.Name}: wants {cust.StatedGenre} — NO RULE. refund -$5";
                }
                else
                {
                    var movie = GameData.Movies[_rules[matchRule].Movie];
                    float sat = GameData.TrueRating(cust, movie);
                    float err = 5f - sat;
                    var tier = Economy.Tier(err);
                    int pay = Economy.Pay(tier);
                    earned += pay;
                    if (tier == SaleTier.Perfect) perfect++; else if (tier == SaleTier.Close) close++; else terrible++;
                    if (AudioTension.I != null)
                    {
                        AudioTension.I.SetError(err);
                        if (tier == SaleTier.Perfect) AudioTension.I.Coin(); else if (tier == SaleTier.Terrible) AudioTension.I.Buzzer();
                    }
                    line = $"> {cust.Name}: '{movie.Title}' [{Mathf.RoundToInt(sat)}★] {(pay >= 0 ? "+$" + pay : "-$" + (-pay))}";
                }
                _log.text += line + "\n";
                yield return new WaitForSecondsRealtime(0.18f);
            }

            GameManager.I.AddMoney(earned);
            if (AudioTension.I != null) AudioTension.I.Silence();

            float acc = (perfect * 1f + close * 0.4f) / batch;
            _summary.text = $"BATCH: +${earned}  (perfect {perfect} / close {close} / refunds {terrible})\n" +
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
                else if (terrible >= 3)
                {
                    yield return new WaitForSecondsRealtime(0.6f);
                    MadFactBootstrap.I.Comms.Show(Speaker.Robot, new[]
                    {
                        "ANALYSIS: " + terrible + " CUSTOMERS COULD NOT BE SERVED.",
                        "MY RULES ARE RIGID. TASTE IS NOT. THIS WILL NOT SCALE.",
                        "RECOMMENDATION: ACQUIRE A REAL ALGORITHM."
                    });
                }
            }
        }
    }
}
