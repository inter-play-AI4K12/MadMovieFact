using System.Collections.Generic;
using MadFact.Telemetry;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MadFact.Editor
{
    public static class BuildParticipantSetupScene
    {
        const string ScenePath = "Assets/Scenes/ParticipantSetup.unity";

        [MenuItem("MadFact/Build Participant Setup Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ParticipantSetup";

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(15, 22, 25, 255);
            camera.orthographic = true;

            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.GetComponent<Light>().type = LightType.Directional;

            var canvasObject = new GameObject("ParticipantSetupCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var background = Image(canvas.transform, "Background", new Color32(15, 22, 25, 255));
            Stretch(background.rectTransform);
            var accent = Image(background.transform, "TopAccent", new Color32(224, 194, 102, 255));
            Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, 5f), Vector2.zero);

            var card = Image(background.transform, "SetupCard", new Color32(244, 237, 213, 255));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(690f, 490f), Vector2.zero);

            Text(card.transform, "Title", "WELCOME TO MADFACT", 28f,
                new Color32(28, 35, 36, 255), FontStyles.Bold,
                new Vector2(620f, 42f), new Vector2(0f, 205f), TextAlignmentOptions.Center);
            Text(card.transform, "Explanation",
                "Before the store opens, choose a display name. With your consent, MadFact records gameplay decisions so researchers can understand how people learn recommendation systems.",
                15f, new Color32(55, 61, 60, 255), FontStyles.Normal,
                new Vector2(610f, 58f), new Vector2(0f, 153f), TextAlignmentOptions.TopLeft);

            Text(card.transform, "DisplayNameLabel", "DISPLAY NAME  (required)", 13f,
                new Color32(28, 35, 36, 255), FontStyles.Bold,
                new Vector2(610f, 22f), new Vector2(0f, 98f), TextAlignmentOptions.Left);
            TMP_InputField displayName = Input(card.transform, "DisplayName",
                "How should the game address you?", new Vector2(610f, 40f), new Vector2(0f, 66f));
            displayName.characterLimit = 50;

            Text(card.transform, "ParticipantIdLabel", "PARTICIPANT / STUDY ID  (optional)", 13f,
                new Color32(28, 35, 36, 255), FontStyles.Bold,
                new Vector2(610f, 22f), new Vector2(0f, 25f), TextAlignmentOptions.Left);
            TMP_InputField participantId = Input(card.transform, "ParticipantId",
                "Letters, numbers, dots, dashes, and underscores", new Vector2(610f, 40f),
                new Vector2(0f, -7f));
            participantId.characterLimit = 64;

            Toggle consent = Toggle(card.transform, "LoggingConsent",
                "I consent to gameplay telemetry being recorded for this study.",
                new Vector2(610f, 36f), new Vector2(0f, -58f));
            consent.isOn = false;

            Text(card.transform, "PrivacyNote",
                "Privacy: events include your display name, study/anonymous ID, game choices, timing, and progress. MadFact does not collect email, location, advertising IDs, or other unnecessary personal information.",
                12f, new Color32(72, 75, 70, 255), FontStyles.Italic,
                new Vector2(610f, 54f), new Vector2(0f, -106f), TextAlignmentOptions.TopLeft);

            TMP_Text validation = Text(card.transform, "ValidationMessage", string.Empty, 13f,
                new Color32(188, 57, 66, 255), FontStyles.Bold,
                new Vector2(610f, 24f), new Vector2(0f, -150f), TextAlignmentOptions.Left);

            Button quit = Button(card.transform, "QuitButton", "QUIT",
                new Color32(71, 75, 70, 255), new Vector2(150f, 42f), new Vector2(-230f, -199f));
            Button continueButton = Button(card.transform, "ContinueButton", "CONTINUE",
                new Color32(52, 126, 83, 255), new Vector2(220f, 42f), new Vector2(195f, -199f));

            var controllerObject = new GameObject("ParticipantSetupController",
                typeof(ParticipantSetupController));
            var serialized = new SerializedObject(controllerObject.GetComponent<ParticipantSetupController>());
            serialized.FindProperty("_displayName").objectReferenceValue = displayName;
            serialized.FindProperty("_participantId").objectReferenceValue = participantId;
            serialized.FindProperty("_consent").objectReferenceValue = consent;
            serialized.FindProperty("_validationMessage").objectReferenceValue = validation;
            serialized.FindProperty("_continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("_quitButton").objectReferenceValue = quit;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = displayName.gameObject;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PutFirstInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Built authored participant setup scene at " + ScenePath);
        }

        static void PutFirstInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != ScenePath) scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static TMP_InputField Input(Transform parent, string name, string placeholder,
            Vector2 size, Vector2 position)
        {
            var background = Image(parent, name, new Color32(255, 252, 239, 255));
            Place(background.rectTransform, Center, Center, Center, size, position);
            var field = background.gameObject.AddComponent<TMP_InputField>();

            var viewport = Rect(parent: background.transform, name: "Text Area");
            Stretch(viewport, 12f, 12f, 6f, 6f);
            var text = Text(viewport, "Text", string.Empty, 15f, new Color32(28, 35, 36, 255),
                FontStyles.Normal, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, stretch: true);
            var hint = Text(viewport, "Placeholder", placeholder, 14f, new Color32(115, 116, 105, 180),
                FontStyles.Italic, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, stretch: true);
            field.textViewport = viewport.GetComponent<RectTransform>();
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return field;
        }

        static Toggle Toggle(Transform parent, string name, string label, Vector2 size, Vector2 position)
        {
            RectTransform root = Rect(parent, name);
            Place(root, Center, Center, Center, size, position);
            var toggle = root.gameObject.AddComponent<Toggle>();
            var box = Image(root, "Background", new Color32(255, 252, 239, 255));
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Center, new Vector2(30f, 30f), new Vector2(0f, 0f));
            var check = Image(box.transform, "Checkmark", new Color32(52, 126, 83, 255));
            Stretch(check.rectTransform, 6f, 6f, 6f, 6f);
            Text(root, "Label", label, 14f, new Color32(28, 35, 36, 255), FontStyles.Normal,
                new Vector2(size.x - 44f, size.y), new Vector2(22f, 0f), TextAlignmentOptions.Left);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        static Button Button(Transform parent, string name, string label, Color color,
            Vector2 size, Vector2 position)
        {
            var image = Image(parent, name, color);
            Place(image.rectTransform, Center, Center, Center, size, position);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            button.colors = colors;
            Text(image.transform, "Label", label, 15f, Color.white, FontStyles.Bold,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
            return button;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string value, float size,
            Color color, FontStyles style, Vector2 rectSize, Vector2 position,
            TextAlignmentOptions alignment, bool stretch = false)
        {
            RectTransform rect = Rect(parent, name);
            if (stretch) Stretch(rect);
            else Place(rect, Center, Center, Center, rectSize, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static Image Image(Transform parent, string name, Color color)
        {
            RectTransform rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
            float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
