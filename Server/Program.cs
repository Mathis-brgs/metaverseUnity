using System.Net;
using System.Net.Sockets;
using System.Text.Json;

var server = new GameServer(25000);
server.Start();

class GameServer
{
    readonly int port;
    TcpListener? listener;
    List<ConnectedClient> clients = new();
    int nextId = 1;

    public GameServer(int port)
    {
        this.port = port;
    }

    public void Start()
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Serveur démarré sur le port {port}");
        Console.WriteLine($"IP locale : {GetLocalIP()}");

        while (true)
        {
            AcceptNewClients();
            ReadClientMessages();
        }
    }

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
            // Vérifier si le client est toujours connecté
            if (!client.IsConnected)
            {
                clients.Remove(client);
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

    // Reçoit un message JSON et le route selon son type
    void HandleMessage(ConnectedClient sender, string json)
    {
        Console.WriteLine($"[{sender.Id}] {json}");

        try
        {
            var doc = JsonDocument.Parse(json);
            string type = doc.RootElement.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "JOIN":
                    // TODO J3 : créer PlayerState, envoyer INIT_STATE au nouveau + PLAYER_JOIN aux autres
                    Broadcast($"{{\"type\":\"PLAYER_JOIN\",\"id\":\"{sender.Id}\"}}", exclude: null);
                    break;

                case "MOVE":
                    // TODO J3 : mettre à jour WorldState (position du joueur)
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
            Console.WriteLine($"  ⚠ JSON invalide reçu de {sender.Id}");
        }
    }

    // Envoie un message à tous les clients (sauf celui exclu si précisé)
    void Broadcast(string message, ConnectedClient? exclude)
    {
        foreach (var client in clients.ToList())
        {
            if (client == exclude) continue;
            client.Send(message);
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

    // Envoie un message au client — le \n est le délimiteur de message
    public void Send(string message)
    {
        try { writer.WriteLine(message); }
        catch { /* client déconnecté, sera nettoyé au prochain tour */ }
    }
}
