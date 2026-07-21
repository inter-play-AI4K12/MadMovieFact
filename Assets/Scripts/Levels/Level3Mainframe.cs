using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Shared interface for Level 4 collaborative filtering and Level 5 matrix
    /// factorization. Level 4 predicts missing ratings by comparing similar customers;
    /// Level 5 introduces latent-factor sliders and optimization.
    /// </summary>
    public class Level3Mainframe : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        MfModel _previewModel;
        MfModel M => GameManager.I != null ? GameManager.I.Matrix : (_previewModel ??= new MfModel());
        MatrixTutorialModel _tutorial;
        CollaborativeFilteringTutorialModel _collabTutorial;
        SparseRatingsTutorialModel _sparseTutorial;

        Image[,] _cellBg; Image[,] _cellGlow; Text[,] _cellGuess; Text[,] _cellTarget;
        Button[] _rowBtn; Button[] _colBtn;
        Text[] _rowLabel; Text[] _colLabel;
        Image[] _rowSel; Image[] _colSel;

        [SerializeField] Slider[] _sliders = new Slider[4];
        [SerializeField] Text[] _sliderName = new Text[4];
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
        bool _miniTutorial;
        bool _miniDialChanged;
        int[,] _stage1Guess;
        [SerializeField] GameObject _sliderPanel, _pickerRoot, _lossBox;
        [SerializeField] Text _pickerLabel;
        [SerializeField] GameObject _standardGrid, _sparseRoot;
        Button[,] _cellBtn;
        Image[,] _sparseCellBg;
        Text[,] _sparseCellValue, _sparseCellOriginal;
        Button[,] _sparseCellButton;
        int[,] _sparseGuess;
        int _sparsePickRow = -1, _sparsePickColumn = -1;
        bool _sparseTask, _sparseEvaluated;
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
        const string FlagStage2Intro = "mf_stage2_two_factor_intro";
        const string FlagGroundMiniDone = "mf_collaborative_2x5_done";
        const string FlagFactorMiniDone = "mf_factor_3x3_two_factor_done";
        const string FlagPowered = "mf_optimizer_powered";

        const int CellW = 70, CellH = 50, GapX = 6, GapY = 6, RowHeadW = 116, ColHeadH = 40;
        const int SparseCellW = 68, SparseCellH = 46, SparseRowHeadW = 108;
        const int AverageTruthRow = 4, AverageTruthColumn = 2;
        const int AverageSourceRowA = 1, AverageSourceRowB = 2;

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
            _rowLabel = new Text[rows]; _colLabel = new Text[cols];
            _rowSel = new Image[rows]; _colSel = new Image[cols];
            _cellBtn = new Button[rows, cols];
            if (_sliderName == null || _sliderName.Length != Latent.Dim)
                _sliderName = new Text[Latent.Dim];

            // These references were originally assigned only while the UI builder ran.
            // Unity does not preserve non-serialized fields in a prefab, so restore every
            // stage-specific panel by its stable authored name before Open() switches stages.
            _sliderPanel = FindObject("Sliders");
            _pickerRoot = FindObject("Picker");
            _lossBox = FindObject("LossBox");
            _standardGrid = FindObject("Grid");
            _sparseRoot = FindObject("SparseMatrix");
            _pickerLabel = UIFactory.FindDeep<Text>(transform, "PickLbl");
            _editLabel = UIFactory.FindDeep<Text>(transform, "Edit");
            _lossLabel = UIFactory.FindDeep<Text>(transform, "LossLbl");
            Transform lossBar = _lossBox != null
                ? UIFactory.FindDeep<Transform>(_lossBox.transform, "BarBg")
                : null;
            _lossFill = lossBar != null ? UIFactory.FindDeep<Image>(lossBar, "Fill") : null;
            _hint = UIFactory.FindDeep<Text>(transform, "Hint");
            _optimizeBtn = UIFactory.FindDeep<Button>(transform, "Optimize");
            _resetBtn = UIFactory.FindDeep<Button>(transform, "Reset");

            for (int j = 0; j < cols; j++)
            {
                int index = j;
                _colBtn[j] = UIFactory.FindDeep<Button>(transform, "Col" + j);
                _colLabel[j] = _colBtn[j].GetComponentInChildren<Text>();
                _colSel[j] = UIFactory.FindDeep<Image>(_colBtn[j].transform, "Sel");
                _colBtn[j].onClick.RemoveAllListeners();
                _colBtn[j].onClick.AddListener(() => SelectCol(index));
            }
            for (int i = 0; i < rows; i++)
            {
                int index = i;
                _rowBtn[i] = UIFactory.FindDeep<Button>(transform, "Row" + i);
                _rowLabel[i] = _rowBtn[i].GetComponentInChildren<Text>();
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
                    int row = i, column = j;
                    _cellBtn[i, j] = cell.GetComponent<Button>();
                    if (_cellBtn[i, j] != null)
                    {
                        _cellBtn[i, j].onClick.RemoveAllListeners();
                        _cellBtn[i, j].onClick.AddListener(() => OnCellClicked(row, column));
                    }
                }
            }
            for (int d = 0; d < _sliders.Length; d++)
            {
                int index = d;
                _sliderName[d] = UIFactory.FindDeep<Text>(transform, "SN" + d);
                _sliders[d].onValueChanged.RemoveAllListeners();
                _sliders[d].onValueChanged.AddListener(value => OnSlider(index, value));
            }
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
            Bind("Reset", ResetTastes);
            Bind("Optimize", RunOptimizer);
            for (int rating = 1; rating <= 5; rating++)
            {
                int selectedRating = rating;
                Bind("Pick" + rating, () => PickValue(selectedRating));
            }
            BindSparseGrid();
        }

        GameObject FindObject(string name)
        {
            Transform found = UIFactory.FindDeep<Transform>(transform, name);
            return found != null ? found.gameObject : null;
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
            BuildSparseGrid(screen.transform);
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
            _standardGrid = gridRoot;
            var grt = UIFactory.RT(gridRoot);
            grt.anchorMin = new Vector2(0, 1); grt.anchorMax = new Vector2(0, 1); grt.pivot = new Vector2(0, 1);
            grt.anchoredPosition = new Vector2(20, -44);
            grt.sizeDelta = new Vector2(RowHeadW + cols * (CellW + GapX), ColHeadH + rows * (CellH + GapY));

            _cellBg = new Image[rows, cols]; _cellGlow = new Image[rows, cols];
            _cellGuess = new Text[rows, cols]; _cellTarget = new Text[rows, cols];
            _rowBtn = new Button[rows]; _colBtn = new Button[cols];
            _rowLabel = new Text[rows]; _colLabel = new Text[cols];
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
                _colLabel[j] = b.GetComponentInChildren<Text>();
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
                _rowLabel[i] = t;
                _rowSel[i] = UIFactory.Image(b.transform, "Sel", new Color(1, 1, 0.4f, 0.25f), null, Image.Type.Simple, false);
                UIFactory.Fill(UIFactory.RT(_rowSel[i].gameObject)); _rowSel[i].gameObject.SetActive(false);

                for (int j = 0; j < cols; j++)
                {
                    // Matrix cells use a plain fill instead of the old inset sprite. This
                    // keeps the numbers crisp and lets the state colour carry the meaning.
                    var cell = UIFactory.Image(gridRoot.transform, $"C{i}_{j}", Theme.CrtBgSoft,
                        Theme.Solid, Image.Type.Simple);
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

                    var tgt = UIFactory.Text(cell.transform, "T", "", 13, Theme.CrtAmber, Theme.Typewriter,
                        TextAnchor.UpperCenter, false, FontStyle.Bold);
                    UIFactory.Place(UIFactory.RT(tgt.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(CellW, 18), new Vector2(0, -1));
                    var targetStroke = tgt.gameObject.AddComponent<Outline>();
                    targetStroke.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    targetStroke.effectDistance = new Vector2(1f, -1f);
                    targetStroke.useGraphicAlpha = true;
                    _cellTarget[i, j] = tgt;

                    var gss = UIFactory.Text(cell.transform, "G", "", 22, Theme.CrtGreen, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                    UIFactory.Fill(UIFactory.RT(gss.gameObject), 0, 12, 0, 0);
                    _cellGuess[i, j] = gss;
                }
            }
        }

        /// <summary>
        /// Builds the final Level 4 exercise as a horizontally scrollable 5 by 9 table.
        /// The table deliberately keeps most cells empty: only five ? cells are tasks.
        /// </summary>
        void BuildSparseGrid(Transform screen)
        {
            _sparseTutorial = new SparseRatingsTutorialModel();
            _sparseRoot = UIFactory.Node(screen, "SparseMatrix");
            RectTransform rootRt = UIFactory.RT(_sparseRoot);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var viewport = UIFactory.Bevel(_sparseRoot.transform, "SparseViewport", Theme.CrtBgSoft, sunken: true);
            UIFactory.Place(UIFactory.RT(viewport.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(548, 310), new Vector2(20, -44));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.viewport = UIFactory.RT(viewport.gameObject);

            var content = UIFactory.Node(viewport.transform, "SparseContent");
            RectTransform contentRt = UIFactory.RT(content);
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(0, 1);
            contentRt.pivot = new Vector2(0, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(
                SparseRowHeadW + SparseRatingsTutorialModel.Columns * (SparseCellW + GapX) + 8,
                ColHeadH + SparseRatingsTutorialModel.Rows * (SparseCellH + GapY));
            scroll.content = contentRt;

            _sparseCellBg = new Image[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparseCellValue = new Text[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparseCellOriginal = new Text[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparseCellButton = new Button[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];

            for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
            {
                var header = UIFactory.Bevel(content.transform, "SparseCol" + column,
                    new Color(0.78f, 0.79f, 0.70f), sunken: false);
                UIFactory.Place(UIFactory.RT(header.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(SparseCellW, ColHeadH - 2),
                    new Vector2(SparseRowHeadW + column * (SparseCellW + GapX), 0));
                var label = UIFactory.Text(header.transform, "Label",
                    SparseRatingsTutorialModel.MovieNames[column].Replace(" ", "\n"),
                    7, Color.black, Theme.Typewriter, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                UIFactory.Fill(UIFactory.RT(label.gameObject), 2, 2, 2, 2);
            }

            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
            {
                var rowHeader = UIFactory.Bevel(content.transform, "SparseRow" + row,
                    new Color(0.78f, 0.79f, 0.70f), sunken: false);
                UIFactory.Place(UIFactory.RT(rowHeader.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(SparseRowHeadW - 4, SparseCellH),
                    new Vector2(0, -(ColHeadH + row * (SparseCellH + GapY))));
                var rowLabel = UIFactory.Text(rowHeader.transform, "Label",
                    SparseRatingsTutorialModel.CustomerNames[row], 9, Color.black,
                    Theme.Typewriter, TextAnchor.MiddleLeft, true, FontStyle.Bold);
                UIFactory.Fill(UIFactory.RT(rowLabel.gameObject), 8, 2, 4, 2);

                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                {
                    int capturedRow = row, capturedColumn = column;
                    var cell = UIFactory.Image(content.transform, $"SCell_{row}_{column}",
                        new Color(0.78f, 0.79f, 0.70f), Theme.Solid, Image.Type.Simple);
                    UIFactory.Place(UIFactory.RT(cell.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(SparseCellW, SparseCellH),
                        new Vector2(SparseRowHeadW + column * (SparseCellW + GapX),
                            -(ColHeadH + row * (SparseCellH + GapY))));
                    _sparseCellBg[row, column] = cell;

                    var button = cell.gameObject.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() => OnSparseCellClicked(capturedRow, capturedColumn));
                    _sparseCellButton[row, column] = button;

                    var original = UIFactory.Text(cell.transform, "Original", "", 7, Color.white,
                        Theme.Typewriter, TextAnchor.UpperCenter, false, FontStyle.Bold);
                    UIFactory.Place(UIFactory.RT(original.gameObject), new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(0, 13), new Vector2(0, -2));
                    _sparseCellOriginal[row, column] = original;

                    var value = UIFactory.Text(cell.transform, "Value", "", 18, Color.black,
                        Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                    UIFactory.Fill(UIFactory.RT(value.gameObject), 0, 10, 0, 0);
                    _sparseCellValue[row, column] = value;
                }
            }

            var track = UIFactory.Image(_sparseRoot.transform, "SparseScrollbar", new Color(0.05f, 0.08f, 0.05f, 0.95f));
            UIFactory.Place(UIFactory.RT(track.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(548, 18), new Vector2(20, -360));
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.numberOfSteps = 0;
            var slidingArea = UIFactory.Node(track.transform, "Sliding Area");
            UIFactory.Fill(UIFactory.RT(slidingArea), 3, 3, 3, 3);
            var handle = UIFactory.Image(slidingArea.transform, "Handle", Theme.CrtAmber);
            RectTransform handleRt = UIFactory.RT(handle.gameObject);
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = new Vector2(0.38f, 1);
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handle;
            scrollbar.value = 0f;
            scroll.horizontalScrollbar = scrollbar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            _sparseRoot.SetActive(false);
        }

        /// <summary>Restores the non-serialized sparse-table lookup arrays in scene instances.</summary>
        void BindSparseGrid()
        {
            if (_sparseRoot == null) return;
            _sparseTutorial = new SparseRatingsTutorialModel();
            _sparseCellBg = new Image[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparseCellValue = new Text[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparseCellOriginal = new Text[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparseCellButton = new Button[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];

            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                {
                    Transform cell = UIFactory.FindDeep<Transform>(_sparseRoot.transform, $"SCell_{row}_{column}");
                    if (cell == null) continue;
                    _sparseCellBg[row, column] = cell.GetComponent<Image>();
                    _sparseCellValue[row, column] = UIFactory.FindDeep<Text>(cell, "Value");
                    _sparseCellOriginal[row, column] = UIFactory.FindDeep<Text>(cell, "Original");
                    Button button = cell.GetComponent<Button>();
                    _sparseCellButton[row, column] = button;
                    if (button != null)
                    {
                        int capturedRow = row, capturedColumn = column;
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => OnSparseCellClicked(capturedRow, capturedColumn));
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

            var dialGrey = new Color(0.55f, 0.62f, 0.55f);
            for (int d = 0; d < 4; d++)
            {
                int cd = d;
                float x = -114 + d * 76;
                var nameT = UIFactory.Text(panel.transform, "SN" + d, "FEATURE\n" + (d + 1), 9, dialGrey, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(nameT.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 34), new Vector2(x, -50));
                _sliderName[d] = nameT;

                var s = UIFactory.VSlider(panel.transform, 0f, 1.2f, 0.5f, dialGrey, v => OnSlider(cd, v));
                UIFactory.Place(UIFactory.RT(s.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(46, 146), new Vector2(x, -98));
                _sliders[d] = s;

                _sliderVal[d] = UIFactory.Text(panel.transform, "SV" + d, "0.50", 12, dialGrey, Theme.Typewriter, TextAnchor.UpperCenter, false);
                UIFactory.Place(UIFactory.RT(_sliderVal[d].gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 18), new Vector2(x, -250));
            }
            SetSlidersInteractable(false);
            ConfigureFactorControls(true);

            BuildStage1Picker(screen);
            // Level 4 is the default authored preview. Its right-hand panel asks for a
            // star rating; Level 5 switches to the hidden-factor sliders in Open().
            _sliderPanel.SetActive(false);
            _pickerRoot.SetActive(true);
        }

        /// <summary>Stage-1 rating picker; occupies the slider panel's spot while it's hidden.</summary>
        void BuildStage1Picker(Transform screen)
        {
            var picker = UIFactory.Bevel(screen.transform, "Picker", new Color(0.10f, 0.14f, 0.10f), sunken: true);
            UIFactory.Place(UIFactory.RT(picker.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(300, 320), new Vector2(-20, -44));
            _pickerRoot = picker.gameObject;

            _pickerLabel = UIFactory.Text(picker.transform, "PickLbl",
                "STAGE 1: FILL THE LEDGER\n\nClick a ? cell on the board,\nthen give your best guess.", 13, Theme.CrtAmber, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
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

            _optimizeBtn = UIFactory.Button(screen.transform, "Optimize", "CHECK ERROR", RunOptimizer, Theme.CrtBgSoft, 16, Theme.SystemSans, Theme.CrtAmber);
            UIFactory.Place(UIFactory.RT(_optimizeBtn.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(180, 38), new Vector2(-20, 14));
            UIFactory.ButtonIcon(_optimizeBtn, ArtSprites.Confirm(), 28f);
            _optimizeBtn.interactable = false;
        }

        // ---- Open / refresh ----------------------------------------------
        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            MadFactBootstrap.I.Storefront.SetLine(0);
            _sparseTask = false;
            _sparsePickRow = _sparsePickColumn = -1;

            // This UI is shared by two dedicated lessons. Level 4 predicts ratings
            // through collaborative filtering; Level 5 starts at latent factors.
            _stage2 = GameManager.I.CurrentLevel == 5;
            _miniTutorial = _stage2
                ? !GameManager.I.Run.HasFlag(FlagFactorMiniDone)
                : !GameManager.I.Run.HasFlag(FlagGroundMiniDone);
            if (_miniTutorial)
            {
                if (_stage2) _tutorial = new MatrixTutorialModel();
                else _collabTutorial = new CollaborativeFilteringTutorialModel();
            }
            _miniDialChanged = false;
            if (_stage2) GameManager.I.Run.SetFlag(FlagStage1Done);
            Text screenTitle = UIFactory.FindDeep<Text>(_root.transform, "Title");
            if (screenTitle != null)
                screenTitle.text = _stage2
                    ? (_miniTutorial
                        ? "█ 3×3 TRAINING ░ TWO-FACTOR PROFILES █"
                        : "█ MAD-FACT MAINFRAME ░ MATRIX FACTORIZATION ENGINE █")
                    : (_miniTutorial
                        ? "█ 2×5 TRAINING ░ COLLABORATIVE FILTERING █"
                        : "█ MAD-FACT MAINFRAME ░ COLLABORATIVE FILTERING █");
            if (_stage1Guess == null)
                _stage1Guess = new int[M.Rows, M.Cols];
            if (_stage2 && _stage2StartTime < 0f) { _stage2StartTime = Time.unscaledTime; _slidersSeen.Clear(); }
            ApplyStage();
            Deselect();
            RefreshGrid();

            if (!_stage2 && !GameManager.I.Run.HasFlag(FlagStage1Intro))
            {
                GameManager.I.Run.SetFlag(FlagStage1Intro);
                string[] lines =
                {
                    "Collaborative filtering uses ratings from people with similar tastes to predict a missing rating.",
                    "Once we predict ratings, the movies with the highest predicted ratings are probably the best ones to recommend to each customer.",
                    "Wendell and Priya rated four movies the same way. Click Priya's ? and use Wendell's rating for that movie as your clue."
                };
                RectTransform grid = UIFactory.FindDeep<RectTransform>(_root.transform, "Grid");
                RectTransform missingCell = UIFactory.RT(_cellBg[
                    _collabTutorial.MissingRow, _collabTutorial.MissingColumn].gameObject);
                MadFactBootstrap.I.Comms.ShowFocused(Speaker.OldDude, lines,
                    new[] { grid, grid, missingCell });
            }
            else if (_stage2 && !GameManager.I.Run.HasFlag(FlagStage2Intro))
            {
                GameManager.I.Run.SetFlag(FlagStage2Intro);
                string[] lines =
                {
                    "Matrix factorization is a way to summarize a ratings table. It gives each customer and movie a short taste profile.",
                    "This training board has two factors. Each factor represents a movie taste. The slider value for that factor shows how much a movie satisfies that taste and how much a user is into that taste. Keep in mind that we are training a robot. It does not know what a taste is, so the factors are nameless.",
                    "ORIG is the original rating. The larger number is the rating predicted from the customer's factors and the movie's factors. Unknown cells show PRED values that update when you move a slider.",
                    "ERROR is the average gap between the predictions and the original ratings. Adjust the two factors and lower ERROR below 0.35, then press CHECK ERROR."
                };
                RectTransform grid = UIFactory.FindDeep<RectTransform>(_root.transform, "Grid");
                RectTransform sliders = UIFactory.RT(_sliderPanel);
                RectTransform exampleCell = UIFactory.RT(_cellBg[0, 0].gameObject);
                RectTransform error = UIFactory.RT(_lossBox);
                MadFactBootstrap.I.Comms.ShowFocused(Speaker.OldDude, lines,
                    new[] { grid, sliders, exampleCell, error });
            }
        }
        public void Close() { _root.SetActive(false); if (AudioTension.I != null) AudioTension.I.Silence(); }

        void ApplyStage()
        {
            bool powered = GameManager.I.Run.HasFlag(FlagPowered);
            if (_standardGrid != null) _standardGrid.SetActive(true);
            if (_sparseRoot != null) _sparseRoot.SetActive(false);
            int activeRows = _miniTutorial
                ? (_stage2 ? 3 : CollaborativeFilteringTutorialModel.Rows)
                : M.Rows;
            int activeColumns = _miniTutorial
                ? (_stage2 ? 3 : CollaborativeFilteringTutorialModel.Columns)
                : M.Cols;
            SetGridDimensions(activeRows, activeColumns);
            ConfigureFactorControls(_stage2 && _miniTutorial);
            if (_sliderPanel != null) _sliderPanel.SetActive(_stage2);
            if (_pickerRoot != null) _pickerRoot.SetActive(!_stage2);
            if (_lossBox != null) _lossBox.SetActive(_stage2);
            if (_optimizeBtn != null)
            {
                _optimizeBtn.gameObject.SetActive(_stage2 && (_miniTutorial || powered));
                Text label = _optimizeBtn.GetComponentInChildren<Text>();
                if (label != null) label.text = _miniTutorial ? "CHECK ERROR" : "RUN OPTIMIZER";
                Image icon = UIFactory.FindDeep<Image>(_optimizeBtn.transform, "Icon");
                if (icon != null) icon.sprite = _miniTutorial ? ArtSprites.Confirm() : ArtSprites.Optimize();
                _optimizeBtn.interactable = _miniTutorial ? _miniDialChanged : powered;
            }
            if (_resetBtn != null) _resetBtn.gameObject.SetActive(_stage2);
            if (_hint != null)
                _hint.text = !_stage2
                    ? (_miniTutorial
                        ? "2×5 TASK: Wendell and Priya match on four movies. Use Wendell's last rating to fill Priya's ?."
                        : "MAIN TASK: Fill each ? by comparing customers with similar rating patterns.")
                    : _miniTutorial
                        ? $"3×3 TASK: Adjust FACTOR 1 and FACTOR 2. Lower mean ERROR below {MatrixTutorialModel.GoalMeanError:0.00}, then press CHECK ERROR."
                    : powered
                        ? "STAGE 2: The optimizer is powered. Hit OPTIMIZE and watch it guess-and-check every dial."
                        : "STAGE 2: Four unlabeled dials per row and column. Grab a row or a tape and tune the board by hand.";
        }

        void SetGridDimensions(int rows, int columns)
        {
            for (int i = 0; i < M.Rows; i++)
            {
                _rowBtn[i].gameObject.SetActive(i < rows);
                if (i < rows)
                {
                    string name = _miniTutorial
                        ? (_stage2
                            ? MatrixTutorialModel.CustomerNames[i]
                            : CollaborativeFilteringTutorialModel.CustomerNames[i])
                        : M.Customers[i].Name;
                    if (_rowLabel[i] != null) _rowLabel[i].text = name;
                    Image icon = UIFactory.FindDeep<Image>(_rowBtn[i].transform, "Icon");
                    if (icon != null) icon.sprite = ArtSprites.CustomerPortrait(name);
                }
                for (int j = 0; j < M.Cols; j++)
                    _cellBg[i, j].gameObject.SetActive(i < rows && j < columns);
            }
            for (int j = 0; j < M.Cols; j++)
            {
                _colBtn[j].gameObject.SetActive(j < columns);
                if (j < columns && _colLabel[j] != null)
                {
                    string movie = _miniTutorial
                        ? (_stage2
                            ? MatrixTutorialModel.MovieNames[j]
                            : CollaborativeFilteringTutorialModel.MovieNames[j])
                        : M.Movies[j].Short;
                    _colLabel[j].text = movie.Replace(" ", "\n");
                }
            }
        }

        // ---- Stage 1: fill in the blanks ----------------------------------
        void OnCellClicked(int i, int j)
        {
            bool known = _miniTutorial ? _collabTutorial.Known[i, j] : M.Known[i, j];
            if (_stage2 || known || _stage1Guess == null) return;
            _pickI = i; _pickJ = j;
            string customer = _miniTutorial
                ? CollaborativeFilteringTutorialModel.CustomerNames[i]
                : M.Customers[i].Name;
            string movie = _miniTutorial
                ? CollaborativeFilteringTutorialModel.MovieNames[j]
                : M.Movies[j].Title;
            if (!_miniTutorial && i == AverageTruthRow && j == AverageTruthColumn)
            {
                int first = Mathf.RoundToInt(M.Target[AverageSourceRowA, j]);
                int second = Mathf.RoundToInt(M.Target[AverageSourceRowB, j]);
                _pickerLabel.text = $"AVERAGE TWO RATINGS\n\nDOT: {first}★   HANK: {second}★\n({first} + {second}) ÷ 2\n\nChoose their average.";
            }
            else
                _pickerLabel.text = $"HOW WOULD\n{customer}\nRATE '{movie}'?\n\nRead their row.\nRead the tape's column.";
            RefreshGrid();
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void PickValue(int value)
        {
            if (_sparseTask)
            {
                PickSparseValue(value);
                return;
            }
            if (_stage2 || _pickI < 0) return;
            _stage1Guess[_pickI, _pickJ] = value;
            _pickI = -1; _pickJ = -1;
            if (AudioTension.I != null) AudioTension.I.Clunk();

            int filled = 0, blanks = 0;
            int taskRows = _miniTutorial ? CollaborativeFilteringTutorialModel.Rows : M.Rows;
            int taskColumns = _miniTutorial ? CollaborativeFilteringTutorialModel.Columns : M.Cols;
            for (int i = 0; i < taskRows; i++)
                for (int j = 0; j < taskColumns; j++)
                {
                    bool known = _miniTutorial ? _collabTutorial.Known[i, j] : M.Known[i, j];
                    if (!known) { blanks++; if (_stage1Guess[i, j] > 0) filled++; }
                }

            _pickerLabel.text = filled < blanks
                ? $"LOGGED {filled} OF {blanks}.\n\nClick the next ? cell."
                : "LEDGER COMPLETE.\nChecking against the vault copy...";
            RefreshGrid();
            if (filled >= blanks) Stage1Evaluate();
        }

        void Stage1Evaluate()
        {
            int correct = 0, blanks = 0;
            int taskRows = _miniTutorial ? CollaborativeFilteringTutorialModel.Rows : M.Rows;
            int taskColumns = _miniTutorial ? CollaborativeFilteringTutorialModel.Columns : M.Cols;
            for (int i = 0; i < taskRows; i++)
                for (int j = 0; j < taskColumns; j++)
                {
                    bool known = _miniTutorial ? _collabTutorial.Known[i, j] : M.Known[i, j];
                    if (known) continue;
                    blanks++;
                    float truth = _miniTutorial
                        ? _collabTutorial.Target[i, j]
                        : FullStageTruth(i, j);
                    float difference = Mathf.Abs(_stage1Guess[i, j] - truth);
                    bool exact = difference <= 0.25f;
                    if (exact) correct++;
                    ShowOriginalRating(_cellTarget[i, j], _cellGlow[i, j], truth);
                    _cellGuess[i, j].color = PredictionColor(difference);
                }

            if (AudioTension.I != null) { if (correct == blanks) AudioTension.I.ChaChing(); else AudioTension.I.Buzzer(); }

            if (_miniTutorial)
            {
                MadFactLokiLogger.Instance?.Log("level_4_collaborative_rating_submitted",
                    correct == blanks
                        ? "Collaborative filtering tutorial rating was correct"
                        : "Collaborative filtering tutorial rating was incorrect",
                    new
                    {
                        customer = CollaborativeFilteringTutorialModel.CustomerNames[_collabTutorial.MissingRow],
                        movie = CollaborativeFilteringTutorialModel.MovieNames[_collabTutorial.MissingColumn],
                        selected_rating = _stage1Guess[_collabTutorial.MissingRow, _collabTutorial.MissingColumn],
                        correct_rating = _collabTutorial.MissingRating,
                        correct = correct == blanks
                    });
                MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
                {
                    correct == blanks
                        ? "Correct. Wendell and Priya rated the first four movies alike, so Wendell's last rating was a strong clue."
                        : "That rating did not match. Compare Priya's pattern with Wendell's matching row and try again.",
                    correct == blanks
                        ? "This is collaborative filtering: use ratings from similar people to help predict a missing rating."
                        : "Look at Wendell's rating in the same movie column as Priya's ?.",
                    "ORIGINAL shows the rating the customer actually gave. Green is exact, yellow is close, and red is far away."
                }, correct == blanks ? BeginFullCollaborativeTask : ResetCollaborativeTutorialGuess);
                return;
            }

            MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
            {
                $"{correct} of {blanks} guesses exactly matched the original ratings.",
                "ORIGINAL is the rating the customer actually gave. Green is exact, yellow is close, and red is far away.",
                "One answer used the average of Dot's and Hank's ratings. Similar customers can provide more than one clue."
            }, BeginSparseTask);
        }

        float FullStageTruth(int row, int column)
        {
            if (row == AverageTruthRow && column == AverageTruthColumn)
            {
                int first = Mathf.RoundToInt(M.Target[AverageSourceRowA, column]);
                int second = Mathf.RoundToInt(M.Target[AverageSourceRowB, column]);
                return (first + second) * 0.5f;
            }
            return Mathf.Round(M.Target[row, column]);
        }

        static Color PredictionColor(float difference)
        {
            if (difference <= 0.25f) return new Color(0.20f, 0.95f, 0.35f);
            if (difference <= 1.25f) return new Color(1f, 0.78f, 0.12f);
            return new Color(0.95f, 0.20f, 0.22f);
        }

        static void ShowOriginalRating(Text label, Image glow, float truth)
        {
            label.text = "ORIGINAL " + truth.ToString("0.0");
            label.fontSize = 7;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperCenter;
            label.color = Color.white;
            glow.color = new Color(1f, 0.78f, 0.12f, 0.52f);
        }

        void BeginFullCollaborativeTask()
        {
            GameManager.I.Run.SetFlag(FlagGroundMiniDone);
            _miniTutorial = false;
            _pickI = _pickJ = -1;
            _stage1Guess = new int[M.Rows, M.Cols];
            Text screenTitle = UIFactory.FindDeep<Text>(_root.transform, "Title");
            if (screenTitle != null)
                screenTitle.text = "█ MAD-FACT MAINFRAME ░ COLLABORATIVE FILTERING █";
            ApplyStage();
            RefreshGrid();
            _pickerLabel.text = "MAIN TASK: PREDICT RATINGS\n\nClick a ? cell, compare similar\ncustomers, then choose 1 to 5 stars.";
        }

        void ResetCollaborativeTutorialGuess()
        {
            _pickI = _pickJ = -1;
            _stage1Guess = new int[M.Rows, M.Cols];
            _cellTarget[_collabTutorial.MissingRow, _collabTutorial.MissingColumn].text = "";
            RefreshGrid();
            _pickerLabel.text = "TRY AGAIN\n\nClick Priya's ?, then check\nWendell's rating in that column.";
        }

        void BeginSparseTask()
        {
            _sparseTutorial = new SparseRatingsTutorialModel();
            _sparseGuess = new int[SparseRatingsTutorialModel.Rows, SparseRatingsTutorialModel.Columns];
            _sparsePickRow = _sparsePickColumn = -1;
            _sparseTask = true;
            _sparseEvaluated = false;

            if (_standardGrid != null) _standardGrid.SetActive(false);
            if (_sparseRoot != null) _sparseRoot.SetActive(true);
            if (_sliderPanel != null) _sliderPanel.SetActive(false);
            if (_pickerRoot != null) _pickerRoot.SetActive(true);
            if (_lossBox != null) _lossBox.SetActive(false);
            if (_resetBtn != null) _resetBtn.gameObject.SetActive(false);
            if (_optimizeBtn != null) _optimizeBtn.gameObject.SetActive(false);

            Text screenTitle = UIFactory.FindDeep<Text>(_root.transform, "Title");
            if (screenTitle != null)
                screenTitle.text = "█ 5×9 REAL-WORLD RATINGS ░ SPARSE MATRIX █";
            if (_hint != null)
                _hint.text = "SPARSE TASK: Predict the five ? cells. Drag the gold bar to see all nine movie columns.";
            if (_pickerLabel != null)
                _pickerLabel.text = "SPARSE RATINGS TABLE\n\nClick a ? cell, compare the\nratings you can see, then choose\n1 to 5 stars.";
            RefreshSparseGrid();

            RectTransform viewport = UIFactory.FindDeep<RectTransform>(_sparseRoot.transform, "SparseViewport");
            RectTransform scrollbar = UIFactory.FindDeep<RectTransform>(_sparseRoot.transform, "SparseScrollbar");
            MadFactBootstrap.I.Comms.ShowFocused(Speaker.OldDude, new[]
            {
                "This table has more movies and far more blanks. Drag the gold bar to see all nine columns, then predict the five ? cells."
            }, new[] { viewport, scrollbar });
        }

        void OnSparseCellClicked(int row, int column)
        {
            if (!_sparseTask || _sparseEvaluated || _sparseTutorial == null ||
                !_sparseTutorial.Task[row, column]) return;
            _sparsePickRow = row;
            _sparsePickColumn = column;
            _pickerLabel.text = $"HOW WOULD\n{SparseRatingsTutorialModel.CustomerNames[row]}\nRATE\n'{SparseRatingsTutorialModel.MovieNames[column]}'?\n\nUse the ratings that are visible.";
            RefreshSparseGrid();
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void PickSparseValue(int value)
        {
            if (!_sparseTask || _sparseEvaluated || _sparsePickRow < 0 || _sparsePickColumn < 0) return;
            int row = _sparsePickRow;
            int column = _sparsePickColumn;
            _sparseGuess[row, column] = value;
            _sparsePickRow = _sparsePickColumn = -1;
            if (AudioTension.I != null) AudioTension.I.Clunk();

            int filled = 0;
            for (int r = 0; r < SparseRatingsTutorialModel.Rows; r++)
                for (int c = 0; c < SparseRatingsTutorialModel.Columns; c++)
                    if (_sparseTutorial.Task[r, c] && _sparseGuess[r, c] > 0) filled++;

            _pickerLabel.text = filled < _sparseTutorial.TaskCount
                ? $"LOGGED {filled} OF {_sparseTutorial.TaskCount}.\n\nDrag the bar if needed, then\nclick the next ? cell."
                : "SPARSE TASK COMPLETE.\n\nChecking the original ratings...";
            RefreshSparseGrid();
            if (filled == _sparseTutorial.TaskCount) EvaluateSparseTask();
        }

        void RefreshSparseGrid()
        {
            if (_sparseTutorial == null || _sparseCellBg == null) return;
            Color paper = new Color(0.78f, 0.79f, 0.70f);
            Color blank = new Color(0.08f, 0.12f, 0.09f);
            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                {
                    bool known = _sparseTutorial.Known[row, column];
                    bool task = _sparseTutorial.Task[row, column];
                    bool selected = task && row == _sparsePickRow && column == _sparsePickColumn;
                    if (_sparseCellBg[row, column] == null) continue;
                    _sparseCellOriginal[row, column].text = selected ? "SELECTED" : "";
                    _sparseCellOriginal[row, column].color = selected ? Color.black : Color.white;
                    _sparseCellBg[row, column].color = selected ? Theme.CrtAmber : known ? paper : blank;
                    _sparseCellValue[row, column].text = known
                        ? _sparseTutorial.Target[row, column].ToString()
                        : task
                            ? (_sparseGuess[row, column] > 0 ? _sparseGuess[row, column].ToString() : "?")
                            : "·";
                    _sparseCellValue[row, column].color = selected || known
                        ? Color.black
                        : task ? Theme.CrtAmber : new Color(0.36f, 0.45f, 0.38f);
                    if (_sparseCellButton[row, column] != null)
                        _sparseCellButton[row, column].interactable = task && !_sparseEvaluated;
                }
        }

        void EvaluateSparseTask()
        {
            _sparseEvaluated = true;
            int correct = 0;
            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                {
                    if (!_sparseTutorial.Task[row, column]) continue;
                    int truth = _sparseTutorial.Target[row, column];
                    int guess = _sparseGuess[row, column];
                    float difference = Mathf.Abs(guess - truth);
                    if (difference <= 0.25f) correct++;
                    _sparseCellBg[row, column].color = new Color(0.24f, 0.17f, 0.04f);
                    _sparseCellOriginal[row, column].text = "ORIGINAL " + truth;
                    _sparseCellOriginal[row, column].color = Color.white;
                    _sparseCellValue[row, column].color = PredictionColor(difference);

                    MadFactLokiLogger.Instance?.Log("level_4_sparse_rating_submitted",
                        "Player predicted a rating in the sparse ratings table", new
                        {
                            customer = SparseRatingsTutorialModel.CustomerNames[row],
                            movie = SparseRatingsTutorialModel.MovieNames[column],
                            selected_rating = guess,
                            original_rating = truth,
                            correct = difference <= 0.25f
                        });
                }

            if (AudioTension.I != null) AudioTension.I.ChaChing();
            MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
            {
                $"{correct} of {_sparseTutorial.TaskCount} predictions exactly matched the original ratings.",
                "ORIGINAL is the rating that customer actually gave. Green is exact, yellow is close, and red is far away.",
                "Most cells are empty because not everyone has watched every movie. Real ratings tables are sparse.",
                "We need a way to learn from the ratings we do have and fill a matrix that is not very full."
            }, () =>
            {
                _sparseTask = false;
                GameManager.I.Run.SetFlag(FlagStage1Done);
                MadFactLokiLogger.Instance?.Log("level_4_sparse_matrix_completed",
                    "Player completed the sparse ratings table", new
                    {
                        correct_predictions = correct,
                        total_predictions = _sparseTutorial.TaskCount,
                        rows = SparseRatingsTutorialModel.Rows,
                        columns = SparseRatingsTutorialModel.Columns
                    });
                MadFactBootstrap.I.OnCollaborativeFilteringGoal();
            });
        }

        void SelectRow(int i)
        {
            if (!_stage2) return;
            _editingRow = true; _editIndex = i;
            HighlightSelection();
            var u = _miniTutorial ? _tutorial.U[i] : M.U[i];
            string customer = _miniTutorial ? MatrixTutorialModel.CustomerNames[i] : M.Customers[i].Name;
            _editLabel.text = "CUSTOMER: " + customer +
                (_miniTutorial ? "\n(two taste factors)" : "\n(four hidden factors)");
            LoadSliders(u);
            SetSlidersInteractable(true);
            MaybePowerOn();
            MadFactLokiLogger.Instance?.Log("collaborative_filter_entity_selected",
                "Player selected a customer taste vector", new
                {
                    level_id = GameManager.I.CurrentLevel,
                    entity_type = "customer",
                    entity_id = customer
                });
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void SelectCol(int j)
        {
            if (!_stage2) return;
            _editingRow = false; _editIndex = j;
            HighlightSelection();
            var v = _miniTutorial ? _tutorial.V[j] : M.V[j];
            string movie = _miniTutorial ? MatrixTutorialModel.MovieNames[j] : M.Movies[j].Title;
            _editLabel.text = "MOVIE: " + movie +
                (_miniTutorial ? "\n(two taste factors)" : "\n(four hidden factors)");
            LoadSliders(v);
            SetSlidersInteractable(true);
            MaybePowerOn();
            MadFactLokiLogger.Instance?.Log("collaborative_filter_entity_selected",
                "Player selected a movie feature vector", new
                {
                    level_id = GameManager.I.CurrentLevel,
                    entity_type = "movie",
                    entity_id = movie
                });
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void Deselect()
        {
            _editingRow = false; _editIndex = -1;
            HighlightSelection();
            _editLabel.text = _miniTutorial
                ? "SELECT A CUSTOMER OR MOVIE\nTHEN ADJUST TWO FACTORS"
                : "SELECT A ROW (customer)\nOR COLUMN (movie)";
            SetSlidersInteractable(false);
        }

        void HighlightSelection()
        {
            for (int i = 0; i < M.Rows; i++) _rowSel[i].gameObject.SetActive(_editingRow && _editIndex == i);
            for (int j = 0; j < M.Cols; j++) _colSel[j].gameObject.SetActive(!_editingRow && _editIndex == j);
        }

        void ConfigureFactorControls(bool twoFactorTutorial)
        {
            int visibleCount = twoFactorTutorial ? MatrixTutorialModel.FactorCount : Latent.Dim;
            for (int dimension = 0; dimension < Latent.Dim; dimension++)
            {
                bool visible = dimension < visibleCount;
                float x = twoFactorTutorial ? -66f + dimension * 132f : -114f + dimension * 76f;
                Color color = twoFactorTutorial
                    ? (dimension == 0 ? Latent.Colors[0] : Latent.Colors[3])
                    : new Color(0.55f, 0.62f, 0.55f);

                if (_sliderName[dimension] != null)
                {
                    _sliderName[dimension].gameObject.SetActive(visible);
                    _sliderName[dimension].text = $"FACTOR\n{dimension + 1}";
                    _sliderName[dimension].color = color;
                    RectTransform rt = _sliderName[dimension].rectTransform;
                    rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                }
                if (_sliders[dimension] != null)
                {
                    _sliders[dimension].gameObject.SetActive(visible);
                    RectTransform rt = UIFactory.RT(_sliders[dimension].gameObject);
                    rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                }
                if (_sliderVal[dimension] != null)
                {
                    _sliderVal[dimension].gameObject.SetActive(visible);
                    _sliderVal[dimension].color = color;
                    RectTransform rt = _sliderVal[dimension].rectTransform;
                    rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                }
            }
        }

        void LoadSliders(Latent v)
        {
            _suppressSliderEvents = true;
            int count = _miniTutorial ? MatrixTutorialModel.FactorCount : Latent.Dim;
            for (int d = 0; d < count; d++) { _sliders[d].value = v[d]; _sliderVal[d].text = v[d].ToString("0.00"); }
            _suppressSliderEvents = false;
        }

        void SetSlidersInteractable(bool on)
        {
            int count = _miniTutorial ? MatrixTutorialModel.FactorCount : Latent.Dim;
            for (int d = 0; d < Latent.Dim; d++)
                if (_sliders[d] != null) _sliders[d].interactable = on && d < count;
        }

        void OnSlider(int d, float value)
        {
            if (_suppressSliderEvents || _editIndex < 0) return;
            Latent vector = _miniTutorial
                ? (_editingRow ? _tutorial.U[_editIndex] : _tutorial.V[_editIndex])
                : (_editingRow ? M.U[_editIndex] : M.V[_editIndex]);
            float previous = vector[d];
            vector[d] = value;
            if (_miniTutorial)
            {
                if (_editingRow) _tutorial.U[_editIndex] = vector;
                else _tutorial.V[_editIndex] = vector;
            }
            else
            {
                if (_editingRow) M.U[_editIndex] = vector;
                else M.V[_editIndex] = vector;
            }
            _sliderVal[d].text = value.ToString("0.00");
            if (_miniTutorial && !_miniDialChanged)
            {
                _miniDialChanged = true;
                if (_optimizeBtn != null) _optimizeBtn.interactable = true;
            }
            // one distinct dial (this entity + this dimension) counts once no matter how
            // many OnValueChanged events the drag that touched it fired.
            _slidersSeen.Add((_editingRow ? 1000 : 0) + _editIndex * 4 + d);
            RefreshGrid();
            if (_miniTutorial)
                _hint.text = $"MEAN ERROR {_tutorial.MeanError():0.00}. Goal: below {MatrixTutorialModel.GoalMeanError:0.00}. Adjust FACTOR 1 and FACTOR 2, then press CHECK ERROR.";
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
                        level_id = GameManager.I.CurrentLevel,
                        entity_type = _editingRow ? "customer" : "movie",
                        entity_id = _miniTutorial
                            ? (_editingRow
                                ? MatrixTutorialModel.CustomerNames[_editIndex]
                                : MatrixTutorialModel.MovieNames[_editIndex])
                            : (_editingRow ? M.Customers[_editIndex].Name : M.Movies[_editIndex].Title),
                        dimension = d + 1,
                        previous_value = previous,
                        new_value = value,
                        mean_error = _miniTutorial ? _tutorial.MeanError() : M.MeanError()
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
            if (_miniTutorial || _optimizing || _stage2StartTime < 0f ||
                GameManager.I.Run.HasFlag(FlagPowered)) return;
            bool longEnough = Time.unscaledTime - _stage2StartTime >= PowerOnMinSeconds;
            bool enoughSliders = _slidersSeen.Count >= PowerOnMinSliders;
            if (longEnough && enoughSliders) PowerOnBeat();
        }

        void PowerOnBeat()
        {
            var comms = MadFactBootstrap.I.Comms;
            GameManager.I.Run.SetFlag(FlagPowered);
            ApplyStage();
            RectTransform error = UIFactory.RT(_lossBox);
            RectTransform optimizer = UIFactory.RT(_optimizeBtn.gameObject);
            comms.ShowFocused(Speaker.OldDude, new[]
            {
                "This 5 by 5 table has many more factor values to adjust. Doing every change by hand would take a long time.",
                "Now meet the OPTIMIZER. It checks the error, changes the factors, and keeps changes that make the error smaller.",
                "Press RUN OPTIMIZER and watch it improve the customer and movie profiles together."
            }, new[] { error, optimizer, optimizer }, () =>
            {
                if (AudioTension.I != null) AudioTension.I.Whir();
            });
        }

        void ResetTastes()
        {
            if (_miniTutorial)
            {
                _tutorial.Reset();
                _miniDialChanged = false;
                if (_optimizeBtn != null) _optimizeBtn.interactable = false;
                if (_hint != null)
                    _hint.text = $"3×3 TASK: Adjust FACTOR 1 and FACTOR 2. Lower mean ERROR below {MatrixTutorialModel.GoalMeanError:0.00}, then press CHECK ERROR.";
            }
            else
                M.ResetCustomerTaste();
            if (_editIndex >= 0)
                LoadSliders(_miniTutorial
                    ? (_editingRow ? _tutorial.U[_editIndex] : _tutorial.V[_editIndex])
                    : (_editingRow ? M.U[_editIndex] : M.V[_editIndex]));
            RefreshGrid();
            MadFactLokiLogger.Instance?.Log("collaborative_filter_values_reset",
                "Player reset collaborative filtering customer values",
                new
                {
                    level_id = GameManager.I.CurrentLevel,
                    mean_error = _miniTutorial ? _tutorial.MeanError() : M.MeanError()
                });
            if (AudioTension.I != null) AudioTension.I.Whir();
        }

        void RefreshGrid()
        {
            // Stage 1 shows the plain ledger: big known ratings, ? blanks, no model
            // guesses, no error glow, no audio tension. The machine hasn't started.
            if (!_stage2)
            {
                int taskRows = _miniTutorial ? CollaborativeFilteringTutorialModel.Rows : M.Rows;
                int taskColumns = _miniTutorial ? CollaborativeFilteringTutorialModel.Columns : M.Cols;
                for (int i = 0; i < taskRows; i++)
                    for (int j = 0; j < taskColumns; j++)
                    {
                        _cellGlow[i, j].color = Color.clear;
                        bool known = _miniTutorial ? _collabTutorial.Known[i, j] : M.Known[i, j];
                        if (known)
                        {
                            // Saved ratings are reference data, so give them a quiet paper
                            // treatment. The player's amber/red/yellow/green choices now stand out.
                            _cellBg[i, j].color = new Color(0.78f, 0.79f, 0.70f);
                            _cellTarget[i, j].text = "";
                            float target = _miniTutorial ? _collabTutorial.Target[i, j] : M.Target[i, j];
                            _cellGuess[i, j].text = target.ToString("0.0");
                            _cellGuess[i, j].color = Color.black;
                        }
                        else
                        {
                            bool selected = i == _pickI && j == _pickJ;
                            _cellBg[i, j].color = selected ? Theme.CrtAmber : Theme.CrtBgSoft;
                            int guessed = _stage1Guess != null ? _stage1Guess[i, j] : 0;
                            _cellTarget[i, j].text = selected ? "SELECTED" : "";
                            _cellTarget[i, j].fontSize = 7;
                            _cellTarget[i, j].fontStyle = FontStyle.Bold;
                            _cellTarget[i, j].alignment = TextAnchor.UpperCenter;
                            _cellTarget[i, j].color = selected ? Color.black : Theme.CrtGreenDim;
                            _cellGuess[i, j].text = guessed > 0 ? guessed.ToString("0") : "?";
                            _cellGuess[i, j].color = selected ? Color.black : Theme.CrtAmber;
                            _cellGlow[i, j].color = selected
                                ? new Color(1f, 0.78f, 0.12f, 0.9f)
                                : Color.clear;
                        }
                    }
                return;
            }

            int activeSize = _miniTutorial ? 3 : M.Rows;
            for (int i = 0; i < activeSize; i++)
                for (int j = 0; j < activeSize; j++)
                {
                    float g = _miniTutorial ? _tutorial.Guess(i, j) : M.Guess(i, j);
                    bool known = _miniTutorial ? _tutorial.Known[i, j] : M.Known[i, j];
                    if (known)
                    {
                        float t = _miniTutorial ? _tutorial.Target[i, j] : M.Target[i, j];
                        float err = Mathf.Abs(t - g);
                        _cellTarget[i, j].text = "ORIG " + t.ToString("0.0");
                        _cellTarget[i, j].fontSize = 13;
                        _cellTarget[i, j].fontStyle = FontStyle.Bold;
                        _cellTarget[i, j].alignment = TextAnchor.UpperCenter;
                        _cellTarget[i, j].color = Theme.CrtAmber;
                        _cellGuess[i, j].text = g.ToString("0.0");
                        var c = ErrColor(err);
                        _cellGuess[i, j].color = c;
                        _cellGlow[i, j].color = new Color(1f, 0.1f, 0.1f, GlowAlpha(err));
                        _cellBg[i, j].color = Color.Lerp(Theme.CrtBgSoft, new Color(0.25f, 0.04f, 0.04f), GlowAlpha(err));
                    }
                    else
                    {
                        bool showLivePrediction = _revealed || _slidersSeen.Count > 0;
                        _cellTarget[i, j].text = showLivePrediction ? "PRED" : "";
                        _cellTarget[i, j].fontSize = 10;
                        _cellTarget[i, j].fontStyle = FontStyle.Bold;
                        _cellTarget[i, j].alignment = TextAnchor.UpperCenter;
                        _cellTarget[i, j].color = new Color(0.5f, 0.9f, 1f);
                        _cellGuess[i, j].text = showLivePrediction ? g.ToString("0.0") : "?";
                        _cellGuess[i, j].color = showLivePrediction
                            ? new Color(0.5f, 0.9f, 1f)
                            : Theme.CrtGreenDim;
                        _cellGlow[i, j].color = new Color(0.4f, 0.8f, 1f,
                            showLivePrediction ? 0.25f : 0f);
                        _cellBg[i, j].color = showLivePrediction
                            ? Color.Lerp(Theme.CrtBgSoft, new Color(0.04f, 0.16f, 0.20f), 0.55f)
                            : Theme.CrtBgSoft;
                    }
                }
            if (_miniTutorial)
                UpdateLossAndAudio(_tutorial.MeanError(), _tutorial.WorstError());
            else
                UpdateLossAndAudio(M.MeanError(), M.WorstError());
        }

        void UpdateLossAndAudio(float mean, float worst)
        {
            float tension = 0.5f * mean + 0.5f * worst;
            if (AudioTension.I != null) AudioTension.I.SetError(tension);

            float norm = Mathf.Clamp01(mean / 2f);
            _lossFill.color = ErrColor(mean);
            var lf = UIFactory.RT(_lossFill.gameObject);
            lf.anchorMax = new Vector2(norm, 1f);
            _lossLabel.text = (_miniTutorial ? "MEAN ERROR " : "ERROR ") + mean.ToString("0.00");
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
            if (_miniTutorial)
            {
                CheckMiniManualTask();
                return;
            }
            MadFactLokiLogger.Instance?.Log("optimizer_started",
                "Player started the collaborative filtering optimizer",
                new { level_id = GameManager.I.CurrentLevel, initial_mean_error = M.MeanError() });
            StartCoroutine(OptimizeRoutine());
        }

        void CheckMiniManualTask()
        {
            if (!_miniDialChanged) return;
            float error = _tutorial.MeanError();
            bool passed = error < MatrixTutorialModel.GoalMeanError;
            _optimizeBtn.interactable = false;
            MadFactLokiLogger.Instance?.Log("matrix_3x3_manual_error_checked",
                "Player checked the manually tuned 3 by 3 matrix factorization error",
                new { level_id = 5, mean_error = error, goal_error = MatrixTutorialModel.GoalMeanError, passed });

            if (passed)
            {
                SetSlidersInteractable(false);
                if (AudioTension.I != null) AudioTension.I.ChaChing();
                MadFactBootstrap.I.Comms.Show(Speaker.OldDude, new[]
                {
                    $"You lowered the mean error to {error:0.00}. Your two-factor profiles now summarize this small ratings table well.",
                    "Next is a 5 by 5 table with four factors. Try adjusting it by hand and notice how much more work it takes."
                }, BeginFullFactorizationTask);
                return;
            }

            if (AudioTension.I != null) AudioTension.I.Beep();
            RectTransform errorBox = UIFactory.RT(_lossBox);
            MadFactBootstrap.I.Comms.ShowFocused(Speaker.OldDude, errorBox, new[]
            {
                $"The mean error is {error:0.00}. Keep adjusting FACTOR 1 and FACTOR 2 so the predictions move closer to ORIG. Your goal is below {MatrixTutorialModel.GoalMeanError:0.00}."
            }, () => _optimizeBtn.interactable = true);
        }

        void BeginFullFactorizationTask()
        {
            GameManager.I.Run.SetFlag(FlagFactorMiniDone);
            _miniTutorial = false;
            _miniDialChanged = false;
            _revealed = false;
            _slidersSeen.Clear();
            _stage2StartTime = Time.unscaledTime;
            M.ResetCustomerTaste();
            Deselect();
            Text screenTitle = UIFactory.FindDeep<Text>(_root.transform, "Title");
            if (screenTitle != null)
                screenTitle.text = "█ MAD-FACT MAINFRAME ░ MATRIX FACTORIZATION ENGINE █";
            _resetBtn.interactable = true;
            ApplyStage();
            RefreshGrid();
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
                _hint.text = $"MACHINE: {verbs[p % verbs.Length]} {focus}'s dials. Guess, check, and keep what helps. ERROR {M.MeanError():0.00}";

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
                new { level_id = GameManager.I.CurrentLevel, final_mean_error = M.MeanError(), payout });

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
                    StartCoroutine(FinishMatrixFactorizationGoal());
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
        IEnumerator FinishMatrixFactorizationGoal()
        {
            yield return ObservationPause(10f);
            HighlightUnderserved();
            MadFactBootstrap.I.OnMatrixFactorizationGoal();
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
            _hint.text = "Take a moment. Check your MONEY and TRUST above.";
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
                "Hey! Your machine now recommends the same big movie to EVERYONE!",
                "I made 'QUASAR RUN' with two lamps and a borrowed camera, and it is GOOD.",
                "No one can rate my movie if the system never shows it. The system will not show it because it has few ratings. See the loop?"
            }, () => comms.Show(Speaker.OldDude, new[]
            {
                "She is right. Look at the board: the tape with the most ratings wins in every column.",
                "Why does the system keep choosing the movie that is already popular?"
            }, () => comms.AskChoice(Speaker.OldDude,
                "QUIZ: 'STAR DRIFTER' tops every list and 'QUASAR RUN' never gets shown. Why?", new[]
            {
                "Popular tapes have more ratings, so the system is more sure about them",
                "The mainframe reads each movie's budget and always favors the expensive ones",
                "Small movies always get worse ratings, so hiding them is correct behavior"
            }, pick =>
            {
                string[] verdict = pick == 0
                    ? new[]
                    {
                        "Exactly. Popular movies have the most data.",
                        "More picks lead to more rentals and more ratings. The loop keeps growing.",
                        "That is POPULARITY BIAS. It means popular choices get an unfair head start.",
                        "A fair system should also show some less-known movies so they get a chance."
                    }
                    : new[]
                    {
                        "Not quite. The machine does not know each movie's budget.",
                        "The problem is the DATA. Popular tapes have more ratings, so the system is more sure about them.",
                        "Those tapes get picked, rented, and rated again. This loop is called POPULARITY BIAS.",
                        "Less-known movies need to be shown before they can earn ratings."
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
