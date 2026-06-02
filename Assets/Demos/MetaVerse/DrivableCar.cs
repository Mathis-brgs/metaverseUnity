using UnityEngine;

public class DrivableCar : MonoBehaviour
{
    public float DriveSpeed = 7f;
    public float ReverseSpeed = 3.5f;
    public float TurnSpeed = 120f;
    public bool AutoDriveWhenEmpty = false;
    public bool UseSceneAnimationWhenEmpty = true;
    public bool ParkAfterDriverExit = true;
    public float AutoDriveSpeed = 3f;
    public float RedLightLookAhead = 5f;
    public float CarLookAhead = 6f;
    public float ObstacleLookAhead = 3.2f;
    public float ObstacleCastRadius = 0.55f;
    public float ObstacleCastHeight = 1.05f;
    public float MinObstacleHeight = 0.55f;
    public LayerMask ObstacleLayers = ~0;
    public float HornInterval = 1.6f;
    public string EngineSoundFileName = "SFX_Cars.mp3";
    public float EngineVolume = 0.22f;
    public Vector3 SeatOffset = new Vector3(0f, 1.1f, 0f);
    public Vector3 ExitOffset = new Vector3(1.8f, 0f, 0f);
    public Transform Seat;

    CharacterController driver;
    Rigidbody rb;
    AudioSource hornSource;
    AudioSource engineSource;
    Animation sceneAnimation;
    bool parked;
    bool stoppedByTraffic;
    bool sceneAnimationStarted;
    float nextHornTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetupSceneCars()
    {
      Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
      foreach (Transform current in transforms) {
        if (!IsCarObjectName(current.name)) { continue; }
        if (current.parent != null && IsCarObjectName(current.parent.name)) { continue; }
        if (current.GetComponent<DrivableCar>() != null) { continue; }
        if (current.GetComponentInChildren<Collider>() == null) { continue; }

        DrivableCar car = current.gameObject.AddComponent<DrivableCar>();
        car.AutoDriveWhenEmpty = current.GetComponentInChildren<Animation>() != null;
      }
    }

    static bool IsCarObjectName(string objectName)
    {
      return objectName.Length > 3 && objectName.StartsWith("Car") && char.IsDigit(objectName[3]);
    }

    public bool HasDriver {
      get { return driver != null; }
    }

    public bool IsStoppedForTraffic {
      get { return parked || stoppedByTraffic; }
    }

    void Awake()
    {
      rb = GetComponent<Rigidbody>();
      if (rb == null) {
        rb = gameObject.AddComponent<Rigidbody>();
      }

      rb.mass = 8f;
      rb.angularDamping = 4f;
      rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
      sceneAnimation = GetComponentInChildren<Animation>();

      if (GetComponentInChildren<Collider>() == null) {
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(2.2f, 1.4f, 4f);
        box.center = new Vector3(0f, 0.8f, 0f);
      }

      if (Seat == null) {
        GameObject seatObject = new GameObject("Seat");
        seatObject.transform.SetParent(transform, false);
        seatObject.transform.localPosition = SeatOffset;
        seatObject.transform.localRotation = Quaternion.identity;
        Seat = seatObject.transform;
      }

      hornSource = GetComponent<AudioSource>();
      if (hornSource == null) {
        hornSource = gameObject.AddComponent<AudioSource>();
      }

      hornSource.playOnAwake = false;
      hornSource.spatialBlend = 1f;
      hornSource.volume = 0.35f;
      hornSource.clip = CreateHornClip();

      engineSource = gameObject.AddComponent<AudioSource>();
      engineSource.playOnAwake = false;
      engineSource.loop = true;
      engineSource.spatialBlend = 1f;
      engineSource.volume = EngineVolume;
      StartCoroutine(LoadEngineSound());

      IgnoreBonusCollisions();
    }

