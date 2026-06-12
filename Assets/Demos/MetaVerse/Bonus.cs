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
            _net.OnBonusSpawn.AddListener(OnRemoteBonusSpawn);
            _net.OnInitState.AddListener(OnInitState);
        }
    }

    void OnDestroy()
    {
        if (_net != null)
        {
            _net.OnBonusTaken.RemoveListener(OnRemoteBonusTaken);
            _net.OnBonusSpawn.RemoveListener(OnRemoteBonusSpawn);
            _net.OnInitState.RemoveListener(OnInitState);
        }
    }

    void OnInitState(InitStateMessage msg)
    {
        if (isCollected || msg.bonuses == null) return;
        foreach (var b in msg.bonuses)
        {
            if (b.id != BonusId) continue;
            if (b.collected)
            {
                // Le serveur confirme que ce bonus a été collecté.
                SetClientVisible(false);
            }
            else
            {
                // Bonus actif : repositionner à la position serveur.
                Respawn(new Vector3(b.x, b.y, b.z));
            }
            return;
        }
        // Bonus absent de la liste serveur = le serveur ne le connaît pas encore
        // (enregistrement en cours ou bonus supplémentaire côté client).
        // On le laisse visible — le serveur validera le TAKE quand on le ramasse.
    }

    void OnRemoteBonusTaken(BonusTakenMessage msg)
    {
        if (!isCollected && msg.bonusId == BonusId)
            SetClientVisible(false);
    }

    void OnRemoteBonusSpawn(BonusSpawnMessage msg)
    {
        if (msg.bonusId != BonusId) return;
        Respawn(new Vector3(msg.x, msg.y, msg.z));
    }

    /// <summary>
    /// Réinitialise le bonus à une nouvelle position (côté serveur ET client).
    /// Côté serveur : appelé par ServerBonusAuthority après le délai de respawn.
    /// Côté client : appelé sur réception de BONUS_SPAWN.
    /// </summary>
    public void Respawn(Vector3 newPos)
    {
        transform.position = newPos;
        isCollected = false;
        enabled = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        SetClientVisible(true);
    }

    void SetClientVisible(bool visible)
    {
        if (ServerMode.Active) return; // côté serveur, géré par SetActive
        isCollected = !visible;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = visible;
    }

    /// <summary>ID stable du bonus (BonusId si renseigné, sinon le nom du GameObject).</summary>
    public string ResolveBonusId()
    {
        return string.IsNullOrEmpty(BonusId) ? gameObject.name : BonusId;
    }

    private bool ShouldHandleObject(Collider other)
    {
        return (CollisionLayers.value & (1 << other.gameObject.layer)) > 0;
    }

    void IgnoreCarCollisions()
    {
        Collider[] bonusColliders = GetComponentsInChildren<Collider>();
        DrivableCar[] cars = FindObjectsByType<DrivableCar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Collider bonusCollider in bonusColliders)
            foreach (DrivableCar car in cars)
                foreach (Collider carCollider in car.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(bonusCollider, carCollider, true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        // Serveur Unity : autorité Physics.
        if (ServerMode.Active)
        {
            ServerPlayerProxy proxy = other.GetComponentInParent<ServerPlayerProxy>();
            if (proxy != null && UnityGameServer.Instance != null)
            {
                isCollected = true;
                enabled = false;
                UnityGameServer.Instance.CollectBonus(ResolveBonusId(), proxy.PlayerId);
                // Ne pas Destroy — ServerBonusAuthority rappelle Respawn() après le délai.
                gameObject.SetActive(false);
            }
            return;
        }

        if (!ShouldHandleObject(other)) return;

        // Client connecté : envoie TAKE au serveur pour validation immédiate.
        // Le serveur reste autoritaire — il rejette les doublons et les ID inconnus.
        if (_net != null && _net.HasSession)
        {
            CharacterController cc = other.GetComponentInParent<CharacterController>();
            if (cc == null) return;
            isCollected = true;
            SetClientVisible(false); // cache immédiatement, le serveur confirme ensuite
            _net.SendTake(ResolveBonusId());
            return;
        }

        // Hors-ligne : comportement local d'origine.
        CharacterScore cScore = other.GetComponentInParent<CharacterScore>()
                             ?? other.GetComponentInChildren<CharacterScore>();
        if (cScore == null)
        {
            CharacterController cc = other.GetComponentInParent<CharacterController>();
            if (cc != null) cScore = cc.GetComponentInChildren<CharacterScore>();
        }

        if (cScore != null)
        {
            isCollected = true;
            enabled = false;
            cScore.AddScore(Points);
            CharacterController controller = FindCollectorController(other, cScore);
            if (controller != null) controller.ApplyBonusSpeedBoost();
            ScorePanelHUD.ShowPickupMessage(controller);
            Destroy(gameObject); // hors-ligne seulement : ok de détruire
        }
    }

    CharacterController FindCollectorController(Collider other, CharacterScore cScore)
    {
        return other.GetComponentInParent<CharacterController>()
            ?? cScore.GetComponentInParent<CharacterController>()
            ?? cScore.GetComponentInChildren<CharacterController>();
    }
}
