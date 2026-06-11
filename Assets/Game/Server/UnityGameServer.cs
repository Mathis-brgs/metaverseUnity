using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Serveur de jeu autoritaire intégré à Unity — remplace le serveur console Server/Program.cs.
/// Tourne dans la scène MetaVerse chargée (autorité Physics : bonus, collisions, voitures).
///
/// Threading : la réception UDP tourne sur un thread dédié et empile les datagrammes ;
/// tout le reste (TCP, WorldState, Physics, envois) est traité sur le thread principal Unity.
/// </summary>
public class UnityGameServer : MonoBehaviour
{
    public static UnityGameServer Instance { get; private set; }

    public int TcpPort = 25000;
    public int UdpPort = 25001;
    public float StateBroadcastInterval = 0.05f; // 20 Hz
    public bool LogMessages = true;

    public WorldState World { get; private set; } = new WorldState();

    // Callbacks pour les autorités de scène (proxies joueurs, voitures, bonus).
    public event Action<PlayerState> PlayerJoined;
    public event Action<string> PlayerLeft;
    public event Action<string, float, float, float, float> PlayerInputReceived; // id, ix, iz, rotY, y
    public event Action<string, float, float, float, float> PlayerTeleport; // id, x, y, z, rotY (MOVE legacy)
    public event Action<string, string> CarEnterRequested; // playerId, carId
    public event Action<string> CarExitRequested;          // playerId

    TcpListener _listener;
    UdpClient _udp;
    Thread _udpThread;
    volatile bool _running;

    readonly List<ServerClient> _clients = new List<ServerClient>();
    readonly Dictionary<string, ServerClient> _clientById = new Dictionary<string, ServerClient>();
    readonly Dictionary<string, IPEndPoint> _udpEndpoints = new Dictionary<string, IPEndPoint>();
    readonly ConcurrentQueue<UdpPacket> _udpInbox = new ConcurrentQueue<UdpPacket>();

    int _nextId = 1;
    float _broadcastTimer;

    const float HitComboWindow = 2f;
    const int HitsBeforeKnockDown = 3;
    const float KnockDownDuration = 1.15f;
    const float StandUpDuration = 1.1f;
    readonly Dictionary<string, (int hits, float lastHitTime)> _attackCombos = new Dictionary<string, (int, float)>();

    struct UdpPacket
    {
        public string Json;
        public IPEndPoint Remote;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        StopServer();
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit()
    {
        StopServer();
    }

    public void StartServer()
    {
        if (_running) return;

        try
        {
            _listener = new TcpListener(IPAddress.Any, TcpPort);
            _listener.Start();

            _udp = new UdpClient(UdpPort);
            _running = true;
            _udpThread = new Thread(UdpReceiveLoop) { IsBackground = true, Name = "UnityServerUDP" };
            _udpThread.Start();

            Debug.Log($"[UnityGameServer] Démarré — TCP:{TcpPort} UDP:{UdpPort} | IP {GetLocalIP()}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[UnityGameServer] Échec démarrage : " + ex.Message);
            StopServer();
        }
    }

    public void StopServer()
    {
        _running = false;

        try { _udp?.Close(); } catch { }
        _udp = null;

        if (_udpThread != null)
        {
            try { if (_udpThread.IsAlive) _udpThread.Join(200); } catch { }
            _udpThread = null;
        }

        foreach (var c in _clients) c.Close();
        _clients.Clear();
        _clientById.Clear();
        _udpEndpoints.Clear();

        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    void Update()
    {
        if (!_running) return;

        AcceptNewClients();
        ReadTcpMessages();
        DrainUdpInbox();
    }

    void LateUpdate()
    {
        if (!_running) return;

        _broadcastTimer += Time.deltaTime;
        if (_broadcastTimer < StateBroadcastInterval) return;
        _broadcastTimer = 0f;
        BroadcastState();
    }

    // ---------------- TCP ----------------

    void AcceptNewClients()
    {
        while (_listener.Pending())
        {
            TcpClient tcp = _listener.AcceptTcpClient();
            var client = new ServerClient(tcp, "p" + _nextId++);
            _clients.Add(client);
            if (LogMessages) Debug.Log($"[UnityGameServer] [+] {client.Id} connecté ({_clients.Count} sockets)");
        }
    }

    void ReadTcpMessages()
    {
        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            ServerClient client = _clients[i];

            if (!client.IsConnected)
            {
                HandleDisconnect(client);
                continue;
            }

            while (client.TryReadLine(out string line))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                HandleTcpMessage(client, line);
            }
        }
    }

