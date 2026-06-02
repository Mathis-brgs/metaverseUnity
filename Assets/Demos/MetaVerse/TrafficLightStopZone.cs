using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrafficLightStopZone : MonoBehaviour
{
    public TrafficLight TrafficLight;
    public bool StopOnlyWhenFacingZone = true;

    Collider stopCollider;

    void Awake()
    {
      stopCollider = GetComponent<Collider>();
      stopCollider.isTrigger = true;

      if (TrafficLight == null) {
        TrafficLight = GetComponentInParent<TrafficLight>();
      }
    }

    public bool ShouldStop(DrivableCar car)
    {
      if (TrafficLight == null || !TrafficLight.IsRed) { return false; }
      if (!StopOnlyWhenFacingZone) { return true; }

      Vector3 toZone = transform.position - car.transform.position;
      toZone.y = 0f;
      if (toZone.sqrMagnitude < 0.001f) { return true; }

      return Vector3.Dot(car.transform.forward, toZone.normalized) > 0.35f;
    }

    void OnDrawGizmos()
    {
      Gizmos.color = TrafficLight != null && TrafficLight.IsRed ? Color.red : Color.green;
      Gizmos.matrix = transform.localToWorldMatrix;
      Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
