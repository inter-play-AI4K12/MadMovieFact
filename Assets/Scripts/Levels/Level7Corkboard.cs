using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 7 turns the market gap into a categorized creative brief. Players pin
    /// settings, characters, story ideas, and effects to a board; the chosen ideas are
    /// carried into Level 8 as an editable image-generation prompt.
    /// </summary>
    public class Level7Corkboard : MonoBehaviour
    {
        const int CategoryCount = 4;
        const int OptionsPerCategory = 6;

        struct StickerDef
        {
            public string Category;
            public string Label;
            public Latent Vibe;
            public Color Color;
            public int ArtIndex;

            public StickerDef(string category, string label, Latent vibe, Color color, int artIndex)
            {
                Category = category;
                Label = label;
                Vibe = vibe;
                Color = color;
                ArtIndex = artIndex;
            }
        }

        static readonly string[] CategoryNames =
            { "SETTING", "CHARACTER", "STORY IDEA", "PROP & EFFECT" };

        static readonly StickerDef[] Palette =
        {
            // SETTING
            new StickerDef("SETTING", "HAUNTED HOUSE", new Latent(0,1,0,0), new Color(.45f,.32f,.55f), 0),
            new StickerDef("SETTING", "MOONLIT VIDEO STORE", new Latent(.15f,.85f,.25f,0), new Color(.34f,.36f,.58f), 1),
            new StickerDef("SETTING", "ABANDONED CARNIVAL", new Latent(0,.75f,.45f,.1f), new Color(.58f,.34f,.48f), 2),
            new StickerDef("SETTING", "FOGGY FOREST", new Latent(.05f,.9f,.1f,0), new Color(.32f,.48f,.40f), 0),
            new StickerDef("SETTING", "SPOOKY SCHOOL", new Latent(0,.65f,.45f,0), new Color(.48f,.42f,.30f), 1),
            new StickerDef("SETTING", "MYSTERY MANSION", new Latent(.05f,.8f,.25f,0), new Color(.52f,.40f,.55f), 2),

            // CHARACTER
            new StickerDef("CHARACTER", "FRIENDLY GHOST", new Latent(0,.75f,.45f,0), new Color(.55f,.55f,.70f), 1),
            new StickerDef("CHARACTER", "LAUGHING SKULL", new Latent(0,.8f,.8f,0), new Color(.70f,.45f,.65f), 3),
            new StickerDef("CHARACTER", "NERVOUS VAMPIRE", new Latent(0,.65f,.65f,0), new Color(.62f,.32f,.40f), 3),
            new StickerDef("CHARACTER", "MONSTER COMEDIAN", new Latent(0,.6f,.9f,0), new Color(.55f,.44f,.26f), 4),
            new StickerDef("CHARACTER", "TWIN DETECTIVES", new Latent(.25f,.35f,.45f,.1f), new Color(.32f,.52f,.62f), 8),
            new StickerDef("CHARACTER", "MUMMY LIBRARIAN", new Latent(0,.7f,.55f,0), new Color(.66f,.58f,.38f), 2),

            // STORY IDEA
            new StickerDef("STORY IDEA", "A CURSE GOES WRONG", new Latent(0,.7f,.75f,.1f), new Color(.72f,.42f,.40f), 4),
            new StickerDef("STORY IDEA", "GHOSTS SAVE THE DAY", new Latent(.05f,.65f,.65f,.15f), new Color(.50f,.52f,.72f), 1),
            new StickerDef("STORY IDEA", "THE MONSTER IS SHY", new Latent(0,.55f,.85f,0), new Color(.66f,.44f,.62f), 3),
            new StickerDef("STORY IDEA", "MIDNIGHT PRANK WAR", new Latent(0,.45f,.9f,.25f), new Color(.78f,.52f,.24f), 5),
            new StickerDef("STORY IDEA", "A VERY BAD SPELL", new Latent(.1f,.7f,.7f,.1f), new Color(.56f,.36f,.68f), 4),
            new StickerDef("STORY IDEA", "THE VILLAIN NEEDS HELP", new Latent(0,.5f,.8f,.1f), new Color(.68f,.38f,.46f), 6),

            // PROP & EFFECT
            new StickerDef("PROP & EFFECT", "BOO-HA-HA SIGN", new Latent(0,.6f,.75f,0), new Color(.85f,.55f,.30f), 4),
            new StickerDef("PROP & EFFECT", "BANANA PEEL", new Latent(0,0,1,0), new Color(.92f,.80f,.25f), 5),
            new StickerDef("PROP & EFFECT", "CLOWN NOSE", new Latent(0,.1f,.95f,0), new Color(.90f,.30f,.30f), 6),
            new StickerDef("PROP & EFFECT", "FLYING BATS", new Latent(0,.85f,.2f,.1f), new Color(.38f,.34f,.48f), 7),
            new StickerDef("PROP & EFFECT", "LIGHTNING STRIKE", new Latent(.15f,.55f,.1f,.65f), new Color(.44f,.58f,.88f), 10),
            new StickerDef("PROP & EFFECT", "ROCKET-PACK BROOM", new Latent(.45f,.2f,.5f,.7f), new Color(.42f,.62f,.78f), 8),
        };

        [SerializeField] GameObject _root;
        [SerializeField] RectTransform _board;
        [SerializeField] Image[] _gapBars = new Image[4];
        [SerializeField] Image[] _posterBars = new Image[4];
        [SerializeField] Text _matchText;
        [SerializeField] Image _matchFill;
        [SerializeField] Button _greenlight;
        [SerializeField] Text _posterInstruction;
        [SerializeField] Button[] _categoryButtons = new Button[CategoryCount];
        [SerializeField] Button[] _optionButtons = new Button[OptionsPerCategory];

        readonly List<StickerDef> _placed = new List<StickerDef>();
        Latent _gap;
        int _category;
        bool _won;

        void Awake()
        {
            RebindVisualReferences();
            if (_root == null) return;
            _gap = GameData.MarketGapVibe();
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
            Bind("Green", TryGreenlight);
            for (int i = 0; i < CategoryCount; i++)
            {
                int category = i;
                Bind("Cat" + i, () => ShowCategory(category));
                if (_categoryButtons[i] == null)
                    _categoryButtons[i] = UIFactory.FindDeep<Button>(transform, "Cat" + i);
            }
            for (int i = 0; i < OptionsPerCategory; i++)
            {
                int slot = i;
                Bind("S" + i, () => AddSticker(_category * OptionsPerCategory + slot));
                if (_optionButtons[i] == null)
                    _optionButtons[i] = UIFactory.FindDeep<Button>(transform, "S" + i);
            }
            ShowCategory(0);
        }

        void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level7Corkboard Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level7");
            UIFactory.Fill(UIFactory.RT(go));
            var level = go.AddComponent<Level7Corkboard>();
            level.Build(go.transform);
            return level;
        }

        void Build(Transform parent)
        {
            _gap = GameData.MarketGapVibe();
            _root = UIFactory.Image(parent, "Level7Corkboard", new Color(.30f, .20f, .10f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            var cork = UIFactory.Image(_root.transform, "Cork", new Color(.62f, .44f, .24f));
            UIFactory.Fill(UIFactory.RT(cork.gameObject));
            var rng = new System.Random(7);
            for (int i = 0; i < 120; i++)
            {
                var fleck = UIFactory.Image(cork.transform, "f", new Color(.5f + (float)rng.NextDouble() * .2f,
                    .36f + (float)rng.NextDouble() * .15f, .18f, .5f), Theme.Disc, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(fleck.gameObject), new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                    new Vector2(.5f, .5f), new Vector2(3 + (float)rng.NextDouble() * 4,
                    3 + (float)rng.NextDouble() * 4), Vector2.zero);
            }

            var title = UIFactory.Text(_root.transform, "Title",
                "THE CORKBOARD: build a categorized brief for the SPOOK-COMEDY gap", 15,
                new Color(.20f, .12f, .05f), Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(760, 24), new Vector2(20, -10));

            var posterFrame = UIFactory.Bevel(_root.transform, "PosterFrame", new Color(.95f, .93f, .86f));
            UIFactory.Place(UIFactory.RT(posterFrame.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(340, 380), new Vector2(-4, 0));
            var pin = UIFactory.Image(posterFrame.transform, "Pin", new Color(.85f, .2f, .2f), Theme.Disc, Image.Type.Simple, false);
            UIFactory.Place(UIFactory.RT(pin.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(16, 16), new Vector2(0, -2));
            var posterBg = UIFactory.Image(posterFrame.transform, "PosterBg", new Color(.12f, .10f, .16f));
            UIFactory.Fill(UIFactory.RT(posterBg.gameObject), 12, 12, 12, 40);
            _board = UIFactory.RT(posterBg.gameObject);
            _posterInstruction = UIFactory.Text(posterBg.transform, "Instructions",
                "CHOOSE A CATEGORY\nTHEN PIN IDEAS HERE\n\nDRAG THEM AROUND\nYOUR POSTER BRIEF",
                15, new Color(.75f, .72f, .66f, .82f), Theme.Typewriter,
                TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_posterInstruction.gameObject), 36, 36, 36, 36);
            var posterTitle = UIFactory.Text(posterFrame.transform, "PT", "YOUR FEATURE FILM", 15,
                new Color(.15f, .1f, .05f), Theme.Typewriter, TextAnchor.LowerCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(posterTitle.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(310, 30), new Vector2(0, 8));

            BuildAnalysis(_root.transform);
            BuildPalette(_root.transform);

            var leave = UIFactory.Button(_root.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(26, 22), new Vector2(-10, -8));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);
            ShowCategory(0);
            RecomputeMatch();
        }

        void BuildAnalysis(Transform root)
        {
            var panel = UIFactory.Bevel(root, "Analysis", Theme.Face);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(205, 380), new Vector2(15, 0));

            PlaceText(panel.transform, "h", "MARKET-GAP VIBE\n(from the matrix)", 13, Theme.Ink,
                new Vector2(187, 40), new Vector2(0, -8), TextAnchor.UpperCenter, FontStyle.Bold);

            for (int d = 0; d < 4; d++)
            {
                float y = -56 - d * 30;
                var name = UIFactory.Text(panel.transform, "gn" + d, Latent.Names[d], 10, Latent.Colors[d],
                    Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(name.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(84, 20), new Vector2(9, y));
                var bg = UIFactory.Image(panel.transform, "gbg" + d, new Color(0, 0, 0, .25f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(92, 16), new Vector2(99, y));
                var fill = UIFactory.Image(bg.transform, "gf" + d, Latent.Colors[d]);
                var fillRect = UIFactory.RT(fill.gameObject);
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(_gap[d]), 1);
                fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                _gapBars[d] = fill;
            }

            PlaceText(panel.transform, "h2", "YOUR BRIEF", 12, Theme.Ink, new Vector2(187, 18),
                new Vector2(0, -184), TextAnchor.UpperCenter, FontStyle.Bold);
            for (int d = 0; d < 4; d++)
            {
                float y = -206 - d * 22;
                var bg = UIFactory.Image(panel.transform, "pbg" + d, new Color(0, 0, 0, .25f), null, Image.Type.Simple, false);
                UIFactory.Place(UIFactory.RT(bg.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(92, 14), new Vector2(99, y));
                var fill = UIFactory.Image(bg.transform, "pf" + d, Latent.Colors[d]);
                var fillRect = UIFactory.RT(fill.gameObject);
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(0, 1);
                fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                _posterBars[d] = fill;
                var name = UIFactory.Text(panel.transform, "pn" + d, Latent.Names[d].Substring(0, 2), 9,
                    Latent.Colors[d], Theme.Typewriter, TextAnchor.MiddleLeft, false);
                UIFactory.Place(UIFactory.RT(name.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(36, 14), new Vector2(9, y));
            }

            var matchBg = UIFactory.Image(panel.transform, "mbg", new Color(0, 0, 0, .3f), null, Image.Type.Simple, false);
            UIFactory.Place(UIFactory.RT(matchBg.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(183, 22), new Vector2(0, 40));
            _matchFill = UIFactory.Image(matchBg.transform, "mf", Theme.ErrorRed);
            var matchRect = UIFactory.RT(_matchFill.gameObject);
            matchRect.anchorMin = new Vector2(0, 0);
            matchRect.anchorMax = new Vector2(0, 1);
            matchRect.offsetMin = matchRect.offsetMax = Vector2.zero;
            _matchText = UIFactory.Text(panel.transform, "mt", "MATCH 0%", 11, Theme.Ink,
                Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_matchText.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(183, 22), new Vector2(0, 40));

            _greenlight = UIFactory.Button(panel.transform, "Green", "BUILD THE PROMPT", TryGreenlight,
                Theme.FaceShade, 12, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(_greenlight.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(183, 32), new Vector2(0, 6));
            UIFactory.ButtonIcon(_greenlight, ArtSprites.Confirm(), 20f);
            _greenlight.interactable = false;
        }

        void BuildPalette(Transform root)
        {
            var panel = UIFactory.Bevel(root, "Palette", Theme.Face);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(250, 400), new Vector2(-15, 0));
            PlaceText(panel.transform, "h", "PICK A CATEGORY, THEN AN IDEA", 12, Theme.Ink,
                new Vector2(232, 24), new Vector2(0, -6), TextAnchor.UpperCenter, FontStyle.Bold);

            string[] tabLabels = { "PLACE", "STAR", "STORY", "EFFECT" };
            for (int i = 0; i < CategoryCount; i++)
            {
                int category = i;
                var tab = UIFactory.Button(panel.transform, "Cat" + i, tabLabels[i],
                    () => ShowCategory(category), Theme.FaceShade, 9, Theme.SystemSans, Theme.Ink);
                UIFactory.Place(UIFactory.RT(tab.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(55, 28), new Vector2(-87 + i * 58, -42));
                _categoryButtons[i] = tab;
            }

            for (int i = 0; i < OptionsPerCategory; i++)
            {
                int slot = i;
                var def = Palette[i];
                float x = i % 2 == 0 ? -57 : 57;
                float y = -91 - (i / 2) * 91;
                var button = UIFactory.Button(panel.transform, "S" + i, def.Label,
                    () => AddSticker(_category * OptionsPerCategory + slot), def.Color, 9,
                    Theme.Typewriter, Color.white);
                UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(108, 82), new Vector2(x, y));
                UIFactory.ButtonIcon(button, ArtSprites.Sticker(def.ArtIndex), 34f);
                button.GetComponentInChildren<Text>().fontSize = 8;
                _optionButtons[i] = button;
            }

            var categoryLabel = UIFactory.Text(panel.transform, "CategoryLabel", CategoryNames[0], 11,
                Theme.CrtAmber, Theme.Typewriter, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(categoryLabel.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(220, 20), new Vector2(0, 8));
        }

        static Text PlaceText(Transform parent, string name, string copy, int size, Color color,
            Vector2 box, Vector2 position, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            var text = UIFactory.Text(parent, name, copy, size, color, Theme.SystemSans, alignment, true, style);
            UIFactory.Place(UIFactory.RT(text.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1), box, position);
            return text;
        }

        public void Open()
        {
            RebindVisualReferences();
            if (_root == null) return;
            transform.SetAsLastSibling();
            _root.SetActive(true);
            _won = false;
            _gap = GameData.MarketGapVibe();
            MadFactBootstrap.I.Storefront.SetLine(0);
            if (_posterInstruction != null) _posterInstruction.gameObject.SetActive(_placed.Count == 0);
            ShowCategory(_category);
            RecomputeMatch();
        }

        public void Close() => _root.SetActive(false);

        void ShowCategory(int category)
        {
            _category = Mathf.Clamp(category, 0, CategoryCount - 1);
            var categoryLabel = UIFactory.FindDeep<Text>(transform, "CategoryLabel");
            if (categoryLabel != null) categoryLabel.text = CategoryNames[_category];

            for (int i = 0; i < CategoryCount; i++)
            {
                var tab = _categoryButtons[i] != null ? _categoryButtons[i] : UIFactory.FindDeep<Button>(transform, "Cat" + i);
                if (tab == null) continue;
                var image = tab.GetComponent<Image>();
                if (image != null) image.color = i == _category ? Theme.CrtAmber : Theme.FaceShade;
                var label = tab.GetComponentInChildren<Text>();
                if (label != null) label.color = i == _category ? Theme.Ink : Theme.InkSoft;
            }

            for (int slot = 0; slot < OptionsPerCategory; slot++)
            {
                var button = _optionButtons[slot] != null ? _optionButtons[slot] : UIFactory.FindDeep<Button>(transform, "S" + slot);
                if (button == null) continue;
                var def = Palette[_category * OptionsPerCategory + slot];
                UIFactory.SetButtonLabel(button, def.Label);
                var image = button.GetComponent<Image>();
                if (image != null) image.color = def.Color;
                var icon = UIFactory.FindDeep<Image>(button.transform, "Icon");
                if (icon != null) icon.sprite = ArtSprites.Sticker(def.ArtIndex);
            }
        }

        void AddSticker(int definitionIndex)
        {
            if (definitionIndex < 0 || definitionIndex >= Palette.Length) return;
            var definition = Palette[definitionIndex];
            _placed.Add(definition);
            if (_posterInstruction != null) _posterInstruction.gameObject.SetActive(false);
            MadFactLokiLogger.Instance?.Log("market_gap_value_added", "Player added a market-gap concept", new
            {
                level_id = 7,
                category = definition.Category,
                concept = definition.Label,
                concept_count = _placed.Count
            });

            var random = Random.insideUnitCircle * 100f;
            var card = UIFactory.Bevel(_board, "Sticker", new Color(.97f, .95f, .88f));
            var cardRect = UIFactory.RT(card.gameObject);
            cardRect.sizeDelta = new Vector2(80, 88);
            cardRect.anchoredPosition = new Vector2(Mathf.Clamp(random.x, -92, 92), Mathf.Clamp(random.y, -114, 114));
            cardRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));

            var picture = UIFactory.Image(card.transform, "Pic", Color.white,
                ArtSprites.Sticker(definition.ArtIndex), Image.Type.Simple, false);
            picture.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(picture.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(68, 54), new Vector2(0, -5));
            var caption = UIFactory.Text(card.transform, "Cap", definition.Label, 9, Theme.Ink,
                Theme.Typewriter, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(caption.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(72, 28), new Vector2(0, 4));

            var remove = UIFactory.Button(card.transform, "X", "", null, Theme.ErrorRed, 11,
                Theme.SystemSans, Color.white);
            UIFactory.Place(UIFactory.RT(remove.gameObject), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(18, 18), new Vector2(-1, -1));
            UIFactory.ButtonIcon(remove, ArtSprites.Remove(), 14f, true);
            var drag = card.gameObject.AddComponent<DraggableSticker>();
            drag.board = _board;
            remove.onClick.AddListener(() =>
            {
                _placed.Remove(definition);
                MadFactLokiLogger.Instance?.Log("market_gap_value_removed", "Player removed a market-gap concept", new
                {
                    level_id = 7,
                    category = definition.Category,
                    concept = definition.Label,
                    concept_count = _placed.Count
                });
                Destroy(card.gameObject);
                if (_posterInstruction != null) _posterInstruction.gameObject.SetActive(_placed.Count == 0);
                RecomputeMatch();
            });

            if (AudioTension.I != null) AudioTension.I.Beep();
            RecomputeMatch();
        }

        void RecomputeMatch()
        {
            RebindVisualReferences();
            Latent sum = new Latent(0, 0, 0, 0);
            foreach (var sticker in _placed)
                for (int dimension = 0; dimension < Latent.Dim; dimension++)
                    sum[dimension] += sticker.Vibe[dimension];

            float maxComponent = .0001f;
            for (int dimension = 0; dimension < Latent.Dim; dimension++)
                maxComponent = Mathf.Max(maxComponent, sum[dimension]);
            for (int dimension = 0; dimension < Latent.Dim; dimension++)
            {
                var bar = _posterBars != null && dimension < _posterBars.Length
                    ? _posterBars[dimension]
                    : null;
                if (bar != null)
                    UIFactory.RT(bar.gameObject).anchorMax =
                        new Vector2(Mathf.Clamp01(sum[dimension] / maxComponent), 1);
            }

            float match = _placed.Count == 0 ? 0f : Cosine(sum, _gap);
            int percentage = Mathf.RoundToInt(match * 100f);
            if (_matchFill != null)
            {
                UIFactory.RT(_matchFill.gameObject).anchorMax = new Vector2(match, 1);
                _matchFill.color = match > .85f ? Theme.Cash : match > .6f ? Theme.Coin : Theme.ErrorRed;
            }

            bool enoughCategories = _placed.Select(sticker => sticker.Category).Distinct().Count() >= 3;
            bool ready = match >= .88f && _placed.Count >= 3 && enoughCategories;
            if (_matchText != null)
                _matchText.text = ready ? "MATCH " + percentage + "%: READY"
                    : _placed.Count < 3 ? "MATCH " + percentage + "% (pin 3+ ideas)"
                    : !enoughCategories ? "MATCH " + percentage + "% (use 3 categories)"
                    : "MATCH " + percentage + "% (need 88%)";
            if (_greenlight == null) return;
            _greenlight.interactable = ready && !_won;
            UIFactory.SetButtonLabel(_greenlight, ready ? "BUILD THE PROMPT" : "BRIEF NOT READY");
            var label = _greenlight.GetComponentInChildren<Text>();
            if (label != null) label.color = ready ? Color.white : new Color(.64f, .64f, .58f);
        }

        /// <summary>
        /// Scene and prefab edits can temporarily lose a serialized UI reference. Matching is
        /// gameplay state, so it must keep working even when one of its optional visuals is
        /// missing. Restore the references we can find, then let RecomputeMatch update each
        /// visual independently instead of abandoning the whole calculation.
        /// </summary>
        void RebindVisualReferences()
        {
            if (_root == null)
            {
                var rootTransform = transform.Find("Level7Corkboard");
                if (rootTransform != null) _root = rootTransform.gameObject;
            }
            if (_board == null)
            {
                var boardImage = UIFactory.FindDeep<Image>(transform, "PosterBg");
                if (boardImage != null) _board = UIFactory.RT(boardImage.gameObject);
            }

            if (_gapBars == null || _gapBars.Length != Latent.Dim)
                _gapBars = new Image[Latent.Dim];
            if (_posterBars == null || _posterBars.Length != Latent.Dim)
                _posterBars = new Image[Latent.Dim];
            for (int dimension = 0; dimension < Latent.Dim; dimension++)
            {
                if (_gapBars[dimension] == null)
                    _gapBars[dimension] = UIFactory.FindDeep<Image>(transform, "gf" + dimension);
                if (_posterBars[dimension] == null)
                    _posterBars[dimension] = UIFactory.FindDeep<Image>(transform, "pf" + dimension);
            }

            if (_matchText == null) _matchText = UIFactory.FindDeep<Text>(transform, "mt");
            if (_matchFill == null) _matchFill = UIFactory.FindDeep<Image>(transform, "mf");
            if (_greenlight == null) _greenlight = UIFactory.FindDeep<Button>(transform, "Green");
            if (_posterInstruction == null)
                _posterInstruction = UIFactory.FindDeep<Text>(transform, "Instructions");
        }

        static float Cosine(Latent a, Latent b)
        {
            float dot = a.Dot(b);
            float magnitudeA = a.Magnitude;
            float magnitudeB = b.Magnitude;
            return magnitudeA < 1e-5f || magnitudeB < 1e-5f
                ? 0f
                : Mathf.Clamp01(dot / (magnitudeA * magnitudeB));
        }

        void TryGreenlight()
        {
            if (_won) return;
            _won = true;
            _greenlight.interactable = false;
            SavePosterBrief();
            if (AudioTension.I != null)
            {
                AudioTension.I.ChaChing();
                AudioTension.I.Clunk();
            }
            GameManager.I.AddMoney(1000);
            MadFactLokiLogger.Instance?.Log("choice_selected", "Player completed a market-gap poster brief", new
            {
                level_id = 7,
                choice_id = "poster_brief_ready",
                concept_count = GameManager.I.Run.PosterConcepts.Count,
                category_count = GameManager.I.Run.PosterConcepts.Select(choice => choice.Category).Distinct().Count(),
                money_after = GameManager.I.Money
            });
            MadFactBootstrap.I.OnGreenlit();
        }

        void SavePosterBrief()
        {
            var run = GameManager.I.Run;
            run.PosterConcepts.Clear();
            var seen = new HashSet<string>();
            foreach (var sticker in _placed)
            {
                string key = sticker.Category + "\n" + sticker.Label;
                if (seen.Add(key))
                    run.PosterConcepts.Add(new PosterConceptChoice(sticker.Category, sticker.Label));
            }

            var prompt = new StringBuilder();
            prompt.Append("Create a portrait movie poster for a family-friendly spooky comedy set in a 1990s VHS-store world. ");
            prompt.Append("The design should include: ");
            for (int category = 0; category < CategoryCount; category++)
            {
                string categoryName = CategoryNames[category];
                string[] choices = run.PosterConcepts
                    .Where(choice => choice.Category == categoryName)
                    .Select(choice => choice.Label.ToLowerInvariant())
                    .ToArray();
                if (choices.Length == 0) continue;
                prompt.Append(categoryName.ToLowerInvariant()).Append(": ")
                    .Append(string.Join(", ", choices)).Append("; ");
            }
            prompt.Append("Make it funny and mysterious, never graphic or frightening. Use fictional characters only. ");
            prompt.Append("Include a bold, readable fictional movie title and room for a short tagline.");
            run.PosterPrompt = prompt.ToString();
            run.PosterGenerations.Clear();
            run.SelectedPosterIndex = -1;
        }
    }

    /// <summary>Lets a pinned sticker be dragged around the poster board.</summary>
    public class DraggableSticker : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform board;

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
            if (AudioTension.I != null) AudioTension.I.Beep();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (board == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;
            local.x = Mathf.Clamp(local.x, -board.rect.width / 2f + 40, board.rect.width / 2f - 40);
            local.y = Mathf.Clamp(local.y, -board.rect.height / 2f + 40, board.rect.height / 2f - 40);
            ((RectTransform)transform).anchoredPosition = local;
        }
    }
}
