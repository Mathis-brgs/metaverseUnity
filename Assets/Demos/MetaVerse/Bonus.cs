using UnityEngine;

public class Bonus : MonoBehaviour
{
    public LayerMask CollisionLayers;
    public int Points = 1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private bool ShouldHandleObject(Collider other) {
       return (CollisionLayers.value & (1 << other.gameObject.layer)) > 0;
    }

    void OnTriggerEnter(Collider other) {
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
        cScore.AddScore(Points);
        CharacterController controller = cScore.GetComponentInParent<CharacterController>();
        if (controller == null) {
          controller = cScore.GetComponentInChildren<CharacterController>();
        }
        ScorePanelHUD.ShowPickupMessage(controller);
      }

      Destroy(gameObject);
    }
}
