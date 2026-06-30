using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Level 3 — The Algorithm Era (Matrix Factorization). The CRT Mainframe shows a
    /// Customers x Movies grid. Each known cell has a Target and a live Guess = 1 + 4·dot(U,V).
    /// Clicking a row (customer) or column (movie) opens four plastic latent-feature sliders.
    /// Tuning one cell breaks others (red glow + audio static): the player feels the coupling.
    /// The OPTIMIZER runs real gradient descent to balance the whole board at once.
    /// </summary>
    public class Level3Mainframe : MonoBehaviour
    {
        GameObject _root;
        MfModel M => GameManager.I.Matrix;

        Image[,] _cellBg; Image[,] _cellGlow; Text[,] _cellGuess; Text[,] _cellTarget;
        Button[] _rowBtn; Button[] _colBtn;
        Image[] _rowSel; Image[] _colSel;

        Slider[] _sliders = new Slider[4];
        Text[] _sliderVal = new Text[4];
        Text _editLabel, _lossLabel, _hint;
        Image _lossFill;
        Button _optimizeBtn, _resetBtn;

        bool _revealed;
        bool _editingRow;
        int _editIndex = -1;
        bool _optimizing;
        bool _suppressSliderEvents;

        const int CellW = 70, CellH = 50, GapX = 6, GapY = 6, RowHeadW = 104, ColHeadH = 40;

        public static Level3Mainframe Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level3");
            var lvl = go.AddComponent<Level3Mainframe>();
            lvl.Build(canvas);
            lvl._root.SetActive(false);
            return lvl;
        }

        void Build(Transform canvas)
        {
            _root = UIFactory.Image(canvas, "Level3Mainframe", Theme.CrtBg).gameObject;
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

            var leave = UIFactory.Button(screen.transform, "Leave", "✕", () => MadFactBootstrap.I.GoStorefront(), Theme.CrtBgSoft, 16, Theme.Typewriter, Theme.CrtGreen);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-12, -10));
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
                var b = UIFactory.Button(gridRoot.transform, "Col" + j, GameData.Movies[j].Title.Replace(" ", "\n"), () => SelectCol(cj), Theme.CrtBgSoft, 11, Theme.Typewriter, Theme.CrtGreen);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(CellW, ColHeadH - 2), new Vector2(RowHeadW + j * (CellW + GapX), 0));
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

            _editLabel = UIFactory.Text(panel.transform, "Edit", "SELECT A ROW OR COLUMN", 14, Theme.CrtAmber, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_editLabel.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 40), new Vector2(0, -8));

            for (int d = 0; d < 4; d++)
            {
                int cd = d;
                float x = -114 + d * 76;
                var col = Latent.Colors[d];
                var nameT = UIFactory.Text(panel.transform, "SN" + d, Latent.Names[d], 11, col, Theme.Typewriter, TextAnchor.UpperCenter, true, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(nameT.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 28), new Vector2(x, -48));

                var s = UIFactory.VSlider(panel.transform, 0f, 1.2f, 0.5f, col, v => OnSlider(cd, v));
                UIFactory.Place(UIFactory.RT(s.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(46, 170), new Vector2(x, -76));
                _sliders[d] = s;

                _sliderVal[d] = UIFactory.Text(panel.transform, "SV" + d, "0.50", 12, col, Theme.Typewriter, TextAnchor.UpperCenter, false);
                UIFactory.Place(UIFactory.RT(_sliderVal[d].gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 18), new Vector2(x, -250));
            }
            SetSlidersInteractable(false);
        }

        void BuildBottom(Transform screen)
        {
            // loss meter
            var lossBox = UIFactory.Bevel(screen.transform, "LossBox", Theme.CrtBgSoft, sunken: true);
            UIFactory.Place(UIFactory.RT(lossBox.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(460, 30), new Vector2(20, 16));
            var lossBar = UIFactory.Image(lossBox.transform, "BarBg", new Color(0, 0, 0, 0.5f));
            UIFactory.Place(UIFactory.RT(lossBar.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(250, 16), new Vector2(150, 0));
            _lossFill = UIFactory.Image(lossBar.transform, "Fill", Theme.ErrorRed);
            var lf = UIFactory.RT(_lossFill.gameObject); lf.anchorMin = new Vector2(0, 0); lf.anchorMax = new Vector2(0, 1); lf.pivot = new Vector2(0, 0.5f);
            lf.offsetMin = Vector2.zero; lf.offsetMax = Vector2.zero; lf.sizeDelta = new Vector2(0, 0);
            _lossLabel = UIFactory.Text(lossBox.transform, "LossLbl", "TOTAL ERROR", 13, Theme.CrtGreen, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_lossLabel.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 24), new Vector2(8, 0));

            _hint = UIFactory.Text(screen.transform, "Hint", "Tip: fixing one customer often breaks another. Feel the friction, then hit OPTIMIZE.", 12, Theme.CrtGreenDim, Theme.Typewriter, TextAnchor.MiddleLeft, true);
            UIFactory.Place(UIFactory.RT(_hint.gameObject), new Vector2(0, 0), new Vector2(0, 0), new Vector2(460, 30), new Vector2(20, 50));

            _resetBtn = UIFactory.Button(screen.transform, "Reset", "RESET TASTES", ResetTastes, Theme.CrtBgSoft, 13, Theme.Typewriter, Theme.CrtGreen);
            UIFactory.Place(UIFactory.RT(_resetBtn.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(150, 34), new Vector2(-180, 16));

            _optimizeBtn = UIFactory.Button(screen.transform, "Optimize", "▶ RUN OPTIMIZER", RunOptimizer, Theme.CrtAmber, 16, Theme.SystemSans, Theme.CrtBg);
            UIFactory.Place(UIFactory.RT(_optimizeBtn.gameObject), new Vector2(1, 0), new Vector2(1, 0), new Vector2(180, 38), new Vector2(-20, 14));
        }

        // ---- Open / refresh ----------------------------------------------
        public void Open()
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            MadFactBootstrap.I.Storefront.SetLine(16);
            Deselect();
            RefreshGrid();
        }
        public void Close() { _root.SetActive(false); if (AudioTension.I != null) AudioTension.I.Silence(); }

        void SelectRow(int i)
        {
            _editingRow = true; _editIndex = i;
            HighlightSelection();
            var u = M.U[i];
            _editLabel.text = "CUSTOMER: " + M.Customers[i].Name + "\n(their taste vibes)";
            LoadSliders(u);
            SetSlidersInteractable(true);
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        void SelectCol(int j)
        {
            _editingRow = false; _editIndex = j;
            HighlightSelection();
            var v = M.V[j];
            _editLabel.text = "MOVIE: " + M.Movies[j].Title + "\n(its feature vibes)";
            LoadSliders(v);
            SetSlidersInteractable(true);
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
            if (_editingRow) M.U[_editIndex][d] = value; else M.V[_editIndex][d] = value;
            _sliderVal[d].text = value.ToString("0.00");
            RefreshGrid();
        }

        void ResetTastes()
        {
            M.ResetCustomerTaste();
            if (_editingRow && _editIndex >= 0) LoadSliders(M.U[_editIndex]);
            RefreshGrid();
            if (AudioTension.I != null) AudioTension.I.Whir();
        }

        void RefreshGrid()
        {
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
            StartCoroutine(OptimizeRoutine());
        }

        IEnumerator OptimizeRoutine()
        {
            _optimizing = true;
            _optimizeBtn.interactable = false; _resetBtn.interactable = false;
            SetSlidersInteractable(false);
            Deselect();
            _hint.text = "OPTIMIZING — gradient descent balancing every cell at once...";
            if (AudioTension.I != null) AudioTension.I.Whir();

            const int maxSteps = 300;
            for (int step = 0; step < maxSteps; step++)
            {
                M.StepGradient(0.004f);   // tuned: stable convergence (~110 steps to <0.05)
                RefreshGrid();
                if (step % 24 == 0 && AudioTension.I != null) AudioTension.I.Whir();
                if (M.MeanError() < 0.04f) break;
                yield return new WaitForSecondsRealtime(0.018f);
            }

            // reveal hidden predictions
            _revealed = true;
            RefreshGrid();
            if (AudioTension.I != null) { AudioTension.I.Silence(); AudioTension.I.Clunk(); }

            // revenue skyrockets
            yield return new WaitForSecondsRealtime(0.4f);
            int payout = 320;
            Vector2 pop = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            GameManager.I.AddMoney(payout);
            GameManager.I.RecordSale(0f, pop); // cha-ching + popup
            _hint.text = $"BALANCED. Empty cells filled with predictions. +${payout} batch revenue!";

            _optimizing = false;
            _resetBtn.interactable = true;

            // highlight the underserved cluster, then hand off to Level 4
            yield return new WaitForSecondsRealtime(1.0f);
            HighlightUnderserved();
            yield return new WaitForSecondsRealtime(0.6f);
            if (!MadFactBootstrap.I.Level3Cleared)
            {
                MadFactBootstrap.I.Level3Cleared = true;
                MadFactBootstrap.I.OnLevel3Goal();
            }
            else _optimizeBtn.interactable = true;
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
