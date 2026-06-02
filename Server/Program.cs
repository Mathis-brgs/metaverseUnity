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
    // Associe un playerId à son endpoint UDP pour pouvoir lui répondre
    Dictionary<string, IPEndPoint> udpEndpoints = new();
    int nextId = 1;
    Stopwatch statTimer = Stopwatch.StartNew();

    public GameServer(int tcpPort, int udpPort)
    {
        this.tcpPort = tcpPort;
        this.udpPort = udpPort;
        udpSocket = new UdpClient(udpPort);
    }

    public void Start()
    {
        listener = new TcpListener(IPAddress.Any, tcpPort);
        listener.Start();
        Console.WriteLine($"Serveur démarré — TCP:{tcpPort}  UDP:{udpPort}");
        Console.WriteLine($"IP locale : {GetLocalIP()}");

        // UDP tourne dans un thread séparé pour ne pas bloquer la boucle TCP
        Task.Run(ListenUDP);

        while (true)
        {
            AcceptNewClients();
            ReadClientMessages();

            // Broadcaster l'état des positions 20x/sec via UDP
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
                udpEndpoints.Remove(client.Id);
                Console.WriteLine($"[-] {client.Id} déconnecté  ({clients.Count} joueurs)");
                Broadcast($"{{\"type\":\"PLAYER_LEFT\",\"id\":\"{client.Id}\"}}", exclude: null);
                // TODO J3 : nettoyer WorldState (tâche D)
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
                case "JOIN":
                    // TODO J3 : créer PlayerState, envoyer INIT_STATE au nouveau
                    Broadcast($"{{\"type\":\"PLAYER_JOIN\",\"id\":\"{sender.Id}\"}}", exclude: null);
                    break;

                case "TAKE":
                    // TODO J3 : appeler WorldState.TryCollectBonus() (tâche D)
                    break;

                default:
                    Console.WriteLine($"  ⚠ type inconnu : {type}");
                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine($"  ⚠ JSON invalide de {sender.Id}");
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
                    string id = doc.RootElement.GetProperty("id").GetString() ?? "";
                    // Mémoriser l'endpoint UDP de ce joueur pour lui envoyer STATE
                    udpEndpoints[id] = remote;
                    Console.WriteLine($"[UDP][{id}] MOVE reçu");
                    // TODO J3 : mettre à jour WorldState.UpdatePlayerPosition()
                }
            }
            catch (JsonException) { }
        }
    }

    // Envoie l'état de toutes les positions à tous les clients connus via UDP
    void BroadcastStateUDP()
    {
        if (udpEndpoints.Count == 0) return;

        // TODO J3 : remplacer par les vraies positions du WorldState
        var state = $"{{\"type\":\"STATE\",\"players\":[]}}";
        byte[] data = System.Text.Encoding.UTF8.GetBytes(state);

        foreach (var endpoint in udpEndpoints.Values)
            udpSocket.Send(data, data.Length, endpoint);
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
