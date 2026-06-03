using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class MetaVerseSoundLibrary
{
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static IEnumerator LoadClip(string fileName, Action<AudioClip> onLoaded)
    {
      AudioClip cachedClip;
      if (clips.TryGetValue(fileName, out cachedClip)) {
        onLoaded?.Invoke(cachedClip);
        yield break;
      }

      string path = Path.Combine(Application.dataPath, "Demos", "MetaVerse", "Sound", fileName);
      string url = "file://" + path.Replace("\\", "/");

      using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG)) {
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
          Debug.LogWarning("Impossible de charger le son MetaVerse: " + fileName + " (" + request.error + ")");
          onLoaded?.Invoke(null);
          yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        clip.name = Path.GetFileNameWithoutExtension(fileName);
        clips[fileName] = clip;
        onLoaded?.Invoke(clip);
      }
    }
}
