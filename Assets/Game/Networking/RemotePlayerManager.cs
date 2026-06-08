using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerManager : MonoBehaviour
{
    [System.Serializable]
    public struct CharacterPrefabEntry
    {
        public string Name;
        public GameObject Prefab;
    }

    public CharacterPrefabEntry[] CharacterPrefabs;

    NetworkManager _net;
    readonly Dictionary<string, GameObject> _spawned = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Vector3> _targetPos = new Dictionary<string, Vector3>();
    readonly Dictionary<string, float> _targetRotY = new Dictionary<string, float>();

    void Awake()
    {
        _net = GetComponent<NetworkManager>();
        if (_net == null) _net = FindFirstObjectByType<NetworkManager>();
    }

    void OnEnable()
    {
        if (_net == null) return;
        _net.OnInitState.AddListener(HandleInitState);
        _net.OnPlayerJoin.AddListener(HandlePlayerJoin);
        _net.OnPlayerLeft.AddListener(HandlePlayerLeft);
        _net.OnState.AddListener(HandleState);
    }

    void OnDisable()
    {
        if (_net == null) return;
        _net.OnInitState.RemoveListener(HandleInitState);
        _net.OnPlayerJoin.RemoveListener(HandlePlayerJoin);
        _net.OnPlayerLeft.RemoveListener(HandlePlayerLeft);
        _net.OnState.RemoveListener(HandleState);
    }

    void HandleInitState(InitStateMessage msg)
    {
        if (msg.players == null) return;
        foreach (var p in msg.players)
        {
            if (p.id == msg.playerId) continue;
            SpawnRemote(p.id, p.character, p.x, p.y, p.z, p.rotY);
        }
    }

    void HandlePlayerJoin(PlayerJoinMessage msg)
    {
        SpawnRemote(msg.id, msg.character, msg.x, msg.y, msg.z, 0f);
    }

    void HandlePlayerLeft(PlayerLeftMessage msg)
    {
        if (!_spawned.TryGetValue(msg.id, out var go)) return;
        Destroy(go);
        _spawned.Remove(msg.id);
        _targetPos.Remove(msg.id);
        _targetRotY.Remove(msg.id);
    }

    void FixedUpdate()
    {
        foreach (var kvp in _spawned)
        {
            if (!_targetPos.TryGetValue(kvp.Key, out var tPos)) continue;
            // Collider statique : on bouge via transform pour ne pas faire entrer le Rigidbody
            // dans la boucle physique (il a été détruit). Physics.SyncTransforms() assure
            // que le CapsuleCollider suit immédiatement.
            kvp.Value.transform.position = Vector3.Lerp(
                kvp.Value.transform.position, tPos, Time.fixedDeltaTime * 12f);
            if (_targetRotY.TryGetValue(kvp.Key, out var tRot))
            {
                var e = kvp.Value.transform.eulerAngles;
                e.y = Mathf.LerpAngle(e.y, tRot, Time.fixedDeltaTime * 12f);
                kvp.Value.transform.eulerAngles = e;
            }
        }
        Physics.SyncTransforms();
    }

    void HandleState(StateMessage msg)
    {
        if (msg.players == null) return;
        foreach (var p in msg.players)
        {
            if (p.id == _net.MyPlayerId) continue;
            if (!_spawned.ContainsKey(p.id)) continue;
            _targetPos[p.id] = new Vector3(p.x, p.y, p.z);
            _targetRotY[p.id] = p.rotY;
        }
    }

    void SpawnRemote(string id, string character, float x, float y, float z, float rotY)
    {
        if (_spawned.ContainsKey(id)) return;

        GameObject prefab = FindPrefab(character);
        if (prefab == null) return;

        var go = Instantiate(prefab, new Vector3(x, y, z), Quaternion.Euler(0f, rotY, 0f));
        go.name = "RemotePlayer_" + id;

        // Désactive le script de mouvement/input
        var controller = go.GetComponentInChildren<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        // Supprime le Rigidbody : le CapsuleCollider devient un collider statique.
        // Un collider statique bloque TOUJOURS un Rigidbody.MovePosition() — comme un mur.
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
            Destroy(rb);

        _spawned[id] = go;
    }

    GameObject FindPrefab(string characterName)
    {
        if (!string.IsNullOrEmpty(characterName))
            foreach (var entry in CharacterPrefabs)
                if (entry.Name == characterName && entry.Prefab != null)
                    return entry.Prefab;

        foreach (var entry in CharacterPrefabs)
            if (entry.Prefab != null) return entry.Prefab;

        return null;
    }
}
