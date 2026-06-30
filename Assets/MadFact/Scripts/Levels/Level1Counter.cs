using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Level 1 — The Manual Era. The player reviews a physical customer file, may ask a
    /// limited number of clarifying questions (action economy), then recommends a tape.
    /// Payment scales with how well the recommendation matches the customer's true taste.
    /// The growing line proves manual labour can't scale.
    /// </summary>
    public class Level1Counter : MonoBehaviour
    {
        GameObject _root;
        Text _name, _history, _stated, _quip, _notes, _qLeft, _result;
        readonly List<Button> _questionButtons = new List<Button>();
        readonly List<Button> _movieButtons = new List<Button>();
        Button _nextBtn;
        Image _portrait;
        Text _portraitInitial;

        CustomerData _cust;
        int _questionsLeft;
        int _served;
        bool _recommended;
        readonly System.Random _rng = new System.Random(12345);

        public static Level1Counter Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level1");
            var lvl = go.AddComponent<Level1Counter>();
            lvl.Build(canvas);
            lvl._root.SetActive(false);
            return lvl;
        }

        void Build(Transform canvas)
        {
            _root = UIFactory.Image(canvas, "Level1Counter", new Color(0, 0, 0, 0.35f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            var window = UIFactory.Bevel(_root.transform, "Window", Theme.Face);
            UIFactory.Place(UIFactory.RT(window.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900, 470), new Vector2(0, 10));

            // title bar
            var tb = UIFactory.Image(window.transform, "TitleBar", Theme.TitleBar);
            var tbr = UIFactory.RT(tb.gameObject); tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(-8, 26); tbr.anchoredPosition = new Vector2(0, -6);
            var tt = UIFactory.Text(tb.transform, "T", "THE COUNTER — manual recommendation terminal", 14, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(tt.gameObject), 8, 0, 8, 0);

            BuildFile(window.transform);
            BuildQuestions(window.transform);
            BuildRecommend(window.transform);

            _result = UIFactory.Text(window.transform, "Result", "", 16, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_result.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(600, 28), new Vector2(0, 50));

            _nextBtn = UIFactory.Button(window.transform, "Next", "NEXT CUSTOMER ▶", NextCustomer, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_nextBtn.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(220, 36), new Vector2(0, 14));
            _nextBtn.gameObject.SetActive(false);

            var leave = UIFactory.Button(window.transform, "Leave", "✕", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-8, -7));
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
                var b = UIFactory.Button(window.transform, "Q" + i, "▾ " + _qText[i], () => Ask(idx), Theme.Face, 14, Theme.SystemSans);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(250, 26), new Vector2(330, -86 - i * 30));
                var t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft;
                _questionButtons.Add(b);
            }
        }

        void BuildRecommend(Transform window)
        {
            var lbl = UIFactory.Text(window.transform, "RLbl", "RECOMMEND A TAPE", 14, Theme.Ink, Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(lbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, 20), new Vector2(610, -44));

            for (int i = 0; i < GameData.Movies.Count; i++)
            {
                int idx = i;
                var m = GameData.Movies[i];
                var b = UIFactory.Button(window.transform, "M" + i, m.Title, () => Recommend(idx), Theme.Plastic, 14, Theme.SystemSans);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(250, 30), new Vector2(610, -68 - i * 36));
                var t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft;
                var rt = UIFactory.RT(t.gameObject); rt.offsetMin = new Vector2(8, rt.offsetMin.y);
                _movieButtons.Add(b);
            }
        }

        // ---- Flow --------------------------------------------------------
        public void Open()
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _served = 0;
            NextCustomer();
        }
        public void Close() => _root.SetActive(false);

        void NextCustomer()
        {
            _recommended = false;
            _questionsLeft = 2;
            _result.text = "";
            _notes.text = "";
            _nextBtn.gameObject.SetActive(false);

            // pick a customer (rotate through regulars, occasionally a gap customer to confuse)
            var pool = GameData.Customers;
            _cust = pool[_rng.Next(pool.Count)];

            _name.text = _cust.Name;
            _history.text = "HISTORY: " + _cust.HistoryGenre + " tapes";
            _stated.text = "WANTS: a " + _cust.StatedGenre + " movie";
            _quip.text = "“" + _cust.Quip + "”";
            _portrait.color = _cust.Shirt;
            _portraitInitial.text = _cust.Name.Substring(0, 1);

            foreach (var b in _questionButtons) b.interactable = true;
            foreach (var b in _movieButtons) b.interactable = true;
            UpdateQLeft();

            // the line keeps growing — the bottleneck
            MadFactBootstrap.I.Storefront.SetLine(Mathf.Clamp(2 + _served, 2, 16));
            MadFactBootstrap.I.Storefront.SetSubtitle($"The line is {2 + _served} deep and growing...");
        }

        void UpdateQLeft()
        {
            _qLeft.text = _questionsLeft > 0 ? $"{_questionsLeft} question(s) left before they get impatient" : "They're getting impatient — just RECOMMEND already!";
            if (_questionsLeft <= 0) foreach (var b in _questionButtons) b.interactable = false;
        }

        void Ask(int qi)
        {
            if (_questionsLeft <= 0 || _recommended) return;
            _questionsLeft--;
            float w = _cust.TrueVibe[(int)_qAxis[qi]];
            string ans = w > 0.66f ? "“Oh yes, absolutely!”" : w > 0.33f ? "“Eh, it's fine I guess.”" : "“Ugh, no thank you.”";
            _notes.text += $"Q: {_qText[qi]}\n   {ans}\n";
            if (AudioTension.I != null) AudioTension.I.Beep();
            UpdateQLeft();
        }

        void Recommend(int mi)
        {
            if (_recommended) return;
            _recommended = true;
            var movie = GameData.Movies[mi];
            float satisfaction = GameData.TrueRating(_cust, movie); // 1..5
            float error = 5f - satisfaction;                        // 0 = perfect

            foreach (var b in _questionButtons) b.interactable = false;
            foreach (var b in _movieButtons) b.interactable = false;

            if (AudioTension.I != null) { AudioTension.I.SetError(error); AudioTension.I.Clunk(); }

            // payout at the register position (top-centre-ish)
            Vector2 pop = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 40);
            var tier = GameManager.I.RecordSale(error, pop);
            string stars = new string('★', Mathf.RoundToInt(satisfaction)) + new string('·', 5 - Mathf.RoundToInt(satisfaction));
            _result.text = $"{Economy.TierLabel(tier)}  —  {movie.Title}  [{stars}]";
            _result.color = Economy.TierColor(tier);

            _served++;
            _nextBtn.gameObject.SetActive(true);

            // upgrade check
            if (GameManager.I.Money >= GameManager.Level1Goal && !MadFactBootstrap.I.Level1Cleared)
            {
                MadFactBootstrap.I.Level1Cleared = true;
                _nextBtn.gameObject.SetActive(false);
                Invoke(nameof(TriggerUpgrade), 1.2f);
            }
        }

        void TriggerUpgrade()
        {
            if (AudioTension.I != null) AudioTension.I.Silence();
            Close();
            MadFactBootstrap.I.OnLevel1Goal();
        }
    }
}
