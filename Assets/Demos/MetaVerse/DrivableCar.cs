using UnityEngine;

public class DrivableCar : MonoBehaviour
{
    public float DriveSpeed = 7f;
    public float ReverseSpeed = 3.5f;
    public float TurnSpeed = 120f;
    public bool AutoDriveWhenEmpty = true;
    public bool ParkAfterDriverExit = true;
    public float AutoDriveSpeed = 3f;
    public float RedLightLookAhead = 5f;
    public float CarLookAhead = 6f;
    public float HornInterval = 1.6f;
    public Vector3 SeatOffset = new Vector3(0f, 1.1f, 0f);
    public Vector3 ExitOffset = new Vector3(1.8f, 0f, 0f);
    public Transform Seat;

    CharacterController driver;
    Rigidbody rb;
    AudioSource hornSource;
    bool parked;
    bool stoppedByTraffic;
    float nextHornTime;

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
    }

    void FixedUpdate()
    {
      rb.angularVelocity = Vector3.zero;

      if (driver != null) {
        parked = false;
        stoppedByTraffic = false;
        DriveWithInput(driver.GetMoveInput());
        return;
      }

      if (parked) {
        stoppedByTraffic = false;
        StopNow();
        return;
      }

      if (AutoDriveWhenEmpty) {
        DriveAutonomously();
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
        StopNow();
      }
    }

    void DriveAutonomously()
    {
      bool blockedByCar = ShouldStopForCarAhead();
      if (ShouldStopForRedLight() || blockedByCar) {
        stoppedByTraffic = true;
        StopNow();

        if (blockedByCar) {
          Honk();
        }
        return;
      }

      stoppedByTraffic = false;
      rb.MovePosition(rb.position + transform.forward * AutoDriveSpeed * Time.fixedDeltaTime);
    }

    bool ShouldStopForRedLight()
    {
      Ray ray = new Ray(transform.position + Vector3.up * 0.7f, transform.forward);
      RaycastHit[] hits = Physics.SphereCastAll(ray, 0.8f, RedLightLookAhead, ~0, QueryTriggerInteraction.Collide);

      foreach (RaycastHit hit in hits) {
        TrafficLightStopZone zone = hit.collider.GetComponent<TrafficLightStopZone>();
        if (zone != null && zone.ShouldStop(this)) {
          return true;
        }
      }

      return false;
    }

    bool ShouldStopForCarAhead()
    {
      Ray ray = new Ray(transform.position + Vector3.up * 0.8f, transform.forward);
      RaycastHit[] hits = Physics.SphereCastAll(ray, 0.9f, CarLookAhead, ~0, QueryTriggerInteraction.Collide);

      foreach (RaycastHit hit in hits) {
        DrivableCar otherCar = hit.collider.GetComponentInParent<DrivableCar>();
        if (otherCar == null || otherCar == this) { continue; }
        if (!IsInFront(otherCar.transform.position)) { continue; }

        return otherCar.IsStoppedForTraffic;
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
