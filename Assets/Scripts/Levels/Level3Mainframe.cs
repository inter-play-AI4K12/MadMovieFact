using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 4 — Collaborative Filtering (Matrix Factorization). The CRT Mainframe shows a
    /// Customers x Movies grid. Each known cell has a Target and a live Guess = 1 + 4·dot(U,V).
    /// Clicking a row (customer) or column (movie) opens four plastic latent-feature sliders.
    /// Tuning one cell breaks others (red glow + audio static): the player feels the coupling.
    /// The OPTIMIZER runs real gradient descent to balance the whole board at once.
    /// </summary>
    public class Level3Mainframe : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        MfModel _previewModel;
        MfModel M => GameManager.I != null ? GameManager.I.Matrix : (_previewModel ??= new MfModel());

        Image[,] _cellBg; Image[,] _cellGlow; Text[,] _cellGuess; Text[,] _cellTarget;
        Button[] _rowBtn; Button[] _colBtn;
        Image[] _rowSel; Image[] _colSel;

        [SerializeField] Slider[] _sliders = new Slider[4];
        [SerializeField] Text[] _sliderVal = new Text[4];
        [SerializeField] Text _editLabel, _lossLabel, _hint;
        [SerializeField] Image _lossFill;
        [SerializeField] Button _optimizeBtn, _resetBtn;

        bool _revealed;
        bool _editingRow;
        int _editIndex = -1;
        bool _optimizing;
        bool _suppressSliderEvents;
        readonly float[] _lastTelemetryValue = { float.NaN, float.NaN, float.NaN, float.NaN };
        readonly float[] _lastTelemetryTime = { -999f, -999f, -999f, -999f };

        // ---- two-stage flow ----------------------------------------------
        // Stage 1: the student fills the blank cells of the ground-truth ledger by
        // reading the patterns (collaborative filtering by hand). Stage 2: the sliders
        // unlock — four UNLABELED dials per row/column — and the optimizer takes over.
        bool _stage2;
        int[,] _stage1Guess;
        GameObject _sliderPanel, _pickerRoot, _lossBox;
        Text _pickerLabel;
        Button[,] _cellBtn;
        int _pickI = -1, _pickJ = -1;
        // A single slider drag fires OnValueChanged dozens of times, so raw event counts
        // trigger the power-on beat almost instantly. Gate on wall-clock time playing with
        // stage 2 plus a real count of distinct dials actually touched instead.
        float _stage2StartTime = -1f;
        readonly HashSet<int> _slidersSeen = new HashSet<int>();
        const float PowerOnMinSeconds = 60f;
        const int PowerOnMinSliders = 10;   // distinct (entity, dimension) dials changed
        const int MainframeStageBase = 100; // first of three post-optimizer payout stages
        const string FlagStage1Done = "mf_stage1_done";
        const string FlagStage1Intro = "mf_stage1_intro";
        const string FlagPowered = "mf_optimizer_powered";

        const int CellW = 70, CellH = 50, GapX = 6, GapY = 6, RowHeadW = 116, ColHeadH = 40;

        void Awake()
        {
            if (_root == null) return;
            // Multidimensional UI arrays are not serialized by Unity. Rebuild the lookup
            // table from stable prefab object names, then restore interaction callbacks.
            int rows = GameData.MatrixCustomers.Length;
            int cols = GameData.MatrixMovieSet.Count;   // the fictional mainframe stock
            _cellBg = new Image[rows, cols]; _cellGlow = new Image[rows, cols];
            _cellGuess = new Text[rows, cols]; _cellTarget = new Text[rows, cols];
            _rowBtn = new Button[rows]; _colBtn = new Button[cols];
            _rowSel = new Image[rows]; _colSel = new Image[cols];

            for (int j = 0; j < cols; j++)
            {
                int index = j;
                _colBtn[j] = UIFactory.FindDeep<Button>(transform, "Col" + j);
                _colSel[j] = UIFactory.FindDeep<Image>(_colBtn[j].transform, "Sel");
                _colBtn[j].onClick.RemoveAllListeners();
                _colBtn[j].onClick.AddListener(() => SelectCol(index));
            }
            for (int i = 0; i < rows; i++)
            {
                int index = i;
                _rowBtn[i] = UIFactory.FindDeep<Button>(transform, "Row" + i);
                _rowSel[i] = UIFactory.FindDeep<Image>(_rowBtn[i].transform, "Sel");
                _rowBtn[i].onClick.RemoveAllListeners();
                _rowBtn[i].onClick.AddListener(() => SelectRow(index));
                for (int j = 0; j < cols; j++)
                {
                    var cell = UIFactory.FindDeep<Image>(transform, $"C{i}_{j}");
                    _cellBg[i, j] = cell;
                    _cellGlow[i, j] = UIFactory.FindDeep<Image>(cell.transform, "Glow");
                    _cellTarget[i, j] = UIFactory.FindDeep<Text>(cell.transform, "T");
                    _cellGuess[i, j] = UIFactory.FindDeep<Text>(cell.transform, "G");
                }
            }
            for (int d = 0; d < _sliders.Length; d++)
            {
                int index = d;
                _sliders[d].onValueChanged.RemoveAllListeners();
                _sliders[d].onValueChanged.AddListener(value => OnSlider(index, value));
            }
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
            Bind("Reset", ResetTastes);
            Bind("Optimize", RunOptimizer);
        }

        void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level3Mainframe Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level3");
            UIFactory.Fill(UIFactory.RT(go));
            var lvl = go.AddComponent<Level3Mainframe>();
            lvl.Build(go.transform);
            return lvl;
        }

        void Build(Transform parent)
        {
            // translucent overlay: the corporate-era backdrop lives on the storefront behind
            _root = UIFactory.Image(parent, "Level3Mainframe", new Color(0, 0, 0, 0.55f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            // CRT screen
            var screen = UIFactory.Image(_root.transform, "Screen", Theme.CrtBg);
            UIFactory.Place(UIFactory.RT(screen.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(920, 480), new Vector2(0, 10));

            var title = UIFactory.Text(screen.transform, "Title", "█ MAD-FACT MAINFRAME ░ MATRIX FACTORIZATION ENGINE █", 16, Theme.CrtGreen, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(700, 22), new Vector2(20, -12));

            BuildGrid(screen.transform);
            BuildSliderPanel(screen.transform);
            BuildBottom(screen.transform);

            // CRT overlays (scanlines + vignette) on top of everything
            var scan = UIFactory.Image(screen.transform, "Scanlines", new Color(1, 1, 1, 1), Theme.Scanlines, Image.Type.Tiled, raycast: false);
            UIFactory.Fill(UIFactory.RT(scan.gameObject));
            var vig = UIFactory.Image(screen.transform, "Vignette", Color.white, Theme.Vignette, Image.Type.Simple, raycast: false);
            UIFactory.Fill(UIFactory.RT(vig.gameObject));
            // faint phosphor glow tint
            var tint = UIFactory.Image(screen.transform, "Tint", new Color(0.2f, 1f, 0.4f, 0.04f), null, Image.Type.Simple, raycast: false);
            UIFactory.Fill(UIFactory.RT(tint.gameObject));

            var leave = UIFactory.Button(screen.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.CrtBgSoft, 16, Theme.Typewriter, Theme.CrtGreen);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-12, -10));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);
        }

        void BuildGrid(Transform screen)
        {
            int rows = M.Rows, cols = M.Cols;
            var gridRoot = UIFactory.Node(screen, "Grid");
            var grt = UIFactory.RT(gridRoot);
            grt.anchorMin = new Vector2(0, 1); grt.anchorMax = new Vector2(0, 1); grt.pivot = new Vector2(0, 1);
            grt.anchoredPosition = new Vector2(20, -44);
            grt.sizeDelta = new Vector2(RowHeadW + cols * (CellW + GapX), ColHeadH + rows * (CellH + GapY));

            _cellBg = new Image[rows, cols]; _cellGlow = new Image[rows, cols];
            _cellGuess = new Text[rows, cols]; _cellTarget = new Text[rows, cols];
            _rowBtn = new Button[rows]; _colBtn = new Button[cols];
            _rowSel = new Image[rows]; _colSel = new Image[cols];

            // column headers (movies)
            for (int j = 0; j < cols; j++)
            {
                int cj = j;
                var b = UIFactory.Button(gridRoot.transform, "Col" + j, M.Movies[j].Short.Replace(" ", "\n"), () => SelectCol(cj), Theme.CrtBgSoft, 11, Theme.Typewriter, Theme.CrtGreen);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(CellW, ColHeadH - 2), new Vector2(RowHeadW + j * (CellW + GapX), 0));
                UIFactory.ButtonIcon(b, ArtSprites.MatrixCover(j), 14f);
                b.GetComponentInChildren<Text>().fontSize = 9;
                _colBtn[j] = b;
                _colSel[j] = UIFactory.Image(b.transform, "Sel", new Color(1, 1, 0.4f, 0.25f), null, Image.Type.Simple, false);
                UIFactory.Fill(UIFactory.RT(_colSel[j].gameObject)); _colSel[j].gameObject.SetActive(false);
            }

            for (int i = 0; i < rows; i++)
            {
                int ci = i;
                var cust = M.Customers[i];
                var b = UIFactory.Button(gridRoot.transform, "Row" + i, cust.Name, () => SelectRow(ci), Theme.CrtBgSoft, 12, Theme.Typewriter, cust.Underserved ? Theme.CrtAmber : Theme.CrtGreen);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(RowHeadW - 4, CellH), new Vector2(0, -(ColHeadH + i * (CellH + GapY))));
                UIFactory.ButtonIcon(b, ArtSprites.CustomerPortrait(cust.Name), 28f);
                var t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap;
                _rowBtn[i] = b;
                _rowSel[i] = UIFactory.Image(b.transform, "Sel", new Color(1, 1, 0.4f, 0.25f), null, Image.Type.Simple, false);
                UIFactory.Fill(UIFactory.RT(_rowSel[i].gameObject)); _rowSel[i].gameObject.SetActive(false);

                for (int j = 0; j < cols; j++)
                {
                    var cell = UIFactory.Bevel(gridRoot.transform, $"C{i}_{j}", Theme.CrtBgSoft, sunken: true);
                    UIFactory.Place(UIFactory.RT(cell.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(CellW, CellH),
                        new Vector2(RowHeadW + j * (CellW + GapX), -(ColHeadH + i * (CellH + GapY))));
                    _cellBg[i, j] = cell;

                    // stage-1: blank ledger cells are clickable fill-in targets
                    var cellBtn = cell.gameObject.AddComponent<Button>();
                    cellBtn.transition = Selectable.Transition.None;
                    int bi = i, bj = j;
                    cellBtn.onClick.AddListener(() => OnCellClicked(bi, bj));
                    if (_cellBtn == null) _cellBtn = new Button[rows, cols];
                    _cellBtn[i, j] = cellBtn;

                    var glow = UIFactory.Image(cell.transform, "Glow", new Color(1, 0, 0, 0), Theme.Glow, Image.Type.Simple, false);
                    UIFactory.Fill(UIFactory.RT(glow.gameObject), -6, -6, -6, -6);
                    _cellGlow[i, j] = glow;

                    var tgt = UIFactory.Text(cell.transform, "T", "", 11, Theme.CrtGreenDim, Theme.Typewriter, TextAnchor.UpperLeft, false);
                    UIFactory.Place(UIFactory.RT(tgt.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(CellW, 16), new Vector2(4, -2));
                    _cellTarget[i, j] = tgt;

                    var gss = UIFactory.Text(cell.transform, "G", "", 22, Theme.CrtGreen, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                    UIFactory.Fill(UIFactory.RT(gss.gameObject), 0, 12, 0, 0);
                    _cellGuess[i, j] = gss;
                }
            }
        }

        void BuildSliderPanel(Transform screen)
        {
            var panel = UIFactory.Bevel(screen.transform, "Sliders", new Color(0.10f, 0.14f, 0.10f), sunken: true);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(300, 320), new Vector2(-20, -44));
            _sliderPanel = panel.gameObject;

            _editLabel = UIFactory.Text(panel.transform, "Edit", "SELECT A ROW OR COLUMN", 14, Theme.CrtAmber, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_editLabel.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 40), new Vector2(0, -8));

            // The dials are deliberately UNLABELED: the whole point of matrix
            // factorization is that the machine discovers what they mean on its own.
            var dialGrey = new Color(0.55f, 0.62f, 0.55f);
            for (int d = 0; d < 4; d++)
            {
                int cd = d;
                float x = -114 + d * 76;
                var nameT = UIFactory.Text(panel.transform, "SN" + d, "FEATURE\n" + (d + 1), 9, dialGrey, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(nameT.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 34), new Vector2(x, -50));

                var s = UIFactory.VSlider(panel.transform, 0f, 1.2f, 0.5f, dialGrey, v => OnSlider(cd, v));
                UIFactory.Place(UIFactory.RT(s.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(46, 146), new Vector2(x, -98));
                _sliders[d] = s;

                _sliderVal[d] = UIFactory.Text(panel.transform, "SV" + d, "0.50", 12, dialGrey, Theme.Typewriter, TextAnchor.UpperCenter, false);
                UIFactory.Place(UIFactory.RT(_sliderVal[d].gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 18), new Vector2(x, -250));
            }
            SetSlidersInteractable(false);

            BuildStage1Picker(screen);
        }

        /// <summary>Stage-1 rating picker; occupies the slider panel's spot while it's hidden.</summary>
        void BuildStage1Picker(Transform screen)
        {
            var picker = UIFactory.Bevel(screen.transform, "Picker", new Color(0.10f, 0.14f, 0.10f), sunken: true);
            UIFactory.Place(UIFactory.RT(picker.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(300, 320), new Vector2(-20, -44));
            _pickerRoot = picker.gameObject;

            _pickerLabel = UIFactory.Text(picker.transform, "PickLbl",
                "STAGE 1 — FILL THE LEDGER\n\nClick a ? cell on the board,\nthen give your best guess.", 13, Theme.CrtAmber, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_pickerLabel.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(260, 130), new Vector2(0, -14));

            for (int v = 1; v <= 5; v++)
            {
                int val = v;
                var b = UIFactory.Button(picker.transform, "Pick" + v, v + "★", () => PickValue(val), Theme.CrtBgSoft, 16, Theme.Typewriter, Theme.CrtGreen);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(230, 32), new Vector2(0, 16 + (5 - v) * 36));
            }
            _pickerRoot.SetActive(false);
        }

        void BuildBottom(Transform screen)
        {
            // loss meter
            var lossBox = UIFactory.Bevel(screen.transform, "LossBox", Theme.CrtBgSoft, sunken: true);
            UIFactory.Place(UIFactory.RT(lossBox.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(460, 30), new Vector2(20, 16));
            _lossBox = lossBox.gameObject;
            var lossBar = UIFactory.Image(lossBox.transform, "BarBg", new Color(0, 0, 0, 0.5f));
            UIFactory.Place(UIFactory.RT(lossBar.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(250, 16), new Vector2(150, 0));
            _lossFill = UIFactory.Image(lossBar.transform, "Fill", Theme.ErrorRed);
            var lf = UIFactory.RT(_lossFill.gameObject); lf.anchorMin = new Vector2(0, 0); lf.anchorMax = new Vector2(0, 1); lf.pivot = new Vector2(0, 0.5f);
            lf.offsetMin = Vector2.zero; lf.offsetMax = Vector2.zero; lf.sizeDelta = new Vector2(0, 0);
            _lossLabel = UIFactory.Text(lossBox.transform, "LossLbl", "TOTAL ERROR", 13, Theme.CrtGreen, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_lossLabel.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 24), new Vector2(8, 0));

            _hint = UIFactory.Text(screen.transform, "Hint", "Tip: fixing one customer often breaks another. Feel the friction, then hit OPTIMIZE.", 12, Theme.CrtGreenDim, Theme.Typewriter, TextAnchor.MiddleLeft, true);
            UIFactory.Place(UIFactory.RT(_hint.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(460, 30), new Vector2(20, 50));

            _resetBtn = UIFactory.Button(screen.transform, "Reset", "RESET", ResetTastes, Theme.CrtBgSoft, 13, Theme.Typewriter, Theme.CrtGreen);
            UIFactory.Place(UIFactory.RT(_resetBtn.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(150, 34), new Vector2(-180, 16));
            UIFactory.ButtonIcon(_resetBtn, ArtSprites.Reset(), 24f);

            _optimizeBtn = UIFactory.Button(screen.transform, "Optimize", "RUN OPTIMIZER", RunOptimizer, Theme.CrtBgSoft, 16, Theme.SystemSans, Theme.CrtAmber);
            UIFactory.Place(UIFactory.RT(_optimizeBtn.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(180, 38), new Vector2(-20, 14));
            UIFactory.ButtonIcon(_optimizeBtn, ArtSprites.Optimize(), 28f);
        }

        // ---- Open / refresh ----------------------------------------------
        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            MadFactBootstrap.I.Storefront.SetLine(0);

            _stage2 = GameManager.I.Run.HasFlag(FlagStage1Done);
            if (_stage1Guess == null)
                _stage1Guess = new int[M.Rows, M.Cols];
            if (_stage2 && _stage2StartTime < 0f) { _stage2StartTime = Time.unscaledTime; _slidersSeen.Clear(); }
            ApplyStage();
            Deselect();
            RefreshGrid();

            if (!_stage2 && !GameManager.I.Run.HasFlag(FlagStage1Intro))
            {
                GameManager.I.Run.SetFlag(FlagStage1Intro);
                MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
                {
                    "The mainframe kept a LEDGER: five customers, five tapes, one-to-five stars.",
                    "But five entries never got logged. Before any machine touches this board — YOU fill them in.",
                    "How? Read the PATTERNS. If two customers agree everywhere else, they'll probably agree on the blanks.",
                    "Click a ? cell and give me your best guess."
                });
            }
        }
        public void Close() { _root.SetActive(false); if (AudioTension.I != null) AudioTension.I.Silence(); }

        void ApplyStage()
        {
            bool powered = GameManager.I.Run.HasFlag(FlagPowered);
            if (_sliderPanel != null) _sliderPanel.SetActive(_stage2);
            if (_pickerRoot != null) _pickerRoot.SetActive(!_stage2);
            if (_lossBox != null) _lossBox.SetActive(_stage2);
            if (_optimizeBtn != null) _optimizeBtn.gameObject.SetActive(_stage2 && powered);
            if (_resetBtn != null) _resetBtn.gameObject.SetActive(_stage2);
            if (_hint != null)
                _hint.text = !_stage2
                    ? "STAGE 1 — five ratings are missing from the ledger. Fill each ? by reading the rows and columns around it."
                    : powered
                        ? "STAGE 2 — the optimizer is powered. Hit OPTIMIZE and watch it guess-and-check every dial."
                        : "STAGE 2 — four unlabeled dials per row and column. Grab a row or a tape and tune the board by hand.";
        }

        // ---- Stage 1: fill in the blanks ----------------------------------
        void OnCellClicked(int i, int j)
        {
            if (_stage2 || M.Known[i, j] || _stage1Guess == null) return;
            _pickI = i; _pickJ = j;
            _pickerLabel.text = $"HOW WOULD\n{M.Customers[i].Name}\nRATE '{M.Movies[j].Title}'?\n\nRead their row.\nRead the tape's column.";
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void PickValue(int value)
        {
            if (_stage2 || _pickI < 0) return;
            _stage1Guess[_pickI, _pickJ] = value;
            _pickI = -1; _pickJ = -1;
            if (AudioTension.I != null) AudioTension.I.Clunk();

            int filled = 0, blanks = 0;
            for (int i = 0; i < M.Rows; i++)
                for (int j = 0; j < M.Cols; j++)
                    if (!M.Known[i, j]) { blanks++; if (_stage1Guess[i, j] > 0) filled++; }

            _pickerLabel.text = filled < blanks
                ? $"LOGGED {filled} OF {blanks}.\n\nClick the next ? cell."
                : "LEDGER COMPLETE.\nChecking against the vault copy...";
            RefreshGrid();
            if (filled >= blanks) Stage1Evaluate();
        }

        void Stage1Evaluate()
        {
            int correct = 0, blanks = 0;
            for (int i = 0; i < M.Rows; i++)
                for (int j = 0; j < M.Cols; j++)
                {
                    if (M.Known[i, j]) continue;
                    blanks++;
                    float truth = GameData.TrueRating(M.Customers[i], M.Movies[j]);
                    bool ok = Mathf.Abs(_stage1Guess[i, j] - truth) <= 0.75f;
                    if (ok) correct++;
                    _cellTarget[i, j].text = "t" + (Mathf.Round(truth * 2f) / 2f).ToString("0.0");
                    _cellGuess[i, j].color = ok ? new Color(0.33f, 0.95f, 0.40f) : Theme.ErrorRed;
                }

            if (AudioTension.I != null) { if (correct >= 3) AudioTension.I.ChaChing(); else AudioTension.I.Buzzer(); }

            MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
            {
                $"Vault copy says: {correct} of {blanks} within one star. " + (correct >= 4 ? "Sharp eyes, kid." : correct >= 2 ? "Not bad for a first shift." : "Rough — but watch what comes next."),
                "Notice HOW you guessed: you compared ROWS. Similar customers rate similarly. That's COLLABORATIVE FILTERING, done by hand.",
                "Now — every customer and every tape gets four hidden dials. No names, no labels. The MACHINE doesn't know what they mean — it just crunches numbers.",
                "Your job: tune them until the board agrees with the ledger. Grab a row, drag a dial, watch the numbers. Off you go."
            }, () =>
            {
                GameManager.I.Run.SetFlag(FlagStage1Done);
                _stage2 = true;
                _stage2StartTime = Time.unscaledTime;
                ApplyStage();
                RefreshGrid();
            });
        }

        void SelectRow(int i)
        {
            if (!_stage2) return;
            _editingRow = true; _editIndex = i;
            HighlightSelection();
            var u = M.U[i];
            _editLabel.text = "CUSTOMER: " + M.Customers[i].Name + "\n(four hidden dials)";
            LoadSliders(u);
            SetSlidersInteractable(true);
            MaybePowerOn();
            MadFactLokiLogger.Instance?.Log("collaborative_filter_entity_selected",
                "Player selected a customer taste vector", new
                {
                    level_id = 4,
                    entity_type = "customer",
                    entity_id = M.Customers[i].Name
                });
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void SelectCol(int j)
        {
            if (!_stage2) return;
            _editingRow = false; _editIndex = j;
            HighlightSelection();
            var v = M.V[j];
            _editLabel.text = "MOVIE: " + M.Movies[j].Title + "\n(four hidden dials)";
            LoadSliders(v);
            SetSlidersInteractable(true);
            MaybePowerOn();
            MadFactLokiLogger.Instance?.Log("collaborative_filter_entity_selected",
                "Player selected a movie feature vector", new
                {
                    level_id = 4,
                    entity_type = "movie",
                    entity_id = M.Movies[j].Title
                });
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void Deselect()
        {
            _editingRow = false; _editIndex = -1;
            HighlightSelection();
            _editLabel.text = "SELECT A ROW (customer)\nOR COLUMN (movie)";
            SetSlidersInteractable(false);
        }

        void HighlightSelection()
        {
            for (int i = 0; i < M.Rows; i++) _rowSel[i].gameObject.SetActive(_editingRow && _editIndex == i);
            for (int j = 0; j < M.Cols; j++) _colSel[j].gameObject.SetActive(!_editingRow && _editIndex == j);
        }

        void LoadSliders(Latent v)
        {
            _suppressSliderEvents = true;
            for (int d = 0; d < 4; d++) { _sliders[d].value = v[d]; _sliderVal[d].text = v[d].ToString("0.00"); }
            _suppressSliderEvents = false;
        }

        void SetSlidersInteractable(bool on)
        {
            for (int d = 0; d < 4; d++) if (_sliders[d] != null) _sliders[d].interactable = on;
        }

        void OnSlider(int d, float value)
        {
            if (_suppressSliderEvents || _editIndex < 0) return;
            float previous = _editingRow ? M.U[_editIndex][d] : M.V[_editIndex][d];
            if (_editingRow) M.U[_editIndex][d] = value; else M.V[_editIndex][d] = value;
            _sliderVal[d].text = value.ToString("0.00");
            // one distinct dial (this entity + this dimension) counts once no matter how
            // many OnValueChanged events the drag that touched it fired.
            _slidersSeen.Add((_editingRow ? 1000 : 0) + _editIndex * 4 + d);
            RefreshGrid();
            MaybePowerOn();

            if (Time.unscaledTime - _lastTelemetryTime[d] >= 0.2f ||
                float.IsNaN(_lastTelemetryValue[d]) ||
                Mathf.Abs(value - _lastTelemetryValue[d]) >= 0.15f)
            {
                _lastTelemetryTime[d] = Time.unscaledTime;
                _lastTelemetryValue[d] = value;
                MadFactLokiLogger.Instance?.Log("collaborative_filter_value_changed",
                    "Player changed a collaborative filtering value", new
                    {
                        level_id = 4,
                        entity_type = _editingRow ? "customer" : "movie",
                        entity_id = _editingRow ? M.Customers[_editIndex].Name : M.Movies[_editIndex].Title,
                        dimension = Latent.Names[d],
                        previous_value = previous,
                        new_value = value,
                        mean_error = M.MeanError()
                    });
            }
        }

        /// <summary>
        /// Fires the power-on beat only once the player has genuinely spent time at the
        /// dials by BOTH measures — real elapsed time AND a real count of distinct dials
        /// touched — firing at whichever of the two is satisfied second.
        /// </summary>
        void MaybePowerOn()
        {
            if (_optimizing || _stage2StartTime < 0f || GameManager.I.Run.HasFlag(FlagPowered)) return;
            bool longEnough = Time.unscaledTime - _stage2StartTime >= PowerOnMinSeconds;
            bool enoughSliders = _slidersSeen.Count >= PowerOnMinSliders;
            if (longEnough && enoughSliders) PowerOnBeat();
        }

        void PowerOnBeat()
        {
            var comms = MadFactBootstrap.I.Comms;
            comms.Show(Speaker.OldDude, new[]
            {
                "Wait. Hold on. Kid. Have you been spinning those dials YOURSELF this whole time?",
                "Oh no. Oh, that's my fault. I forgot to POWER ON the optimizer. The part of the machine that does the machine part.",
                "It does exactly what you've been doing — nudge a dial, check the error, keep what helped — except a few thousand times a minute, without lunch breaks.",
                "Flipping the big switch... NOW. Hit OPTIMIZE and watch it work. You've earned the sit-down."
            }, () =>
            {
                GameManager.I.Run.SetFlag(FlagPowered);
                ApplyStage();
                if (AudioTension.I != null) AudioTension.I.Whir();
            });
        }

        void ResetTastes()
        {
            M.ResetCustomerTaste();
            if (_editingRow && _editIndex >= 0) LoadSliders(M.U[_editIndex]);
            RefreshGrid();
            MadFactLokiLogger.Instance?.Log("collaborative_filter_values_reset",
                "Player reset collaborative filtering customer values",
                new { level_id = 4, mean_error = M.MeanError() });
            if (AudioTension.I != null) AudioTension.I.Whir();
        }

        void RefreshGrid()
        {
            // Stage 1 shows the plain ledger: big known ratings, ? blanks, no model
            // guesses, no error glow, no audio tension. The machine hasn't started.
            if (!_stage2)
            {
                for (int i = 0; i < M.Rows; i++)
                    for (int j = 0; j < M.Cols; j++)
                    {
                        _cellGlow[i, j].color = Color.clear;
                        _cellBg[i, j].color = Theme.CrtBgSoft;
                        if (M.Known[i, j])
                        {
                            _cellTarget[i, j].text = "";
                            _cellGuess[i, j].text = M.Target[i, j].ToString("0.0");
                            _cellGuess[i, j].color = Theme.CrtGreen;
                        }
                        else
                        {
                            int guessed = _stage1Guess != null ? _stage1Guess[i, j] : 0;
                            _cellTarget[i, j].text = "";
                            _cellGuess[i, j].text = guessed > 0 ? guessed.ToString("0") : "?";
                            _cellGuess[i, j].color = Theme.CrtAmber;
                        }
                    }
                return;
            }

            for (int i = 0; i < M.Rows; i++)
                for (int j = 0; j < M.Cols; j++)
                {
                    float g = M.Guess(i, j);
                    if (M.Known[i, j])
                    {
                        float t = M.Target[i, j];
                        float err = Mathf.Abs(t - g);
                        _cellTarget[i, j].text = "t" + t.ToString("0.0");
                        _cellGuess[i, j].text = g.ToString("0.0");
                        var c = ErrColor(err);
                        _cellGuess[i, j].color = c;
                        _cellGlow[i, j].color = new Color(1f, 0.1f, 0.1f, GlowAlpha(err));
                        _cellBg[i, j].color = Color.Lerp(Theme.CrtBgSoft, new Color(0.25f, 0.04f, 0.04f), GlowAlpha(err));
                    }
                    else
                    {
                        _cellTarget[i, j].text = _revealed ? "pred" : "—";
                        _cellGuess[i, j].text = _revealed ? g.ToString("0.0") : "?";
                        _cellGuess[i, j].color = _revealed ? new Color(0.5f, 0.9f, 1f) : Theme.CrtGreenDim;
                        _cellGlow[i, j].color = new Color(0.4f, 0.8f, 1f, _revealed ? 0.25f : 0f);
                        _cellBg[i, j].color = Theme.CrtBgSoft;
                    }
                }
            UpdateLossAndAudio();
        }

        void UpdateLossAndAudio()
        {
            float mean = M.MeanError();
            float worst = M.WorstError();
            float tension = 0.5f * mean + 0.5f * worst;
            if (AudioTension.I != null) AudioTension.I.SetError(tension);

            float norm = Mathf.Clamp01(mean / 2f);
            _lossFill.color = ErrColor(mean);
            var lf = UIFactory.RT(_lossFill.gameObject);
            lf.anchorMax = new Vector2(norm, 1f);
            _lossLabel.text = "ERROR " + mean.ToString("0.00");
        }

        static Color ErrColor(float e)
        {
            if (e < 1f) return Color.Lerp(new Color(0.33f, 0.95f, 0.40f), new Color(0.95f, 0.7f, 0.2f), e);
            return Color.Lerp(new Color(0.95f, 0.7f, 0.2f), new Color(0.9f, 0.18f, 0.22f), Mathf.Clamp01((e - 1f) / 1.5f));
        }
        static float GlowAlpha(float e) => Mathf.Clamp01((e - 0.6f) / 1.8f) * 0.9f;

        // ---- The Optimizer (gradient descent) ----------------------------
        void RunOptimizer()
        {
            if (_optimizing) return;
            MadFactLokiLogger.Instance?.Log("optimizer_started",
                "Player started the collaborative filtering optimizer",
                new { level_id = 4, initial_mean_error = M.MeanError() });
            StartCoroutine(OptimizeRoutine());
        }

        IEnumerator OptimizeRoutine()
        {
            _optimizing = true;
            _optimizeBtn.interactable = false; _resetBtn.interactable = false;
            SetSlidersInteractable(false);
            if (AudioTension.I != null) AudioTension.I.Whir();

            // The show: the machine visibly does what the player was doing — it walks
            // the board row by row, column by column, twisting each set of dials a
            // little, checking the error, keeping what helps. Guess and check, fast.
            string[] verbs = { "nudging", "twisting", "testing", "wiggling", "second-guessing", "re-tuning" };
            const int passes = 56;          // focus shifts across rows/columns
            const int stepsPerPass = 5;     // gradient steps shown per focus
            for (int p = 0; p < passes; p++)
            {
                bool onRow = (p % 2) == 0;
                int idx = (p / 2) % (onRow ? M.Rows : M.Cols);
                _editingRow = onRow; _editIndex = idx;
                HighlightSelection();
                string focus = onRow ? M.Customers[idx].Name : M.Movies[idx].Short;
                _editLabel.text = (onRow ? "CUSTOMER: " : "TAPE: ") + focus + "\n(machine at the dials)";
                _hint.text = $"MACHINE: {verbs[p % verbs.Length]} {focus}'s dials — guess, check, keep what helps.  ERROR {M.MeanError():0.00}";

                for (int s = 0; s < stepsPerPass; s++)
                {
                    M.StepGradient(0.004f);   // tuned: stable convergence
                    // show THIS row/column's dials physically moving
                    _suppressSliderEvents = true;
                    var vec = onRow ? M.U[idx] : M.V[idx];
                    for (int d = 0; d < 4; d++) { _sliders[d].value = vec[d]; _sliderVal[d].text = vec[d].ToString("0.00"); }
                    _suppressSliderEvents = false;
                    RefreshGrid();
                    yield return new WaitForSecondsRealtime(0.04f);
                }
                if (p % 6 == 0 && AudioTension.I != null) AudioTension.I.Whir();
                if (M.MeanError() < 0.04f) break;
            }

            Deselect();
            // reveal hidden predictions
            _revealed = true;
            RefreshGrid();
            if (AudioTension.I != null) { AudioTension.I.Silence(); AudioTension.I.Clunk(); }

            // The till keeps climbing through the rest of the sequence in three payout
            // stages, each grown off CURRENT trust — so whatever the player just did (like
            // the Gibbs choice below) is felt immediately in the next payout instead of
            // sitting invisibly in a meter nobody's watching. Each stage is followed by a
            // genuine ~12s observation window, not just a beat — long enough to actually
            // read the money and trust numbers, not just glimpse them changing.
            yield return new WaitForSecondsRealtime(1.5f);
            int payout = GrowMainframePayout(MainframeStageBase, "BALANCED. Empty cells filled with predictions.");

            _optimizing = false;
            _resetBtn.interactable = true;
            MadFactLokiLogger.Instance?.Log("optimizer_completed",
                "Collaborative filtering optimizer completed",
                new { level_id = 4, final_mean_error = M.MeanError(), payout });

            yield return ObservationPause(12f);

            // Money on the table attracts vultures: Gibbs makes his pitch mid-level,
            // right when the machine has just proven how profitable personalization is.
            bool pitching = true;
            PrivacyScenario.Play(MadFactBootstrap.I.Comms, () => pitching = false);
            yield return new WaitUntil(() => !pitching);

            // whatever just happened with Gibbs is already baked into trust by now — grow
            // the SAME running total off it, so accepting his offer visibly caps how much
            // the machine earns next instead of just moving a number nobody sees again.
            yield return new WaitForSecondsRealtime(0.6f);
            payout = GrowMainframePayout(payout, "The machine keeps compounding what it learned.");
            yield return ObservationPause(12f);

            // the crowd's math shows its other face next: popularity bias, via Iris's
            // complaint.
            if (!MadFactBootstrap.I.Level4Cleared)
            {
                MadFactBootstrap.I.Level4Cleared = true;
                PopularityBiasScene(() =>
                {
                    GrowMainframePayout(payout, "Still climbing.");
                    StartCoroutine(FinishLevel4Goal());
                });
            }
            else _optimizeBtn.interactable = true;
        }

        /// <summary>
        /// The underserved-cluster highlight comes AFTER Iris's scene fully resolves,
        /// timed to land with "you see that cluster?" — not sitting unexplained through
        /// a scene that isn't about it — with an observation window first so the last
        /// payout is actually read before the hint bar changes underneath it.
        /// </summary>
        IEnumerator FinishLevel4Goal()
        {
            yield return ObservationPause(10f);
            HighlightUnderserved();
            MadFactBootstrap.I.OnLevel4Goal();
        }

        /// <summary>
        /// Holds whatever message is already on the hint bar for a beat, then switches to
        /// an explicit "go look at your stats" prompt for the rest of the window. A long
        /// silent pause with nothing moving on screen reads as the game hanging unless
        /// something on screen explains that the wait is deliberate.
        /// </summary>
        IEnumerator ObservationPause(float totalSeconds, float promptAfter = 4f)
        {
            float firstLeg = Mathf.Min(promptAfter, totalSeconds);
            yield return new WaitForSecondsRealtime(firstLeg);
            _hint.text = "Take a moment — check your MONEY and TRUST above.";
            float remaining = totalSeconds - firstLeg;
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        }

        /// <summary>
        /// Popularity bias beat: the optimizer's filled predictions crown the big hit,
        /// an indie filmmaker protests, and Pellings quizzes the player on why the
        /// crowd's math buries small tapes.
        /// </summary>
        void PopularityBiasScene(System.Action then)
        {
            _hint.text = "PREDICTIONS FILLED. 'STAR DRIFTER' now tops 4 of 5 customers' lists.";
            var comms = MadFactBootstrap.I.Comms;
            var iris = ArtSprites.CustomerPortrait("INDIE IRIS");

            comms.ShowNamed("INDIE IRIS  (independent filmmaker)", "INCOMING COMPLAINT", iris, new[]
            {
                "Hey! Basement guy! Your machine only recommends the big blockbuster to EVERYONE now!",
                "I made 'QUASAR RUN' with two lamps and a borrowed camera, and it's GOOD.",
                "But nobody rates what nobody's shown, and nobody's shown what nobody rates. See the problem?!"
            }, () => comms.Show(Speaker.OldDude, new[]
            {
                "She's got a point, kid. Look at the board — the tape with the most ratings wins every column.",
                "Quick — tell me WHY the crowd's math piles onto the big hit."
            }, () => comms.AskChoice(Speaker.OldDude,
                "QUIZ: 'STAR DRIFTER' tops every list and 'QUASAR RUN' never gets shown. Why?", new[]
            {
                "Popular tapes have the most ratings, so the math is most confident about them",
                "The mainframe reads each movie's budget and always favors the expensive ones",
                "Small movies always get worse ratings, so hiding them is correct behavior"
            }, pick =>
            {
                string[] verdict = pick == 0
                    ? new[]
                    {
                        "Exactly. The crowd's data is thickest around what's already popular.",
                        "More recommendations, more rentals, more ratings — the loop feeds itself.",
                        "That's POPULARITY BIAS. The little tapes never get the EXPOSURE to prove themselves.",
                        "Remember Iris. A fair system has to spend some recommendations on the long shots."
                    }
                    : new[]
                    {
                        "Nope. The machine doesn't know budgets, and 'QUASAR RUN' is terrific.",
                        "It's the DATA: popular tapes have the most ratings, so the math is surest about them.",
                        "Sure bets get recommended, get rented, get rated — the loop feeds itself. POPULARITY BIAS.",
                        "The little tapes never get the EXPOSURE to prove themselves. Remember Iris."
                    };
                comms.Show(Speaker.OldDude, verdict, then);
            })));
        }

        /// <summary>
        /// Grows a running payout by a factor read from CURRENT trust — 0.7x at zero trust
        /// up to 1.7x at full trust — and pays it out with the usual cha-ching + popup.
        /// Trust is read live (not captured once) so a swing from the Gibbs choice between
        /// stages changes what the NEXT stage earns, not just some invisible meter.
        /// </summary>
        int GrowMainframePayout(int previous, string message)
        {
            float factor = 0.7f + GameManager.I.Trust / 100f;
            int payout = Mathf.Max(1, Mathf.RoundToInt(previous * factor));
            Vector2 pop = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            GameManager.I.AddMoney(payout);
            GameManager.I.RecordSale(0f, pop); // cha-ching + popup
            _hint.text = $"{message} +${payout}  (trust {GameManager.I.Trust}%)";
            return payout;
        }

        void HighlightUnderserved()
        {
            for (int i = 0; i < M.Rows; i++)
            {
                if (!M.Customers[i].Underserved) continue;
                _rowSel[i].color = new Color(1f, 0.3f, 0.3f, 0.35f);
                _rowSel[i].gameObject.SetActive(true);
                for (int j = 0; j < M.Cols; j++)
                    _cellGlow[i, j].color = new Color(1f, 0.4f, 0.2f, 0.5f);
            }
            _hint.text = "ALERT: one customer cluster rates EVERYTHING low. A market gap?";
        }
    }
}
