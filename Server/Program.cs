using System.Net;
using System.Net.Sockets;

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
            // Accepter un nouveau client si quelqu'un frappe à la porte
            if (listener.Pending())
            {
                TcpClient tcp = listener.AcceptTcpClient();
                var client = new ConnectedClient(tcp, $"p{nextId++}");
                clients.Add(client);
                Console.WriteLine($"[+] Joueur connecté : {client.Id}  ({clients.Count} joueurs)");
            }

            // Lire les messages de chaque client connecté
            foreach (var client in clients.ToList())
            {
                string? message = client.ReadLine();
                if (message != null)
                    Console.WriteLine($"[{client.Id}] {message}");
            }
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

    public ConnectedClient(TcpClient tcp, string id)
    {
        this.tcp = tcp;
        this.Id = id;
        // StreamReader lit ligne par ligne — résout le problème de messages collés
        reader = new StreamReader(tcp.GetStream());
    }

    // Retourne un message complet (jusqu'au \n) ou null si rien à lire
    public string? ReadLine()
    {
        if (tcp.Available == 0) return null;
        return reader.ReadLine();
    }
}
