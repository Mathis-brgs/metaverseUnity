using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerManager : MonoBehaviour
{
    public GameObject RemotePlayerPrefab;

    NetworkManager _net;
    readonly Dictionary<string, GameObject> _remotes = new Dictionary<string, GameObject>();

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
            SpawnRemote(p.id, p.x, p.y, p.z, p.rotY);
        }
    }

    void HandlePlayerJoin(PlayerJoinMessage msg)
    {
        SpawnRemote(msg.id, msg.x, msg.y, msg.z, 0f);
    }

    void HandlePlayerLeft(PlayerLeftMessage msg)
    {
        if (!_remotes.TryGetValue(msg.id, out var go)) return;
        Destroy(go);
        _remotes.Remove(msg.id);
    }

    void HandleState(StateMessage msg)
    {
        if (msg.players == null) return;
        foreach (var p in msg.players)
        {
            if (p.id == _net.MyPlayerId) continue;
            if (!_remotes.TryGetValue(p.id, out var go)) continue;
            go.transform.position = new Vector3(p.x, p.y, p.z);
            go.transform.eulerAngles = new Vector3(0f, p.rotY, 0f);
        }
    }

    void SpawnRemote(string id, float x, float y, float z, float rotY)
    {
        if (_remotes.ContainsKey(id) || RemotePlayerPrefab == null) return;
        var go = Instantiate(RemotePlayerPrefab, new Vector3(x, y, z), Quaternion.Euler(0f, rotY, 0f));
        go.name = "RemotePlayer_" + id;

        // Désactiver l'input local sur le joueur distant
        var controller = go.GetComponentInChildren<CharacterController>();
        if (controller != null) controller.enabled = false;

        _remotes[id] = go;
    }
}
