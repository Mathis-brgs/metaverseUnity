using UnityEngine;

public enum TrafficLightState
{
    Red,
    Orange,
    Green
}

public class TrafficLight : MonoBehaviour
{
    public TrafficLightState State = TrafficLightState.Red;
    public float RedDuration = 4f;
    public float OrangeDuration = 1.2f;
    public float GreenDuration = 4f;
    public Vector3 StopZoneOffset = new Vector3(0f, 0.8f, -3f);
    public Vector3 StopZoneSize = new Vector3(5f, 2f, 1.5f);
    public Renderer RedRenderer;
    public Renderer OrangeRenderer;
    public Renderer GreenRenderer;
    public TrafficLightStopZone StopZone;

    float timer;

    public bool IsRed {
      get { return State == TrafficLightState.Red; }
    }

    void Start()
    {
      EnsureVisuals();
      EnsureStopZone();
      ApplyLightColors();
    }

    void Update()
    {
      timer += Time.deltaTime;

      if (State == TrafficLightState.Red && timer >= RedDuration) {
        SetState(TrafficLightState.Green);
      } else if (State == TrafficLightState.Green && timer >= GreenDuration) {
        SetState(TrafficLightState.Orange);
      } else if (State == TrafficLightState.Orange && timer >= OrangeDuration) {
        SetState(TrafficLightState.Red);
      }
    }

    public void SetState(TrafficLightState state)
    {
      State = state;
      timer = 0f;
      ApplyLightColors();
    }

    void ApplyLightColors()
    {
      SetRendererColor(RedRenderer, State == TrafficLightState.Red ? Color.red : Color.black);
      SetRendererColor(OrangeRenderer, State == TrafficLightState.Orange ? new Color(1f, 0.55f, 0f) : Color.black);
      SetRendererColor(GreenRenderer, State == TrafficLightState.Green ? Color.green : Color.black);
    }

    void SetRendererColor(Renderer target, Color color)
    {
      if (target == null) { return; }

      MaterialPropertyBlock block = new MaterialPropertyBlock();
      target.GetPropertyBlock(block);
      block.SetColor("_BaseColor", color);
      block.SetColor("_Color", color);
      block.SetColor("_EmissionColor", color);
      target.SetPropertyBlock(block);
    }

    void EnsureVisuals()
    {
      if (RedRenderer != null || OrangeRenderer != null || GreenRenderer != null) { return; }

      RedRenderer = CreateLightBulb("Red", new Vector3(0f, 2.2f, 0f));
      OrangeRenderer = CreateLightBulb("Orange", new Vector3(0f, 1.55f, 0f));
      GreenRenderer = CreateLightBulb("Green", new Vector3(0f, 0.9f, 0f));
    }

    Renderer CreateLightBulb(string bulbName, Vector3 localPosition)
    {
      GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bulb.name = bulbName;
      bulb.transform.SetParent(transform, false);
      bulb.transform.localPosition = localPosition;
      bulb.transform.localScale = Vector3.one * 0.45f;

      Collider bulbCollider = bulb.GetComponent<Collider>();
      if (bulbCollider != null) {
        Destroy(bulbCollider);
      }

      return bulb.GetComponent<Renderer>();
    }

    void EnsureStopZone()
    {
      if (StopZone != null) { return; }

      StopZone = GetComponentInChildren<TrafficLightStopZone>();
      if (StopZone != null) {
        StopZone.TrafficLight = this;
        return;
      }

      GameObject stopZoneObject = new GameObject("Traffic Light Stop Zone");
      stopZoneObject.transform.SetParent(transform, false);
      stopZoneObject.transform.localPosition = StopZoneOffset;
      stopZoneObject.transform.localRotation = Quaternion.identity;

      BoxCollider stopCollider = stopZoneObject.AddComponent<BoxCollider>();
      stopCollider.size = StopZoneSize;
      stopCollider.isTrigger = true;

      StopZone = stopZoneObject.AddComponent<TrafficLightStopZone>();
      StopZone.TrafficLight = this;
    }
}
