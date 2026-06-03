using UnityEngine;

public class CityAmbienceSound : MonoBehaviour
{
    public string AmbienceFileName = "SFX_Brouhaha.mp3";
    public float Volume = 0.28f;
    public float RestartAtPercent = 0.3f;

    AudioSource audioSource;
    AudioClip ambienceClip;
    float restartTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateAmbience()
    {
      if (FindFirstObjectByType<CityAmbienceSound>() != null) { return; }

      GameObject ambience = new GameObject("City Ambience Sound");
      ambience.AddComponent<CityAmbienceSound>();
    }

    void Awake()
    {
      audioSource = CreateAmbienceSource();
    }

    void Start()
    {
      StartCoroutine(LoadAndPlay());
    }

    System.Collections.IEnumerator LoadAndPlay()
    {
      AudioClip loadedClip = null;
      yield return MetaVerseSoundLibrary.LoadClip(AmbienceFileName, clip => loadedClip = clip);

      if (loadedClip == null) { yield break; }

      ambienceClip = loadedClip;
      restartTime = Mathf.Max(0.01f, Mathf.Clamp01(RestartAtPercent) * ambienceClip.length);
      audioSource.clip = ambienceClip;
      audioSource.time = 0f;
      audioSource.volume = Volume;
      audioSource.Play();
    }

    AudioSource CreateAmbienceSource()
    {
      AudioSource audioSource = gameObject.AddComponent<AudioSource>();
      audioSource.playOnAwake = false;
      audioSource.loop = false;
      audioSource.spatialBlend = 0f;
      audioSource.volume = Volume;
      return audioSource;
    }

    void Update()
    {
      if (audioSource == null || ambienceClip == null || !audioSource.isPlaying) { return; }

      if (audioSource.time >= restartTime) {
        audioSource.time = 0f;
      }
    }
}
