using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string MutedKey = "audio_muted";
    private Text muteLabel;

    private void Awake()
    {
        ApplyMute(PlayerPrefs.GetInt(MutedKey, 0) == 1);
        BuildInterface();
    }

    public void StartGame()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next = current + 1;

        if (next >= 0 && next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.LogWarning("Main Menu: Add a gameplay scene after MainMenu in Build Settings.");
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ToggleMute()
    {
        ApplyMute(!AudioListener.pause);
        PlayerPrefs.SetInt(MutedKey, AudioListener.pause ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyMute(bool muted)
    {
        AudioListener.pause = muted;
        AudioListener.volume = muted ? 0f : 1f;
        if (muteLabel != null)
            muteLabel.text = muted ? "UNMUTE" : "MUTE";
    }

    private void BuildInterface()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform);
        }

        var canvasObject = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateImage(canvasObject.transform, "Background", new Color(0.025f, 0.035f, 0.075f, 1f), Vector2.zero, Vector2.one);
        CreateImage(canvasObject.transform, "Top Glow", new Color(0.1f, 0.3f, 0.55f, 0.28f), new Vector2(0f, 0.68f), Vector2.one);

        var panel = CreateImage(canvasObject.transform, "Menu Panel", new Color(0.045f, 0.065f, 0.12f, 0.96f),
            new Vector2(0.31f, 0.16f), new Vector2(0.69f, 0.84f));

        CreateText(panel.transform, "Title", "MAD MOVIE FACT", 66, FontStyle.Bold,
            new Color(0.93f, 0.96f, 1f), new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.94f));
        CreateText(panel.transform, "Subtitle", "LIGHTS. CAMERA. TRIVIA.", 22, FontStyle.Normal,
            new Color(0.35f, 0.75f, 1f), new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.72f));

        CreateButton(panel.transform, "Start Game", "START GAME", new Vector2(0.16f, 0.42f), new Vector2(0.84f, 0.56f), StartGame);
        var muteButton = CreateButton(panel.transform, "Mute", "MUTE", new Vector2(0.16f, 0.25f), new Vector2(0.84f, 0.39f), ToggleMute);
        muteLabel = muteButton.GetComponentInChildren<Text>();
        ApplyMute(AudioListener.pause);
        CreateButton(panel.transform, "Exit", "EXIT", new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.22f), ExitGame);

        CreateText(canvasObject.transform, "Footer", "A MADFACT GAME", 18, FontStyle.Normal,
            new Color(0.55f, 0.62f, 0.72f), new Vector2(0.35f, 0.045f), new Vector2(0.65f, 0.1f));
    }

    private static GameObject CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
        Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = size;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin,
        Vector2 anchorMax, UnityEngine.Events.UnityAction action)
    {
        var go = CreateImage(parent, name, new Color(0.08f, 0.17f, 0.28f, 1f), anchorMin, anchorMax);
        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.55f, 0.85f, 1f);
        colors.pressedColor = new Color(0.25f, 0.65f, 0.95f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        CreateText(go.transform, "Label", label, 30, FontStyle.Bold, Color.white, Vector2.zero, Vector2.one);
        return button;
    }
}
