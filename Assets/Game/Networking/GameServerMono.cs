using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Serveur Unity — remplace Server/Program.cs.
/// Physique et collisions gérées par Unity Physics côté serveur.
/// Ajouter sur un GameObject vide dans la scène. Activer sur le host, désactiver sur les clients.
/// </summary>
public class GameServerMono : MonoBehaviour
{
    [Header("Réseau")]
    public int   TcpPort       = 25000;
    public int   UdpPort       = 25001;
    public float StateInterval = 0.05f;

    // ── Réseau ────────────────────────────────────────────────────────────────
    TcpListener  _tcpListener;
    UdpClient    _udpSocket;

    readonly List<SrvClient>                 _clients = new List<SrvClient>();
    readonly Dictionary<string, IPEndPoint>  _udpEPs  = new Dictionary<string, IPEndPoint>();
    readonly object _netLock = new object();

    // ── World state ───────────────────────────────────────────────────────────
    readonly SrvWorldState _world     = new SrvWorldState();
    readonly object        _worldLock = new object();

    // ── File thread → main thread ─────────────────────────────────────────────
    readonly ConcurrentQueue<Action>                    _mainQueue = new ConcurrentQueue<Action>();
    readonly ConcurrentQueue<(string json, IPEndPoint ep)> _udpQueue  = new ConcurrentQueue<(string, IPEndPoint)>();

    // ── Proxies physiques (main thread uniquement) ────────────────────────────
    readonly Dictionary<string, Rigidbody> _proxies     = new Dictionary<string, Rigidbody>();
    readonly Dictionary<string, Vector3>   _pendingPos  = new Dictionary<string, Vector3>();
    readonly Dictionary<string, float>     _pendingRotY = new Dictionary<string, float>();
    readonly Dictionary<string, Vector3>   _prevPos     = new Dictionary<string, Vector3>();

    float _stateTimer;
    bool  _running;
    int   _nextId = 1;

    // ═══════════════════════════════════════════════════════════════════════════
    // Unity lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    void Start()
    {
        _running = true;
        InitBonuses();

        _udpSocket   = new UdpClient(UdpPort);
        _tcpListener = new TcpListener(IPAddress.Any, TcpPort);
        _tcpListener.Start();

        Debug.Log($"[Server] Démarré — TCP:{TcpPort}  UDP:{UdpPort}  IP:{GetLocalIP()}");

        ThreadPool.QueueUserWorkItem(_ => AcceptLoop());
        ThreadPool.QueueUserWorkItem(_ => UdpLoop());
    }

    void Update()
    {
        // Actions venant des threads réseau → main thread
        while (_mainQueue.TryDequeue(out var a)) a();

        _stateTimer += Time.deltaTime;
        if (_stateTimer >= StateInterval) { _stateTimer = 0f; BroadcastState(); }
    }

    void FixedUpdate()
    {
        // ── Traiter les MOVE reçus en UDP ──────────────────────────────────────
        while (_udpQueue.TryDequeue(out var item))
        {
            try
            {
                var msg = JsonUtility.FromJson<SrvMoveMsg>(item.json);
                if (msg == null || msg.type != "MOVE") continue;
                lock (_worldLock)
                {
                    if (!_world.Players.ContainsKey(msg.id)) continue;
                    _udpEPs[msg.id]     = item.ep;
                    _pendingPos[msg.id]  = new Vector3(msg.x, msg.y, msg.z);
                    _pendingRotY[msg.id] = msg.rotY;
                }
            }
            catch { }
        }

        // ── Appliquer les positions sur les proxies (physique Unity résout collisions) ──
        foreach (var kv in _proxies)
        {
            if (!_pendingPos.TryGetValue(kv.Key, out var target)) continue;
            var rb = kv.Value;
            if (rb == null) continue;

            // Garder Y du proxy (gravité) — corriger X,Z seulement
            var t = new Vector3(target.x, rb.position.y, target.z);
            rb.MovePosition(t);

            if (_pendingRotY.TryGetValue(kv.Key, out var ry))
                rb.MoveRotation(Quaternion.Euler(0f, ry, 0f));
        }

        // ── Relire positions corrigées → world state ───────────────────────────
        lock (_worldLock)
        {
            foreach (var kv in _proxies)
            {
                if (!_world.Players.TryGetValue(kv.Key, out var p) || kv.Value == null) continue;
                var pos = kv.Value.position;

                // Vitesse pour animer le client
                if (_prevPos.TryGetValue(kv.Key, out var prev))
                    p.Speed = (pos - prev).magnitude / Time.fixedDeltaTime;
                _prevPos[kv.Key] = pos;

                p.X    = pos.x;
                p.Y    = pos.y;
                p.Z    = pos.z;
                p.RotY = kv.Value.rotation.eulerAngles.y;
            }
        }
    }

