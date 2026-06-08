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
    readonly Dictionary<string, Rigidbody> _rigidbodies = new Dictionary<string, Rigidbody>();
    readonly Dictionary<string, Animator> _animators = new Dictionary<string, Animator>();
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
        _rigidbodies.Remove(msg.id);
        _animators.Remove(msg.id);
        _targetPos.Remove(msg.id);
        _targetRotY.Remove(msg.id);
    }

    void FixedUpdate()
    {
        foreach (var kvp in _spawned)
        {
            if (!_targetPos.TryGetValue(kvp.Key, out var tPos)) continue;
            if (!_rigidbodies.TryGetValue(kvp.Key, out var rb) || rb == null) continue;

            Vector3 prevPos = rb.position;
            Vector3 nextPos = Vector3.Lerp(prevPos, tPos, Time.fixedDeltaTime * 12f);

            // MovePosition comme le joueur local : respecte la physique et la collision
            rb.MovePosition(nextPos);

            if (_targetRotY.TryGetValue(kvp.Key, out var tRot))
                rb.MoveRotation(Quaternion.Euler(0f, Mathf.LerpAngle(rb.rotation.eulerAngles.y, tRot, Time.fixedDeltaTime * 12f), 0f));

            // Anime selon la vitesse de déplacement
            if (_animators.TryGetValue(kvp.Key, out var anim) && anim != null)
            {
                float speed = (nextPos - prevPos).magnitude / Time.fixedDeltaTime;
                anim.SetFloat("Walk", Mathf.Clamp01(speed / 3f));
            }
        }
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

        // Désactive uniquement le script d'input — le Rigidbody reste non-kinematic
        // pour que la collision fonctionne exactement comme entre deux joueurs locaux
        var controller = go.GetComponentInChildren<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
            _rigidbodies[id] = rb;

        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) _animators[id] = anim;

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
