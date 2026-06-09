using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autorité serveur des voitures. Enregistre les <see cref="DrivableCar"/> de la scène dans le
/// <see cref="WorldState"/>, valide les demandes CAR_ENTER/CAR_EXIT et conduit les voitures occupées
/// à partir des intentions réseau du conducteur. Réécrit les positions des voitures pour le broadcast STATE.
/// </summary>
public class ServerCarAuthority : MonoBehaviour
{
    public float EnterRadius = 4f;

    UnityGameServer _server;
    readonly Dictionary<string, DrivableCar> _cars = new Dictionary<string, DrivableCar>();
    readonly Dictionary<string, Vector2> _driverInput = new Dictionary<string, Vector2>();

    public void Bind(UnityGameServer server)
    {
        _server = server;
        _server.CarEnterRequested += OnEnterRequested;
        _server.CarExitRequested += OnExitRequested;
        _server.PlayerInputReceived += OnPlayerInput;
    }

    void OnDestroy()
    {
        if (_server == null) return;
        _server.CarEnterRequested -= OnEnterRequested;
        _server.CarExitRequested -= OnExitRequested;
        _server.PlayerInputReceived -= OnPlayerInput;
    }

    void Start()
    {
        StartCoroutine(RegisterCarsDeferred());
    }

    IEnumerator RegisterCarsDeferred()
    {
        // DrivableCar.AutoSetupSceneCars tourne en AfterSceneLoad ; on attend un peu.
        yield return null;
        yield return new WaitForSeconds(0.2f);
        RegisterAllCars();
    }

    void RegisterAllCars()
    {
        if (_server == null) return;

        DrivableCar[] cars = FindObjectsByType<DrivableCar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var car in cars)
        {
            string id = ResolveCarId(car);
            _cars[id] = car;
            car.ServerId = id;
            Vector3 p = car.transform.position;
            _server.World.AddOrUpdateCar(id, p.x, p.y, p.z, car.transform.eulerAngles.y);
        }

        Debug.Log($"[ServerCarAuthority] {cars.Length} voitures enregistrées.");
    }

    string ResolveCarId(DrivableCar car)
    {
        string baseId = car.gameObject.name;
        if (!_cars.ContainsKey(baseId)) return baseId;

        int i = 2;
        while (_cars.ContainsKey(baseId + "_" + i)) i++;
        return baseId + "_" + i;
    }

    void OnPlayerInput(string playerId, float ix, float iz, float rotY)
    {
        // Pour un conducteur : ix = braquage, iz = accélération.
        _driverInput[playerId] = new Vector2(ix, iz);
    }

    void OnEnterRequested(string playerId, string carId)
    {
        if (_server == null) return;
        if (!_server.World.Players.TryGetValue(playerId, out var player)) return;
        if (!string.IsNullOrEmpty(player.InCarId)) return;

        DrivableCar car = !string.IsNullOrEmpty(carId) && _cars.ContainsKey(carId)
            ? _cars[carId]
            : FindNearestFreeCar(player);

        if (car == null) return;
        string resolvedId = car.ServerId;

        if (!_server.World.TryEnterCar(resolvedId, playerId)) return;

        car.BeginNetworkDrive();
        _server.NotifyCarEntered(resolvedId, playerId);
        Debug.Log($"[ServerCarAuthority] {playerId} monte dans {resolvedId}");
    }

    void OnExitRequested(string playerId)
    {
        if (_server == null) return;
        if (!_server.World.Players.TryGetValue(playerId, out var player)) return;
        if (string.IsNullOrEmpty(player.InCarId)) return;

        string carId = player.InCarId;
        if (!_server.World.TryExitCar(playerId)) return;

        if (_cars.TryGetValue(carId, out var car))
        {
            car.EndNetworkDrive();
            // Repositionne le joueur à côté de la voiture.
            var proxy = FindProxy(playerId);
            if (proxy != null)
                proxy.transform.position = car.GetExitPosition();
        }

        _server.NotifyCarExited(carId);
        Debug.Log($"[ServerCarAuthority] {playerId} descend de {carId}");
    }

    void FixedUpdate()
    {
        if (_server == null) return;

        foreach (var kvp in _cars)
        {
            DrivableCar car = kvp.Value;
            if (car == null) continue;

            string carId = kvp.Key;
            if (_server.World.Cars.TryGetValue(carId, out var carState))
            {
                if (!string.IsNullOrEmpty(carState.DriverId) && car.IsNetworkDriven)
                {
                    Vector2 input = _driverInput.TryGetValue(carState.DriverId, out var v) ? v : Vector2.zero;
                    car.SetNetworkInput(input);
                }

                Vector3 p = car.transform.position;
                carState.X = p.x;
                carState.Y = p.y;
                carState.Z = p.z;
                carState.RotY = car.transform.eulerAngles.y;
            }
        }
    }

    DrivableCar FindNearestFreeCar(PlayerState player)
    {
        DrivableCar best = null;
        float bestDist = EnterRadius * EnterRadius;
        Vector3 playerPos = new Vector3(player.X, player.Y, player.Z);

        foreach (var car in _cars.Values)
        {
            if (car == null) continue;
            if (_server.World.Cars.TryGetValue(car.ServerId, out var cs) && !string.IsNullOrEmpty(cs.DriverId))
                continue;

            float d = (car.transform.position - playerPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = car;
            }
        }

        return best;
    }

    ServerPlayerProxy FindProxy(string playerId)
    {
        foreach (var proxy in FindObjectsByType<ServerPlayerProxy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (proxy.PlayerId == playerId) return proxy;
        return null;
    }
}
