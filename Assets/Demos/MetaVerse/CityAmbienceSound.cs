using UnityEngine;

public class CityAmbienceSound : MonoBehaviour
{
    public string AmbienceFileName = "traffic_city.mp3";
    public string VoiceFileName = "voix.mp3";
    public float Volume = 0.5f;
    public float VoiceVolume = 0.16f;
    public float RestartAtPercent = 0.3f;
    public float VoiceRestartAtPercent = 0.92f;

    AudioSource audioSource;
    AudioSource voiceSource;
    AudioClip ambienceClip;
    AudioClip voiceClip;
    float restartTime;
    float voiceRestartTime;

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
      voiceSource = CreateAmbienceSource();
      voiceSource.volume = VoiceVolume;
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

      AudioClip loadedVoiceClip = null;
      yield return MetaVerseSoundLibrary.LoadClip(VoiceFileName, clip => loadedVoiceClip = clip);

      if (loadedVoiceClip == null) { yield break; }

      voiceClip = loadedVoiceClip;
      voiceRestartTime = Mathf.Max(0.01f, Mathf.Clamp01(VoiceRestartAtPercent) * voiceClip.length);
      voiceSource.clip = voiceClip;
      voiceSource.time = 0f;
      voiceSource.volume = VoiceVolume;
      voiceSource.Play();
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
      if (audioSource != null && ambienceClip != null && audioSource.isPlaying && audioSource.time >= restartTime) {
        audioSource.time = 0f;
      }

      if (voiceSource != null && voiceClip != null && voiceSource.isPlaying && voiceSource.time >= voiceRestartTime) {
        voiceSource.time = 0f;
      }
    }
}
