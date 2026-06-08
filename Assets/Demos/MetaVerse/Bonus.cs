using UnityEngine;

public class Bonus : MonoBehaviour
{
    public LayerMask CollisionLayers;
    public int Points = 1;
    bool isCollected;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        IgnoreCarCollisions();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private bool ShouldHandleObject(Collider other) {
       return (CollisionLayers.value & (1 << other.gameObject.layer)) > 0;
    }

    void IgnoreCarCollisions()
    {
      Collider[] bonusColliders = GetComponentsInChildren<Collider>();
      DrivableCar[] cars = FindObjectsByType<DrivableCar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

      foreach (Collider bonusCollider in bonusColliders) {
        foreach (DrivableCar car in cars) {
          Collider[] carColliders = car.GetComponentsInChildren<Collider>();
          foreach (Collider carCollider in carColliders) {
            Physics.IgnoreCollision(bonusCollider, carCollider, true);
          }
        }
      }
    }

    void OnTriggerEnter(Collider other) {
      if (isCollected) { return; }
      if (!ShouldHandleObject(other)) { return; }

      CharacterScore cScore = other.GetComponentInParent<CharacterScore>();
      if (cScore == null) {
        cScore = other.GetComponentInChildren<CharacterScore>();
      }
      if (cScore == null) {
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (controller != null) {
          cScore = controller.GetComponentInChildren<CharacterScore>();
        }
      }

      if (cScore != null) {
        isCollected = true;
        enabled = false;
        cScore.AddScore(Points);
        CharacterController controller = FindCollectorController(other, cScore);
        if (controller != null) {
          controller.ApplyBonusSpeedBoost();
        }
        ScorePanelHUD.ShowPickupMessage(controller);
      }

      if (!isCollected) { return; }

      Destroy(gameObject);
    }

    CharacterController FindCollectorController(Collider other, CharacterScore cScore)
    {
      CharacterController controller = other.GetComponentInParent<CharacterController>();
      if (controller != null) { return controller; }

      controller = cScore.GetComponentInParent<CharacterController>();
      if (controller != null) { return controller; }

      return cScore.GetComponentInChildren<CharacterController>();
    }
}