    void FixedUpdate()
    {
      rb.angularVelocity = Vector3.zero;

      if (driver != null) {
        parked = false;
        stoppedByTraffic = false;
        SetSceneAnimationPlaying(false);
        DriveWithInput(driver.GetMoveInput());
        return;
      }

      if (parked) {
        stoppedByTraffic = false;
        SetSceneAnimationPlaying(false);
        SetEngineSoundPlaying(false);
        StopNow();
        return;
      }

      if (AutoDriveWhenEmpty) {
        DriveAutonomously();
      } else {
        SetSceneAnimationPlaying(false);
        SetEngineSoundPlaying(false);
      }
    }

    public bool CanEnter(CharacterController character)
    {
      return driver == null && character != null && !character.IsDrivingCar;
    }

    public void Enter(CharacterController character)
    {
      if (!CanEnter(character)) { return; }

      StopNow();
      parked = false;
      driver = character;
      driver.EnterCar(this);
    }

    public void Exit(CharacterController character)
    {
      if (driver != character) { return; }

      driver = null;
      StopNow();
      parked = ParkAfterDriverExit;
    }

    public Vector3 GetExitPosition()
    {
      return transform.TransformPoint(ExitOffset);
    }

    public void StopNow()
    {
      rb.linearVelocity = Vector3.zero;
      rb.angularVelocity = Vector3.zero;
    }

    void DriveWithInput(Vector2 input)
    {
      float throttle = input.y;
      float turn = input.x;
      float speed = throttle >= 0f ? DriveSpeed : ReverseSpeed;
      Vector3 movement = transform.forward * throttle * speed * Time.fixedDeltaTime;

      rb.MovePosition(rb.position + movement);

      if (Mathf.Abs(turn) > 0.01f) {
        float direction = Mathf.Abs(throttle) > 0.01f ? Mathf.Sign(throttle) : 1f;
        Quaternion rotation = Quaternion.AngleAxis(turn * direction * TurnSpeed * Time.fixedDeltaTime, Vector3.up);
        rb.MoveRotation(rb.rotation * rotation);
      }

      if (input.sqrMagnitude < 0.001f) {
        SetEngineSoundPlaying(false);
        StopNow();
      } else {
        SetEngineSoundPlaying(true);
      }
    }

    void DriveAutonomously()
    {
      bool blockedByObstacle = ShouldStopForObstacleAhead(Mathf.Max(CarLookAhead, ObstacleLookAhead));
      if (ShouldStopForRedLight() || blockedByObstacle) {
        stoppedByTraffic = true;
        SetSceneAnimationPlaying(false);
        SetEngineSoundPlaying(false);
        StopNow();

        if (blockedByObstacle) {
          Honk();
        }
        return;
      }

      stoppedByTraffic = false;
      SetSceneAnimationPlaying(true);
      SetEngineSoundPlaying(true);
      if (UseSceneAnimationWhenEmpty && sceneAnimation != null) {
        return;
      }

      rb.MovePosition(rb.position + transform.forward * AutoDriveSpeed * Time.fixedDeltaTime);
    }

    void SetSceneAnimationPlaying(bool shouldPlay)
    {
      if (!UseSceneAnimationWhenEmpty || sceneAnimation == null) { return; }

      sceneAnimation.enabled = true;

      if (shouldPlay) {
        SetSceneAnimationSpeed(1f);
        if (!sceneAnimationStarted) {
          sceneAnimation.Play();
          sceneAnimationStarted = true;
        }
      } else {
        SetSceneAnimationSpeed(0f);
      }
    }

    void SetSceneAnimationSpeed(float speed)
    {
      foreach (AnimationState state in sceneAnimation) {
        state.speed = speed;
      }
    }

    System.Collections.IEnumerator LoadEngineSound()
    {
      AudioClip loadedClip = null;
      yield return MetaVerseSoundLibrary.LoadClip(EngineSoundFileName, clip => loadedClip = clip);

      if (loadedClip != null && engineSource != null) {
        engineSource.clip = loadedClip;
      }
    }

    void SetEngineSoundPlaying(bool shouldPlay)
    {
      if (engineSource == null || engineSource.clip == null) { return; }

      engineSource.volume = EngineVolume;
      if (shouldPlay) {
        if (!engineSource.isPlaying) {
          engineSource.Play();
        }
      } else if (engineSource.isPlaying) {
        engineSource.Stop();
      }
    }

