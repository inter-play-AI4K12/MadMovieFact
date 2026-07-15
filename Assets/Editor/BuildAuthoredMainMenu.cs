using MadFact;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time/editor-only authoring utility for MainMenu.unity. Runtime code binds the
/// resulting objects but never creates or replaces the hierarchy.
/// </summary>
public static class BuildAuthoredMainMenu
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("MadFact/Rebuild Authored Main Menu")]
    public static void Rebuild()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<MainMenuController>();
        if (controller == null)
        {
            var controllerObject = new GameObject("MainMenuController");
            controller = controllerObject.AddComponent<MainMenuController>();
        }

        DestroyExisting("MainMenuCanvas");
        DestroyExisting("EventSystem");

        var canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        eventSystem.AddComponent<InputSystemUIInputModule>();

        var background = UIFactory.Image(canvas.transform, "Background", Theme.CrtBg);
        UIFactory.Fill(UIFactory.RT(background.gameObject));

        var storefront = UIFactory.Image(background.transform, "StorefrontGhost", new Color(1, 1, 1, 0.18f), ArtSprites.StorefrontBackground(), Image.Type.Simple, false);
        storefront.preserveAspect = true;
        UIFactory.Fill(UIFactory.RT(storefront.gameObject));

        var shade = UIFactory.Image(background.transform, "Shade", new Color(0, 0, 0, 0.58f));
        UIFactory.Fill(UIFactory.RT(shade.gameObject));

        var panel = UIFactory.DialogWindow(background.transform, "MainMenuPanel", new Color(0.06f, 0.09f, 0.10f, 0.96f));
        UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620, 500), Vector2.zero);

        var title = UIFactory.Text(panel.transform, "Title", "PELLINGS VIDEO", 30, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 46), new Vector2(0, -42));

        var subtitle = UIFactory.Text(panel.transform, "Subtitle", "recommendation systems, one cursed VHS shop at a time", 13, Theme.CrtAmber, Theme.Typewriter, TextAnchor.MiddleCenter, false);
        UIFactory.Place(UIFactory.RT(subtitle.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 24), new Vector2(0, -86));

        var start = AddButton(panel.transform, 0, "START FULL GAME", ArtSprites.Play());
        var hub = AddButton(panel.transform, 1, "STORE FRONT HUB", ArtSprites.StoreLogo());
        var level1 = AddButton(panel.transform, 2, "LEVEL 1 · MANUAL", ArtSprites.CustomerPortrait("WENDELL"));
        var level2 = AddButton(panel.transform, 3, "LEVEL 2 · RULES", ArtSprites.Robot());
        var level3 = AddButton(panel.transform, 4, "LEVEL 3 · CONTENT", ArtSprites.MovieCover(0));
        var level4 = AddButton(panel.transform, 5, "LEVEL 4 · COLLABORATIVE", ArtSprites.Optimize());
        var level5 = AddButton(panel.transform, 6, "LEVEL 5 · MARKET GAP", ArtSprites.Goal());

        var quit = UIFactory.Button(panel.transform, "Quit", "QUIT", null, Theme.Face, 14, Theme.SystemSans, Theme.TitleText);
        UIFactory.Place(UIFactory.RT(quit.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(160, 34), new Vector2(0, 20));
        UIFactory.ButtonIcon(quit, ArtSprites.Stop(), 22f);

        foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            text.font = Theme.Fallback;
        RuntimeSkin.Apply(canvas.transform);

        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("_canvas").objectReferenceValue = canvas;
        serializedController.FindProperty("_startFullGame").objectReferenceValue = start;
        serializedController.FindProperty("_storefront").objectReferenceValue = hub;
        serializedController.FindProperty("_level1").objectReferenceValue = level1;
        serializedController.FindProperty("_level2").objectReferenceValue = level2;
        serializedController.FindProperty("_level3").objectReferenceValue = level3;
        serializedController.FindProperty("_level4").objectReferenceValue = level4;
        serializedController.FindProperty("_level5").objectReferenceValue = level5;
        serializedController.FindProperty("_quit").objectReferenceValue = quit;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasObject;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        Debug.Log("Rebuilt MainMenu.unity as a fully authored, editable UI hierarchy.");
    }

    static Button AddButton(Transform parent, int row, string label, Sprite icon)
    {
        var button = UIFactory.Button(parent, "Menu_" + row, label, null, Theme.Cash, 14, Theme.SystemSans, Theme.TitleText);
        UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(410, 34), new Vector2(0, -126 - row * 40));
        UIFactory.ButtonIcon(button, icon, 24f);
        return button;
    }

    static void DestroyExisting(string objectName)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null) Object.DestroyImmediate(existing);
    }
}
