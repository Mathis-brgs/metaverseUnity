using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Diagnostics;

var server = new GameServer(25000, 25001);
server.Start();

class GameServer
{
    readonly int tcpPort;
    readonly int udpPort;
    TcpListener? listener;
    UdpClient udpSocket;
    List<ConnectedClient> clients = new();
    Dictionary<string, IPEndPoint> udpEndpoints = new();
    WorldState worldState = new();
    readonly object stateLock = new();
    int nextId = 1;
    Stopwatch statTimer = Stopwatch.StartNew();

    public GameServer(int tcpPort, int udpPort)
    {
        this.tcpPort = tcpPort;
        this.udpPort = udpPort;
        udpSocket = new UdpClient(udpPort);
        InitBonuses();
    }

    // Positions des bonus hardcodées — à synchroniser avec la scène Unity de C
    void InitBonuses()
    {
        worldState.AddOrUpdateBonus("b0",  2.0f, 0.5f, -1.0f);
        worldState.AddOrUpdateBonus("b1", -3.0f, 0.5f,  4.0f);
        worldState.AddOrUpdateBonus("b2",  5.0f, 0.5f,  2.0f);
    }

    public void Start()
    {
        listener = new TcpListener(IPAddress.Any, tcpPort);
        listener.Start();
        Console.WriteLine($"Serveur démarré — TCP:{tcpPort}  UDP:{udpPort}");
        Console.WriteLine($"IP locale : {GetLocalIP()}");

        Task.Run(ListenUDP);

        while (true)
        {
            AcceptNewClients();
            ReadClientMessages();

            if (statTimer.ElapsedMilliseconds >= 50)
            {
                BroadcastStateUDP();
                statTimer.Restart();
            }
        }
    }

    // --- TCP ---

    void AcceptNewClients()
    {
        if (listener == null || !listener.Pending()) return;

        TcpClient tcp = listener.AcceptTcpClient();
        var client = new ConnectedClient(tcp, $"p{nextId++}");
        clients.Add(client);
        Console.WriteLine($"[+] {client.Id} connecté  ({clients.Count} joueurs)");
    }

    void ReadClientMessages()
    {
        foreach (var client in clients.ToList())
        {
            if (!client.IsConnected)
            {
                clients.Remove(client);
                lock (stateLock)
                {
                    udpEndpoints.Remove(client.Id);
                    worldState.Players.Remove(client.Id);
                }
                Console.WriteLine($"[-] {client.Id} déconnecté  ({clients.Count} joueurs)");
                Broadcast($"{{\"type\":\"PLAYER_LEFT\",\"id\":\"{client.Id}\"}}", exclude: null);
                continue;
            }

            string? message = client.ReadLine();
            if (message != null)
                HandleMessage(client, message);
        }
    }

