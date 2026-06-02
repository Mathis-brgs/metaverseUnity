using System.Collections;
using UnityEngine;

public class CityAmbienceSound : MonoBehaviour
{
    public string AmbienceFileName = "SFX_Brouhaha.mp3";
    public float Volume = 0.28f;
    public float RestartAtPercent = 0.5f;
    public float CrossfadeDuration = 0.1f;

    AudioSource sourceA;
    AudioSource sourceB;
    AudioClip ambienceClip;
    bool playingA = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateAmbience()
    {
      if (FindFirstObjectByType<CityAmbienceSound>() != null) { return; }

      GameObject ambience = new GameObject("City Ambience Sound");
      ambience.AddComponent<CityAmbienceSound>();
    }

    void Awake()
    {
      sourceA = CreateAmbienceSource();
      sourceB = CreateAmbienceSource();
    }

    void Start()
    {
      StartCoroutine(LoadAndPlay());
    }

    IEnumerator LoadAndPlay()
    {
      AudioClip loadedClip = null;
      yield return MetaVerseSoundLibrary.LoadClip(AmbienceFileName, clip => loadedClip = clip);

      if (loadedClip == null) { yield break; }

      ambienceClip = loadedClip;
      StartCoroutine(PlayLoopWithCrossfade());
    }

    AudioSource CreateAmbienceSource()
    {
      AudioSource audioSource = gameObject.AddComponent<AudioSource>();
      audioSource.playOnAwake = false;
      audioSource.loop = false;
      audioSource.spatialBlend = 0f;
      audioSource.volume = 0f;
      return audioSource;
    }

    IEnumerator PlayLoopWithCrossfade()
    {
      if (ambienceClip == null) { yield break; }

      sourceA.clip = ambienceClip;
      sourceA.volume = Volume;
      sourceA.Play();
      playingA = true;

      while (true) {
        AudioSource activeSource = playingA ? sourceA : sourceB;
        AudioSource nextSource = playingA ? sourceB : sourceA;
        float restartTime = Mathf.Clamp01(RestartAtPercent) * ambienceClip.length;
        float fadeDuration = Mathf.Min(CrossfadeDuration, Mathf.Max(0.01f, restartTime * 0.5f));
        float waitTime = Mathf.Max(0.01f, restartTime - fadeDuration);

        yield return new WaitForSeconds(waitTime);

        nextSource.clip = ambienceClip;
        nextSource.time = 0f;
        nextSource.volume = 0f;
        nextSource.Play();

        float elapsed = 0f;
        while (elapsed < fadeDuration) {
          float t = elapsed / fadeDuration;
          activeSource.volume = Mathf.Lerp(Volume, 0f, t);
          nextSource.volume = Mathf.Lerp(0f, Volume, t);
          elapsed += Time.deltaTime;
          yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        nextSource.volume = Volume;
        playingA = !playingA;
      }
    }
}
