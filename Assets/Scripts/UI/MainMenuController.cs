using MadFact.Telemetry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Runtime-authored front menu and pause overlay. GameMenu.unity is used both as the
    /// first scene and as an additive scene over a paused level, so the level remains
    /// exactly where the player left it.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _mainPanel;
        GameObject _settingsPanel;
        GameObject _profilePanel;
        GameObject _confirmPanel;
        GameObject _levelsPanel;
        Button _continueButton;
        TMP_InputField _displayName;
        TMP_InputField _participantId;
        Toggle _consent;
        TMP_Text _profileMessage;
        TMP_Text _profileSummary;
        TMP_Text _mainSubtitle;
        TMP_Text _muteLabel;
        bool _profileReturnToSettings;

        void Awake()
        {
            EnsureManagers();
            if (!BindAuthoredMenu())
            {
                DestroyMenuHierarchy(false);
                Build();
            }
            BindActions();
        }

        void Start()
        {
            RefreshMain();
            if (MadFactSessionManager.HasSavedProfile)
                ShowOnly(_mainPanel);
            else
                OpenProfile(false);
            MadFactLokiLogger.Instance?.Log("game_menu_opened", "Game menu opened",
                new { pause_overlay = MenuPauseCoordinator.IsPaused });
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (_mainPanel.activeSelf && MenuPauseCoordinator.IsPaused) ContinueGame();
            else ShowOnly(_mainPanel);
        }

        void DestroyMenuHierarchy(bool immediate)
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (immediate) DestroyImmediate(canvas.gameObject);
                else Destroy(canvas.gameObject);
            }
            foreach (EventSystem eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (immediate) DestroyImmediate(eventSystem.gameObject);
                else Destroy(eventSystem.gameObject);
            }
        }

        void EnsureManagers()
        {
            if (GameManager.I == null) new GameObject("GameManager").AddComponent<GameManager>();
            if (AudioTension.I == null) new GameObject("Audio").AddComponent<AudioTension>();
            if (MusicManager.I == null) new GameObject("Music").AddComponent<MusicManager>();
        }

        void Build()
        {
            var canvasObject = new GameObject("GameMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = null;

            Sprite backgroundSprite = Resources.Load<Sprite>("Backgrounds/GameMenuBackground");
            var background = UIFactory.Image(_canvas.transform, "VHS Store Background", Color.white,
                backgroundSprite, Image.Type.Simple, false);
            UIFactory.Fill(UIFactory.RT(background.gameObject));
            background.preserveAspect = true;

            var shade = UIFactory.Image(_canvas.transform, "Menu Shade", new Color(0, 0, 0, 0.34f), null,
                Image.Type.Simple, false);
            UIFactory.Fill(UIFactory.RT(shade.gameObject));

            _mainPanel = CreatePanel("Main", new Vector2(540, 520), new Vector2(0, 0));
            AddMainTitle(_mainPanel.transform);
            _continueButton = AddButton(_mainPanel.transform, "CONTINUE", -80, ContinueGame);
            AddButton(_mainPanel.transform, "PLAY", -126, Play);
            AddButton(_mainPanel.transform, "SETTINGS", -172, OpenSettings);
            AddButton(_mainPanel.transform, "QUIT", -218, Quit);

            _settingsPanel = CreatePanel("Settings", new Vector2(520, 410), Vector2.zero);
            AddTitle(_settingsPanel.transform, "SETTINGS", "PLAYER INFO & SOUND");
            _profileSummary = AddProfileSummary(_settingsPanel.transform);
            var mute = AddButton(_settingsPanel.transform, "", -86, ToggleMute, 310);
            mute.gameObject.name = "MuteVolume";
            _muteLabel = mute.GetComponentInChildren<TMP_Text>();
            AddButton(_settingsPanel.transform, "EDIT LOGGER INFO", -134, () => OpenProfile(true), 310);
            AddButton(_settingsPanel.transform, "BACK", -182, () => ShowOnly(_mainPanel), 180);

            _profilePanel = CreatePanel("LoggerInfo", new Vector2(620, 470), Vector2.zero);
            AddTitle(_profilePanel.transform, "LOGGER INFORMATION", "SHOWN ONCE BEFORE YOUR FIRST GAME");
            AddLabel(_profilePanel.transform, "DISPLAY NAME", 112);
            _displayName = AddInput(_profilePanel.transform, "DisplayName", 76, "How should the game address you?");
            AddLabel(_profilePanel.transform, "PARTICIPANT / STUDY ID (OPTIONAL)", 30);
            _participantId = AddInput(_profilePanel.transform, "ParticipantId", -6, "letters, numbers, dots, dashes, underscores");
            _consent = AddToggle(_profilePanel.transform, -58,
                "I consent to gameplay telemetry being recorded for this study.");
            var privacy = SharpText(_profilePanel.transform, "Privacy",
                "Recorded events include this info, game choices, timing, and progress. No email, location, or advertising IDs are collected.",
                13, Theme.FaceLight, TextAlignmentOptions.TopLeft, true);
            UIFactory.Place(UIFactory.RT(privacy.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(520, 42), new Vector2(0, -105));
            _profileMessage = SharpText(_profilePanel.transform, "Validation", "", 13,
                Theme.ErrorRed, TextAlignmentOptions.Center, true, FontStyles.Bold);
            UIFactory.Place(UIFactory.RT(_profileMessage.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(520, 28), new Vector2(0, -130));
            AddButton(_profilePanel.transform, "SAVE & CONTINUE", -170, SaveProfile, 250);
            AddButton(_profilePanel.transform, "CANCEL", -210, CloseProfile, 150);

            _confirmPanel = CreatePanel("Confirm", new Vector2(520, 270), Vector2.zero);
            AddTitle(_confirmPanel.transform, "START A NEW GAME?", "YOUR CURRENT PROGRESS WILL BE DISMISSED.");
            var warning = SharpText(_confirmPanel.transform, "Warning",
                "This cannot be undone. Continue will no longer return to the current level.",
                16, Theme.FaceLight, TextAlignmentOptions.Center, true);
            UIFactory.Place(UIFactory.RT(warning.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(430, 54), new Vector2(0, 12));
            AddButton(_confirmPanel.transform, "YES, CHOOSE A LEVEL", -68, ShowLevelSelect, 250);
            AddButton(_confirmPanel.transform, "CANCEL", -112, () => ShowOnly(_mainPanel), 160);

            BuildLevelPanel();
            RuntimeSkin.Apply(_canvas.transform);
            NormalizeFormControls();
            RefreshProfileSummary();
        }

        bool BindAuthoredMenu()
        {
            GameObject canvasObject = GameObject.Find("GameMenuCanvas");
            if (canvasObject == null || !canvasObject.TryGetComponent(out _canvas)) return false;
            _canvas.pixelPerfect = true;

            _mainPanel = FindPanel("MainPanel");
            _settingsPanel = FindPanel("SettingsPanel");
            _profilePanel = FindPanel("LoggerInfoPanel");
            _confirmPanel = FindPanel("ConfirmPanel");
            _levelsPanel = FindPanel("LevelSelectPanel");
            if (_mainPanel == null || _settingsPanel == null || _profilePanel == null ||
                _confirmPanel == null || _levelsPanel == null)
                return false;

            _continueButton = UIFactory.FindDeep<Button>(_mainPanel.transform, "CONTINUE");
            _displayName = UIFactory.FindDeep<TMP_InputField>(_profilePanel.transform, "DisplayName");
            _participantId = UIFactory.FindDeep<TMP_InputField>(_profilePanel.transform, "ParticipantId");
            _consent = UIFactory.FindDeep<Toggle>(_profilePanel.transform, "Consent");
            _profileMessage = UIFactory.FindDeep<TMP_Text>(_profilePanel.transform, "Validation");
            _profileSummary = UIFactory.FindDeep<TMP_Text>(_settingsPanel.transform, "LoggerSummary");
            _mainSubtitle = UIFactory.FindDeep<TMP_Text>(_mainPanel.transform, "Subtitle");
            Button mute = UIFactory.FindDeep<Button>(_settingsPanel.transform, "MuteVolume");
            _muteLabel = mute != null ? mute.GetComponentInChildren<TMP_Text>(true) : null;

            bool valid = _continueButton != null && _displayName != null && _participantId != null &&
                _consent != null && _profileMessage != null && _profileSummary != null &&
                _mainSubtitle != null && _muteLabel != null;
            if (valid)
            {
                RuntimeSkin.Apply(_canvas.transform);
                NormalizeFormControls();
            }
            return valid;
        }

        void NormalizeFormControls()
        {
            NormalizeInput(_displayName);
            NormalizeInput(_participantId);
            NormalizeToggle(_consent);
        }

        static void NormalizeInput(TMP_InputField field)
        {
            if (field == null) return;
            field.transition = Selectable.Transition.None;
            field.customCaretColor = true;
            field.caretColor = Theme.CrtGreen;
            field.selectionColor = new Color(Theme.CrtGreen.r, Theme.CrtGreen.g, Theme.CrtGreen.b, .35f);

            Image border = field.GetComponent<Image>();
            Transform backgroundTransform = field.transform.Find("Background");
            Image background = backgroundTransform != null ? backgroundTransform.GetComponent<Image>() : null;
            SetSimpleImage(border, Theme.CrtGreenDim);
            SetSimpleImage(background, new Color(.025f, .055f, .06f, 1f));
            field.targetGraphic = background;

            foreach (Image image in field.GetComponentsInChildren<Image>(true))
                if (image != border && image != background)
                    image.enabled = false;
        }

        static void NormalizeToggle(Toggle toggle)
        {
            if (toggle == null) return;
            toggle.transition = Selectable.Transition.None;

            Image box = toggle.transform.Find("Box")?.GetComponent<Image>();
            Image background = toggle.transform.Find("Box/Background")?.GetComponent<Image>();
            Image check = toggle.transform.Find("Box/Background/Checkmark")?.GetComponent<Image>();
            SetSimpleImage(box, Theme.CrtGreenDim);
            SetSimpleImage(background, new Color(.025f, .055f, .06f, 1f));
            SetSimpleImage(check, Theme.CrtGreen);
            toggle.targetGraphic = background;
            toggle.graphic = check;

            foreach (Image image in toggle.GetComponentsInChildren<Image>(true))
                if (image != box && image != background && image != check)
                    image.enabled = false;
        }

        static void SetSimpleImage(Image image, Color color)
        {
            if (image == null) return;
            image.enabled = true;
            image.sprite = Theme.Solid;
            image.type = Image.Type.Simple;
            image.color = color;
        }

        GameObject FindPanel(string name)
        {
            Transform panel = _canvas.transform.Find(name);
            return panel != null ? panel.gameObject : null;
        }

        void BindActions()
        {
            BindButton(_mainPanel, "CONTINUE", ContinueGame);
            BindButton(_mainPanel, "PLAY", Play);
            BindButton(_mainPanel, "SETTINGS", OpenSettings);
            BindButton(_mainPanel, "QUIT", Quit);
            BindButton(_settingsPanel, "MuteVolume", ToggleMute);
            BindButton(_settingsPanel, "EDITLOGGERINFO", () => OpenProfile(true));
            BindButton(_settingsPanel, "BACK", () => ShowOnly(_mainPanel));
            BindButton(_profilePanel, "SAVE&CONTINUE", SaveProfile);
            BindButton(_profilePanel, "CANCEL", CloseProfile);
            BindButton(_confirmPanel, "YES,CHOOSEALEVEL", ShowLevelSelect);
            BindButton(_confirmPanel, "CANCEL", () => ShowOnly(_mainPanel));
            BindButton(_levelsPanel, "BACK", () => ShowOnly(_mainPanel));

            for (int level = 1; level <= LevelSceneCatalog.ComingSoonLevel; level++)
            {
                int selectedLevel = level;
                BindButton(_levelsPanel, "Level" + level, () => StartLevel(selectedLevel));
            }
        }

        static void BindButton(GameObject panel, string name, UnityEngine.Events.UnityAction action)
        {
            if (panel == null) return;
            Button button = UIFactory.FindDeep<Button>(panel.transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

#if UNITY_EDITOR
        /// <summary>Rebuilds the complete editable menu hierarchy in GameMenu.unity.</summary>
        public void BuildAuthoredHierarchy()
        {
            DestroyMenuHierarchy(true);
            Build();
            BindActions();

            // Keep the main panel visible in Scene view so the game's title artwork and
            // primary navigation can be edited without entering Play Mode.
            ShowOnly(_mainPanel);
        }
#endif

        void BuildLevelPanel()
        {
            _levelsPanel = CreatePanel("LevelSelect", new Vector2(820, 430), Vector2.zero);
            AddTitle(_levelsPanel.transform, "CHOOSE A SHIFT", "SELECT A LEVEL TO START A NEW RUN");
            AddDayColumn("DAY 1", -260, new[] { 1, 2 });
            AddDayColumn("DAY 2", 0, new[] { 3, 4, 5 });
            AddDayColumn("DAY 3", 260, new[] { 6, 7, 8 });
            AddButton(_levelsPanel.transform, "BACK", -185, () => ShowOnly(_mainPanel), 160);
        }

        void AddDayColumn(string title, float x, int[] levels)
        {
            var column = UIFactory.Bevel(_levelsPanel.transform, title, new Color(.04f, .09f, .08f, .94f), true);
            UIFactory.Place(UIFactory.RT(column.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(225, 250), new Vector2(x, -4));
            var heading = SharpText(column.transform, "Heading", title, 20, Theme.CrtAmber,
                TextAlignmentOptions.Center, false, FontStyles.Bold);
            UIFactory.Place(UIFactory.RT(heading.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(190, 34), new Vector2(0, -28));
            for (int i = 0; i < levels.Length; i++)
            {
                int level = levels[i];
                var button = UIFactory.Button(column.transform, "Level" + level,
                    level <= LevelSceneCatalog.MaxPlayableLevel
                        ? "LEVEL " + level
                        : "LEVEL " + level + " · COMING SOON",
                    () => StartLevel(level),
                    level <= LevelSceneCatalog.MaxPlayableLevel ? Theme.Cash : Theme.FaceDark, 14,
                    Theme.SystemSans, Theme.TitleText);
                UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(185, 42), new Vector2(0, -78 - i * 56));
                button.interactable = level <= LevelSceneCatalog.MaxPlayableLevel;
            }
        }

        GameObject CreatePanel(string name, Vector2 size, Vector2 position)
        {
            var panel = UIFactory.DialogWindow(_canvas.transform, name + "Panel",
                new Color(.025f, .055f, .06f, .96f));
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                size, position);
            return panel.gameObject;
        }

        static void AddTitle(Transform parent, string title, string subtitle)
        {
            var heading = SharpText(parent, "Title", title, 29, Theme.CrtGreen,
                TextAlignmentOptions.Center, false, FontStyles.Bold);
            UIFactory.Place(UIFactory.RT(heading.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(540, 40), new Vector2(0, -54));
            var sub = SharpText(parent, "Subtitle", subtitle, 13, Theme.CrtAmber,
                TextAlignmentOptions.Center, false);
            UIFactory.Place(UIFactory.RT(sub.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(540, 24), new Vector2(0, -88));
        }

        void AddMainTitle(Transform parent)
        {
            Sprite titleSprite = Resources.Load<Sprite>("UI/MainMenuTitle");
            var title = UIFactory.Image(parent, "MainMenuTitle", Color.white, titleSprite,
                Image.Type.Simple, false);
            title.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(490, 276), new Vector2(0, -12));

            _mainSubtitle = SharpText(parent, "Subtitle", "BE KIND. REWIND. RECOMMEND.", 14,
                Theme.CrtAmber, TextAlignmentOptions.Center, false, FontStyles.Bold);
            UIFactory.Place(UIFactory.RT(_mainSubtitle.gameObject), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(480, 24), new Vector2(0, -43));
        }

        static Button AddButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action,
            float width = 240)
        {
            var button = UIFactory.Button(parent, label.Replace(" ", ""), label, action, Theme.Cash, 15,
                Theme.SystemSans, Theme.TitleText);
            var legacyLabel = button.GetComponentInChildren<Text>();
            if (legacyLabel != null) legacyLabel.gameObject.SetActive(false);
            var sharpLabel = SharpText(button.transform, "SharpLabel", label, 17, Theme.TitleText,
                TextAlignmentOptions.Center, false, FontStyles.Bold);
            UIFactory.Fill(UIFactory.RT(sharpLabel.gameObject), 8, 3, 8, 3);
            UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(width, 40), new Vector2(0, y));
            return button;
        }

        static void AddLabel(Transform parent, string value, float y)
        {
            var label = SharpText(parent, value, value, 14, Theme.CrtAmber,
                TextAlignmentOptions.Left, false, FontStyles.Bold);
            UIFactory.Place(UIFactory.RT(label.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(520, 22), new Vector2(0, y));
        }

        static TMP_InputField AddInput(Transform parent, string name, float y, string placeholder)
        {
            var border = UIFactory.Image(parent, name, Theme.CrtGreenDim);
            UIFactory.Place(UIFactory.RT(border.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(520, 38), new Vector2(0, y));

            var background = UIFactory.Image(border.transform, "Background",
                new Color(.025f, .055f, .06f, 1f));
            UIFactory.Fill(UIFactory.RT(background.gameObject), 2, 2, 2, 2);

            var viewport = UIFactory.Node(background.transform, "Text Area");
            UIFactory.Fill(UIFactory.RT(viewport), 10, 3, 10, 3);
            viewport.AddComponent<RectMask2D>();
            var text = SharpText(viewport.transform, "Text", "", 17, Color.white,
                TextAlignmentOptions.Left, false);
            UIFactory.Fill(UIFactory.RT(text.gameObject));
            var hint = SharpText(viewport.transform, "Placeholder", placeholder, 15,
                new Color(.72f, .76f, .72f, .72f), TextAlignmentOptions.Left, false,
                FontStyles.Italic);
            UIFactory.Fill(UIFactory.RT(hint.gameObject));

            var field = border.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = background;
            field.textViewport = UIFactory.RT(viewport);
            field.textComponent = text;
            field.placeholder = hint;
            field.customCaretColor = true;
            field.caretColor = Theme.CrtGreen;
            field.selectionColor = new Color(Theme.CrtGreen.r, Theme.CrtGreen.g, Theme.CrtGreen.b, .35f);
            return field;
        }

        static Toggle AddToggle(Transform parent, float y, string label)
        {
            var root = UIFactory.Node(parent, "Consent");
            UIFactory.Place(UIFactory.RT(root), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(520, 36), new Vector2(0, y));
            var toggle = root.AddComponent<Toggle>();
            var box = UIFactory.Image(root.transform, "Box", Theme.CrtGreenDim);
            UIFactory.Place(UIFactory.RT(box.gameObject), new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(28, 28), Vector2.zero);
            var boxFill = UIFactory.Image(box.transform, "Background", new Color(.025f, .055f, .06f, 1f));
            UIFactory.Fill(UIFactory.RT(boxFill.gameObject), 2, 2, 2, 2);
            var check = UIFactory.Image(boxFill.transform, "Checkmark", Theme.CrtGreen);
            UIFactory.Fill(UIFactory.RT(check.gameObject), 6, 6, 6, 6);
            toggle.targetGraphic = boxFill;
            toggle.graphic = check;
            var text = SharpText(root.transform, "Label", label, 14, Theme.FaceLight,
                TextAlignmentOptions.Left, true);
            UIFactory.Place(UIFactory.RT(text.gameObject), new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(475, 36), new Vector2(42, 0));
            return toggle;
        }

        static TextMeshProUGUI SharpText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment, bool wrap, FontStyles style = FontStyles.Normal)
        {
            var go = UIFactory.Node(parent, name);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        TMP_Text AddProfileSummary(Transform parent)
        {
            var session = SharpText(parent, "LoggerSummary", "", 16, Theme.FaceLight,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(UIFactory.RT(session.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(430, 112), new Vector2(0, 42));
            return session;
        }

        void RefreshMain()
        {
            _continueButton.interactable = MenuPauseCoordinator.IsPaused &&
                GameManager.I != null && GameManager.I.CanContinue;
            if (_muteLabel != null)
                _muteLabel.text = AudioListener.pause ? "UNMUTE VOLUME" : "MUTE VOLUME";
            RefreshProfileSummary();
        }

        void RefreshProfileSummary()
        {
            MadFactSessionManager.LoadSavedProfile(out string displayName, out string participantId,
                out bool consent);
            bool saved = MadFactSessionManager.HasSavedProfile;
            if (_profileSummary != null)
            {
                _profileSummary.text = saved
                    ? "DISPLAY NAME:  " + displayName + "\n" +
                      "PARTICIPANT ID:  " + (string.IsNullOrEmpty(participantId) ? "anonymous" : participantId) + "\n" +
                      "GAMEPLAY LOGGING:  " + (consent ? "enabled" : "disabled")
                    : "No player information has been saved yet.\n" +
                      "Press EDIT LOGGER INFO to add your name and study details.";
            }
            if (_mainSubtitle != null)
                _mainSubtitle.text = saved && !string.IsNullOrEmpty(displayName)
                    ? "WELCOME, " + displayName.ToUpperInvariant()
                    : "BE KIND. REWIND. RECOMMEND.";
        }

        void Play()
        {
            if (!MadFactSessionManager.HasSavedProfile)
            {
                OpenProfile(false);
                return;
            }
            if (!MadFactSessionManager.Instance.TryStartSavedSession())
            {
                OpenProfile(false);
                return;
            }
            if (GameManager.I.HasActiveRun) ShowOnly(_confirmPanel);
            else ShowLevelSelect();
        }

        void OpenSettings()
        {
            if (!MadFactSessionManager.HasSavedProfile)
            {
                OpenProfile(false);
                return;
            }
            RefreshMain();
            ShowOnly(_settingsPanel);
        }

        void OpenProfile(bool returnToSettings)
        {
            _profileReturnToSettings = returnToSettings;
            MadFactSessionManager.LoadSavedProfile(out string displayName, out string participantId,
                out bool consent);
            _displayName.text = displayName;
            _participantId.text = participantId;
            _consent.isOn = consent;
            _profileMessage.text = "";
            ShowOnly(_profilePanel);
            _displayName.Select();
            MadFactLokiLogger.Instance?.Log(
                "logger_info_opened",
                "Logger information screen opened",
                new { first_time = !MadFactSessionManager.HasSavedProfile });
        }

        void SaveProfile()
        {
            string displayName = (_displayName.text ?? "").Trim();
            string participantId = (_participantId.text ?? "").Trim();
            string error = MadFactSessionManager.Validate(displayName, participantId, _consent.isOn);
            if (error != null)
            {
                _profileMessage.text = error +
                    (_consent.isOn ? "" : " Check the consent box before saving.");
                MadFactLokiLogger.Instance?.Log(
                    "participant_profile_validation_failed",
                    "Logger information validation failed",
                    new { reason = ProfileValidationReason(displayName, participantId, _consent.isOn) });
                return;
            }

            EventSystem.current?.SetSelectedGameObject(null);
            MadFactSessionManager.SaveProfile(displayName, participantId, _consent.isOn);
            MadFactSessionManager.Instance.ApplySavedProfileToSession();
            if (!MadFactSessionManager.Instance.HasActiveSession)
                MadFactSessionManager.Instance.TryStartSavedSession();
            RefreshProfileSummary();
            MadFactLokiLogger.Instance?.Log("participant_profile_saved",
                "Player information saved from the game menu",
                new { participant_id_supplied = participantId.Length > 0 });
            CloseProfile();
        }

        void CloseProfile() => ShowOnly(_profileReturnToSettings ? _settingsPanel : _mainPanel);

        static string ProfileValidationReason(string displayName, string participantId, bool consent)
        {
            if (displayName.Length == 0) return "display_name_missing";
            if (displayName.Length > 50) return "display_name_too_long";
            if (!MadFactSessionManager.IsParticipantIdValid(participantId)) return "participant_id_invalid";
            if (!consent) return "consent_missing";
            return "unknown";
        }

        void ShowLevelSelect() => ShowOnly(_levelsPanel);

        void StartLevel(int level)
        {
            if (level < 1 || level > LevelSceneCatalog.MaxPlayableLevel) return;
            Time.timeScale = 1f;
            GameManager.I.StartNewAtLevel(level);
            MadFactLokiLogger.Instance?.Log("game_started", "Game started from level select",
                new { start_mode = "day_level_select", level_id = level });
            SceneManager.LoadScene(LevelSceneCatalog.PathForLevel(level), LoadSceneMode.Single);
        }

        void ContinueGame()
        {
            if (!_continueButton.interactable) return;
            MenuPauseCoordinator.Resume();
        }

        void ToggleMute()
        {
            AudioListener.pause = !AudioListener.pause;
            PlayerPrefs.SetInt("madfact.audio_muted", AudioListener.pause ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMain();
        }

        void ShowOnly(GameObject shown)
        {
            _mainPanel.SetActive(shown == _mainPanel);
            _settingsPanel.SetActive(shown == _settingsPanel);
            _profilePanel.SetActive(shown == _profilePanel);
            _confirmPanel.SetActive(shown == _confirmPanel);
            _levelsPanel.SetActive(shown == _levelsPanel);
            RefreshMain();
        }

        void Quit()
        {
            Time.timeScale = 1f;
            MadFactSessionManager.Instance?.EndSession("game_menu_quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