    void HandleMessage(ConnectedClient sender, string json)
    {
        Console.WriteLine($"[TCP][{sender.Id}] {json}");

        try
        {
            var doc = JsonDocument.Parse(json);
            string type = doc.RootElement.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "JOIN":   HandleJoin(sender, doc);   break;
                case "TAKE":   HandleTake(sender, doc);   break;
                default: Console.WriteLine($"  ⚠ type inconnu : {type}"); break;
            }
        }
        catch (JsonException) { Console.WriteLine($"  ⚠ JSON invalide de {sender.Id}"); }
    }

    void HandleJoin(ConnectedClient sender, JsonDocument doc)
    {
        // Anti double-JOIN
        if (worldState.Players.ContainsKey(sender.Id)) return;

        // Max 4 joueurs
        if (worldState.Players.Count >= 4)
        {
            sender.Send("{\"type\":\"ERROR\",\"message\":\"Serveur plein\"}");
            return;
        }

        string name = doc.RootElement.GetProperty("name").GetString() ?? sender.Id;

        lock (stateLock)
        {
            var player = worldState.Players.ContainsKey(sender.Id)
                ? worldState.Players[sender.Id]
                : new PlayerState { Id = sender.Id, Name = name };
            player.Name = name;
            worldState.Players[sender.Id] = player;
        }

        // Envoyer l'état complet au nouveau joueur
        sender.Send(BuildInitState(sender.Id));

        // Notifier les autres
        Broadcast($"{{\"type\":\"PLAYER_JOIN\",\"id\":\"{sender.Id}\",\"name\":\"{name}\",\"x\":0,\"y\":0,\"z\":0}}", exclude: sender);
        Console.WriteLine($"  → INIT_STATE envoyé à {sender.Id}, PLAYER_JOIN broadcast");
    }

    void HandleTake(ConnectedClient sender, JsonDocument doc)
    {
        string bonusId = doc.RootElement.GetProperty("bonusId").GetString() ?? "";
        bool collected;
        int newScore;

        lock (stateLock)
        {
            collected = worldState.TryCollectBonus(bonusId, sender.Id);
            newScore = collected && worldState.Players.TryGetValue(sender.Id, out var p) ? p.Score : 0;
        }

        if (collected)
        {
            Broadcast($"{{\"type\":\"BONUS_TAKEN\",\"bonusId\":\"{bonusId}\",\"byPlayerId\":\"{sender.Id}\",\"newScore\":{newScore}}}", exclude: null);
            Console.WriteLine($"  → {sender.Id} a pris {bonusId}, score={newScore}");
        }
    }

    string BuildInitState(string forPlayerId)
    {
        lock (stateLock)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var players = worldState.Players.Values
                .Select(p => $"{{\"id\":\"{p.Id}\",\"name\":\"{p.Name}\",\"x\":{p.X.ToString(ic)},\"y\":{p.Y.ToString(ic)},\"z\":{p.Z.ToString(ic)},\"rotY\":{p.RotY.ToString(ic)},\"score\":{p.Score}}}");
            var bonuses = worldState.Bonuses.Values
                .Where(b => !b.IsCollected)
                .Select(b => $"{{\"id\":\"{b.Id}\",\"x\":{b.X.ToString(ic)},\"y\":{b.Y.ToString(ic)},\"z\":{b.Z.ToString(ic)}}}");

            return $"{{\"type\":\"INIT_STATE\",\"playerId\":\"{forPlayerId}\"," +
                   $"\"players\":[{string.Join(",", players)}]," +
                   $"\"bonuses\":[{string.Join(",", bonuses)}]}}";
        }
    }

    void Broadcast(string message, ConnectedClient? exclude)
    {
        foreach (var client in clients.ToList())
        {
            if (client == exclude) continue;
            client.Send(message);
        }
    }

    // --- UDP ---

    void ListenUDP()
    {
        Console.WriteLine($"UDP en écoute sur le port {udpPort}");
        while (true)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpSocket.Receive(ref remote);
            string json = System.Text.Encoding.UTF8.GetString(data);

            try
            {
                var doc = JsonDocument.Parse(json);
                string type = doc.RootElement.GetProperty("type").GetString() ?? "";

                if (type == "MOVE")
                {
                    string id  = doc.RootElement.GetProperty("id").GetString() ?? "";
                    float x    = doc.RootElement.GetProperty("x").GetSingle();
                    float y    = doc.RootElement.GetProperty("y").GetSingle();
                    float z    = doc.RootElement.GetProperty("z").GetSingle();
                    float rotY = doc.RootElement.GetProperty("rotY").GetSingle();

                    // Anti MOVE avant JOIN + validation coordonnées
                    if (!worldState.Players.ContainsKey(id)) continue;
                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
                        float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)) continue;

                    lock (stateLock)
                    {
                        udpEndpoints[id] = remote;
                        worldState.UpdatePlayerPosition(id, x, y, z, rotY);
                    }
                }
            }
            catch (JsonException) { }
        }
    }

    void BroadcastStateUDP()
    {
        string state;
        lock (stateLock)
        {
            var players = worldState.Players.Values
                .Select(p => $"{{\"id\":\"{p.Id}\",\"x\":{p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"y\":{p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"z\":{p.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"rotY\":{p.RotY.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
            state = $"{{\"type\":\"STATE\",\"players\":[{string.Join(",", players)}]}}";
        }

        byte[] data = System.Text.Encoding.UTF8.GetBytes(state);
        lock (stateLock)
        {
            foreach (var endpoint in udpEndpoints.Values)
                udpSocket.Send(data, data.Length, endpoint);
        }
    }

    string GetLocalIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                return ip.ToString();
        return "introuvable";
    }
}

class ConnectedClient
{
    public string Id { get; }
    TcpClient tcp;
    StreamReader reader;
    StreamWriter writer;

    public ConnectedClient(TcpClient tcp, string id)
    {
        this.tcp = tcp;
        this.Id = id;
        reader = new StreamReader(tcp.GetStream());
        writer = new StreamWriter(tcp.GetStream()) { AutoFlush = true };
    }

    public bool IsConnected => tcp.Connected;

    public string? ReadLine()
    {
        if (tcp.Available == 0) return null;
        return reader.ReadLine();
    }

    public void Send(string message)
    {
        try { writer.WriteLine(message); }
        catch { }
    }
}
