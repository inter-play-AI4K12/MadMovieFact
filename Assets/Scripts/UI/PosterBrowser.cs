using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// The shelf browser from the design sketch: a "◀ GENRE ▶" carousel over a grid of
    /// poster thumbnails; clicking a poster opens a detail view with the big poster,
    /// title + year, age sticker, and the 8 genre feature bars printed on the box.
    /// Level 1 (manual) and Level 3 (content-based) both mount one of these.
    /// </summary>
    public class PosterBrowser : MonoBehaviour
    {
        /// <summary>Invoked with the catalog index when the player commits to a tape.</summary>
        public Action<int> OnRecommend;
        /// <summary>Optional extra detail line (e.g. the content engine's MATCH % readout).</summary>
        public Func<int, string> DetailExtra;

        [SerializeField] Genre _genre = Genre.SciFi;
        [SerializeField] GameObject _gridRoot, _detailRoot;
        [SerializeField] Text _genreLabel, _emptyLabel;
        int _detailIndex = -1;
        bool _locked;

        public int DetailIndex => _detailIndex;

        void Awake()
        {
            // The browser shell and its initial shelf are authored into the prefab.
            // Reconnect transient UnityEvent listeners after deserialization instead of
            // rebuilding the whole browser when a scene starts.
            if (_gridRoot == null) _gridRoot = UIFactory.FindDeep<Transform>(transform, "Grid")?.gameObject;
            if (_detailRoot == null) _detailRoot = UIFactory.FindDeep<Transform>(transform, "Detail")?.gameObject;
            if (_genreLabel == null) _genreLabel = UIFactory.FindDeep<Text>(transform, "GenreName");
            if (_emptyLabel == null) _emptyLabel = UIFactory.FindDeep<Text>(transform, "Empty");

            BindButton("GPrev", () => CycleGenre(-1));
            BindButton("GNext", () => CycleGenre(1));
            BindVisiblePosterButtons();
        }

        void BindButton(string objectName, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, objectName);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        void BindVisiblePosterButtons()
        {
            if (_gridRoot == null) return;
            foreach (Transform child in _gridRoot.transform)
            {
                if (!child.name.StartsWith("Poster") ||
                    !int.TryParse(child.name.Substring("Poster".Length), out int movieIndex))
                    continue;

                var button = child.GetComponent<Button>();
                if (button == null) continue;
                int capturedIndex = movieIndex;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { if (!_locked) OpenDetail(capturedIndex); });
            }
        }

        public static PosterBrowser Create(Transform parent, string name = "PosterBrowser")
        {
            var go = UIFactory.Node(parent, name);
            var browser = go.AddComponent<PosterBrowser>();
            browser.Build(go.transform);
            return browser;
        }

        void Build(Transform root)
        {
            // ---- genre carousel header ----
            var prev = UIFactory.Button(root, "GPrev", "", () => CycleGenre(-1), Theme.Face, 12);
            UIFactory.Place(UIFactory.RT(prev.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, 24), new Vector2(0, 0));
            UIFactory.ButtonIcon(prev, ArtSprites.Back(), 18f, true);

            _genreLabel = UIFactory.Text(root, "GenreName", GenreInfo.Name(_genre), 15, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            var glr = UIFactory.RT(_genreLabel.gameObject);
            glr.anchorMin = new Vector2(0, 1); glr.anchorMax = new Vector2(1, 1);
            glr.pivot = new Vector2(0.5f, 1);
            glr.sizeDelta = new Vector2(-60, 24); glr.anchoredPosition = new Vector2(0, 0);

            var next = UIFactory.Button(root, "GNext", "", () => CycleGenre(1), Theme.Face, 12);
            UIFactory.Place(UIFactory.RT(next.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 24), new Vector2(0, 0));
            UIFactory.ButtonIcon(next, ArtSprites.Next(), 18f, true);

            // ---- grid + detail containers fill the rest ----
            _gridRoot = UIFactory.Node(root, "Grid");
            UIFactory.Fill(UIFactory.RT(_gridRoot), 0, 28, 0, 0);
            _detailRoot = UIFactory.Node(root, "Detail");
            UIFactory.Fill(UIFactory.RT(_detailRoot), 0, 28, 0, 0);
            _detailRoot.SetActive(false);

            _emptyLabel = UIFactory.Text(root, "Empty", "— this shelf is empty —", 13, Theme.InkSoft, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Italic);
            var elr = UIFactory.RT(_emptyLabel.gameObject);
            UIFactory.Fill(elr, 0, 28, 0, 0);
            _emptyLabel.gameObject.SetActive(false);

            RebuildGrid();
        }

        // ---- navigation ----------------------------------------------------
        void CycleGenre(int d)
        {
            _genre = (Genre)(((int)_genre + d + GenreInfo.Count) % GenreInfo.Count);
            if (AudioTension.I != null) AudioTension.I.Beep();
            CloseDetail();
            RebuildGrid();
        }

        public void SetGenre(Genre g)
        {
            _genre = g;
            CloseDetail();
            RebuildGrid();
        }

        /// <summary>Back to the grid on the current shelf (called between customers).</summary>
        public void ResetView()
        {
            CloseDetail();
            RebuildGrid();
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == "Recommend" || b.gameObject.name.StartsWith("Poster"))
                    b.interactable = !locked;
        }

        // ---- grid ------------------------------------------------------------
        void RebuildGrid()
        {
            foreach (Transform child in _gridRoot.transform) Destroy(child.gameObject);
            _genreLabel.text = GenreInfo.Name(_genre);   // the arrow buttons flank this label

            var movies = GameData.MoviesInGenre(_genre);
            _emptyLabel.gameObject.SetActive(movies.Count == 0);
            _gridRoot.SetActive(true);
            _detailRoot.SetActive(false);

            for (int i = 0; i < movies.Count && i < 6; i++)
            {
                int mi = movies[i];
                var m = GameData.Movies[mi];
                int col = i % 3, row = i / 3;

                var cell = UIFactory.Node(_gridRoot.transform, "Poster" + mi);
                var crt = UIFactory.RT(cell.gameObject);
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.sizeDelta = new Vector2(88, 142);
                crt.anchoredPosition = new Vector2((col - 1) * 94, -6 - row * 150);

                var btn = cell.AddComponent<Button>();
                var bg = cell.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.001f);   // invisible but raycastable
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { if (!_locked) OpenDetail(mi); });

                var poster = UIFactory.Image(cell.transform, "Img", Color.white, ArtSprites.MovieCover(mi), Image.Type.Simple, false);
                poster.preserveAspect = true;
                UIFactory.Place(UIFactory.RT(poster.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(80, 112), new Vector2(0, 0));

                var label = UIFactory.Text(cell.transform, "T", m.Title, 9, Theme.Ink, Theme.Typewriter, TextAnchor.UpperCenter, true);
                UIFactory.Place(UIFactory.RT(label.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(88, 26), new Vector2(0, 0));
            }
        }

        // ---- detail ----------------------------------------------------------
        void OpenDetail(int mi)
        {
            _detailIndex = mi;
            var m = GameData.Movies[mi];
            foreach (Transform child in _detailRoot.transform) Destroy(child.gameObject);
            _gridRoot.SetActive(false);
            _emptyLabel.gameObject.SetActive(false);
            _detailRoot.SetActive(true);
            if (AudioTension.I != null) AudioTension.I.Clunk();

            var title = UIFactory.Text(_detailRoot.transform, "Title", m.TitleWithYear, 13, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Italic);
            var trt = UIFactory.RT(title.gameObject);
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 20); trt.anchoredPosition = Vector2.zero;

            var poster = UIFactory.Image(_detailRoot.transform, "Big", Color.white, ArtSprites.MovieCover(mi), Image.Type.Simple, false);
            poster.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(poster.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(120, 150), new Vector2(-60, -22));

            // age sticker
            var chip = UIFactory.Bevel(_detailRoot.transform, "Rating", m.Rating == AgeRating.R ? Theme.ErrorRed : Theme.Face, false, false);
            UIFactory.Place(UIFactory.RT(chip.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(52, 20), new Vector2(60, -24));
            var chipT = UIFactory.Text(chip.transform, "T", GenreInfo.RatingLabel(m.Rating), 11,
                m.Rating == AgeRating.R ? Color.white : Theme.Ink, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(chipT.gameObject));

            var blurb = UIFactory.Text(_detailRoot.transform, "Blurb", m.Blurb, 10, Theme.InkSoft, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Place(UIFactory.RT(blurb.gameObject), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(130, 120), new Vector2(10, -50));

            // 8 genre feature bars, two columns of four (like the sketch)
            float barsTop = -180;
            for (int g = 0; g < GenreInfo.Count; g++)
            {
                int col = g / 4, row = g % 4;
                float x = -136 + col * 140;
                float y = barsTop - row * 17;

                var lbl = UIFactory.Text(_detailRoot.transform, "fl" + g, GenreInfo.Names[g], 8, Theme.InkSoft, Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Italic);
                UIFactory.Place(UIFactory.RT(lbl.gameObject), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(72, 14), new Vector2(x, y - 2));

                var bg = UIFactory.Image(_detailRoot.transform, "fb" + g, new Color(0, 0, 0, 0.18f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(58, 9), new Vector2(x + 74, y - 2));
                var fill = UIFactory.Image(bg.transform, "ff" + g, GenreInfo.Colors[g], null, Image.Type.Simple, false);
                var frt = UIFactory.RT(fill.gameObject);
                frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(Mathf.Clamp01(m.Features[g]), 1);
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            }

            float extraY = barsTop - 4 * 17 - 4;
            if (DetailExtra != null)
            {
                string extra = DetailExtra(mi);
                if (!string.IsNullOrEmpty(extra))
                {
                    var ex = UIFactory.Text(_detailRoot.transform, "Extra", extra, 11, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                    var ert = UIFactory.RT(ex.gameObject);
                    ert.anchorMin = new Vector2(0, 1); ert.anchorMax = new Vector2(1, 1); ert.pivot = new Vector2(0.5f, 1);
                    ert.sizeDelta = new Vector2(0, 16); ert.anchoredPosition = new Vector2(0, extraY);
                }
            }

            var back = UIFactory.Button(_detailRoot.transform, "Back", "BACK", () => { CloseDetail(); RebuildGrid(); }, Theme.Face, 12);
            UIFactory.Place(UIFactory.RT(back.gameObject), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(88, 28), new Vector2(-140, extraY - 18));
            UIFactory.ButtonIcon(back, ArtSprites.Back(), 18f);

            var rec = UIFactory.Button(_detailRoot.transform, "Recommend", "RECOMMEND", () =>
            {
                if (_locked) return;
                int picked = _detailIndex;
                CloseDetail(); RebuildGrid();
                OnRecommend?.Invoke(picked);
            }, Theme.Cash, 12, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(rec.gameObject), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(140, 28), new Vector2(-44, extraY - 18));
            UIFactory.ButtonIcon(rec, ArtSprites.MovieTape(), 20f);
            rec.interactable = !_locked;
        }

        void CloseDetail()
        {
            _detailIndex = -1;
            foreach (Transform child in _detailRoot.transform) Destroy(child.gameObject);
            _detailRoot.SetActive(false);
            _gridRoot.SetActive(true);
        }
    }
}