    void HandleDisconnect(ServerClient client)
    {
        _clients.Remove(client);
        if (!string.IsNullOrEmpty(client.PlayerId))
        {
            _clientById.Remove(client.PlayerId);
            _udpEndpoints.Remove(client.PlayerId);

            bool wasKnown = World.Players.ContainsKey(client.PlayerId);
            World.RemovePlayer(client.PlayerId);

            if (wasKnown)
            {
                PlayerLeft?.Invoke(client.PlayerId);
                Broadcast(new PlayerLeftMessage { type = "PLAYER_LEFT", id = client.PlayerId });
                if (LogMessages) Debug.Log($"[UnityGameServer] [-] {client.PlayerId} déconnecté");
            }
        }
        client.Close();
    }

    void HandleTcpMessage(ServerClient sender, string json)
    {
        if (LogMessages) Debug.Log($"[UnityGameServer] TCP[{sender.Id}] ← {json}");

        NetHeader header = SafeParse<NetHeader>(json);
        if (header == null || string.IsNullOrEmpty(header.type)) return;

        switch (header.type)
        {
            case "JOIN":      HandleJoin(sender, SafeParse<JoinMessage>(json)); break;
            case "TAKE":      HandleTakeLegacy(SafeParse<TakeMessage>(json)); break;
            case "CAR_ENTER": HandleCarEnter(SafeParse<CarEnterMessage>(json)); break;
            case "CAR_EXIT":  HandleCarExit(SafeParse<CarExitMessage>(json)); break;
            case "ATTACK":    HandleAttack(SafeParse<AttackMessage>(json)); break;
            default:
                if (LogMessages) Debug.LogWarning("[UnityGameServer] type TCP inconnu: " + header.type);
                break;
        }
    }

    void HandleJoin(ServerClient sender, JoinMessage msg)
    {
        if (msg == null) return;
        if (!string.IsNullOrEmpty(sender.PlayerId)) return; // déjà JOIN

        if (World.Players.Count >= World.MaxPlayers)
        {
            SendTo(sender, new ErrorMessage { type = "ERROR", message = "Serveur plein" });
            return;
        }

        string id = sender.Id;
        sender.PlayerId = id;
        _clientById[id] = sender;

        bool hasSpawnPos = IsValid(msg.x) && IsValid(msg.y) && IsValid(msg.z);
        var player = new PlayerState
        {
            Id = id,
            Name = string.IsNullOrEmpty(msg.name) ? id : msg.name,
            Character = string.IsNullOrEmpty(msg.character) ? "barbarian" : msg.character,
            X = hasSpawnPos ? msg.x : 0f,
            Y = hasSpawnPos ? msg.y : 0f,
            Z = hasSpawnPos ? msg.z : 0f,
            RotY = IsValid(msg.rotY) ? msg.rotY : 0f,
        };
        World.Players[id] = player;

        // INIT_STATE au nouveau joueur
        SendTo(sender, BuildInitState(id));

        // PLAYER_JOIN aux autres
        Broadcast(new PlayerJoinMessage
        {
            type = "PLAYER_JOIN",
            id = id,
            name = player.Name,
            character = player.Character,
            x = player.X, y = player.Y, z = player.Z
        }, exclude: sender);

        PlayerJoined?.Invoke(player);
        if (LogMessages) Debug.Log($"[UnityGameServer] {id} ({player.Name}/{player.Character}) a rejoint");
    }

