using MadFact;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the scene-authored level prefabs synchronized with their canonical UI builders.
/// The runtime uses these prefabs directly, so visual improvements must be baked here
/// instead of rebuilding and replacing the level hierarchy every time Play is pressed.
/// </summary>
public static class RefreshAuthoredLevelPrefabs
{
    static readonly string[] DedicatedScenePaths =
    {
        "Assets/Scenes/Storefront.unity",
        "Assets/Scenes/Level01_ManualRecommendation.unity",
        "Assets/Scenes/Level02_RuleBasedRecommendation.unity",
        "Assets/Scenes/Level03_ContentBasedRecommendation.unity",
        "Assets/Scenes/Level04_CollaborativeFiltering.unity",
        "Assets/Scenes/Level05_MarketGapResearch.unity"
    };

    static readonly string[] SharedUiScenePaths =
    {
        "Assets/Scenes/MadMovieFact.unity",
        "Assets/Scenes/Storefront.unity",
        "Assets/Scenes/Level01_ManualRecommendation.unity",
        "Assets/Scenes/Level02_RuleBasedRecommendation.unity",
        "Assets/Scenes/Level03_ContentBasedRecommendation.unity",
        "Assets/Scenes/Level04_CollaborativeFiltering.unity",
        "Assets/Scenes/Level05_MarketGapResearch.unity"
    };

    static readonly string[] AuthoredUiPrefabPaths =
    {
        "Assets/Prefabs/Environment/Storefront.prefab",
        "Assets/Prefabs/UI/HUD.prefab",
        "Assets/Prefabs/UI/DialogueBox.prefab",
        "Assets/Prefabs/Levels/Level1Counter.prefab",
        "Assets/Prefabs/Levels/Level2Robot.prefab",
        "Assets/Prefabs/Levels/Level3ContentBased.prefab",
        "Assets/Prefabs/Levels/Level3Mainframe.prefab",
        "Assets/Prefabs/Levels/Level4Corkboard.prefab"
    };

    static RefreshAuthoredLevelPrefabs()
    {
        EditorApplication.delayCall += RefreshEditorPreview;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += RefreshEditorPreview;
    }

