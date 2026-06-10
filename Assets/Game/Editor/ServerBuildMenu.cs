#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Outils éditeur pour le serveur Unity dédié :
///   - tester le mode serveur dans l'éditeur (toggle persistant),
///   - ajouter la scène MetaVerse au build,
///   - produire un build "Dedicated Server" headless (remplace dotnet run).
/// </summary>
public static class ServerBuildMenu
{
    const string MetaVerseScene = "Assets/Demos/MetaVerse/MetaVerse.unity";
    const string ForceServerPref = "MetaVerse.ForceServerInEditor";

    [MenuItem("MetaVerse/Server/Force Server In Editor", false, 0)]
    static void ToggleForceServer()
    {
        bool value = !EditorPrefs.GetBool(ForceServerPref, false);
        EditorPrefs.SetBool(ForceServerPref, value);
        Debug.Log($"[ServerBuildMenu] Force Server In Editor = {value} (relancer le Play).");
    }

    [MenuItem("MetaVerse/Server/Force Server In Editor", true)]
    static bool ToggleForceServerValidate()
    {
        Menu.SetChecked("MetaVerse/Server/Force Server In Editor", EditorPrefs.GetBool(ForceServerPref, false));
        return true;
    }

    [MenuItem("MetaVerse/Server/Add MetaVerse Scene To Build", false, 20)]
    static void AddSceneToBuild()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == MetaVerseScene) { Debug.Log("[ServerBuildMenu] Scène déjà dans le build."); return; }

        scenes.Add(new EditorBuildSettingsScene(MetaVerseScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[ServerBuildMenu] MetaVerse ajoutée au build.");
    }

    [MenuItem("MetaVerse/Server/Build Dedicated Server", false, 40)]
    static void BuildDedicatedServer()
    {
        string folder = EditorUtility.SaveFolderPanel("Dossier de sortie du serveur dédié", "", "MetaverseServer");
        if (string.IsNullOrEmpty(folder)) return;

        BuildTarget target = BuildTarget.StandaloneLinux64;
        string exeName = "MetaverseServer";

        var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { MetaVerseScene },
            locationPathName = Path.Combine(folder, exeName),
            target = target,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[ServerBuildMenu] Serveur dédié construit : {options.locationPathName}\n" +
                      "Lancer avec : <exe> -batchmode -nographics");
        else
            Debug.LogError("[ServerBuildMenu] Échec du build serveur : " + report.summary.result);
    }

    static string ExecutableExtension(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return ".exe";
            case BuildTarget.StandaloneOSX:
                return ".app";
            default:
                return ""; // Linux : binaire sans extension
        }
    }
}
#endif
