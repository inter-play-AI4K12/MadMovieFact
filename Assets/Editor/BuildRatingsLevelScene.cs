using MadFact;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the editor-visible Level 3 ratings lesson and repairs the level numbers on
/// the moved content-based and market-gap scenes.
/// </summary>
public static class BuildRatingsLevelScene
{
    const string RatingsScene = "Assets/Scenes/Level03_RatingsTable.unity";
    const string ContentScene = "Assets/Scenes/Level06_ContentBasedRecommendation.unity";
    const string MarketScene = "Assets/Scenes/Level07_MarketGapResearch.unity";
    const string PosterScene = "Assets/Scenes/Level08_PosterGeneration.unity";

    [MenuItem("MadFact/Rebuild Ratings Level 3 Scene")]
    public static void Rebuild()
    {
        RefreshAuthoredLevelPrefabs.RefreshLevel3Ratings();
        BuildRatingsScene();
        SetDedicatedLevel(ContentScene, 6);
        SetDedicatedLevel(MarketScene, 7);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(RatingsScene, OpenSceneMode.Single);
        var ratings = Object.FindAnyObjectByType<Level3RatingsTable>();
        Selection.activeGameObject = ratings != null ? ratings.gameObject : scene.GetRootGameObjects()[0];
        SceneView.lastActiveSceneView?.FrameSelected();
        SceneView.RepaintAll();
        Debug.Log("Rebuilt editor-visible Level 3 ratings table and renumbered Levels 6 and 7.");
    }

    [MenuItem("MadFact/Rebuild Poster Generation Level 8 Scene")]
    public static void RebuildPosterGeneration()
    {
        RefreshAuthoredLevelPrefabs.RefreshPosterPipeline();
        BuildPosterScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(PosterScene, OpenSceneMode.Single);
        var posterStudio = Object.FindAnyObjectByType<Level8PosterStudio>();
        Selection.activeGameObject = posterStudio != null
            ? posterStudio.gameObject
            : scene.GetRootGameObjects()[0];
        SceneView.lastActiveSceneView?.FrameSelected();
        SceneView.RepaintAll();
        Debug.Log("Built the editor-visible Level 8 poster generation scene and enabled it in Build Settings.");
    }

    static void BuildRatingsScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Theme.Lino;

        var lightObject = new GameObject("Directional Light", typeof(Light));
        lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        lightObject.GetComponent<Light>().type = LightType.Directional;

        var canvasObject = new GameObject("MadFactCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        scaler.referencePixelsPerUnit = 100f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        StorefrontView storefront = InstantiateComponent<StorefrontView>(
            "Assets/Prefabs/Environment/Storefront.prefab", canvas.transform);
        Level3RatingsTable ratings = InstantiateComponent<Level3RatingsTable>(
            "Assets/Prefabs/Levels/Level3RatingsTable.prefab", canvas.transform);
        Hud hud = InstantiateComponent<Hud>("Assets/Prefabs/UI/HUD.prefab", canvas.transform);
        CommsBox comms = InstantiateComponent<CommsBox>("Assets/Prefabs/UI/DialogueBox.prefab", canvas.transform);

        UIFactory.Fill((RectTransform)storefront.transform, 0, 46, 0, 0);
        UIFactory.Fill((RectTransform)ratings.transform);

        var hudRect = (RectTransform)hud.transform;
        hudRect.anchorMin = new Vector2(0, 1);
        hudRect.anchorMax = new Vector2(1, 1);
        hudRect.pivot = new Vector2(.5f, 1);
        hudRect.sizeDelta = new Vector2(0, 46);
        hudRect.anchoredPosition = Vector2.zero;

        var commsRect = (RectTransform)comms.transform;
        commsRect.anchorMin = commsRect.anchorMax = new Vector2(.5f, 0);
        commsRect.pivot = new Vector2(.5f, 0);
        commsRect.sizeDelta = new Vector2(820, 188);
        commsRect.anchoredPosition = new Vector2(0, 16);
        comms.gameObject.SetActive(false);

        storefront.transform.SetAsFirstSibling();
        ratings.transform.SetSiblingIndex(1);
        hud.transform.SetAsLastSibling();
        comms.transform.SetAsLastSibling();

