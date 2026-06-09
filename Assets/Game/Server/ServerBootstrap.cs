using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Point d'entrée du serveur Unity. Quand l'instance tourne en mode serveur (voir <see cref="ServerMode"/>),
/// désactive le rendu/audio/contrôles client, démarre <see cref="UnityGameServer"/> et branche les autorités
/// de scène (proxies joueurs, bonus, voitures).
///
/// S'auto-instancie après chargement de la scène, suivant la convention du projet
/// (cf. ScorePanelHUD, DrivableCar, ConnectionUI).
/// </summary>
public class ServerBootstrap : MonoBehaviour
{
    public GameObject PlayerProxyPrefab; // optionnel : sinon une capsule est générée

    UnityGameServer _server;
    ServerCarAuthority _cars;
    readonly Dictionary<string, ServerPlayerProxy> _proxies = new Dictionary<string, ServerPlayerProxy>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (!ServerMode.Active) return;
        if (FindFirstObjectByType<ServerBootstrap>() != null) return;

        var go = new GameObject("ServerBootstrap");
        go.AddComponent<ServerBootstrap>();
    }

    void Awake()
    {
        if (!ServerMode.Active)
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log("[ServerBootstrap] Mode serveur Unity actif — désactivation des systèmes client.");
        DisableClientSystems();

        _server = gameObject.AddComponent<UnityGameServer>();
        _server.PlayerJoined += OnPlayerJoined;
        _server.PlayerLeft += OnPlayerLeft;
        _server.PlayerInputReceived += OnPlayerInput;
        _server.PlayerTeleport += OnPlayerTeleport;

        // Autorités de scène
        gameObject.AddComponent<ServerBonusAuthority>().Bind(_server);
        _cars = gameObject.AddComponent<ServerCarAuthority>();
        _cars.Bind(_server);

        _server.StartServer();
    }

    void OnDestroy()
    {
        if (_server == null) return;
        _server.PlayerJoined -= OnPlayerJoined;
        _server.PlayerLeft -= OnPlayerLeft;
        _server.PlayerInputReceived -= OnPlayerInput;
        _server.PlayerTeleport -= OnPlayerTeleport;
    }

    // ---------------- Proxies joueurs ----------------

    void OnPlayerJoined(PlayerState player)
    {
        if (_proxies.ContainsKey(player.Id)) return;

        GameObject go;
        if (PlayerProxyPrefab != null)
            go = Instantiate(PlayerProxyPrefab, Vector3.zero, Quaternion.identity);
        else
            go = CreateDefaultProxy();

        go.name = "ServerProxy_" + player.Id;
        var proxy = go.GetComponent<ServerPlayerProxy>();
        if (proxy == null) proxy = go.AddComponent<ServerPlayerProxy>();
        proxy.Init(player, _server.World);
        _proxies[player.Id] = proxy;
    }

    void OnPlayerLeft(string playerId)
    {
        if (_proxies.TryGetValue(playerId, out var proxy))
        {
            if (proxy != null) Destroy(proxy.gameObject);
            _proxies.Remove(playerId);
        }
    }

    void OnPlayerInput(string playerId, float ix, float iz, float rotY)
    {
        if (_proxies.TryGetValue(playerId, out var proxy) && proxy != null)
            proxy.SetInput(ix, iz, rotY);
    }

    // Client legacy (MOVE) : on place le proxy à la position annoncée pour que les triggers fonctionnent.
    void OnPlayerTeleport(string playerId, float x, float y, float z, float rotY)
    {
        if (_proxies.TryGetValue(playerId, out var proxy) && proxy != null)
            proxy.ApplyDirectPosition(new Vector3(x, y, z), rotY);
    }

    GameObject CreateDefaultProxy()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        // Pas de rendu nécessaire côté serveur.
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;
        return go;
    }

    // ---------------- Désactivation client ----------------

    void DisableClientSystems()
    {
        ConnectionUI.Enabled = false;

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            cam.enabled = false;
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            listener.enabled = false;
        foreach (var orbit in FindObjectsByType<CameraMouseOrbit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            orbit.enabled = false;

        foreach (var face in FindObjectsByType<FaceCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            face.enabled = false;

        foreach (var hud in FindObjectsByType<ScorePanelHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hud.gameObject.SetActive(false);

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            canvas.enabled = false;

        foreach (var ambience in FindObjectsByType<CityAmbienceSound>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ambience.enabled = false;

        // Décor purement visuel / audio — inutile côté serveur.
        foreach (var bird in FindObjectsByType<Bird>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            bird.enabled = false;

        // Joueurs locaux pilotés au clavier : inutiles côté serveur (les proxies font autorité).
        foreach (var controller in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            controller.enabled = false;

        // Le client réseau ne doit pas tourner sur le serveur.
        foreach (var nm in FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            nm.enabled = false;
        foreach (var rpm in FindObjectsByType<RemotePlayerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            rpm.enabled = false;
    }
}
