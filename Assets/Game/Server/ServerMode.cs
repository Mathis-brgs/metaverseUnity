using System;
using UnityEngine;

/// <summary>
/// Détecte et expose si cette instance Unity tourne en mode serveur dédié (autorité Physics).
///
/// Le mode serveur est actif si :
///   - le build est compilé avec le define UNITY_SERVER (Dedicated Server build), OU
///   - l'exécutable est lancé avec l'argument -server / -batchmode, OU
///   - ForceServerInEditor est activé manuellement (pour tester dans l'éditeur).
/// </summary>
public static class ServerMode
{
    /// <summary>Mettre true dans l'éditeur pour lancer la scène MetaVerse en serveur (test local).</summary>
    public static bool ForceServerInEditor = false;

    static bool? _cached;

    public static bool Active
    {
        get
        {
            if (_cached.HasValue) return _cached.Value;
            _cached = Detect();
            return _cached.Value;
        }
    }

    static bool Detect()
    {
#if UNITY_SERVER
        return true;
#else
        if (ForceServerInEditor)
            return true;

#if UNITY_EDITOR
        // Toggle persistant via le menu MetaVerse/Server/Force Server In Editor.
        if (UnityEditor.EditorPrefs.GetBool("MetaVerse.ForceServerInEditor", false))
            return true;
#endif

        if (Application.isBatchMode)
            return true;

        try
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (string.Equals(arg, "-server", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }

        return false;
#endif
    }

    /// <summary>Permet de forcer le mode avant la première lecture (ex. depuis un launcher).</summary>
    public static void Override(bool active)
    {
        _cached = active;
    }
}
