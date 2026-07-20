using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 6 — Market Gap Research & Movie Making. The optimized matrix revealed an underserved
    /// demographic (high Spooky + high Funny, no inventory match). On the Corkboard the
    /// player drags magazine-cutout stickers — each carrying latent weights — to design a
    /// movie poster that targets that gap. Match the gap vibe to greenlight the blockbuster.
    /// </summary>
    public class Level4Corkboard : MonoBehaviour
    {
        struct StickerDef { public string Label; public Latent Vibe; public Color Color; public StickerDef(string l, Latent v, Color c) { Label = l; Vibe = v; Color = c; } }

        [SerializeField] GameObject _root;
        [SerializeField] RectTransform _board;
        Latent _gap;
        readonly List<StickerDef> _placed = new List<StickerDef>();
        [SerializeField] Image[] _gapBars = new Image[4];
        [SerializeField] Image[] _posterBars = new Image[4];
        [SerializeField] Text _matchText;
        [SerializeField] Image _matchFill;
        [SerializeField] Button _greenlight;
        [SerializeField] Text _posterInstruction;
        bool _won;

        static readonly StickerDef[] Palette =
        {
            new StickerDef("HAUNTED\nHOUSE", new Latent(0,1,0,0), new Color(0.45f,0.32f,0.55f)),
            new StickerDef("GHOST", new Latent(0,1,0,0), new Color(0.55f,0.55f,0.70f)),
            new StickerDef("TOMBSTONE", new Latent(0,1,0,0), new Color(0.40f,0.42f,0.45f)),
            new StickerDef("LAUGHING\nSKULL", new Latent(0,1,1,0), new Color(0.70f,0.45f,0.65f)),
            new StickerDef("\"BOO-\nHA-HA!\"", new Latent(0,1,1,0), new Color(0.85f,0.55f,0.30f)),
            new StickerDef("BANANA\nPEEL", new Latent(0,0,1,0), new Color(0.92f,0.80f,0.25f)),
            new StickerDef("CLOWN\nNOSE", new Latent(0,0,1,0), new Color(0.90f,0.30f,0.30f)),
            new StickerDef("PUNCH-\nLINE", new Latent(0,0,1,0), new Color(0.95f,0.70f,0.20f)),
            new StickerDef("ROCKET", new Latent(1,0,0,0), new Color(0.40f,0.55f,0.85f)),
            new StickerDef("RAY GUN", new Latent(1,0,0,0), new Color(0.45f,0.70f,0.80f)),
            new StickerDef("FIREBALL", new Latent(0,0,0,1), new Color(0.90f,0.45f,0.20f)),
            new StickerDef("CAR\nCHASE", new Latent(0,0,0,1), new Color(0.80f,0.50f,0.25f)),
        };

        void Awake()
        {
            if (_root == null) return;
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
            Bind("Green", TryGreenlight);
            for (int i = 0; i < Palette.Length; i++)
            {
                int index = i;
                Bind("S" + i, () => AddSticker(index));
            }
        }

        void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level4Corkboard Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level4");
            UIFactory.Fill(UIFactory.RT(go));
            var lvl = go.AddComponent<Level4Corkboard>();
            lvl.Build(go.transform);
            return lvl;
        }

        void Build(Transform parent)
        {
            _gap = GameData.MarketGapVibe();

            _root = UIFactory.Image(parent, "Level4Corkboard", new Color(0.30f, 0.20f, 0.10f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            // cork texture (mottled)
            var cork = UIFactory.Image(_root.transform, "Cork", new Color(0.62f, 0.44f, 0.24f));
            UIFactory.Fill(UIFactory.RT(cork.gameObject));
            var rng = new System.Random(7);
            for (int i = 0; i < 120; i++)
            {
                var fleck = UIFactory.Image(cork.transform, "f", new Color(0.5f + (float)rng.NextDouble() * 0.2f, 0.36f + (float)rng.NextDouble() * 0.15f, 0.18f, 0.5f), Theme.Disc, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(fleck.gameObject), new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()), new Vector2(0.5f, 0.5f), new Vector2(3 + (float)rng.NextDouble() * 4, 3 + (float)rng.NextDouble() * 4), Vector2.zero);
            }

            var title = UIFactory.Text(_root.transform, "Title", "THE CORKBOARD: design a poster for the SPOOK-COMEDY gap", 17, new Color(0.20f, 0.12f, 0.05f), Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(760, 24), new Vector2(20, -10));

            // poster (the board where stickers go)
            var posterFrame = UIFactory.Bevel(_root.transform, "PosterFrame", new Color(0.95f, 0.93f, 0.86f));
            UIFactory.Place(UIFactory.RT(posterFrame.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 380), Vector2.zero);
            var pin = UIFactory.Image(posterFrame.transform, "Pin", new Color(0.85f, 0.2f, 0.2f), Theme.Disc, Image.Type.Simple, false);
            UIFactory.Place(UIFactory.RT(pin.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(16, 16), new Vector2(0, -2));
            var posterBg = UIFactory.Image(posterFrame.transform, "PosterBg", new Color(0.12f, 0.10f, 0.16f));
            UIFactory.Fill(UIFactory.RT(posterBg.gameObject), 12, 12, 12, 40);
            _board = UIFactory.RT(posterBg.gameObject);
            _posterInstruction = UIFactory.Text(posterBg.transform, "Instructions",
                "CLICK A CUTOUT\nTO PIN IT HERE\n\nTHEN DRAG IT\nAROUND YOUR POSTER",
                16, new Color(0.75f, 0.72f, 0.66f, 0.82f), Theme.Typewriter,
                TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_posterInstruction.gameObject), 48, 48, 48, 48);
            var ptitle = UIFactory.Text(posterFrame.transform, "PT", "YOUR FEATURE FILM", 16, new Color(0.15f, 0.1f, 0.05f), Theme.Typewriter, TextAnchor.LowerCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(ptitle.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(340, 30), new Vector2(0, 8));

            BuildAnalysis(_root.transform);
            BuildPalette(_root.transform);

            var leave = UIFactory.Button(_root.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-10, -8));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);

            RecomputeMatch();
        }

        void BuildAnalysis(Transform root)
        {
            var panel = UIFactory.Bevel(root, "Analysis", Theme.Face);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(220, 380), new Vector2(20, 0));

            UIFactory.Place(UIFactory.RT(UIFactory.Text(panel.transform, "h", "MARKET-GAP VIBE\n(from the matrix)", 14, Theme.Ink, Theme.SystemSans, TextAnchor.UpperCenter, true, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 40), new Vector2(0, -8));

            for (int d = 0; d < 4; d++)
            {
                float y = -56 - d * 30;
                UIFactory.Place(UIFactory.RT(UIFactory.Text(panel.transform, "gn" + d, Latent.Names[d], 11, Latent.Colors[d], Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold).gameObject),
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, 20), new Vector2(10, y));
                var bg = UIFactory.Image(panel.transform, "gbg" + d, new Color(0, 0, 0, 0.25f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, 16), new Vector2(104, y));
                var fill = UIFactory.Image(bg.transform, "gf" + d, Latent.Colors[d]);
                var f = UIFactory.RT(fill.gameObject); f.anchorMin = new Vector2(0, 0); f.anchorMax = new Vector2(Mathf.Clamp01(_gap[d]), 1); f.offsetMin = Vector2.zero; f.offsetMax = Vector2.zero;
                _gapBars[d] = fill;
            }

            UIFactory.Place(UIFactory.RT(UIFactory.Text(panel.transform, "h2", "YOUR POSTER", 13, Theme.Ink, Theme.SystemSans, TextAnchor.UpperCenter, false, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 18), new Vector2(0, -184));
            for (int d = 0; d < 4; d++)
            {
                float y = -206 - d * 22;
                var bg = UIFactory.Image(panel.transform, "pbg" + d, new Color(0, 0, 0, 0.25f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, 14), new Vector2(104, y));
                var fill = UIFactory.Image(bg.transform, "pf" + d, Latent.Colors[d]);
                var f = UIFactory.RT(fill.gameObject); f.anchorMin = new Vector2(0, 0); f.anchorMax = new Vector2(0, 1); f.offsetMin = Vector2.zero; f.offsetMax = Vector2.zero;
                _posterBars[d] = fill;
                UIFactory.Place(UIFactory.RT(UIFactory.Text(panel.transform, "pn" + d, Latent.Names[d].Substring(0, 2), 10, Latent.Colors[d], Theme.Typewriter, TextAnchor.MiddleLeft, false).gameObject),
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, 14), new Vector2(10, y));
            }

            // match meter
            var mbg = UIFactory.Image(panel.transform, "mbg", new Color(0, 0, 0, 0.3f), null, Image.Type.Simple, false);
            UIFactory.Place(UIFactory.RT(mbg.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(196, 22), new Vector2(0, 40));
            _matchFill = UIFactory.Image(mbg.transform, "mf", Theme.ErrorRed);
            var mf = UIFactory.RT(_matchFill.gameObject); mf.anchorMin = new Vector2(0, 0); mf.anchorMax = new Vector2(0, 1); mf.offsetMin = Vector2.zero; mf.offsetMax = Vector2.zero;
            _matchText = UIFactory.Text(panel.transform, "mt", "MATCH 0%", 13, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_matchText.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(196, 22), new Vector2(0, 40));

            _greenlight = UIFactory.Button(panel.transform, "Green", "GREENLIGHT", TryGreenlight, Theme.FaceShade, 15, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(_greenlight.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(196, 32), new Vector2(0, 6));
            UIFactory.ButtonIcon(_greenlight, ArtSprites.Confirm(), 22f);
            _greenlight.interactable = false;
        }

        void BuildPalette(Transform root)
        {
            var panel = UIFactory.Bevel(root, "Palette", Theme.Face);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(220, 400), new Vector2(-20, 0));
            UIFactory.Place(UIFactory.RT(UIFactory.Text(panel.transform, "h", "CUTOUTS & STICKERS\n(click to pin to poster)", 13, Theme.Ink, Theme.SystemSans, TextAnchor.UpperCenter, true, FontStyle.Bold).gameObject),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 48), new Vector2(0, -4));

            for (int i = 0; i < Palette.Length; i++)
            {
                int ci = i;
                var d = Palette[i];
                float x = (i % 2 == 0) ? -50 : 50;
                float y = -58 - (i / 2) * 56;
                var b = UIFactory.Button(panel.transform, "S" + i, d.Label, () => AddSticker(ci), d.Color, 11, Theme.Typewriter, Color.white);
                UIFactory.Place(UIFactory.RT(b.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(94, 50), new Vector2(x, y));
                UIFactory.ButtonIcon(b, ArtSprites.Sticker(i), 38f);
                b.GetComponentInChildren<Text>().fontSize = 9;
            }
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            _won = false;               // re-entering re-arms the greenlight
            MadFactBootstrap.I.Storefront.SetLine(0);
            if (_posterInstruction != null) _posterInstruction.gameObject.SetActive(_placed.Count == 0);
            RecomputeMatch();
        }
        public void Close() => _root.SetActive(false);

        void AddSticker(int defIndex)
        {
            var d = Palette[defIndex];
            _placed.Add(d);
            if (_posterInstruction != null) _posterInstruction.gameObject.SetActive(false);
            MadFactLokiLogger.Instance?.Log("market_gap_value_added",
                "Player added a market-gap concept", new
                {
                    level_id = 6,
                    concept = d.Label.Replace("\n", " "),
                    concept_count = _placed.Count
                });

            var rng = Random.insideUnitCircle * 110f;
            var card = UIFactory.Bevel(_board, "Sticker", new Color(0.97f, 0.95f, 0.88f));
            var crt = UIFactory.RT(card.gameObject);
            crt.sizeDelta = new Vector2(86, 92);
            crt.anchoredPosition = new Vector2(Mathf.Clamp(rng.x, -110, 110), Mathf.Clamp(rng.y, -120, 120));
            crt.localRotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));

            var pic = UIFactory.Image(card.transform, "Pic", Color.white, ArtSprites.Sticker(defIndex), Image.Type.Simple, false);
            pic.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(pic.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(74, 60), new Vector2(0, -5));
            var cap = UIFactory.Text(card.transform, "Cap", d.Label.Replace("\n", " "), 11, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, true);
            UIFactory.Place(UIFactory.RT(cap.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(80, 24), new Vector2(0, 4));

            // remove button
            var x = UIFactory.Button(card.transform, "X", "", null, Theme.ErrorRed, 11, Theme.SystemSans, Color.white);
            UIFactory.Place(UIFactory.RT(x.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(18, 18), new Vector2(-1, -1));
            UIFactory.ButtonIcon(x, ArtSprites.Remove(), 14f, true);
            int placedIdx = _placed.Count - 1;
            var drag = card.gameObject.AddComponent<DraggableSticker>();
            drag.board = _board;
            x.onClick.AddListener(() =>
            {
                _placed.Remove(d);
                MadFactLokiLogger.Instance?.Log("market_gap_value_removed",
                    "Player removed a market-gap concept",
                    new { level_id = 6, concept = d.Label.Replace("\n", " "), concept_count = _placed.Count });
                Destroy(card.gameObject);
                if (_posterInstruction != null) _posterInstruction.gameObject.SetActive(_placed.Count == 0);
                RecomputeMatch();
            });

            if (AudioTension.I != null) AudioTension.I.Beep();
            RecomputeMatch();
        }

        void RecomputeMatch()
        {
            Latent sum = new Latent(0, 0, 0, 0);
            foreach (var s in _placed) for (int d = 0; d < 4; d++) sum[d] += s.Vibe[d];
            float maxComp = 0.0001f; for (int d = 0; d < 4; d++) maxComp = Mathf.Max(maxComp, sum[d]);
            for (int d = 0; d < 4; d++)
            {
                var f = UIFactory.RT(_posterBars[d].gameObject);
                f.anchorMax = new Vector2(Mathf.Clamp01(sum[d] / maxComp), 1);
            }

            float match = _placed.Count == 0 ? 0f : Cosine(sum, _gap);
            int pct = Mathf.RoundToInt(match * 100f);
            var mf = UIFactory.RT(_matchFill.gameObject);
            mf.anchorMax = new Vector2(match, 1);
            _matchFill.color = match > 0.85f ? Theme.Cash : match > 0.6f ? Theme.Coin : Theme.ErrorRed;

            bool ok = match >= 0.88f && _placed.Count >= 3;
            // always say what's missing — a dead button with no explanation reads as broken
            _matchText.text = ok ? "MATCH " + pct + "%: GO!"
                : _placed.Count < 3 ? "MATCH " + pct + "% (pin 3+ cutouts)"
                : "MATCH " + pct + "% (need 88%)";
            _greenlight.interactable = ok && !_won;
            UIFactory.SetButtonLabel(_greenlight, ok ? "GREENLIGHT" : "NEED 88% MATCH");
            _greenlight.GetComponentInChildren<Text>().color = ok ? Color.white : new Color(0.64f, 0.64f, 0.58f);
        }

        static float Cosine(Latent a, Latent b)
        {
            float dot = a.Dot(b), ma = a.Magnitude, mb = b.Magnitude;
            if (ma < 1e-5f || mb < 1e-5f) return 0f;
            return Mathf.Clamp01(dot / (ma * mb));
        }

        void TryGreenlight()
        {
            if (_won) return;
            _won = true;
            _greenlight.interactable = false;
            if (AudioTension.I != null) { AudioTension.I.ChaChing(); AudioTension.I.Clunk(); }
            GameManager.I.AddMoney(1000);
            MadFactLokiLogger.Instance?.Log("choice_selected", "Player greenlit a market-gap movie", new
            {
                level_id = 6,
                choice_id = "greenlight",
                concept_count = _placed.Count,
                money_after = GameManager.I.Money
            });
            MadFactBootstrap.I.OnGreenlit();
        }
    }

    /// <summary>Lets a pinned sticker be dragged around the poster board.</summary>
    public class DraggableSticker : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform board;
        public void OnBeginDrag(PointerEventData e) { transform.SetAsLastSibling(); if (AudioTension.I != null) AudioTension.I.Beep(); }
        public void OnDrag(PointerEventData e)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out local))
            {
                local.x = Mathf.Clamp(local.x, -board.rect.width / 2f + 40, board.rect.width / 2f - 40);
                local.y = Mathf.Clamp(local.y, -board.rect.height / 2f + 40, board.rect.height / 2f - 40);
                ((RectTransform)transform).anchoredPosition = local;
            }
        }
    }
}