    /// <summary>
    /// Makes scene-authored UI readable outside Play Mode. The final game uses dynamic
    /// OS fonts that Unity cannot serialize into prefabs, so Edit Mode receives the
    /// built-in font as a non-dirty preview. RuntimeSkin replaces it when gameplay starts.
    /// </summary>
    [MenuItem("MadFact/Refresh Editor UI Preview")]
    public static void RefreshEditorPreview()
    {
        if (Application.isPlaying) return;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            RuntimeSkin.Apply(canvas.transform);

        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    [MenuItem("MadFact/Refresh Authored Level Prefabs")]
    public static void Refresh()
    {
        var stagingRoot = new GameObject("__PrefabStagingRoot", typeof(RectTransform));
        stagingRoot.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            Save(StorefrontView.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/Environment/Storefront.prefab");
            Save(Hud.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/UI/HUD.prefab");
            Save(CommsBox.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/UI/DialogueBox.prefab");
            Save(Level1Counter.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/Levels/Level1Counter.prefab");
            Save(Level2Robot.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/Levels/Level2Robot.prefab");
            Save(Level3ContentBased.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/Levels/Level3ContentBased.prefab");
            Save(Level3Mainframe.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/Levels/Level3Mainframe.prefab");
            Save(Level4Corkboard.Create(stagingRoot.transform).gameObject,
                "Assets/Prefabs/Levels/Level4Corkboard.prefab");

            BakeSerializablePreviewFonts();
            AssetDatabase.SaveAssets();
            Debug.Log("Refreshed all authored MadFact UI prefabs from their current builders.");
        }
        finally
        {
            Object.DestroyImmediate(stagingRoot);
        }
    }

    /// <summary>
    /// Batch-friendly safeguard for the layouts that previously drifted behind staging.
    /// Throws when a prefab is stale so CI or a local batch run fails loudly.
    /// </summary>
    [MenuItem("MadFact/Validate Authored Level Prefabs")]
    public static void Validate()
    {
        var level1 = Load("Assets/Prefabs/Levels/Level1Counter.prefab");
        var level2 = Load("Assets/Prefabs/Levels/Level2Robot.prefab");
        var level3Content = Load("Assets/Prefabs/Levels/Level3ContentBased.prefab");
        var level3Mainframe = Load("Assets/Prefabs/Levels/Level3Mainframe.prefab");
        var level4 = Load("Assets/Prefabs/Levels/Level4Corkboard.prefab");
        var dialogue = Load("Assets/Prefabs/UI/DialogueBox.prefab");
        var hud = Load("Assets/Prefabs/UI/HUD.prefab");

        RequireActive(level1.transform, "Level1Counter");
        RequireActive(level2.transform, "Level2Robot");
        RequireActive(level3Content.transform, "Level3ContentBased");
        RequireActive(level3Mainframe.transform, "Level3Mainframe");
        RequireActive(level4.transform, "Level4Corkboard");

        var dialogueBody = Require(dialogue.transform, "Body").GetComponent<Text>();
        if (dialogueBody == null || dialogueBody.lineSpacing < 1.2f)
            throw new System.InvalidOperationException("Dialogue body line spacing must remain at least 1.2.");
        Require(hud.transform, "Trust");
        Require(hud.transform, "TrustFill");

        foreach (string prefabPath in AuthoredUiPrefabPaths)
        {
            var prefab = Load(prefabPath);
            foreach (var text in prefab.GetComponentsInChildren<Text>(true))
                if (text.font == null)
                    throw new System.InvalidOperationException($"Text '{text.name}' in {prefabPath} has no serialized preview font.");
        }

        var level1Shelf = Require(level1.transform, "Shelf");
        if (level1Shelf.GetComponent<PosterBrowser>() == null)
            throw new System.InvalidOperationException("Level 1 shelf must use the authored PosterBrowser.");
        Require(level1Shelf, "GenreName");
        Require(level1Shelf, "Grid");
        Require(level1Shelf, "Detail");
        Require(level1Shelf, "Poster0");

        RequireApproximately(Require(level1.transform, "Result").GetComponent<RectTransform>().anchoredPosition,
            new Vector2(18, 52), "Level 1 result position");
        RequireApproximately(Require(level1.transform, "Next").GetComponent<RectTransform>().anchoredPosition,
            new Vector2(18, 12), "Level 1 next-customer position");

        var level3Shelf = Require(level3Content.transform, "Shelf");
        if (level3Shelf.GetComponent<PosterBrowser>() == null)
            throw new System.InvalidOperationException("Level 3 shelf must use the authored PosterBrowser.");
        Require(level3Content.transform, "ProfilePanel");
        for (int i = 0; i < GenreInfo.Count; i++)
            Require(level3Content.transform, "pf" + i);

        var log = Require(level2.transform, "Log").GetComponent<RectTransform>();
        var summary = Require(level2.transform, "Sum").GetComponent<RectTransform>();
        RequireApproximately(log.offsetMin, new Vector2(12, 52), "Level 2 CRT log inset");
        RequireApproximately(summary.sizeDelta, new Vector2(-24, 44), "Level 2 summary size");
        RequireApproximately(summary.anchoredPosition, new Vector2(0, 6), "Level 2 summary position");

        Debug.Log("Validated visible authored roots for all levels and detailed Level 1/2 layouts.");
    }

    /// <summary>
    /// Removes the old plain-Transform placeholders from dedicated Canvas hierarchies.
    /// A stretch-anchored RectTransform under a regular Transform has no parent rectangle,
    /// which made every directly-played level active but effectively zero-sized.
    /// </summary>
    [MenuItem("MadFact/Repair Dedicated Level Scene Slots")]
    public static void RepairDedicatedLevelSceneSlots()
    {
        string previouslyOpen = SceneManager.GetActiveScene().path;

        foreach (string scenePath in DedicatedScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
                throw new System.InvalidOperationException($"Scene {scenePath} has no Canvas.");

            var invalidSlots = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (transform.name.StartsWith("_LevelSlot_") && !(transform is RectTransform))
                        invalidSlots.Add(transform);

            foreach (var slot in invalidSlots)
            {
                while (slot.childCount > 0)
                    slot.GetChild(0).SetParent(canvas.transform, false);
                Object.DestroyImmediate(slot.gameObject);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Repaired {scenePath}: removed {invalidSlots.Count} invalid level slot(s).");
        }

        if (!string.IsNullOrEmpty(previouslyOpen) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previouslyOpen) != null)
            EditorSceneManager.OpenScene(previouslyOpen, OpenSceneMode.Single);
    }

    /// <summary>
    /// Keeps scene composition visually truthful in Edit Mode: environment behind the
    /// level, persistent HUD above it, and dialogue above everything when explicitly shown.
    /// </summary>
    [MenuItem("MadFact/Normalize Dedicated Scene UI Order")]
    public static void NormalizeDedicatedSceneUiOrder()
    {
        string previouslyOpen = SceneManager.GetActiveScene().path;

        foreach (string scenePath in SharedUiScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
                throw new System.InvalidOperationException($"Scene {scenePath} has no Canvas.");

            var storefront = canvas.GetComponentInChildren<StorefrontView>(true);
            var hud = canvas.GetComponentInChildren<Hud>(true);
            var comms = canvas.GetComponentInChildren<CommsBox>(true);
            RestoreSharedUiRectTransforms(storefront, hud, comms);
            if (storefront != null) storefront.transform.SetAsFirstSibling();

            // Move the level after the environment but before the persistent chrome.
            foreach (Transform child in canvas.transform)
            {
                if (child.GetComponent<Level1Counter>() != null ||
                    child.GetComponent<Level2Robot>() != null ||
                    child.GetComponent<Level3ContentBased>() != null ||
                    child.GetComponent<Level3Mainframe>() != null ||
                    child.GetComponent<Level4Corkboard>() != null)
                    child.SetSiblingIndex(Mathf.Min(1, canvas.transform.childCount - 1));
            }

            if (hud != null) hud.transform.SetAsLastSibling();
            if (comms != null)
            {
                comms.gameObject.SetActive(false);
                comms.transform.SetAsLastSibling();
            }

            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(previouslyOpen) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previouslyOpen) != null)
            EditorSceneManager.OpenScene(previouslyOpen, OpenSceneMode.Single);
    }

    static void RestoreSharedUiRectTransforms(StorefrontView storefront, Hud hud, CommsBox comms)
    {
        // Prefab-instantiation tools can create scene overrides that stretch every root
        // to the full Canvas. That makes the opaque HUD cover the storefront and turns
        // the dialogue window into a full-screen invisible click blocker.
        if (storefront != null)
        {
            var rt = (RectTransform)storefront.transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Fill(rt, 0, 46, 0, 0);
        }

        if (hud != null)
        {
            var rt = (RectTransform)hud.transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 46);
            rt.anchoredPosition = Vector2.zero;
        }

        if (comms != null)
        {
            var rt = (RectTransform)comms.transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(820, 188);
            rt.anchoredPosition = new Vector2(0, 16);
        }
    }

    static void Save(GameObject source, string assetPath)
    {
        source.hideFlags = HideFlags.None;
        SetSerializablePreviewFonts(source);
        PrefabUtility.SaveAsPrefabAsset(source, assetPath, out bool success);
        if (!success)
            throw new System.InvalidOperationException($"Could not refresh prefab at {assetPath}.");
    }

    /// <summary>
    /// Dynamic OS fonts are intentionally runtime-only. Store Unity's built-in font in
    /// every prefab so all labels render immediately in Scene/Game view while editing.
    /// RuntimeSkin replaces this preview font when Play Mode begins.
    /// </summary>
    [MenuItem("MadFact/Bake Serializable Preview Fonts")]
    public static void BakeSerializablePreviewFonts()
    {
        foreach (string prefabPath in AuthoredUiPrefabPaths)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SetSerializablePreviewFonts(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Baked serializable Edit Mode fonts into all authored MadFact UI prefabs.");
    }

    static void SetSerializablePreviewFonts(GameObject root)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            text.font = Theme.Fallback;
    }

    static GameObject Load(string assetPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            throw new System.InvalidOperationException($"Missing prefab at {assetPath}.");
        return prefab;
    }

    static Transform Require(Transform root, string name)
    {
        var result = UIFactory.FindDeep<Transform>(root, name);
        if (result == null)
            throw new System.InvalidOperationException($"Missing '{name}' under '{root.name}'.");
        return result;
    }

    static void RequireApproximately(Vector2 actual, Vector2 expected, string label)
    {
        if ((actual - expected).sqrMagnitude > 0.01f)
            throw new System.InvalidOperationException($"{label} is {actual}; expected {expected}.");
    }

    static void RequireActive(Transform root, string visualRootName)
    {
        foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate == root || candidate.name != visualRootName) continue;
            if (!candidate.gameObject.activeSelf)
                throw new System.InvalidOperationException($"{visualRootName} must be active so it is visible in Edit Mode.");
            return;
        }
        throw new System.InvalidOperationException($"Missing visual root '{visualRootName}' under prefab '{root.name}'.");
    }
}
