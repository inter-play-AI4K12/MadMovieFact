using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 8 turns the Level 7 creative brief into an editable image prompt. The
    /// browser talks only to the same-origin local relay, which keeps OPENAI_API_KEY on
    /// the server. Up to seven generated drafts remain browsable for the active run.
    /// </summary>
    public sealed class Level8PosterStudio : MonoBehaviour
    {
        public const int MaxGenerations = 7;
        const int StyleCount = 6;
        const string PosterRelayPath = "/api/poster/generate";
        const int LocalRelayPort = 8081;

        [Serializable]
        sealed class PosterRequest
        {
            public string session_id;
            public string prompt;
        }

        [Serializable]
        sealed class PosterResponse
        {
            public string image_base64;
            public string mime_type;
            public int remaining;
        }

        [Serializable]
        sealed class RelayError
        {
            public string error;
            public string code;
        }

        struct StyleDef
        {
            public string Label;
            public string Prompt;
            public Color Color;

            public StyleDef(string label, string prompt, Color color)
            {
                Label = label;
                Prompt = prompt;
                Color = color;
            }
        }

        static readonly StyleDef[] Styles =
        {
            new StyleDef("1990s VHS", "1990s painted VHS box art, bold airbrushed lighting, slightly worn print texture", new Color(.15f,.55f,.64f)),
            new StyleDef("HAND-PAINTED", "hand-painted gouache poster with visible brush texture and a warm theatrical composition", new Color(.68f,.46f,.24f)),
            new StyleDef("COMIC BOOK", "bold comic-book inks, halftone dots, expressive shapes, and an energetic cover composition", new Color(.72f,.28f,.28f)),
            new StyleDef("NEON MYSTERY", "neon mystery poster with cyan and magenta rim light, playful fog, and deep shadows", new Color(.48f,.30f,.68f)),
            new StyleDef("CLAY ANIMATION", "playful clay-animation look with handmade miniature sets and friendly characters", new Color(.42f,.62f,.36f)),
            new StyleDef("PAPER COLLAGE", "cut-paper collage with layered shapes, tactile craft texture, and bold silhouettes", new Color(.72f,.60f,.28f)),
        };

        [SerializeField] GameObject _root;
        [SerializeField] InputField _prompt;
        [SerializeField] Text _counter;
        [SerializeField] Text _status;
        [SerializeField] Text _currentLabel;
        [SerializeField] Image _poster;
        [SerializeField] Text _posterPlaceholder;
        [SerializeField] Button _generate;
        [SerializeField] Button _usePoster;
        [SerializeField] Button[] _styleButtons = new Button[StyleCount];
        [SerializeField] Button[] _historyButtons = new Button[MaxGenerations];
        [SerializeField] Image[] _historyImages = new Image[MaxGenerations];

        readonly List<Sprite> _historySprites = new List<Sprite>();
        int _selectedStyle;
        int _selectedGeneration = -1;
        bool _requestInFlight;

        void Awake()
        {
            if (_root == null) return;
            if (_prompt == null) _prompt = UIFactory.FindDeep<InputField>(transform, "PromptInput");
            if (_generate == null) _generate = UIFactory.FindDeep<Button>(transform, "Generate");
            if (_usePoster == null) _usePoster = UIFactory.FindDeep<Button>(transform, "UsePoster");

            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
            Bind("Generate", StartGeneration);
            Bind("UsePoster", UseSelectedPoster);
            for (int i = 0; i < StyleCount; i++)
            {
                int style = i;
                Bind("Style" + i, () => SelectStyle(style));
                if (_styleButtons[i] == null)
                    _styleButtons[i] = UIFactory.FindDeep<Button>(transform, "Style" + i);
            }
            for (int i = 0; i < MaxGenerations; i++)
            {
                int generation = i;
                Bind("History" + i, () => SelectHistory(generation));
                if (_historyButtons[i] == null)
                    _historyButtons[i] = UIFactory.FindDeep<Button>(transform, "History" + i);
                if (_historyImages[i] == null && _historyButtons[i] != null)
                    _historyImages[i] = UIFactory.FindDeep<Image>(_historyButtons[i].transform, "Preview");
            }
            if (_prompt != null)
            {
                _prompt.onValueChanged.RemoveAllListeners();
                _prompt.onValueChanged.AddListener(SavePromptEdit);
            }
            SelectStyle(0);
        }

        void OnDestroy()
        {
            foreach (var sprite in _historySprites)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
            _historySprites.Clear();
        }

        void Bind(string objectName, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, objectName);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level8PosterStudio Create(Transform canvas)
        {
            var node = UIFactory.Node(canvas, "Level8");
            UIFactory.Fill(UIFactory.RT(node));
            var studio = node.AddComponent<Level8PosterStudio>();
            studio.Build(node.transform);
            return studio;
        }

        void Build(Transform parent)
        {
            _root = UIFactory.Image(parent, "Level8PosterStudio", new Color(.035f, .075f, .08f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            var title = UIFactory.Text(_root.transform, "Title", "POSTER LAB: turn your brief into a movie poster",
                16, Theme.CrtAmber, Theme.Typewriter, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(760, 25), new Vector2(18, -10));
            var leave = UIFactory.Button(_root.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(26, 22), new Vector2(-10, -8));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);

            BuildPromptPanel(_root.transform);
            BuildPosterPanel(_root.transform);
            BuildChoicePanel(_root.transform);
            SelectStyle(0);
        }

        void BuildPromptPanel(Transform parent)
        {
            var panel = UIFactory.Bevel(parent, "PromptPanel", new Color(.10f, .13f, .13f));
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(320, 405), new Vector2(15, -8));
            PlaceText(panel.transform, "PromptHeading", "1. EDIT YOUR PROMPT", 13, Theme.CrtGreen,
                new Vector2(190, 24), new Vector2(-51, -12), TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceText(panel.transform, "PromptHelp",
                "Your Level 7 ideas are already here. Change any words you want before generating.",
                10, Theme.TitleText, new Vector2(292, 42), new Vector2(0, -40), TextAnchor.UpperLeft);

            var fieldBackground = UIFactory.Image(panel.transform, "PromptInput", new Color(.018f, .035f, .034f),
                Theme.Solid, Image.Type.Simple, true);
            UIFactory.Place(UIFactory.RT(fieldBackground.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(292, 235), new Vector2(0, -88));
            _prompt = fieldBackground.gameObject.AddComponent<InputField>();
            _prompt.contentType = InputField.ContentType.Standard;
            _prompt.lineType = InputField.LineType.MultiLineNewline;
            _prompt.characterLimit = 1800;
            _prompt.targetGraphic = fieldBackground;
            _prompt.caretColor = Theme.CrtGreen;
            _prompt.selectionColor = new Color(.20f, .60f, .65f, .45f);

            var promptText = UIFactory.Text(fieldBackground.transform, "PromptText", DefaultPrompt(), 11,
                new Color(.92f, .92f, .84f), Theme.SystemSans, TextAnchor.UpperLeft, true);
            promptText.resizeTextForBestFit = false;
            UIFactory.Fill(UIFactory.RT(promptText.gameObject), 10, 10, 10, 10);
            _prompt.textComponent = promptText;
            _prompt.text = DefaultPrompt();
            var placeholder = UIFactory.Text(fieldBackground.transform, "Placeholder", "Describe your poster...", 11,
                new Color(.65f, .66f, .60f, .6f), Theme.SystemSans, TextAnchor.UpperLeft, true, FontStyle.Italic);
            UIFactory.Fill(UIFactory.RT(placeholder.gameObject), 10, 10, 10, 10);
            _prompt.placeholder = placeholder;
            _prompt.onValueChanged.AddListener(SavePromptEdit);

            _generate = UIFactory.Button(panel.transform, "Generate", "GENERATE POSTER", StartGeneration,
                Theme.Cash, 14, Theme.SystemSans, Color.white);
            UIFactory.Place(UIFactory.RT(_generate.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(292, 38), new Vector2(0, 42));
            UIFactory.ButtonIcon(_generate, ArtSprites.Play(), 22f);
            _counter = PlaceText(panel.transform, "Counter", "0 / 7 POSTERS", 10, Theme.CrtAmber,
                new Vector2(100, 24), new Vector2(96, -12), TextAnchor.MiddleRight, FontStyle.Bold);
            _status = PlaceText(panel.transform, "Status", "Choose a style, then generate your first draft.",
                9, Theme.TitleText, new Vector2(292, 32), new Vector2(0, -365), TextAnchor.UpperCenter);
        }

        void BuildPosterPanel(Transform parent)
        {
            var panel = UIFactory.Bevel(parent, "PosterPanel", new Color(.12f, .12f, .11f));
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(270, 405), new Vector2(15, -8));
            PlaceText(panel.transform, "PosterHeading", "CURRENT DRAFT", 13, Theme.CrtAmber,
                new Vector2(244, 24), new Vector2(0, -12), TextAnchor.MiddleCenter, FontStyle.Bold);

            var matte = UIFactory.Image(panel.transform, "PosterMatte", new Color(.025f, .025f, .03f));
            UIFactory.Place(UIFactory.RT(matte.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(228, 298), new Vector2(0, -43));
            matte.gameObject.AddComponent<RectMask2D>();
            _poster = UIFactory.Image(matte.transform, "GeneratedPoster", Color.white, Theme.Solid, Image.Type.Simple, false);
            UIFactory.Fill(UIFactory.RT(_poster.gameObject), 6, 6, 6, 6);
            _poster.preserveAspect = true;
            _poster.gameObject.SetActive(false);
            _posterPlaceholder = UIFactory.Text(matte.transform, "PosterPlaceholder",
                "YOUR GENERATED\nPOSTER WILL\nAPPEAR HERE", 15, new Color(.58f, .58f, .52f), Theme.Typewriter,
                TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_posterPlaceholder.gameObject), 24, 24, 24, 24);

            _currentLabel = PlaceText(panel.transform, "CurrentLabel", "NO DRAFT SELECTED", 9, Theme.TitleText,
                new Vector2(242, 22), new Vector2(0, -346), TextAnchor.MiddleCenter, FontStyle.Bold);
            _usePoster = UIFactory.Button(panel.transform, "UsePoster", "USE THIS POSTER", UseSelectedPoster,
                Theme.CrtAmber, 12, Theme.SystemSans, Theme.Ink);
            UIFactory.Place(UIFactory.RT(_usePoster.gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(228, 34), new Vector2(0, 8));
            UIFactory.ButtonIcon(_usePoster, ArtSprites.Confirm(), 20f);
            _usePoster.interactable = false;
        }

        void BuildChoicePanel(Transform parent)
        {
            var panel = UIFactory.Bevel(parent, "ChoicePanel", new Color(.10f, .13f, .13f));
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(225, 405), new Vector2(-15, -8));
            PlaceText(panel.transform, "StyleHeading", "2. CHOOSE A STYLE", 12, Theme.CrtGreen,
                new Vector2(201, 22), new Vector2(0, -12), TextAnchor.MiddleCenter, FontStyle.Bold);

            for (int i = 0; i < StyleCount; i++)
            {
                int style = i;
                var button = UIFactory.Button(panel.transform, "Style" + i, Styles[i].Label,
                    () => SelectStyle(style), Styles[i].Color, 9, Theme.SystemSans, Color.white);
                float x = i % 2 == 0 ? -50 : 50;
                float y = -48 - (i / 2) * 48;
                UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(96, 38), new Vector2(x, y));
                _styleButtons[i] = button;
            }

            PlaceText(panel.transform, "HistoryHeading", "YOUR PREVIOUS DRAFTS", 11, Theme.CrtAmber,
                new Vector2(201, 22), new Vector2(0, -202), TextAnchor.MiddleCenter, FontStyle.Bold);
            PlaceText(panel.transform, "HistoryHelp", "Click a thumbnail to compare it.", 9, Theme.TitleText,
                new Vector2(201, 18), new Vector2(0, -224), TextAnchor.MiddleCenter);

            for (int i = 0; i < MaxGenerations; i++)
            {
                int generation = i;
                int row = i / 4;
                int column = i % 4;
                float x = -75 + column * 50;
                float y = -253 - row * 70;
                var button = UIFactory.Button(panel.transform, "History" + i, (i + 1).ToString(),
                    () => SelectHistory(generation), new Color(.16f, .18f, .17f), 9,
                    Theme.SystemSans, new Color(.70f, .70f, .64f));
                UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(44, 62), new Vector2(x, y));
                var preview = UIFactory.Image(button.transform, "Preview", new Color(.08f, .09f, .09f),
                    Theme.Solid, Image.Type.Simple, false);
                UIFactory.Fill(UIFactory.RT(preview.gameObject), 3, 3, 3, 16);
                preview.preserveAspect = true;
                _historyButtons[i] = button;
                _historyImages[i] = preview;
            }
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
            transform.SetAsLastSibling();
            _root.SetActive(true);
            MadFactBootstrap.I.Storefront.SetLine(0);
            var run = GameManager.I.Run;
            if (string.IsNullOrWhiteSpace(run.PosterPrompt)) run.PosterPrompt = DefaultPrompt();
            if (_prompt != null) _prompt.SetTextWithoutNotify(run.PosterPrompt);
            RebuildHistorySprites();
            RefreshHistory();
            SelectStyle(_selectedStyle);
            int preferred = run.SelectedPosterIndex >= 0
                ? run.SelectedPosterIndex
                : run.PosterGenerations.Count - 1;
            SelectHistory(preferred);
            SetStatus(run.PosterGenerations.Count == 0
                ? "Choose a style, then generate your first draft."
                : "You can edit the prompt and generate another draft.", Theme.TitleText);
        }

        public void Close()
        {
            _root.SetActive(false);
        }

        void SavePromptEdit(string value)
        {
            if (GameManager.I != null && GameManager.I.Run != null)
                GameManager.I.Run.PosterPrompt = value ?? "";
        }

        void SelectStyle(int styleIndex)
        {
            _selectedStyle = Mathf.Clamp(styleIndex, 0, StyleCount - 1);
            for (int i = 0; i < StyleCount; i++)
            {
                var button = _styleButtons[i] != null ? _styleButtons[i] : UIFactory.FindDeep<Button>(transform, "Style" + i);
                if (button == null) continue;
                var image = button.GetComponent<Image>();
                if (image != null) image.color = i == _selectedStyle
                    ? Color.Lerp(Styles[i].Color, Color.white, .22f)
                    : Styles[i].Color;
                var label = button.GetComponentInChildren<Text>();
                if (label != null) label.fontStyle = i == _selectedStyle ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        void StartGeneration()
        {
            if (_requestInFlight || GameManager.I == null) return;
            var run = GameManager.I.Run;
            if (run.PosterGenerations.Count >= MaxGenerations)
            {
                SetStatus("You have all seven drafts. Choose your favorite from the history.", Theme.CrtAmber);
                RefreshControls();
                return;
            }

            string basePrompt = _prompt == null ? "" : _prompt.text.Trim();
            if (basePrompt.Length < 20)
            {
                SetStatus("Add a little more detail to the prompt before generating.", Theme.ErrorRed);
                return;
            }
            run.PosterPrompt = basePrompt;
            StartCoroutine(GeneratePoster(basePrompt, _selectedStyle));
        }

        IEnumerator GeneratePoster(string basePrompt, int styleIndex)
        {
            _requestInFlight = true;
            RefreshControls();
            SetStatus("Painting your poster... this can take about two minutes.", Theme.CrtAmber);
            string requestPrompt = ComposeGenerationPrompt(basePrompt, Styles[styleIndex].Prompt);
            var payload = new PosterRequest
            {
                session_id = GameManager.I.Run.PosterGenerationSessionId,
                prompt = requestPrompt
            };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            using (var request = new UnityWebRequest(PosterEndpoint(), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 150;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
                {
                    ShowRelayError(request);
                    _requestInFlight = false;
                    RefreshControls();
                    yield break;
                }

                PosterResponse response;
                try
                {
                    response = JsonUtility.FromJson<PosterResponse>(request.downloadHandler.text);
                }
                catch (Exception)
                {
                    response = null;
                }
                if (response == null || string.IsNullOrWhiteSpace(response.image_base64))
                {
                    SetStatus("The poster server returned an empty image. Please try again.", Theme.ErrorRed);
                    _requestInFlight = false;
                    RefreshControls();
                    yield break;
                }

                var record = new PosterGenerationRecord(basePrompt, Styles[styleIndex].Label,
                    response.image_base64, string.IsNullOrWhiteSpace(response.mime_type) ? "image/jpeg" : response.mime_type);
                GameManager.I.Run.PosterGenerations.Add(record);
                GameManager.I.Run.SelectedPosterIndex = GameManager.I.Run.PosterGenerations.Count - 1;
                AddHistorySprite(record);
                SelectHistory(GameManager.I.Run.SelectedPosterIndex);
                MadFactLokiLogger.Instance?.Log("poster_generated", "Player generated a poster draft", new
                {
                    level_id = 8,
                    generation_number = GameManager.I.Run.PosterGenerations.Count,
                    style = record.Style,
                    prompt_length = basePrompt.Length,
                    remaining = Mathf.Max(0, MaxGenerations - GameManager.I.Run.PosterGenerations.Count)
                });
                if (AudioTension.I != null) AudioTension.I.ChaChing();
            }

            _requestInFlight = false;
            SetStatus(GameManager.I.Run.PosterGenerations.Count >= MaxGenerations
                ? "Seven drafts complete. Choose your favorite."
                : "Draft ready. Edit the prompt or style to make another.", Theme.Cash);
            RefreshHistory();
            RefreshControls();
        }

        static string ComposeGenerationPrompt(string basePrompt, string stylePrompt)
        {
            return basePrompt.Trim() + "\n\nVisual style: " + stylePrompt + ". " +
                "Create one polished portrait poster at a 2:3 aspect ratio. Keep it appropriate for sixth-grade students: " +
                "no gore, graphic violence, sexual content, drugs, hateful imagery, or real-person likenesses.";
        }

        static string PosterEndpoint()
        {
            string explicitEndpoint = Environment.GetEnvironmentVariable("MADFACT_POSTER_RELAY");
            if (!string.IsNullOrWhiteSpace(explicitEndpoint)) return explicitEndpoint.Trim();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri page))
                return new Uri(page, PosterRelayPath).AbsoluteUri;
            return PosterRelayPath;
#else
            // Unity's WebGL Build & Run preview commonly occupies port 8080. Keep the
            // credential-holding relay separate so requests cannot reach Unity's
            // static-file-only server by accident.
            return "http://127.0.0.1:" + LocalRelayPort + PosterRelayPath;
#endif
        }

        void ShowRelayError(UnityWebRequest request)
        {
            string responseText = request.downloadHandler == null ? "" : request.downloadHandler.text;
            RelayError relayError = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(responseText))
                    relayError = JsonUtility.FromJson<RelayError>(responseText);
            }
            catch (Exception) { }

            Debug.LogWarning("Level 8 poster request failed. endpoint=" + request.url
                + ", result=" + request.result + ", http=" + request.responseCode
                + ", relay_code=" + (relayError == null ? "" : relayError.code));

            if (relayError != null && relayError.code == "moderation_blocked")
            {
                SetStatus("That prompt was blocked. Rewrite it as a safe, family-friendly poster and try again.", Theme.ErrorRed);
                return;
            }
            if (request.responseCode == 429)
            {
                SetStatus("The image service is busy. Wait a moment and try again.", Theme.ErrorRed);
                return;
            }
            if (relayError != null && relayError.code == "poster_not_configured")
            {
                SetStatus("Poster generation is not configured. Add OPENAI_API_KEY to the launcher's .env file.", Theme.ErrorRed);
                return;
            }
            if (relayError != null && relayError.code == "credential_rejected")
            {
                SetStatus("The poster API key was rejected. Check OPENAI_API_KEY and restart the launcher.", Theme.ErrorRed);
                return;
            }
            if (request.result == UnityWebRequest.Result.ConnectionError || request.responseCode == 0)
            {
                SetStatus("Poster server not found. Start the MadFact launcher on port " + LocalRelayPort + ", then try again.", Theme.ErrorRed);
                return;
            }
            if (request.responseCode == 404 || request.responseCode == 405 ||
                (!string.IsNullOrWhiteSpace(responseText) && responseText.TrimStart().StartsWith("<")))
            {
                SetStatus("This is Unity's preview server, not the MadFact launcher. Start serve-web on port "
                    + LocalRelayPort + " and reopen the game there.", Theme.ErrorRed);
                return;
            }
            SetStatus(relayError != null && !string.IsNullOrWhiteSpace(relayError.error)
                ? relayError.error
                : "The poster could not be generated. Please try again.", Theme.ErrorRed);
        }

        void RebuildHistorySprites()
        {
            foreach (var sprite in _historySprites)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
            _historySprites.Clear();
            foreach (var record in GameManager.I.Run.PosterGenerations)
                AddHistorySprite(record);
        }

        void AddHistorySprite(PosterGenerationRecord record)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(record.ImageBase64);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!texture.LoadImage(bytes, false))
                {
                    Destroy(texture);
                    _historySprites.Add(null);
                    return;
                }
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = "GeneratedPoster" + (_historySprites.Count + 1);
                _historySprites.Add(sprite);
            }
            catch (Exception)
            {
                _historySprites.Add(null);
            }
        }

        void SelectHistory(int index)
        {
            int count = GameManager.I == null ? 0 : GameManager.I.Run.PosterGenerations.Count;
            if (index < 0 || index >= count || index >= _historySprites.Count || _historySprites[index] == null)
            {
                _selectedGeneration = -1;
                if (_poster != null) _poster.gameObject.SetActive(false);
                if (_posterPlaceholder != null) _posterPlaceholder.gameObject.SetActive(true);
                if (_currentLabel != null) _currentLabel.text = "NO DRAFT SELECTED";
                RefreshHistory();
                RefreshControls();
                return;
            }

            _selectedGeneration = index;
            GameManager.I.Run.SelectedPosterIndex = index;
            _poster.sprite = _historySprites[index];
            _poster.gameObject.SetActive(true);
            _posterPlaceholder.gameObject.SetActive(false);
            var record = GameManager.I.Run.PosterGenerations[index];
            _currentLabel.text = "DRAFT " + (index + 1) + " · " + record.Style;
            RefreshHistory();
            RefreshControls();
        }

        void RefreshHistory()
        {
            int count = GameManager.I == null ? 0 : GameManager.I.Run.PosterGenerations.Count;
            for (int i = 0; i < MaxGenerations; i++)
            {
                var button = _historyButtons[i] != null ? _historyButtons[i] : UIFactory.FindDeep<Button>(transform, "History" + i);
                var preview = _historyImages[i];
                if (button == null || preview == null) continue;
                bool filled = i < count && i < _historySprites.Count && _historySprites[i] != null;
                button.interactable = filled;
                preview.sprite = filled ? _historySprites[i] : Theme.Solid;
                preview.color = filled ? Color.white : new Color(.08f, .09f, .09f);
                var background = button.GetComponent<Image>();
                if (background != null) background.color = i == _selectedGeneration
                    ? Theme.CrtAmber
                    : new Color(.16f, .18f, .17f);
                var label = button.GetComponentInChildren<Text>();
                if (label != null) label.text = filled ? (i + 1).ToString() : "";
            }
            if (_counter != null) _counter.text = count + " / " + MaxGenerations + " POSTERS";
        }

        void RefreshControls()
        {
            int count = GameManager.I == null ? 0 : GameManager.I.Run.PosterGenerations.Count;
            if (_generate != null) _generate.interactable = !_requestInFlight && count < MaxGenerations;
            if (_usePoster != null) _usePoster.interactable = !_requestInFlight && _selectedGeneration >= 0;
            if (_prompt != null) _prompt.interactable = !_requestInFlight;
            foreach (var style in _styleButtons)
                if (style != null) style.interactable = !_requestInFlight;
            if (_counter != null) _counter.text = count + " / " + MaxGenerations + " POSTERS";
            if (_generate != null)
                UIFactory.SetButtonLabel(_generate, count >= MaxGenerations ? "7-DRAFT LIMIT REACHED" : "GENERATE POSTER");
        }

        void SetStatus(string copy, Color color)
        {
            if (_status == null) return;
            _status.text = copy;
            _status.color = color;
        }

        void UseSelectedPoster()
        {
            if (_requestInFlight || GameManager.I == null || _selectedGeneration < 0 ||
                _selectedGeneration >= GameManager.I.Run.PosterGenerations.Count) return;
            GameManager.I.Run.SelectedPosterIndex = _selectedGeneration;
            var record = GameManager.I.Run.PosterGenerations[_selectedGeneration];
            MadFactLokiLogger.Instance?.Log("poster_selected", "Player selected the final poster", new
            {
                level_id = 8,
                selected_generation = _selectedGeneration + 1,
                generation_count = GameManager.I.Run.PosterGenerations.Count,
                style = record.Style
            });
            MadFactBootstrap.I.OnPosterSelected();
        }

        static string DefaultPrompt()
        {
            return "Create a portrait movie poster for a family-friendly spooky comedy set in a 1990s VHS-store world. " +
                "Show a friendly ghost and a monster comedian inside a moonlit video store. Make it funny and mysterious, " +
                "never graphic or frightening. Include a bold fictional title and room for a short tagline.";
        }
    }
}