    void OnDestroy()
    {
        _running = false;
        try { _tcpListener?.Stop(); }  catch { }
        try { _udpSocket?.Close(); }   catch { }
        foreach (var rb in _proxies.Values)
            if (rb != null) Destroy(rb.gameObject);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TCP — accept + read
    // ═══════════════════════════════════════════════════════════════════════════

    void AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var tcp = _tcpListener.AcceptTcpClient();
                _mainQueue.Enqueue(() =>
                {
                    string id = $"p{_nextId++}";
                    var c = new SrvClient(tcp, id);
                    lock (_netLock) _clients.Add(c);
                    Debug.Log($"[+] {id} connecté ({_clients.Count} joueurs)");
                    ThreadPool.QueueUserWorkItem(_ => ReadClientLoop(c));
                });
            }
            catch { break; }
        }
    }

    void ReadClientLoop(SrvClient c)
    {
        while (_running)
        {
            string line = c.ReadLine();
            if (line == null)   { _mainQueue.Enqueue(() => OnDisconnect(c)); break; }
            if (line.Length > 0) _mainQueue.Enqueue(() => HandleTcpMessage(c, line));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Déconnexion
    // ═══════════════════════════════════════════════════════════════════════════

    void OnDisconnect(SrvClient c)
    {
        lock (_netLock) { _clients.Remove(c); _udpEPs.Remove(c.Id); }
        lock (_worldLock)
        {
            _world.Players.Remove(c.Id);
            bool last;
            lock (_netLock) last = _clients.Count == 0;
            if (last) { _world.Bonuses.Clear(); InitBonuses(); }
        }
        DestroyProxy(c.Id);
        Debug.Log($"[-] {c.Id} déconnecté ({_clients.Count} joueurs)");
        Broadcast($"{{\"type\":\"PLAYER_LEFT\",\"id\":\"{c.Id}\"}}", null);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Messages TCP
    // ═══════════════════════════════════════════════════════════════════════════

    void HandleTcpMessage(SrvClient sender, string json)
    {
        Debug.Log($"[TCP][{sender.Id}] {json}");
        try
        {
            var h = JsonUtility.FromJson<SrvHeader>(json);
            if (h == null) return;
            switch (h.type)
            {
                case "JOIN": HandleJoin(sender, json); break;
                case "TAKE": HandleTake(sender, json); break;
            }
        }
        catch (Exception e) { Debug.LogWarning($"[Server] Parse error: {e.Message}"); }
    }

    void HandleJoin(SrvClient sender, string json)
    {
        var msg       = JsonUtility.FromJson<SrvJoinMsg>(json);
        string name   = string.IsNullOrEmpty(msg.name)      ? sender.Id   : msg.name;
        string chara  = string.IsNullOrEmpty(msg.character)  ? "barbarian" : msg.character;

        lock (_worldLock)
        {
            if (_world.Players.ContainsKey(sender.Id)) return;
            if (_world.Players.Count >= 4)
            {
                sender.Send("{\"type\":\"ERROR\",\"message\":\"Serveur plein\"}");
                return;
            }
            _world.Players[sender.Id] = new SrvPlayerState
                { Id = sender.Id, Name = name, Character = chara };
        }

        SpawnProxy(sender.Id, 0f, 1f, 0f);
        sender.Send(BuildInitState(sender.Id));
        Broadcast(
            $"{{\"type\":\"PLAYER_JOIN\",\"id\":\"{sender.Id}\",\"name\":\"{name}\"," +
            $"\"character\":\"{chara}\",\"x\":0,\"y\":0,\"z\":0}}", sender);
        Debug.Log($"  → INIT_STATE → {sender.Id}");
    }

    void HandleTake(SrvClient sender, string json)
    {
        var msg = JsonUtility.FromJson<SrvTakeMsg>(json);
        bool ok; int score;
        lock (_worldLock)
        {
            ok    = _world.TryCollectBonus(msg.bonusId, sender.Id);
            score = ok && _world.Players.TryGetValue(sender.Id, out var p) ? p.Score : 0;
        }
        if (ok)
            Broadcast(
                $"{{\"type\":\"BONUS_TAKEN\",\"bonusId\":\"{msg.bonusId}\"," +
                $"\"byPlayerId\":\"{sender.Id}\",\"newScore\":{score}}}", null);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UDP
    // ═══════════════════════════════════════════════════════════════════════════

    void UdpLoop()
    {
        while (_running)
        {
            try
            {
                IPEndPoint ep   = new IPEndPoint(IPAddress.Any, 0);
                byte[]     data = _udpSocket.Receive(ref ep);
                _udpQueue.Enqueue((Encoding.UTF8.GetString(data), ep));
            }
            catch { if (!_running) break; }
        }
    }

    void BroadcastState()
    {
        string state;
        lock (_worldLock)
        {
            var ic  = CultureInfo.InvariantCulture;
            var sb  = new StringBuilder();
            bool f  = true;
            foreach (var p in _world.Players.Values)
            {
                if (!f) sb.Append(','); f = false;
                sb.Append(
                    $"{{\"id\":\"{p.Id}\"," +
                    $"\"x\":{p.X.ToString(ic)},\"y\":{p.Y.ToString(ic)},\"z\":{p.Z.ToString(ic)}," +
                    $"\"rotY\":{p.RotY.ToString(ic)},\"speed\":{p.Speed.ToString(ic)}}}");
            }
            state = $"{{\"type\":\"STATE\",\"players\":[{sb}]}}";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(state);
        lock (_netLock)
        {
            foreach (var ep in _udpEPs.Values)
                try { _udpSocket.Send(bytes, bytes.Length, ep); } catch { }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INIT_STATE
    // ═══════════════════════════════════════════════════════════════════════════

    string BuildInitState(string forId)
    {
        lock (_worldLock)
        {
            var ic = CultureInfo.InvariantCulture;
            var pl = new StringBuilder();
            var bo = new StringBuilder();
            bool fp = true, fb = true;

            foreach (var p in _world.Players.Values)
            {
                if (!fp) pl.Append(','); fp = false;
                pl.Append(
                    $"{{\"id\":\"{p.Id}\",\"name\":\"{p.Name}\"," +
                    $"\"character\":\"{p.Character ?? "barbarian"}\"," +
                    $"\"x\":{p.X.ToString(ic)},\"y\":{p.Y.ToString(ic)},\"z\":{p.Z.ToString(ic)}," +
                    $"\"rotY\":{p.RotY.ToString(ic)},\"score\":{p.Score}}}");
            }
            foreach (var b in _world.Bonuses.Values)
            {
                if (b.IsCollected) continue;
                if (!fb) bo.Append(','); fb = false;
                bo.Append(
                    $"{{\"id\":\"{b.Id}\"," +
                    $"\"x\":{b.X.ToString(ic)},\"y\":{b.Y.ToString(ic)},\"z\":{b.Z.ToString(ic)}}}");
            }
            return $"{{\"type\":\"INIT_STATE\",\"playerId\":\"{forId}\"," +
                   $"\"players\":[{pl}],\"bonuses\":[{bo}]}}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Proxies physiques
    // ═══════════════════════════════════════════════════════════════════════════

    void SpawnProxy(string id, float x, float y, float z)
    {
        if (_proxies.ContainsKey(id)) return;

        var go  = new GameObject($"ServerProxy_{id}");
        go.transform.position = new Vector3(x, y, z);

        // Capsule collider = corps physique du joueur
        var col    = go.AddComponent<CapsuleCollider>();
        col.radius = 0.4f;
        col.height = 1.8f;
        col.center = new Vector3(0f, 0.9f, 0f);

        // Rigidbody non-kinematic : physique native, collision réelle
        var rb = go.AddComponent<Rigidbody>();
        rb.mass                 = 80f;
        rb.linearDamping        = 10f;  // freine rapidement quand plus de MOVE
        rb.constraints          = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation        = RigidbodyInterpolation.Interpolate;

        _proxies[id] = rb;
        Debug.Log($"[Server] Proxy spawné pour {id}");
    }

    void DestroyProxy(string id)
    {
        if (!_proxies.TryGetValue(id, out var rb)) return;
        if (rb != null) Destroy(rb.gameObject);
        _proxies.Remove(id);
        _pendingPos.Remove(id);
        _pendingRotY.Remove(id);
        _prevPos.Remove(id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    void Broadcast(string msg, SrvClient exclude)
    {
        List<SrvClient> snap;
        lock (_netLock) snap = new List<SrvClient>(_clients);
        foreach (var c in snap) if (c != exclude) c.Send(msg);
    }

    void InitBonuses()
    {
        lock (_worldLock)
        {
            _world.AddOrUpdateBonus("b0",  2.0f, 0.5f, -1.0f);
            _world.AddOrUpdateBonus("b1", -3.0f, 0.5f,  4.0f);
            _world.AddOrUpdateBonus("b2",  5.0f, 0.5f,  2.0f);
            for (int i = 1; i <= 8; i++)
                _world.AddOrUpdateBonus($"extra_{i}", 0f, 0f, 0f);
        }
    }

    string GetLocalIP()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                return ip.ToString();
        return "127.0.0.1";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SrvClient — wrapper TCP par joueur connecté
    // ═══════════════════════════════════════════════════════════════════════════

    class SrvClient
    {
        public string Id;
        readonly TcpClient    _tcp;
        readonly StreamReader _reader;
        readonly StreamWriter _writer;
        bool _dead;

        public SrvClient(TcpClient tcp, string id)
        {
            Id      = id;
            _tcp    = tcp;
            _reader = new StreamReader(tcp.GetStream());
            _writer = new StreamWriter(tcp.GetStream()) { AutoFlush = true };
        }

        // Retourne null = déconnecté, "" = pas de données, string = ligne reçue
        public string ReadLine()
        {
            try
            {
                if (!_tcp.Client.Poll(100_000, SelectMode.SelectRead)) return "";
                if (_tcp.Available == 0) { _dead = true; return null; }
                return _reader.ReadLine();
            }
            catch { _dead = true; return null; }
        }

        public void Send(string msg)
        {
            try { _writer.WriteLine(msg); }
            catch { _dead = true; }
        }
    }

    // ── DTOs JSON (JsonUtility, main thread uniquement) ────────────────────────
    [Serializable] class SrvHeader  { public string type; }
    [Serializable] class SrvJoinMsg { public string type; public string name; public string character; }
    [Serializable] class SrvTakeMsg { public string type; public string bonusId; }
    [Serializable] class SrvMoveMsg { public string type; public string id; public float x; public float y; public float z; public float rotY; }
}
