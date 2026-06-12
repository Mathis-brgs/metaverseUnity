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
        _server.CarEnterRequested   += OnEnterRequested;
        _server.CarExitRequested    += OnExitRequested;
        _server.PlayerInputReceived += OnPlayerInput;
        _server.PlayerLeft          += OnPlayerLeft;
    }

    void OnDestroy()
    {
        if (_server == null) return;
        _server.CarEnterRequested   -= OnEnterRequested;
        _server.CarExitRequested    -= OnExitRequested;
        _server.PlayerInputReceived -= OnPlayerInput;
        _server.PlayerLeft          -= OnPlayerLeft;
    }

    void OnPlayerLeft(string playerId)
    {
        _driverInput.Remove(playerId);

        // Si ce joueur conduisait une voiture, la libérer.
        foreach (var kv in _server.World.Cars)
        {
            if (kv.Value.DriverId != playerId) continue;
            if (_cars.TryGetValue(kv.Key, out var car) && car != null)
                car.EndNetworkDrive();
            // WorldState déjà nettoyé par RemovePlayer → TryExitCar.
            break;
        }
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

        _cars.Clear();
        DrivableCar.AssignNetworkIds(_cars);
        foreach (var kvp in _cars)
        {
            var car = kvp.Value;
            Vector3 p = car.transform.position;
            _server.World.AddOrUpdateCar(kvp.Key, p.x, p.y, p.z, car.transform.eulerAngles.y);
        }

        Debug.Log($"[ServerCarAuthority] {_cars.Count} voitures enregistrées.");
    }

    void OnPlayerInput(string playerId, float ix, float iz, float rotY, float y)
    {
        // Pour un conducteur : ix = braquage, iz = accélération.
        _driverInput[playerId] = new Vector2(ix, iz);
    }

    void OnEnterRequested(string playerId, string carId)
    {
        if (_server == null) return;
        if (!_server.World.Players.TryGetValue(playerId, out var player)) return;
        if (!string.IsNullOrEmpty(player.InCarId)) return;

        Vector3 playerPos = GetPlayerWorldPosition(playerId, player);
        DrivableCar car = ResolveEnterCar(carId, playerPos, player);

        if (car == null) return;
        string resolvedId = car.ServerId;

        if (!_server.World.TryEnterCar(resolvedId, playerId)) return;

        car.BeginNetworkDrive();
        SyncDriverToCar(playerId, player, car);
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

            // Avant de calculer la position de sortie, on aligne le transform serveur
            // sur le WorldState (mis à jour par la position du client dans chaque INPUT).
            // Sans ce snap, car.GetExitPosition() utilise la position physique du serveur
            // qui a divergé de celle du client → le joueur se téléporte à la sortie.
            if (_server.World.Cars.TryGetValue(carId, out var cs))
                car.SnapToPosition(new Vector3(cs.X, cs.Y, cs.Z), cs.RotY);

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
                    // Le client envoie sa position de voiture dans chaque INPUT (client-autoritaire).
                    // Ne pas écraser carState avec la physique serveur — HandleInput l'a déjà mis à jour.
                }
                else
                {
                    // Pas de conducteur : la physique serveur (ou animation) fait autorité.
                    Vector3 p = car.transform.position;
                    carState.X = p.x;
                    carState.Y = p.y;
                    carState.Z = p.z;
                    carState.RotY = car.transform.eulerAngles.y;
                }

                if (!string.IsNullOrEmpty(carState.DriverId)
                    && _server.World.Players.TryGetValue(carState.DriverId, out var driver))
                {
                    driver.X = carState.X;
                    driver.Y = carState.Y;
                    driver.Z = carState.Z;
                    driver.RotY = carState.RotY;
                    SyncDriverToCar(carState.DriverId, driver, car);
                }
            }
        }
    }

    Vector3 GetPlayerWorldPosition(string playerId, PlayerState player)
    {
        var proxy = FindProxy(playerId);
        if (proxy != null) return proxy.transform.position;
        return new Vector3(player.X, player.Y, player.Z);
    }

    DrivableCar ResolveEnterCar(string carId, Vector3 playerPos, PlayerState player)
    {
        if (!string.IsNullOrEmpty(carId) && _cars.TryGetValue(carId, out var requested))
        {
            if (_server.World.Cars.TryGetValue(carId, out var cs) && !string.IsNullOrEmpty(cs.DriverId))
                return null;
            float d = (requested.transform.position - playerPos).sqrMagnitude;
            if (d <= EnterRadius * EnterRadius) return requested;
        }

        return FindNearestFreeCar(playerPos);
    }

    void SyncDriverToCar(string playerId, PlayerState player, DrivableCar car)
    {
        Vector3 seatPos = car.Seat != null ? car.Seat.position : car.transform.position;
        float rotY = car.transform.eulerAngles.y;
        player.X = seatPos.x;
        player.Y = seatPos.y;
        player.Z = seatPos.z;
        player.RotY = rotY;

        var proxy = FindProxy(playerId);
        if (proxy != null)
        {
            proxy.transform.position = seatPos;
            proxy.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
    }

    DrivableCar FindNearestFreeCar(Vector3 playerPos)
    {
        DrivableCar best = null;
        float bestDist = EnterRadius * EnterRadius;

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
