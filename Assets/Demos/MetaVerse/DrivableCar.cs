using UnityEngine;

public class DrivableCar : MonoBehaviour
{
    public float DriveSpeed = 7f;
    public float ReverseSpeed = 3.5f;
    public float TurnSpeed = 120f;
    public bool AutoDriveWhenEmpty = true;
    public float AutoDriveSpeed = 3f;
    public float RedLightLookAhead = 5f;
    public Vector3 SeatOffset = new Vector3(0f, 1.1f, 0f);
    public Vector3 ExitOffset = new Vector3(1.8f, 0f, 0f);
    public Transform Seat;

    CharacterController driver;
    Rigidbody rb;

    public bool HasDriver {
      get { return driver != null; }
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
    }

    void FixedUpdate()
    {
      rb.angularVelocity = Vector3.zero;

      if (driver != null) {
        DriveWithInput(driver.GetMoveInput());
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
      driver = character;
      driver.EnterCar(this);
    }

    public void Exit(CharacterController character)
    {
      if (driver != character) { return; }

      driver = null;
      StopNow();
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
      if (ShouldStopForRedLight()) {
        StopNow();
        return;
      }

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
}