        var bootstrapObject = new GameObject("MadFactBootstrap");
        var bootstrap = bootstrapObject.AddComponent<MadFactBootstrap>();
        bootstrap.Storefront = storefront;
        bootstrap.Hud = hud;
        bootstrap.Comms = comms;
        bootstrap.L3Ratings = ratings;
        bootstrap.StartPhaseOverride = 3;
        var serializedBootstrap = new SerializedObject(bootstrap);
        serializedBootstrap.FindProperty("_canvas").objectReferenceValue = canvas;
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, RatingsScene))
            throw new System.InvalidOperationException("Could not save " + RatingsScene);
    }

    static void BuildPosterScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Theme.Lino;

        var lightObject = new GameObject("Directional Light", typeof(Light));
        lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        lightObject.GetComponent<Light>().type = LightType.Directional;

        var canvasObject = new GameObject("MadFactCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        scaler.referencePixelsPerUnit = 100f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        StorefrontView storefront = InstantiateComponent<StorefrontView>(
            "Assets/Prefabs/Environment/Storefront.prefab", canvas.transform);
        Level8PosterStudio posterStudio = InstantiateComponent<Level8PosterStudio>(
            "Assets/Prefabs/Levels/Level8PosterStudio.prefab", canvas.transform);
        Hud hud = InstantiateComponent<Hud>("Assets/Prefabs/UI/HUD.prefab", canvas.transform);
        CommsBox comms = InstantiateComponent<CommsBox>("Assets/Prefabs/UI/DialogueBox.prefab", canvas.transform);

        UIFactory.Fill((RectTransform)storefront.transform, 0, 46, 0, 0);
        UIFactory.Fill((RectTransform)posterStudio.transform);

        var hudRect = (RectTransform)hud.transform;
        hudRect.anchorMin = new Vector2(0, 1);
        hudRect.anchorMax = new Vector2(1, 1);
        hudRect.pivot = new Vector2(.5f, 1);
        hudRect.sizeDelta = new Vector2(0, 46);
        hudRect.anchoredPosition = Vector2.zero;

        var commsRect = (RectTransform)comms.transform;
        commsRect.anchorMin = commsRect.anchorMax = new Vector2(.5f, 0);
        commsRect.pivot = new Vector2(.5f, 0);
        commsRect.sizeDelta = new Vector2(820, 188);
        commsRect.anchoredPosition = new Vector2(0, 16);
        comms.gameObject.SetActive(false);

        storefront.transform.SetAsFirstSibling();
        posterStudio.transform.SetSiblingIndex(1);
        hud.transform.SetAsLastSibling();
        comms.transform.SetAsLastSibling();

        var bootstrapObject = new GameObject("MadFactBootstrap");
        var bootstrap = bootstrapObject.AddComponent<MadFactBootstrap>();
        bootstrap.Storefront = storefront;
        bootstrap.Hud = hud;
        bootstrap.Comms = comms;
        bootstrap.L8 = posterStudio;
        bootstrap.StartPhaseOverride = 8;
        var serializedBootstrap = new SerializedObject(bootstrap);
        serializedBootstrap.FindProperty("_canvas").objectReferenceValue = canvas;
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, PosterScene))
            throw new System.InvalidOperationException("Could not save " + PosterScene);
    }

    static T InstantiateComponent<T>(string prefabPath, Transform parent) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new System.InvalidOperationException("Missing prefab: " + prefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        T component = instance.GetComponent<T>();
        if (component == null)
            throw new System.InvalidOperationException($"Prefab {prefabPath} has no {typeof(T).Name} component.");
        return component;
    }

    static void SetDedicatedLevel(string scenePath, int level)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            throw new System.InvalidOperationException("Missing moved scene: " + scenePath);
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var bootstrap = Object.FindAnyObjectByType<MadFactBootstrap>();
        if (bootstrap == null)
            throw new System.InvalidOperationException("Scene has no MadFactBootstrap: " + scenePath);
        bootstrap.StartPhaseOverride = level;
        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(LevelSceneCatalog.MainMenu, false),
            new EditorBuildSettingsScene(LevelSceneCatalog.FullGame, false),
            new EditorBuildSettingsScene(LevelSceneCatalog.GameMenu, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.Storefront, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.ManualRecommendation, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.RuleBasedRecommendation, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.RatingsTable, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.CollaborativeFiltering, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.MatrixFactorization, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.ContentBasedRecommendation, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.MarketGapResearch, true),
            new EditorBuildSettingsScene(LevelSceneCatalog.PosterGeneration, true)
        };
    }
}