    bool ShouldStopForRedLight()
    {
      Ray ray = new Ray(transform.position + Vector3.up * 0.7f, transform.forward);
      RaycastHit[] hits = Physics.SphereCastAll(ray, 0.8f, RedLightLookAhead, ~0, QueryTriggerInteraction.Collide);

      foreach (RaycastHit hit in hits) {
        if (IsOwnCollider(hit.collider)) { continue; }

        TrafficLightStopZone zone = hit.collider.GetComponent<TrafficLightStopZone>();
        if (zone != null && zone.ShouldStop(this)) {
          return true;
        }

        TrafficLight trafficLight = hit.collider.GetComponentInParent<TrafficLight>();
        if (trafficLight != null && trafficLight.IsRed && IsInFront(trafficLight.transform.position)) {
          return true;
        }
      }

      return false;
    }

    bool ShouldStopForObstacleAhead(float lookAhead)
    {
      Ray ray = new Ray(transform.position + Vector3.up * ObstacleCastHeight, transform.forward);
      RaycastHit[] hits = Physics.SphereCastAll(ray, ObstacleCastRadius, lookAhead, ObstacleLayers, QueryTriggerInteraction.Ignore);

      foreach (RaycastHit hit in hits) {
        if (IsOwnCollider(hit.collider)) { continue; }
        if (hit.collider.GetComponentInParent<Bonus>() != null) { continue; }
        if (!IsInFront(hit.collider.bounds.center)) { continue; }
        if (IsTooLowToBlockCar(hit.collider)) { continue; }

        DrivableCar otherCar = hit.collider.GetComponentInParent<DrivableCar>();
        CharacterController character = hit.collider.GetComponentInParent<CharacterController>();

        if (otherCar != null || character != null || !hit.collider.isTrigger) {
          return true;
        }
      }

      return false;
    }

    void IgnoreBonusCollisions()
    {
      Collider[] carColliders = GetComponentsInChildren<Collider>();
      Bonus[] bonuses = FindObjectsByType<Bonus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

      foreach (Collider carCollider in carColliders) {
        foreach (Bonus bonus in bonuses) {
          Collider[] bonusColliders = bonus.GetComponentsInChildren<Collider>();
          foreach (Collider bonusCollider in bonusColliders) {
            Physics.IgnoreCollision(carCollider, bonusCollider, true);
          }
        }
      }
    }

    bool IsTooLowToBlockCar(Collider hitCollider)
    {
      DrivableCar otherCar = hitCollider.GetComponentInParent<DrivableCar>();
      CharacterController character = hitCollider.GetComponentInParent<CharacterController>();
      if (otherCar != null || character != null) { return false; }

      return hitCollider.bounds.max.y < transform.position.y + MinObstacleHeight;
    }

    bool IsOwnCollider(Collider hitCollider)
    {
      if (hitCollider == null) { return false; }
      if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) {
        return true;
      }

      return false;
    }

    bool IsInFront(Vector3 position)
    {
      Vector3 toPosition = position - transform.position;
      toPosition.y = 0f;
      if (toPosition.sqrMagnitude < 0.001f) { return false; }

      return Vector3.Dot(transform.forward, toPosition.normalized) > 0.35f;
    }

    void Honk()
    {
      if (Time.time < nextHornTime || hornSource == null || hornSource.clip == null) { return; }

      hornSource.Play();
      nextHornTime = Time.time + HornInterval;
    }

    AudioClip CreateHornClip()
    {
      const int sampleRate = 22050;
      const float duration = 0.22f;
      int sampleCount = Mathf.CeilToInt(sampleRate * duration);
      float[] samples = new float[sampleCount];

      for (int i = 0; i < sampleCount; i++) {
        float time = i / (float)sampleRate;
        float envelope = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
        float tone = Mathf.Sin(2f * Mathf.PI * 440f * time) + 0.45f * Mathf.Sin(2f * Mathf.PI * 880f * time);
        samples[i] = tone * envelope * 0.35f;
      }

      AudioClip clip = AudioClip.Create("CarHorn", sampleCount, 1, sampleRate, false);
      clip.SetData(samples, 0);
      return clip;
    }
}
