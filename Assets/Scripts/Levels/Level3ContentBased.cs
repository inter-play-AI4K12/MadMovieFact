using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 3 — Content-Based Recommendation. The TASTE-MATCH 3000 builds a genre profile
    /// from each customer's rental history, scores every box against it, and hands the
    /// player its TOP PICKS — the player mostly just approves the machine's best match.
    /// Two truths get taught the hard way:
    ///  - recommending only what matches the profile narrows the profile (filter bubble),
    ///  - the box can't see what's inside (features said 99%, the customer said "meh").
    /// </summary>
    public class Level3ContentBased : MonoBehaviour
    {
        class Visit
        {
            public string Customer;
            public string[] Arrival;               // what they say walking up
            public string Note;                    // engine status line
            public float[] EngineProfile;          // null = the customer's real GenreTaste
            public bool RequireBubbleBreak;        // only an out-of-bubble pick advances
            public bool Finale;                    // Tibbs beat: wraps the level afterwards
            public string ProfileCaption = "PROFILE FROM RENTAL HISTORY";
            public Color CaptionColor = default;
        }

        [SerializeField] GameObject _root;
        Image _portrait;
        Text _name, _quip, _note, _profileCaption, _result;
        readonly Image[] _profileFills = new Image[GenreInfo.Count];
        Button _nextBtn;
        PosterBrowser _browser;
        GameObject _suggestRoot;
        readonly List<Button> _suggestButtons = new List<Button>();

        List<Visit> _visits;
        int _visitIndex;
        bool _recommended;
        bool _locked;
        CustomerData _cust;
        Visit _visit;

        const string FlagBubbleLesson = "filter_bubble_lesson";
        const string FlagIntro = "l3_engine_intro";

        void Awake()
        {
            if (_root == null) return;
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
        }

        void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level3ContentBased Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level3ContentBased");
            UIFactory.Fill(UIFactory.RT(go));
            var lvl = go.AddComponent<Level3ContentBased>();
            lvl.Build(go.transform);
            lvl._root.SetActive(false);
            return lvl;
        }

        void Build(Transform parent)
        {
            // translucent overlay: the startup-era backdrop lives on the storefront behind
            _root = UIFactory.Image(parent, "Level3ContentBased", new Color(0, 0, 0, 0.4f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            var window = UIFactory.DialogWindow(_root.transform, "Window", Theme.Face);
            UIFactory.Place(UIFactory.RT(window.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900, 470), new Vector2(0, 10));

            var tb = UIFactory.Image(window.transform, "TitleBar", new Color(0.06f, 0.09f, 0.10f, 1f));
            var tbr = UIFactory.RT(tb.gameObject); tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(-8, 26); tbr.anchoredPosition = new Vector2(0, -6);
            var tt = UIFactory.Text(tb.transform, "T", "TASTE-MATCH 3000  ·  CONTENT-BASED RECOMMENDATION", 12, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(tt.gameObject), 8, 0, 8, 0);

            BuildProfilePanel(window.transform);

            // ---- engine output column (middle): status + auto-generated top picks ----
            var noteBg = UIFactory.Bevel(window.transform, "NoteBg", new Color(0.05f, 0.09f, 0.06f), sunken: true);
            UIFactory.Place(UIFactory.RT(noteBg.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(256, 64), new Vector2(328, -40));
            _note = UIFactory.Text(noteBg.transform, "Note", "", 11, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Fill(UIFactory.RT(_note.gameObject), 8, 6, 8, 6);

            var pickLbl = UIFactory.Text(window.transform, "PickLbl", "ENGINE TOP PICKS — click one to serve", 12, Theme.Ink, Theme.SystemSans, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(pickLbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(256, 18), new Vector2(330, -110));

            _suggestRoot = UIFactory.Node(window.transform, "Suggestions");
            UIFactory.Place(UIFactory.RT(_suggestRoot), new Vector2(0, 1), new Vector2(0, 1), new Vector2(256, 270), new Vector2(328, -130));

            var how = UIFactory.Text(window.transform, "How",
                "MATCH = profile × box · or browse the shelf →", 10, Theme.InkSoft, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(how.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(300, 16), new Vector2(330, 14));

            _result = UIFactory.Text(window.transform, "Result", "", 14, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_result.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(560, 26), new Vector2(18, 52));

            _nextBtn = UIFactory.Button(window.transform, "Next", "NEXT CUSTOMER", NextVisit, Theme.Cash, 16, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(_nextBtn.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(220, 36), new Vector2(18, 12));
            UIFactory.ButtonIcon(_nextBtn, ArtSprites.Next(), 24f);
            _nextBtn.gameObject.SetActive(false);

            _browser = PosterBrowser.Create(window.transform, "Shelf");
            UIFactory.Place(UIFactory.RT(_browser.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(292, 384), new Vector2(-12, -40));
            _browser.OnRecommend = Recommend;
            _browser.DetailExtra = mi =>
            {
                if (_cust == null) return "";
                int pct = Mathf.RoundToInt(EngineMatch(mi) * 100f);
                return "ENGINE MATCH: " + pct + "%";
            };

            var leave = UIFactory.Button(window.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-8, -7));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);
        }

        void BuildProfilePanel(Transform window)
        {
            var panel = UIFactory.Bevel(window.transform, "ProfilePanel", Theme.Face, sunken: true);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, 384), new Vector2(16, -40));

            var pf = UIFactory.Bevel(panel.transform, "PortraitFrame", Theme.FaceDark, sunken: true);
            UIFactory.Place(UIFactory.RT(pf.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, 78), new Vector2(12, -12));
            _portrait = UIFactory.Image(pf.transform, "P", Color.white);
            _portrait.preserveAspect = true;
            UIFactory.Fill(UIFactory.RT(_portrait.gameObject), 4, 4, 4, 4);

            _name = UIFactory.Text(panel.transform, "Name", "", 15, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_name.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 40), new Vector2(94, -14));
            _quip = UIFactory.Text(panel.transform, "Quip", "", 12, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(_quip.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 46), new Vector2(94, -46));

            _profileCaption = UIFactory.Text(panel.transform, "PCap", "PROFILE FROM RENTAL HISTORY", 11, Theme.Ink, Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_profileCaption.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(276, 18), new Vector2(12, -100));

            for (int g = 0; g < GenreInfo.Count; g++)
            {
                float y = -124 - g * 30;
                // keep clear of the sunken bevel's chunky left border (~26px at this canvas scale)
                var lbl = UIFactory.Text(panel.transform, "pl" + g, GenreInfo.Names[g], 11, Theme.Ink, Theme.SystemSans, TextAnchor.UpperLeft, false);
                UIFactory.Place(UIFactory.RT(lbl.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, 20), new Vector2(30, y - 2));

                var bg = UIFactory.Image(panel.transform, "pb" + g, new Color(0, 0, 0, 0.22f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(160, 14), new Vector2(120, y - 3));
                var fill = UIFactory.Image(bg.transform, "pf" + g, GenreInfo.Colors[g], null, Image.Type.Simple, false);
                var frt = UIFactory.RT(fill.gameObject);
                frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                _profileFills[g] = fill;
            }
        }

        // ---- visit script --------------------------------------------------
        // Wendell's filter-bubble arc is spread across five visits, interleaved with
        // other customers so it reads as a relationship curdling over time rather than
        // an instant complaint: stoked -> happy -> a little tired -> outright groaning
        // -> stuck in the loop. Each other-customer visit is a single light beat —
        // per Erfan, the mechanic itself stays simple; only the pacing got longer.
        List<Visit> BuildVisits() => new List<Visit>
        {
            new Visit
            {
                Customer = "ROSA",
                Arrival = new[] { "Ooh, is this that new computer thing? Fine. Something that makes my heart do the thing." },
                Note = "> NEW PROFILE: ROSA\n> history: 14 romance rentals\n> ranking every box..._"
            },
            new Visit
            {
                Customer = "WENDELL",
                Arrival = new[] { "Back again! The gizmo knows I like space, right? Show me what it's got." },
                Note = "> NEW PROFILE: WENDELL\n> history: sci-fi, sci-fi, sci-fi\n> ranking every box..._"
            },
            new Visit
            {
                Customer = "EARL",
                Arrival = new[] { "The machine reads the boxes? Hmph. I read the boxes for free. Real footage, please." },
                Note = "> NEW PROFILE: EARL\n> history: documentaries only\n> ranking every box..._"
            },
            new Visit
            {
                Customer = "WENDELL",
                Arrival = new[] { "Two for two! This little machine's got my number." },
                Note = "> RETURNING: WENDELL (x2)\n> profile still matches strongly\n> ranking every box..._"
            },
            new Visit
            {
                Customer = "DOT",
                Arrival = new[] { "I want stuff blowin' up. That's the whole ask." },
                Note = "> NEW PROFILE: DOT\n> history: action, action, action\n> ranking every box..._"
            },
            new Visit
            {
                Customer = "WENDELL",
                Arrival = new[] { "Me again... the machine only ever shows me the space shelf now. Which — fair. But still." },
                Note = "> RETURNING: WENDELL (x3)\n> every serve reinforced SCI-FI\n> other genres losing exposure_",
                EngineProfile = Narrowed("WENDELL", 0.45f),
                ProfileCaption = "ENGINE PROFILE (NARROWING)",
                CaptionColor = new Color(0.85f, 0.65f, 0.2f)
            },
            new Visit
            {
                Customer = "WENDELL",
                Arrival = new[] { "Oh. Let me guess. Another one with a spaceship on the cover. ...Ugh, NO — okay, fine. Give it here." },
                Note = "> RETURNING: WENDELL (x4)\n> <color=#E0C266>profile narrowing further</color>\n> diversity: LOW_",
                EngineProfile = Narrowed("WENDELL", 0.25f),
                ProfileCaption = "ENGINE PROFILE (NARROWER STILL)",
                CaptionColor = new Color(0.90f, 0.50f, 0.20f)
            },
            new Visit
            {
                Customer = "WENDELL",
                Arrival = new[]
                {
                    "Okay, STOP. Every single time it's the same space tapes. I'm stuck in a LOOP here!",
                    "I know I like space! But is this ALL I am to that thing?!"
                },
                Note = "> RETURNING: WENDELL (x5)\n> <color=#F05A66>WARNING: profile overfit</color>\n> diversity: CRITICAL_",
                EngineProfile = Narrowed("WENDELL", 0.12f),
                ProfileCaption = "ENGINE PROFILE (OVERFIT!)",
                CaptionColor = Theme.ErrorRed,
                RequireBubbleBreak = true
            },
            new Visit
            {
                Customer = "THE TIBBS TWINS",
                Arrival = new[] { "The computer people said the machine GETS us. Scary AND funny. Prove it, machine." },
                Note = "> NEW PROFILE: TIBBS TWINS\n> history: horror + comedy\n> ranking every box..._",
                Finale = true
            },
        };

        static float[] Narrowed(string customer, float keep)
        {
            var c = GameData.CustomerByName(customer);
            var p = new float[GenreInfo.Count];
            for (int i = 0; i < GenreInfo.Count; i++)
                p[i] = i == (int)Genre.SciFi ? 1f : c.GenreTaste[i] * keep;
            return p;
        }

        float[] EffectiveProfile => _visit != null && _visit.EngineProfile != null ? _visit.EngineProfile : _cust != null ? _cust.GenreTaste : null;

        float EngineMatch(int movieIndex)
        {
            var profile = EffectiveProfile;
            if (profile == null) return 0f;
            var m = GameData.Movies[movieIndex];
            float dot = 0f, ca = 0f, cb = 0f;
            for (int i = 0; i < GenreInfo.Count; i++)
            {
                dot += profile[i] * m.Features[i];
                ca += profile[i] * profile[i];
                cb += m.Features[i] * m.Features[i];
            }
            if (ca <= 0f || cb <= 0f) return 0f;
            return Mathf.Clamp01(dot / (Mathf.Sqrt(ca) * Mathf.Sqrt(cb)));
        }

        /// <summary>The genre contributing most to this match — the engine "shows its work".</summary>
        (Genre genre, float p, float b) TopOverlap(int movieIndex)
        {
            var profile = EffectiveProfile;
            var m = GameData.Movies[movieIndex];
            int best = 0; float bestV = -1f;
            for (int g = 0; g < GenreInfo.Count; g++)
            {
                float v = profile[g] * m.Features[g];
                if (v > bestV) { bestV = v; best = g; }
            }
            return ((Genre)best, profile[best], m.Features[best]);
        }

        // ---- engine suggestions ---------------------------------------------
        void RebuildSuggestions()
        {
            foreach (Transform child in _suggestRoot.transform) Destroy(child.gameObject);
            _suggestButtons.Clear();

            // rank the catalog, skip anything this customer already took home
            var ranked = new List<int>();
            for (int i = 0; i < GameData.Movies.Count; i++)
                if (!GameManager.I.Run.HasServed(_cust.Name, GameData.Movies[i].Title)) ranked.Add(i);
            ranked.Sort((a, b) => EngineMatch(b).CompareTo(EngineMatch(a)));

            for (int s = 0; s < 3 && s < ranked.Count; s++)
            {
                int mi = ranked[s];
                var m = GameData.Movies[mi];
                int pct = Mathf.RoundToInt(EngineMatch(mi) * 100f);
                var overlap = TopOverlap(mi);

                var card = UIFactory.Button(_suggestRoot.transform, "Suggest" + s, "", () => Recommend(mi), Theme.Plastic, 11, Theme.SystemSans);
                UIFactory.Place(UIFactory.RT(card.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(256, 84), new Vector2(0, -s * 90));
                card.interactable = !_locked;
                _suggestButtons.Add(card);

                var poster = UIFactory.Image(card.transform, "Thumb", Color.white, ArtSprites.MovieCover(mi), Image.Type.Simple, false);
                poster.preserveAspect = true;
                UIFactory.Place(UIFactory.RT(poster.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 70), new Vector2(6, 0));

                var title = UIFactory.Text(card.transform, "T", (s + 1) + ". " + m.Title, 10, Theme.TitleText, Theme.SystemSans, TextAnchor.UpperLeft, true, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 26), new Vector2(58, -6));

                string loopTag = _visit.RequireBubbleBreak && m.Primary == Genre.SciFi ? "  <color=#F05A66>(LOOP?)</color>" : "";
                var match = UIFactory.Text(card.transform, "M", "MATCH " + pct + "%" + loopTag, 11, Theme.Cash, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(match.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 16), new Vector2(58, -36));

                var why = UIFactory.Text(card.transform, "W",
                    $"why: {GenreInfo.Name(overlap.genre)} {overlap.p:0.0}×{overlap.b:0.0}", 10, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Italic);
                UIFactory.Place(UIFactory.RT(why.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, 14), new Vector2(58, -54));

                // remove the default empty label's raycast confusion — keep as is (label is empty)
                var label = card.GetComponentInChildren<Text>();
                if (label != null && string.IsNullOrEmpty(label.text)) label.raycastTarget = false;
            }
        }

        void SetLocked(bool locked)
        {
            _locked = locked;
            _browser.SetLocked(locked);
            foreach (var b in _suggestButtons) if (b != null) b.interactable = !locked;
        }

        // ---- flow ------------------------------------------------------------
        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            MadFactBootstrap.I.Storefront.SetLine(12);
            _visits = BuildVisits();
            _visitIndex = 0;

            if (!GameManager.I.Run.HasFlag(FlagIntro))
            {
                GameManager.I.Run.SetFlag(FlagIntro);
                MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
                {
                    "New office, new machine. The TASTE-MATCH 3000 reads what's PRINTED on every box.",
                    "It keeps a genre PROFILE for each customer, built from their rental history.",
                    "Profile times box features — that's the MATCH score. The machine ranks the shelf and hands you its top picks.",
                    "Your job's easy now: look at the picks, click the best one. ...Keep an eye on it, though. Machines get tunnel vision."
                }, ShowVisit);
            }
            else ShowVisit();
        }

        public void Close() => _root.SetActive(false);

        void NextVisit()
        {
            _visitIndex++;
            if (_visitIndex >= _visits.Count) { Complete(); return; }
            ShowVisit();
        }

        void ShowVisit()
        {
            _visit = _visits[_visitIndex];
            _cust = GameData.CustomerByName(_visit.Customer);
            _recommended = false;
            _result.text = "";
            _nextBtn.gameObject.SetActive(false);
            SetLocked(false);
            _browser.SetGenre(_cust.StatedGenre);

            _name.text = _cust.Name + "  (" + _cust.Age + ")";
            _quip.text = "“" + _cust.Quip + "”";
            _note.text = _visit.Note;
            _portrait.sprite = ArtSprites.CustomerPortrait(_cust.Name);
            _profileCaption.text = _visit.ProfileCaption;
            _profileCaption.color = _visit.CaptionColor == default ? Theme.Ink : _visit.CaptionColor;

            var profile = EffectiveProfile;
            for (int g = 0; g < GenreInfo.Count; g++)
            {
                var frt = UIFactory.RT(_profileFills[g].gameObject);
                frt.anchorMax = new Vector2(Mathf.Clamp01(profile[g]), 1f);
                _profileFills[g].color = _visit.RequireBubbleBreak && g == (int)Genre.SciFi
                    ? Theme.ErrorRed : GenreInfo.Colors[g];
            }

            RebuildSuggestions();

            // the filter-bubble beat interrupts before the player may serve
            if (_visit.RequireBubbleBreak && !GameManager.I.Run.HasFlag(FlagBubbleLesson))
            {
                SetLocked(true);
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, _visit.Arrival, BubbleLesson);
            }
            else
            {
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, _visit.Arrival);
            }
        }

        void BubbleLesson()
        {
            var comms = MadFactBootstrap.I.Comms;
            comms.Show(Speaker.OldDude, new[]
            {
                "Hear that, kid? That's the FILTER BUBBLE popping its head up.",
                "Look at the ENGINE TOP PICKS — space, space, space. The engine only suggests what matches his profile.",
                "And every serve narrows the profile further. The other tapes never get EXPOSURE. Round and round he goes.",
                "Pop quiz, kid. Show me you see it."
            }, () => comms.AskChoice(Speaker.OldDude,
                "QUIZ: Why does Wendell keep seeing the same space tapes?", new[]
            {
                "The engine only suggests what matches his profile, so it never widens",
                "The store stopped stocking new sci-fi, so there is nothing left to show",
                "His member card expired, so the engine deleted his rental history"
            }, pick =>
            {
                GameManager.I.Run.SetFlag(FlagBubbleLesson);
                string[] verdict = pick == 0
                    ? new[]
                    {
                        "THAT'S IT. The system feeds on its own output — match, narrow, match, narrow.",
                        "Now BREAK the loop. Ignore the machine's picks. Grab him something from a DIFFERENT shelf that still fits.",
                        "Hint: flip boxes on other shelves. Some of them list a little SCI-FI in the small bars."
                    }
                    : new[]
                    {
                        "Not quite. The shelf's full and Wendell's still Wendell.",
                        "It's the LOOP: the engine suggests what matches, the profile narrows, repeat.",
                        "Now BREAK it. Ignore the machine's picks. Grab him something from a DIFFERENT shelf that still fits.",
                        "Hint: flip boxes on other shelves. Some of them list a little SCI-FI in the small bars."
                    };
                comms.Show(Speaker.OldDude, verdict, () => SetLocked(false));
            }));
        }

        void Recommend(int mi)
        {
            if (_recommended || _locked) return;
            var movie = GameData.Movies[mi];

            // The engine already filters served titles out of its own top picks, so a
            // repeat can only happen via the manual shelf browser. Allow it, but it's a
            // bad sale and a trust hit rather than a free no-op — same rule as Level 1.
            if (GameManager.I.Run.HasServed(_cust.Name, movie.Title))
            {
                _recommended = true;
                SetLocked(true);
                if (AudioTension.I != null) AudioTension.I.Buzzer();
                var pop0 = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 40);
                var repeatTier = GameManager.I.RecordSale(4f, pop0); // forced-terrible error
                GameManager.I.AddTrust(-5);
                GameManager.I.Run.RecordRecommendation("l3_visit_" + _visitIndex, _cust.Name, Phase.Level3, movie, repeatTier, 1f);
                _result.text = $"ALREADY SEEN  —  {movie.Title}";
                _result.color = Theme.ErrorRed;
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, new[]
                    { $"'{movie.Title}'? I've already SEEN that one. That's kind of the whole problem." },
                    () => _nextBtn.gameObject.SetActive(true));
                return;
            }

            // the bubble visit only advances on an out-of-bubble pick
            if (_visit.RequireBubbleBreak)
            {
                bool breaksBubble = movie.Primary != Genre.SciFi && movie.Features[(int)Genre.SciFi] >= 0.35f;
                if (!breaksBubble)
                {
                    if (AudioTension.I != null) AudioTension.I.Buzzer();
                    _result.text = "STILL IN THE LOOP — try a different shelf";
                    _result.color = Theme.ErrorRed;
                    MadFactBootstrap.I.Comms.ShowCustomer(_cust, movie.Primary == Genre.SciFi
                        ? new[] { "That's the SAME SHELF. That's the loop! That's the thing I'm complaining about!" }
                        : new[] { "I mean... it's different, but it's got NOTHING for me. Space, remember? Some space." });
                    return;
                }
            }

            _recommended = true;
            SetLocked(true);

            float satisfaction = GameData.TrueRating(_cust, movie);
            float error = 5f - satisfaction;
            int enginePct = Mathf.RoundToInt(EngineMatch(mi) * 100f);

            if (AudioTension.I != null) AudioTension.I.Clunk();
            Vector2 pop = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 40);
            var tier = GameManager.I.RecordSale(error, pop);
            GameManager.I.Run.RecordRecommendation("l3_visit_" + _visitIndex, _cust.Name, Phase.Level3, movie, tier, satisfaction);
            MadFactLokiLogger.Instance?.Log("movie_recommended", "Player accepted a content-based movie recommendation", new
            {
                interaction_id = "l3_visit_" + _visitIndex,
                level_id = 3,
                customer_id = _cust.Name,
                movie_id = movie.Title,
                engine_match_percent = enginePct,
                satisfaction,
                sale_tier = tier.ToString(),
                filter_bubble_break = _visit.RequireBubbleBreak
            });
            MadFactLokiLogger.Instance?.Log("interaction_completed", "Content-based recommendation completed", new
            {
                interaction_id = "l3_visit_" + _visitIndex,
                outcome = tier.ToString(),
                engine_match_percent = enginePct
            });

            string stars = new string('★', Mathf.RoundToInt(satisfaction)) + new string('·', 5 - Mathf.RoundToInt(satisfaction));
            _result.text = $"ENGINE {enginePct}%  →  {Economy.TierLabel(tier)}  [{stars}]";
            _result.color = Economy.TierColor(tier);

            if (_visit.RequireBubbleBreak)
            {
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, new[]
                {
                    $"'{movie.Title}'? Off the {GenreInfo.Name(movie.Primary)} shelf? For ME?",
                    "...huh. It's got space stuff IN it. I'd never have found this back in my loop.",
                    "Okay. Okay! The machine's forgiven. Mostly."
                }, () => MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
                {
                    "See what you did there? YOU added the diversity — the engine never would have.",
                    "A content engine can't leave the profile on its own. Someone has to widen the window."
                }, () => _nextBtn.gameObject.SetActive(true)));
            }
            else if (_visit.Finale)
            {
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, tier == SaleTier.Perfect
                    ? new[] { "Whoa. Okay. The machine gets us!" }
                    : new[]
                    {
                        $"The screen said {enginePct}% match. This is NOT {enginePct}%.",
                        "It's close! But it's missing the... the joke inside the scream. You know?"
                    }, () => MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
                {
                    $"Look at that: the box said {enginePct}%, the FACE said 'meh'.",
                    "The box only lists what's PRINTED on it. It can't see what's inside the tape — or inside the customer.",
                    "Scary-AND-funny isn't a label anyone prints. It's a hidden vibe.",
                    "To find THAT, we don't read boxes. We let customers rate tapes and find the pattern. Basement. Now."
                }, Complete));
            }
            else
            {
                string[] reaction = tier == SaleTier.Perfect
                    ? new[] { "Yes! Exactly this. The machine can stay." }
                    : tier == SaleTier.Close
                        ? new[] { $"The screen said {enginePct}%... it's fine. It's fine. It's a rental." }
                        : new[] { $"{enginePct} percent?! The machine and I need to have a TALK." };
                MadFactBootstrap.I.Comms.ShowCustomer(_cust, reaction, () => _nextBtn.gameObject.SetActive(true));
            }
        }

        void Complete()
        {
            if (MadFactBootstrap.I.Level3Cleared) { Close(); MadFactBootstrap.I.GoStorefront(); return; }
            MadFactBootstrap.I.Level3Cleared = true;
            Close();
            MadFactBootstrap.I.OnContentBasedGoal();
        }
    }
}
