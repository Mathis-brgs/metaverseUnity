using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autorité serveur des bonus.
/// - Enregistre les bonus de la scène dans le WorldState.
/// - Re-scanne les bonus quand le premier joueur se connecte (robustesse au démarrage).
/// - Remet tous les bonus en jeu quand tous les joueurs se déconnectent (reset inter-sessions).
/// - Fait réapparaître chaque bonus après un délai, à une position aléatoire dans un rayon.
/// - Remet tous les bonus en jeu immédiatement sur GAME_OVER (GameReset).
/// </summary>
public class ServerBonusAuthority : MonoBehaviour
{
    public float RespawnDelay  = 10f;
    public float RespawnRadius = 15f;

    UnityGameServer _server;
    readonly Dictionary<string, Vector3>  _originalPos    = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Bonus>    _bonusObjects   = new Dictionary<string, Bonus>();
    readonly HashSet<string>              _pendingRespawn = new HashSet<string>();

    public void Bind(UnityGameServer server)
    {
        _server = server;
        _server.BonusTaken   += OnBonusTaken;
        _server.GameReset    += OnGameReset;
        _server.PlayerJoined += OnPlayerJoined;
        _server.PlayerLeft   += OnPlayerLeft;
    }

    void OnDestroy()
    {
        if (_server == null) return;
        _server.BonusTaken   -= OnBonusTaken;
        _server.GameReset    -= OnGameReset;
        _server.PlayerJoined -= OnPlayerJoined;
        _server.PlayerLeft   -= OnPlayerLeft;
    }

    void Start()
    {
        StartCoroutine(RegisterBonusesDeferred());
    }

    // Scan initial au démarrage du serveur (les bonus b0-b14 sont dans la scène).
    IEnumerator RegisterBonusesDeferred()
    {
        yield return null;
        RegisterAllBonuses();
        yield return new WaitForSeconds(0.5f);
        RegisterAllBonuses();
    }

    void OnPlayerJoined(PlayerState player) { }

    // Quand tous les joueurs partent, on remet tous les bonus à leur position d'origine
    // pour que la prochaine session commence avec un plateau complet.
    void OnPlayerLeft(string playerId)
    {
        if (_server.World.Players.Count != 0) return; // il reste des joueurs

        StopAllCoroutines();
        _pendingRespawn.Clear();

        Debug.Log("[ServerBonusAuthority] Tous les joueurs partis — reset des bonus pour la prochaine session.");

        foreach (var kvp in _bonusObjects)
        {
            Bonus bonus = kvp.Value;
            if (bonus == null) continue;

            Vector3 origin = _originalPos.TryGetValue(kvp.Key, out var o) ? o : bonus.transform.position;
            bonus.Respawn(origin);
            // NotifyBonusSpawn met à jour le WorldState (pas de clients à notifier ici).
            _server.NotifyBonusSpawn(kvp.Key, origin.x, origin.y, origin.z);
        }
    }

    void RegisterAllBonuses()
    {
        if (_server == null) return;

        Bonus[] bonuses = FindObjectsByType<Bonus>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int newCount = 0;
        foreach (var bonus in bonuses)
        {
            string id = bonus.ResolveBonusId();
            if (string.IsNullOrEmpty(id)) continue;

            Vector3 p = bonus.transform.position;
            _server.World.AddOrUpdateBonus(id, p.x, p.y, p.z);

            if (!_originalPos.ContainsKey(id))
            {
                _originalPos[id] = p;
                newCount++;
            }

            _bonusObjects[id] = bonus;
        }

        if (newCount > 0)
            Debug.Log($"[ServerBonusAuthority] +{newCount} nouveaux bonus (total {_bonusObjects.Count}).");
    }

    void OnBonusTaken(string bonusId, string byPlayerId)
    {
        if (_pendingRespawn.Contains(bonusId)) return;
        _pendingRespawn.Add(bonusId);
        StartCoroutine(RespawnAfterDelay(bonusId));
    }

    IEnumerator RespawnAfterDelay(string bonusId)
    {
        yield return new WaitForSeconds(RespawnDelay);
        _pendingRespawn.Remove(bonusId);
        RespawnBonus(bonusId);
    }

    void RespawnBonus(string bonusId)
    {
        if (!_bonusObjects.TryGetValue(bonusId, out Bonus bonus) || bonus == null) return;

        Vector3 origin = _originalPos.TryGetValue(bonusId, out var o) ? o : bonus.transform.position;
        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist   = Random.Range(0f, RespawnRadius);
        Vector3 newPos = origin + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

        bonus.Respawn(newPos);
        _server.NotifyBonusSpawn(bonusId, newPos.x, newPos.y, newPos.z);
        Debug.Log($"[ServerBonusAuthority] {bonusId} réapparu à {newPos}");
    }

    void OnGameReset()
    {
        StopAllCoroutines();
        _pendingRespawn.Clear();

        foreach (var kvp in _bonusObjects)
        {
            Bonus bonus = kvp.Value;
            if (bonus == null) continue;

            Vector3 origin = _originalPos.TryGetValue(kvp.Key, out var o) ? o : bonus.transform.position;
            bonus.Respawn(origin);
            _server.NotifyBonusSpawn(kvp.Key, origin.x, origin.y, origin.z);
        }
    }
}