    // Legacy : un client non-autoritaire peut encore demander TAKE par id.
    void HandleTakeLegacy(TakeMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.bonusId)) return;
        CollectBonus(msg.bonusId, msg.playerId);
    }

    void HandleCarEnter(CarEnterMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.playerId)) return;
        CarEnterRequested?.Invoke(msg.playerId, msg.carId);
    }

    void HandleCarExit(CarExitMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.playerId)) return;
        CarExitRequested?.Invoke(msg.playerId);
    }

    // ---------------- UDP ----------------

    void UdpReceiveLoop()
    {
        while (_running)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udp.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);
                Debug.Log($"[UDP] reçu {data.Length}b de {remote}");
                _udpInbox.Enqueue(new UdpPacket { Json = json, Remote = remote });
            }
            catch (SocketException) { /* socket fermé */ }
            catch (ObjectDisposedException) { break; }
            catch (Exception) { }
        }
    }

    void DrainUdpInbox()
    {
        while (_udpInbox.TryDequeue(out UdpPacket packet))
        {
            NetHeader header = SafeParse<NetHeader>(packet.Json);
            if (header == null || string.IsNullOrEmpty(header.type)) continue;

            switch (header.type)
            {
                case "MOVE":  HandleMoveLegacy(SafeParse<MoveMessage>(packet.Json), packet.Remote); break;
                case "INPUT": HandleInput(SafeParse<InputMessage>(packet.Json), packet.Remote); break;
            }
        }
    }

    // Legacy : positions directes (utilisé tant que des clients n'envoient pas INPUT).
    void HandleMoveLegacy(MoveMessage msg, IPEndPoint remote)
    {
        if (msg == null || string.IsNullOrEmpty(msg.id)) return;
        if (!World.Players.ContainsKey(msg.id)) return;
        if (!IsValid(msg.x) || !IsValid(msg.y) || !IsValid(msg.z)) return;

        _udpEndpoints[msg.id] = remote;
        World.UpdatePlayerPosition(msg.id, msg.x, msg.y, msg.z, msg.rotY);
        // Déplace aussi le proxy serveur pour que les triggers de bonus se déclenchent au bon endroit.
        PlayerTeleport?.Invoke(msg.id, msg.x, msg.y, msg.z, msg.rotY);
    }

    void HandleInput(InputMessage msg, IPEndPoint remote)
    {
        if (msg == null || string.IsNullOrEmpty(msg.id)) return;
        if (!World.Players.ContainsKey(msg.id)) return;
        if (!IsValid(msg.ix) || !IsValid(msg.iz) || !IsValid(msg.rotY)) return;

        _udpEndpoints[msg.id] = remote;
        float y = IsValid(msg.y) ? msg.y : float.NaN;
        PlayerInputReceived?.Invoke(msg.id, Mathf.Clamp(msg.ix, -1f, 1f), Mathf.Clamp(msg.iz, -1f, 1f), msg.rotY, y);
    }

    void HandleAttack(AttackMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.attackerId) || string.IsNullOrEmpty(msg.targetId)) return;
        if (msg.attackerId == msg.targetId) return;
        if (!World.Players.TryGetValue(msg.attackerId, out var attacker)) return;
        if (!World.Players.TryGetValue(msg.targetId, out var target)) return;
        if (!string.IsNullOrEmpty(attacker.InCarId) || !string.IsNullOrEmpty(target.InCarId)) return;
        if (Time.time < target.StunnedUntil) return;

        float dx = attacker.X - target.X;
        float dz = attacker.Z - target.Z;
        const float maxRange = 2.2f;
        if (dx * dx + dz * dz > maxRange * maxRange) return;

        string comboKey = msg.attackerId + ">" + msg.targetId;
        if (!_attackCombos.TryGetValue(comboKey, out var combo) || Time.time - combo.lastHitTime > HitComboWindow)
            combo = (0, 0f);

        combo.hits++;
        combo.lastHitTime = Time.time;
        int hitIndex = combo.hits;
        bool knockDown = hitIndex >= HitsBeforeKnockDown;

        if (knockDown)
        {
            combo.hits = 0;
            target.StunnedUntil = Time.time + KnockDownDuration + StandUpDuration;
        }

        _attackCombos[comboKey] = combo;

        Broadcast(new PlayerHitMessage
        {
            type = "PLAYER_HIT",
            attackerId = msg.attackerId,
            targetId = msg.targetId,
            attackerX = attacker.X,
            attackerZ = attacker.Z,
            knockDown = knockDown ? 1 : 0,
            hitIndex = hitIndex
        });
        if (LogMessages) Debug.Log($"[UnityGameServer] {msg.attackerId} frappe {msg.targetId} (hit {hitIndex}{(knockDown ? " KO" : "")})");
    }

    // ---------------- Autorité gameplay (appelée par la scène serveur) ----------------

    /// <summary>Collecte autoritaire d'un bonus (appelée par le trigger serveur). Diffuse BONUS_TAKEN.</summary>
    public void CollectBonus(string bonusId, string playerId)
    {
        if (string.IsNullOrEmpty(bonusId)) return;

        bool collected = World.TryCollectBonus(bonusId, playerId);
        if (!collected) return;

        int newScore = World.Players.TryGetValue(playerId, out var p) ? p.Score : 0;
        Broadcast(new BonusTakenMessage
        {
            type = "BONUS_TAKEN",
            bonusId = bonusId,
            byPlayerId = playerId,
            newScore = newScore
        });
        if (LogMessages) Debug.Log($"[UnityGameServer] {playerId} a pris {bonusId} (score {newScore})");
    }

    public void NotifyCarEntered(string carId, string driverId)
    {
        Broadcast(new CarEnteredMessage { type = "CAR_ENTERED", carId = carId, driverId = driverId });
    }

    public void NotifyCarExited(string carId)
    {
        Broadcast(new CarExitedMessage { type = "CAR_EXITED", carId = carId });
    }

    // ---------------- Sérialisation ----------------

    InitStateMessage BuildInitState(string forPlayerId)
    {
        var players = new List<NetPlayerSnapshot>();
        foreach (var p in World.Players.Values)
        {
            players.Add(new NetPlayerSnapshot
            {
                id = p.Id, name = p.Name, character = p.Character,
                x = p.X, y = p.Y, z = p.Z, rotY = p.RotY, score = p.Score,
                inCarId = p.InCarId ?? ""
            });
        }

        var bonuses = new List<NetBonusSnapshot>();
        foreach (var b in World.Bonuses.Values)
        {
            if (b.IsCollected) continue;
            bonuses.Add(new NetBonusSnapshot { id = b.Id, x = b.X, y = b.Y, z = b.Z });
        }

        var cars = new List<NetCarSnapshot>();
        foreach (var c in World.Cars.Values)
        {
            cars.Add(new NetCarSnapshot { id = c.Id, x = c.X, y = c.Y, z = c.Z, rotY = c.RotY, driverId = c.DriverId ?? "" });
        }

        return new InitStateMessage
        {
            type = "INIT_STATE",
            playerId = forPlayerId,
            players = players.ToArray(),
            bonuses = bonuses.ToArray(),
            cars = cars.ToArray()
        };
    }

    void BroadcastState()
    {
        var players = new List<NetPlayerPosition>();
        foreach (var p in World.Players.Values)
        {
            // On n'envoie la position que si on a déjà reçu un MOVE/INPUT (position réelle, pas (0,0,0) par défaut)
            if (!_udpEndpoints.ContainsKey(p.Id)) continue;
            players.Add(new NetPlayerPosition
            {
                id = p.Id, x = p.X, y = p.Y, z = p.Z, rotY = p.RotY, inCarId = p.InCarId ?? ""
            });
        }

        var cars = new List<NetCarPosition>();
        foreach (var c in World.Cars.Values)
        {
            cars.Add(new NetCarPosition { id = c.Id, x = c.X, y = c.Y, z = c.Z, rotY = c.RotY, driverId = c.DriverId ?? "" });
        }

        var state = new StateMessage { type = "STATE", players = players.ToArray(), cars = cars.ToArray() };
        // STATE envoyé via TCP (Fly.io ne SNAT pas le UDP sortant, les clients ne reçoivent pas les réponses UDP)
        Broadcast(state);
    }

    // ---------------- Envoi TCP ----------------

    void SendTo(ServerClient client, object payload)
    {
        client.Send(JsonUtility.ToJson(payload) + "\n");
    }

    void Broadcast(object payload, ServerClient exclude = null)
    {
        string line = JsonUtility.ToJson(payload) + "\n";
        foreach (var c in _clients)
        {
            if (c == exclude) continue;
            c.Send(line);
        }
    }

    // ---------------- Utilitaires ----------------

    static T SafeParse<T>(string json) where T : class
    {
        try { return JsonUtility.FromJson<T>(json); }
        catch { return null; }
    }

    static bool IsValid(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    public static string GetLocalIP()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}

