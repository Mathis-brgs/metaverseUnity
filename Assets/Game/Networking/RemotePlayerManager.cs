using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Côté client : instancie/synchronise les joueurs distants et applique les positions autoritaires
/// envoyées par le serveur Unity (joueurs + voitures), avec interpolation.
/// Réconcilie aussi doucement le joueur local si sa position diverge trop du serveur.
/// </summary>
public class RemotePlayerManager : MonoBehaviour
{
    [System.Serializable]
    public struct CharacterPrefabEntry
    {
        public string Name;
        public GameObject Prefab;
    }

    public CharacterPrefabEntry[] CharacterPrefabs;

    [Tooltip("Seuil (m) au-delà duquel le joueur local est resynchronisé sur la position serveur.")]
    public float LocalReconcileThreshold = 2.5f;
    public float InterpolationSpeed = 12f;

    NetworkManager _net;
    readonly Dictionary<string, GameObject> _spawned = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Vector3> _targetPos = new Dictionary<string, Vector3>();
    readonly Dictionary<string, float> _targetRotY = new Dictionary<string, float>();
    readonly Dictionary<string, bool> _inCar = new Dictionary<string, bool>();
    readonly Dictionary<string, DrivableCar> _carsByName = new Dictionary<string, DrivableCar>();
    readonly Dictionary<string, Animator> _animators = new Dictionary<string, Animator>();
    readonly Dictionary<string, Vector3> _prevStatePos = new Dictionary<string, Vector3>();
    const float StateInterval = 0.05f; // 20 Hz — aligné avec le serveur
    const float WalkSpeedRef = 3f;     // CharacterController.WalkSpeed par défaut

    void Awake()
    {
        _net = GetComponent<NetworkManager>();
        if (_net == null) _net = FindFirstObjectByType<NetworkManager>();
    }

    void OnEnable()
    {
        if (_net == null) return;
        _net.OnInitState.AddListener(HandleInitState);
        _net.OnInitState.AddListener(ApplyInitialCars);
        _net.OnPlayerJoin.AddListener(HandlePlayerJoin);
        _net.OnPlayerLeft.AddListener(HandlePlayerLeft);
        _net.OnState.AddListener(HandleState);
    }

    void OnDisable()
    {
        if (_net == null) return;
        _net.OnInitState.RemoveListener(HandleInitState);
        _net.OnInitState.RemoveListener(ApplyInitialCars);
        _net.OnPlayerJoin.RemoveListener(HandlePlayerJoin);
        _net.OnPlayerLeft.RemoveListener(HandlePlayerLeft);
        _net.OnState.RemoveListener(HandleState);
    }

    void HandleInitState(InitStateMessage msg)
    {
        if (msg.players != null)
        {
            foreach (var p in msg.players)
            {
                if (p.id == msg.playerId) continue; // joueur local déjà dans la scène
                SpawnRemote(p.id, p.character, p.x, p.y, p.z, p.rotY);
            }
        }
    }

    void ApplyInitialCars(InitStateMessage msg)
    {
        if (msg.cars != null)
            ApplyCars(msg.cars);
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
        _inCar.Remove(msg.id);
        _animators.Remove(msg.id);
        _prevStatePos.Remove(msg.id);
    }

    void Update()
    {
        float t = Time.deltaTime * InterpolationSpeed;
        foreach (var kvp in _spawned)
        {
            // Joueur en voiture : caché, pas d'interpolation au sol.
            if (_inCar.TryGetValue(kvp.Key, out bool inCar) && inCar) continue;
            if (!_targetPos.TryGetValue(kvp.Key, out var tPos)) continue;

            kvp.Value.transform.position = Vector3.Lerp(kvp.Value.transform.position, tPos, t);
            if (_targetRotY.TryGetValue(kvp.Key, out var tRot))
            {
                var e = kvp.Value.transform.eulerAngles;
                e.y = Mathf.LerpAngle(e.y, tRot, t);
                kvp.Value.transform.eulerAngles = e;
            }
        }
    }

    void HandleState(StateMessage msg)
    {
        if (msg.players != null)
        {
            foreach (var p in msg.players)
            {
                if (p.id == _net.MyPlayerId)
                {
                    ReconcileLocal(p);
                    continue;
                }

                if (!_spawned.ContainsKey(p.id)) continue;

                bool inCar = !string.IsNullOrEmpty(p.inCarId);
                _inCar[p.id] = inCar;
                SetRemoteVisible(p.id, !inCar);

                if (!inCar)
                {
                    var newPos = new Vector3(p.x, p.y, p.z);
                    if (_prevStatePos.TryGetValue(p.id, out var prev))
                    {
                        float walkAmount = Mathf.Clamp01(Vector3.Distance(prev, newPos) / (StateInterval * WalkSpeedRef));
                        if (_animators.TryGetValue(p.id, out var anim) && anim != null)
                            anim.SetFloat("Walk", walkAmount);
                    }
                    _prevStatePos[p.id] = newPos;
                    _targetPos[p.id] = newPos;
                    _targetRotY[p.id] = p.rotY;
                }
            }
        }

        if (msg.cars != null)
        {
            foreach (var c in msg.cars)
            {
                DrivableCar car = ResolveCar(c.id);
                if (car != null)
                    car.ApplyNetworkTransform(new Vector3(c.x, c.y, c.z), c.rotY);
            }
        }
    }

    void ReconcileLocal(NetPlayerPosition serverPos)
    {
        if (_net.InputSource == null) return;
        if (_net.InputSource.IsDrivingCar) return; // en voiture : suivi via le siège

        Transform local = _net.InputSource.transform;
        Vector3 target = new Vector3(serverPos.x, serverPos.y, serverPos.z);
        if ((local.position - target).sqrMagnitude > LocalReconcileThreshold * LocalReconcileThreshold)
            local.position = target;
    }

    void ApplyCars(NetCarSnapshot[] cars)
    {
        foreach (var c in cars)
        {
            DrivableCar car = ResolveCar(c.id);
            if (car != null)
                car.ApplyNetworkTransform(new Vector3(c.x, c.y, c.z), c.rotY);
        }
    }

    void ApplyCars(NetCarPosition[] cars)
    {
        foreach (var c in cars)
        {
            DrivableCar car = ResolveCar(c.id);
            if (car != null)
                car.ApplyNetworkTransform(new Vector3(c.x, c.y, c.z), c.rotY);
        }
    }

    void SetRemoteVisible(string id, bool visible)
    {
        if (!_spawned.TryGetValue(id, out var go) || go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    DrivableCar ResolveCar(string carId)
    {
        if (string.IsNullOrEmpty(carId)) return null;
        if (_carsByName.TryGetValue(carId, out var cached) && cached != null)
            return cached;

        foreach (var car in FindObjectsByType<DrivableCar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (car.ServerId == carId || car.gameObject.name == carId)
            {
                car.ServerId = carId;
                _carsByName[carId] = car;
                return car;
            }
        }
        return null;
    }

    void SpawnRemote(string id, string character, float x, float y, float z, float rotY)
    {
        if (_spawned.ContainsKey(id)) return;

        GameObject prefab = FindPrefab(character);
        if (prefab == null) return;

        var go = Instantiate(prefab, new Vector3(x, y, z), Quaternion.Euler(0f, rotY, 0f));
        go.name = "RemotePlayer_" + id;

        var controller = go.GetComponentInChildren<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) _animators[id] = anim;

        var startPos = new Vector3(x, y, z);
        _spawned[id] = go;
        _targetPos[id] = startPos;
        _targetRotY[id] = rotY;
        _prevStatePos[id] = startPos;
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
