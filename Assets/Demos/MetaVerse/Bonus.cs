using UnityEngine;

public class Bonus : MonoBehaviour
{
    public LayerMask CollisionLayers;
    public int Points = 1;
    [Tooltip("ID unique du bonus — doit correspondre au serveur (ex: b0, b1, b2)")]
    public string BonusId;
    bool isCollected;
    NetworkManager _net;

    void Start()
    {
        IgnoreCarCollisions();
        if (string.IsNullOrEmpty(BonusId))
            BonusId = gameObject.name;
        _net = FindFirstObjectByType<NetworkManager>();
        if (_net != null)
        {
            _net.OnBonusTaken.AddListener(OnRemoteBonusTaken);
            _net.OnInitState.AddListener(OnInitState);
        }
    }

    void OnDestroy()
    {
        if (_net != null)
        {
            _net.OnBonusTaken.RemoveListener(OnRemoteBonusTaken);
            _net.OnInitState.RemoveListener(OnInitState);
        }
    }

    void OnInitState(InitStateMessage msg)
    {
        if (isCollected || msg.bonuses == null) return;
        foreach (var b in msg.bonuses)
            if (b.id == BonusId) return; 
        isCollected = true;
        Destroy(gameObject);
    }

    void OnRemoteBonusTaken(BonusTakenMessage msg)
    {
        if (!isCollected && msg.bonusId == BonusId)
        {
            isCollected = true;
            Destroy(gameObject);
        }
    }

    /// <summary>ID stable du bonus (BonusId si renseigné, sinon le nom du GameObject).</summary>
    public string ResolveBonusId() {
       return string.IsNullOrEmpty(BonusId) ? gameObject.name : BonusId;
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

      // Serveur Unity : autorité Physics. Le proxy du joueur entre dans le trigger → collecte.
      if (ServerMode.Active) {
        ServerPlayerProxy proxy = other.GetComponentInParent<ServerPlayerProxy>();
        if (proxy != null && UnityGameServer.Instance != null) {
          isCollected = true;
          enabled = false;
          UnityGameServer.Instance.CollectBonus(ResolveBonusId(), proxy.PlayerId);
          Destroy(gameObject);
        }
        return;
      }

      // Client connecté : le serveur fait autorité, on attend BONUS_TAKEN (ne pas collecter localement).
      if (_net != null && _net.HasSession) {
        return;
      }

      // Hors-ligne : comportement local d'origine.
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