/// <summary>Connexion TCP d'un client côté serveur, avec découpage par '\n' (non bloquant).</summary>
public class ServerClient
{
    public string Id { get; }
    public string PlayerId { get; set; }

    readonly TcpClient _tcp;
    readonly NetworkStream _stream;
    readonly StringBuilder _buffer = new StringBuilder();
    readonly byte[] _readChunk = new byte[4096];
    bool _disconnected;

    public ServerClient(TcpClient tcp, string id)
    {
        _tcp = tcp;
        Id = id;
        _tcp.NoDelay = true;
        _stream = tcp.GetStream();
    }

    public bool IsConnected
    {
        get
        {
            if (_disconnected) return false;
            try
            {
                if (!_tcp.Connected) return false;
                // Poll + Available == 0 => fermeture distante
                if (_tcp.Client.Poll(0, SelectMode.SelectRead) && _tcp.Available == 0)
                {
                    _disconnected = true;
                    return false;
                }
                return true;
            }
            catch { _disconnected = true; return false; }
        }
    }

    public bool TryReadLine(out string line)
    {
        line = null;

        try
        {
            while (_tcp.Available > 0)
            {
                int read = _stream.Read(_readChunk, 0, Math.Min(_readChunk.Length, _tcp.Available));
                if (read <= 0) { _disconnected = true; break; }
                _buffer.Append(Encoding.UTF8.GetString(_readChunk, 0, read));
            }
        }
        catch { _disconnected = true; }

        string all = _buffer.ToString();
        int idx = all.IndexOf('\n');
        if (idx < 0) return false;

        line = all.Substring(0, idx).TrimEnd('\r');
        _buffer.Clear();
        _buffer.Append(all.Substring(idx + 1));
        return true;
    }

    public void Send(string message)
    {
        if (_disconnected) return;
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            _stream.Write(bytes, 0, bytes.Length);
        }
        catch { _disconnected = true; }
    }

    public void Close()
    {
        _disconnected = true;
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
    }
}
