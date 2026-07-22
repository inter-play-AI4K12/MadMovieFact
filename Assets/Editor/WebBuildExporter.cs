using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Creates the distributable WebGL build used for local testing and deployment.
/// The same implementation is available from Unity's MadFact menu and batch mode.
/// </summary>
public static class WebBuildExporter
{
    const string MenuPath = "MadFact/Build/WebGL Export";
    const string RelativeBuildDirectory = "Builds/WebGL";
    const string RelativeArchivePath = "Builds/MadMovieFact-WebGL.zip";
    static readonly string[] WebLauncherFiles =
    {
        "serve-web.sh",
        "serve_web.py",
        "serve-web.cmd",
        "serve-web.ps1"
    };

    /// <summary>
    /// Builds all enabled Editor Build Settings scenes and reveals the resulting ZIP.
    /// </summary>
    [MenuItem(MenuPath)]
    public static void BuildFromMenu()
    {
        try
        {
            string archivePath = Build();
            EditorUtility.RevealInFinder(archivePath);
            EditorUtility.DisplayDialog(
                "WebGL export complete",
                $"Created:\n{archivePath}",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("WebGL export failed", exception.Message, "OK");
        }
    }

    /// <summary>
    /// Entry point used by scripts/build-web.sh through Unity's -executeMethod option.
    /// An exception makes the batch-mode Unity process return a failure exit code.
    /// </summary>
    public static void BuildFromCommandLine()
    {
        Build();
    }

    static string Build()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string buildDirectory = Path.Combine(projectRoot, RelativeBuildDirectory);
        string archivePath = Path.Combine(projectRoot, RelativeArchivePath);
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && File.Exists(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes were found in Editor Build Settings.");

        Directory.CreateDirectory(Path.GetDirectoryName(buildDirectory));
        if (Directory.Exists(buildDirectory))
            Directory.Delete(buildDirectory, true);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildDirectory,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"WebGL build ended with {report.summary.result}: " +
                $"{report.summary.totalErrors} error(s), {report.summary.totalWarnings} warning(s).");
        }

        CopyWebLaunchers(projectRoot, buildDirectory);

        if (File.Exists(archivePath))
            File.Delete(archivePath);

        // Include the WebGL directory itself so extracting the ZIP creates one clean root folder.
        ZipFile.CreateFromDirectory(
            buildDirectory,
            archivePath,
            System.IO.Compression.CompressionLevel.Optimal,
            includeBaseDirectory: true);

        Debug.Log(
            $"WebGL export succeeded: {scenes.Length} scene(s), " +
            $"{report.summary.totalSize} bytes, {report.summary.totalTime}.\n" +
            $"Build: {buildDirectory}\nArchive: {archivePath}");

        return archivePath;
    }

    static void CopyWebLaunchers(string projectRoot, string buildDirectory)
    {
        string scriptsDirectory = Path.Combine(projectRoot, "scripts");
        foreach (string fileName in WebLauncherFiles)
        {
            string sourcePath = Path.Combine(scriptsDirectory, fileName);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    $"The WebGL launcher is missing: {sourcePath}",
                    sourcePath);

            File.Copy(sourcePath, Path.Combine(buildDirectory, fileName), overwrite: true);
        }

        string envExample = Path.Combine(projectRoot, ".env.example");
        if (!File.Exists(envExample))
            throw new FileNotFoundException(
                $"The telemetry environment example is missing: {envExample}",
                envExample);
        File.Copy(envExample, Path.Combine(buildDirectory, ".env.example"), overwrite: true);
    }
}
