using MadFact;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the shared mainframe prefab truthful in both authored scenes: Level 4 shows
/// the 2x5 collaborative-filtering rating task, while Level 5 shows its two-factor
/// manual-error tutorial before the full optimizer lesson.
/// </summary>
public static class BuildCollaborativeFilteringScene
{
    const string CollaborativeScene = "Assets/Scenes/Level04_CollaborativeFiltering.unity";
    const string FactorizationScene = "Assets/Scenes/Level05_MatrixFactorization.unity";

    [MenuItem("MadFact/Rebuild Collaborative Filtering Level 4 Scene")]
    public static void Rebuild()
    {
        RefreshAuthoredLevelPrefabs.RefreshCollaborativeFiltering();
        RefreshAuthoredLevelPrefabs.RefreshDialogueBox();
        ConfigureCollaborativeScene();
        ConfigureFactorizationScene();
        BuildRatingsLevelScene.UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(CollaborativeScene, OpenSceneMode.Single);
        var level = Object.FindAnyObjectByType<Level3Mainframe>();
        Selection.activeGameObject = level != null ? level.gameObject : scene.GetRootGameObjects()[0];
        SceneView.lastActiveSceneView?.FrameSelected();
        SceneView.RepaintAll();
        Debug.Log("Rebuilt editor-visible Level 4 collaborative filtering and Level 5 factorization scenes.");
    }

    static void ConfigureCollaborativeScene()
    {
        Scene scene = OpenLevelScene(CollaborativeScene, 4, out Level3Mainframe level);
        SetActive(level, "Sliders", false);
        SetActive(level, "Picker", true);
        SetActive(level, "Grid", true);
        SetActive(level, "SparseMatrix", false);
        SetActive(level, "LossBox", false);
        SetActive(level, "Reset", false);
        SetActive(level, "Optimize", false);

        SetText(level, "Title", "█ 2×5 TRAINING ░ COLLABORATIVE FILTERING █");
        SetText(level, "Hint",
            "2×5 TASK: Wendell and Priya match on four movies. Use Wendell's last rating to fill Priya's ?.");
        SetText(level, "PickLbl",
            "PREDICT PRIYA'S RATING\n\nClick the ?, compare Wendell's\nmatching row, then choose 1 to 5 stars.");

        var tutorial = new CollaborativeFilteringTutorialModel();
        for (int row = 0; row < GameData.MatrixCustomers.Length; row++)
        {
            SetActive(level, "Row" + row, row < CollaborativeFilteringTutorialModel.Rows);
            if (row < CollaborativeFilteringTutorialModel.Rows)
                SetText(level, "Row" + row, CollaborativeFilteringTutorialModel.CustomerNames[row]);
            for (int column = 0; column < GameData.MatrixMovieSet.Count; column++)
            {
                SetActive(level, $"C{row}_{column}", row < CollaborativeFilteringTutorialModel.Rows);
                if (row < CollaborativeFilteringTutorialModel.Rows)
                    SetNestedText(level, $"C{row}_{column}", "G", tutorial.Known[row, column]
                        ? tutorial.Target[row, column].ToString("0.0")
                        : "?");
            }
        }
        for (int column = 0; column < GameData.MatrixMovieSet.Count; column++)
        {
            SetActive(level, "Col" + column, true);
            SetText(level, "Col" + column,
                CollaborativeFilteringTutorialModel.MovieNames[column].Replace(" ", "\n"));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void ConfigureFactorizationScene()
    {
        Scene scene = OpenLevelScene(FactorizationScene, 5, out Level3Mainframe level);
        SetActive(level, "Sliders", true);
        SetActive(level, "Picker", false);
        SetActive(level, "Grid", true);
        SetActive(level, "SparseMatrix", false);
        SetActive(level, "LossBox", true);
        SetActive(level, "Reset", true);
        SetActive(level, "Optimize", true);
        SetText(level, "Title", "█ 3×3 TRAINING ░ TWO-FACTOR PROFILES █");
        SetText(level, "Hint",
            "3×3 TASK: Adjust FACTOR 1 and FACTOR 2. Lower mean ERROR below 0.35, then press CHECK ERROR.");
        SetText(level, "Optimize", "CHECK ERROR");

        for (int row = 0; row < GameData.MatrixCustomers.Length; row++)
        {
            SetActive(level, "Row" + row, row < 3);
            if (row < 3)
                SetText(level, "Row" + row, MatrixTutorialModel.CustomerNames[row]);
            for (int column = 0; column < GameData.MatrixMovieSet.Count; column++)
            {
                SetActive(level, $"C{row}_{column}", row < 3 && column < 3);
                if (row < 3 && column < 3)
                {
                    var model = new MatrixTutorialModel();
                    SetNestedText(level, $"C{row}_{column}", "T",
                        model.Known[row, column] ? "ORIG " + model.Target[row, column].ToString("0.0") : "PRED");
                    SetNestedText(level, $"C{row}_{column}", "G",
                        model.Known[row, column] ? model.Guess(row, column).ToString("0.0") : "?");
                }
            }
        }
        for (int column = 0; column < GameData.MatrixMovieSet.Count; column++)
        {
            SetActive(level, "Col" + column, column < 3);
            if (column < 3)
                SetText(level, "Col" + column, MatrixTutorialModel.MovieNames[column].Replace(" ", "\n"));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Scene OpenLevelScene(string path, int levelNumber, out Level3Mainframe level)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        level = Object.FindAnyObjectByType<Level3Mainframe>();
        if (level == null)
            throw new System.InvalidOperationException("Scene has no Level3Mainframe: " + path);
        var bootstrap = Object.FindAnyObjectByType<MadFactBootstrap>();
        if (bootstrap == null)
            throw new System.InvalidOperationException("Scene has no MadFactBootstrap: " + path);
        bootstrap.StartPhaseOverride = levelNumber;
        EditorUtility.SetDirty(bootstrap);
        return scene;
    }

    static void SetActive(Level3Mainframe level, string name, bool active)
    {
        Transform target = UIFactory.FindDeep<Transform>(level.transform, name);
        if (target == null)
            throw new System.InvalidOperationException($"Missing authored object '{name}'.");
        target.gameObject.SetActive(active);
        EditorUtility.SetDirty(target.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
    }

    static void SetText(Level3Mainframe level, string parentName, string value)
    {
        Transform parent = UIFactory.FindDeep<Transform>(level.transform, parentName);
        Text text = parent != null ? parent.GetComponentInChildren<Text>(true) : null;
        if (text == null)
            throw new System.InvalidOperationException($"Missing authored text under '{parentName}'.");
        text.text = value;
        EditorUtility.SetDirty(text);
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }

    static void SetNestedText(Level3Mainframe level, string parentName, string childName, string value)
    {
        Transform parent = UIFactory.FindDeep<Transform>(level.transform, parentName);
        Text text = parent != null ? UIFactory.FindDeep<Text>(parent, childName) : null;
        if (text == null)
            throw new System.InvalidOperationException($"Missing authored text '{parentName}/{childName}'.");
        text.text = value;
        EditorUtility.SetDirty(text);
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }
}
