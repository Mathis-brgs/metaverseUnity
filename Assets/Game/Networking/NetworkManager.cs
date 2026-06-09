using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Client MetaVerse — TCP (événements) + UDP (positions), protocole A.
/// Requiert <see cref="TCPClient"/> sur le même GameObject.
/// </summary>
[RequireComponent(typeof(TCPClient))]
public class NetworkManager : MonoBehaviour
{
    [Header("Connexion")]
    public string ServerIP = "127.0.0.1";
    public string PlayerName = "Joueur";
    public string SelectedCharacter = "barbarian";
    public bool ConnectOnStart = false;

    [Header("Ports (alignés avec le serveur A)")]
    [Tooltip("Port TCP — JOIN, INIT_STATE, TAKE, etc.")]
    public int TcpPort = 25000;
    [Tooltip("Port UDP serveur — MOVE et réception STATE")]
    public int UdpServerPort = 25001;

    [Header("Envoi MOVE (UDP, legacy)")]
    public bool SendMoveAutomatically;
    public Transform MoveSource;
    public float MoveSendInterval = 0.05f;

    [Header("Envoi INPUT (UDP, serveur autoritaire)")]
    [Tooltip("Recommandé avec le serveur Unity : envoie l'intention de déplacement, pas la position.")]
    public bool SendInputAutomatically;
    public CharacterController InputSource;

    [Header("Debug")]
    public bool LogMessages = true;

    [Header("Événements")]
    public UnityEvent<InitStateMessage> OnInitState;
    public UnityEvent<PlayerJoinMessage> OnPlayerJoin;
    public UnityEvent<PlayerLeftMessage> OnPlayerLeft;
    public UnityEvent<StateMessage> OnState;
    public UnityEvent<BonusTakenMessage> OnBonusTaken;
    public UnityEvent<CarEnteredMessage> OnCarEntered;
    public UnityEvent<CarExitedMessage> OnCarExited;
    public UnityEvent<ErrorMessage> OnError;

    TCPClient _tcp;
    UdpClient _udp;
    IPEndPoint _udpServerEp;
    readonly StringBuilder _tcpLineBuffer = new StringBuilder();
    float _moveTimer;

    public string MyPlayerId { get; private set; } = "";
    public bool HasSession => !string.IsNullOrEmpty(MyPlayerId);
    public bool IsTcpConnected => _tcp != null && _tcp.IsConnected;

    void Awake()
    {
        _tcp = GetComponent<TCPClient>();
        if (ServerMode.Active)
            enabled = false;
    }

    void Start()
    {
        if (ServerMode.Active) return;
        if (ConnectOnStart)
            Connect(PlayerName);
    }

    void Update()
    {
        if (ServerMode.Active) return;
        ReceiveUdp();
        TrySendMoveTick();
        TrySendInputTick();
    }

    void OnDisable()
    {
        Disconnect();
    }

    /// <summary>Connexion TCP, ouverture UDP, envoi JOIN.</summary>
    public bool Connect(string playerName)
    {
        if (IsTcpConnected)
        {
            Debug.LogWarning("[NetworkManager] Déjà connecté (TCP).");
            return false;
        }

        PlayerName = playerName;
        MyPlayerId = "";
        _tcpLineBuffer.Clear();

        _tcp.DestinationIP = ServerIP;
        _tcp.DestinationPort = TcpPort;
        if (!_tcp.Connect(OnTcpChunkReceived))
        {
            Debug.LogWarning("[NetworkManager] Échec connexion TCP.");
            return false;
        }

        if (!InitUdp())
        {
            _tcp.Close();
            return false;
        }

        SendTcp(new JoinPayload { type = "JOIN", name = playerName, character = SelectedCharacter });

        // Connecté au serveur autoritaire : les voitures sont pilotées par le serveur (positions via STATE).
        DrivableCar.ClientSuppressed = true;

        if (LogMessages)
            Debug.Log($"[NetworkManager] JOIN → TCP {ServerHost}:{TcpPort}");
        return true;
    }

    public void Disconnect()
    {
        MyPlayerId = "";
        _tcpLineBuffer.Clear();
        DrivableCar.ClientSuppressed = false;
        CloseUdp();
        if (_tcp != null)
            _tcp.Close();
    }

    public void SendMove(float x, float y, float z, float rotY)
    {
        if (!HasSession || _udp == null) return;
        SendUdp(new MovePayload
        {
            type = "MOVE",
            id = MyPlayerId,
            x = x,
            y = y,
            z = z,
            rotY = rotY
        });
    }

    public void SendInput(float ix, float iz, float rotY)
    {
        if (!HasSession || _udp == null) return;
        SendUdp(new InputPayload
        {
            type = "INPUT",
            id = MyPlayerId,
            ix = ix,
            iz = iz,
            rotY = rotY
        });
    }

    public void SendTake(string bonusId)
    {
        if (!HasSession || !IsTcpConnected) return;
        SendTcp(new TakePayload
        {
            type = "TAKE",
            playerId = MyPlayerId,
            bonusId = bonusId
        });
    }

    public void SendCarEnter(string carId)
    {
        if (!HasSession || !IsTcpConnected) return;
        SendTcp(new CarEnterPayload { type = "CAR_ENTER", playerId = MyPlayerId, carId = carId ?? "" });
    }

    public void SendCarExit()
    {
        if (!HasSession || !IsTcpConnected) return;
        SendTcp(new CarExitPayload { type = "CAR_EXIT", playerId = MyPlayerId });
    }

    string ServerHost => _tcp != null ? _tcp.DestinationIP : "127.0.0.1";

    bool InitUdp()
    {
        CloseUdp();
        try
        {
            _udp = new UdpClient(0);
            _udpServerEp = new IPEndPoint(IPAddress.Parse(ServerHost), UdpServerPort);
            if (LogMessages)
                Debug.Log($"[NetworkManager] UDP prêt → {ServerHost}:{UdpServerPort}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NetworkManager] UDP init: " + ex.Message);
            return false;
        }
    }

    void CloseUdp()
    {
        if (_udp == null) return;
        _udp.Close();
        _udp = null;
    }

    void ReceiveUdp()
    {
        if (_udp == null) return;

        try
        {
            while (_udp.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udp.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);
                if (LogMessages)
                    Debug.Log("[NetworkManager] UDP ← " + json);
                HandleUdpJson(json);
            }
        }
        catch (SocketException ex)
        {
            Debug.LogWarning("[NetworkManager] UDP recv: " + ex.Message);
        }
    }

    void TrySendMoveTick()
    {
        if (!SendMoveAutomatically || !HasSession) return;
        _moveTimer += Time.deltaTime;
        if (_moveTimer < MoveSendInterval) return;
        _moveTimer = 0f;

        Transform src = MoveSource != null ? MoveSource : transform;
        Vector3 p = src.position;
        SendMove(p.x, p.y, p.z, src.eulerAngles.y);
    }

    float _inputTimer;

    void TrySendInputTick()
    {
        if (!SendInputAutomatically || !HasSession || InputSource == null) return;
        _inputTimer += Time.deltaTime;
        if (_inputTimer < MoveSendInterval) return;
        _inputTimer = 0f;

        float ix, iz;
        if (InputSource.IsDrivingCar)
        {
            // En voiture : ix = braquage, iz = accélération (entrée brute).
            Vector2 raw = InputSource.GetMoveInput();
            ix = raw.x;
            iz = raw.y;
        }
        else
        {
            // À pied : direction monde relative caméra.
            Vector3 dir = InputSource.GetDesiredWorldMove();
            ix = dir.x;
            iz = dir.z;
        }

        SendInput(ix, iz, InputSource.transform.eulerAngles.y);
    }

    void OnTcpChunkReceived(string chunk)
    {
        _tcpLineBuffer.Append(chunk);
        while (TryExtractTcpLine(out string line))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (LogMessages)
                Debug.Log("[NetworkManager] TCP ← " + line);
            HandleTcpJson(line);
        }
    }

    bool TryExtractTcpLine(out string line)
    {
        string all = _tcpLineBuffer.ToString();
        int idx = all.IndexOf('\n');
        if (idx < 0)
        {
            line = null;
            return false;
        }
        line = all.Substring(0, idx).TrimEnd('\r');
        _tcpLineBuffer.Clear();
        _tcpLineBuffer.Append(all.Substring(idx + 1));
        return true;
    }

    void HandleTcpJson(string json)
    {
        NetHeader header = JsonUtility.FromJson<NetHeader>(json);
        if (header == null || string.IsNullOrEmpty(header.type))
        {
            Debug.LogWarning("[NetworkManager] TCP sans type: " + json);
            return;
        }

        switch (header.type)
        {
            case "INIT_STATE":
                var init = JsonUtility.FromJson<InitStateMessage>(json);
                MyPlayerId = init.playerId;
                if (LogMessages)
                    Debug.Log($"[NetworkManager] INIT_STATE — id={MyPlayerId}");
                OnInitState?.Invoke(init);
                break;
            case "PLAYER_JOIN":
                OnPlayerJoin?.Invoke(JsonUtility.FromJson<PlayerJoinMessage>(json));
                break;
            case "PLAYER_LEFT":
                OnPlayerLeft?.Invoke(JsonUtility.FromJson<PlayerLeftMessage>(json));
                break;
            case "BONUS_TAKEN":
                OnBonusTaken?.Invoke(JsonUtility.FromJson<BonusTakenMessage>(json));
                break;
            case "CAR_ENTERED":
                OnCarEntered?.Invoke(JsonUtility.FromJson<CarEnteredMessage>(json));
                break;
            case "CAR_EXITED":
                OnCarExited?.Invoke(JsonUtility.FromJson<CarExitedMessage>(json));
                break;
            case "ERROR":
                OnError?.Invoke(JsonUtility.FromJson<ErrorMessage>(json));
                break;
            default:
                Debug.LogWarning("[NetworkManager] TCP type inconnu: " + header.type);
                break;
        }
    }

    void HandleUdpJson(string json)
    {
        NetHeader header = JsonUtility.FromJson<NetHeader>(json);
        if (header == null || string.IsNullOrEmpty(header.type)) return;

        if (header.type == "STATE")
            OnState?.Invoke(JsonUtility.FromJson<StateMessage>(json));
        else if (LogMessages)
            Debug.LogWarning("[NetworkManager] UDP type inconnu: " + header.type);
    }

    void SendTcp(object payload)
    {
        string json = JsonUtility.ToJson(payload) + "\n";
        _tcp.SendTCPMessage(json);
    }

    void SendUdp(object payload)
    {
        if (_udp == null) return;
        string json = JsonUtility.ToJson(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            _udp.Send(bytes, bytes.Length, _udpServerEp);
            if (LogMessages)
                Debug.Log("[NetworkManager] UDP → " + json);
        }
        catch (SocketException ex)
        {
            Debug.LogWarning("[NetworkManager] UDP send: " + ex.Message);
        }
    }

    [Serializable]
    struct JoinPayload
    {
        public string type;
        public string name;
        public string character;
    }

    [Serializable]
    struct MovePayload
    {
        public string type;
        public string id;
        public float x;
        public float y;
        public float z;
        public float rotY;
    }

    [Serializable]
    struct InputPayload
    {
        public string type;
        public string id;
        public float ix;
        public float iz;
        public float rotY;
    }

    [Serializable]
    struct TakePayload
    {
        public string type;
        public string playerId;
        public string bonusId;
    }

    [Serializable]
    struct CarEnterPayload
    {
        public string type;
        public string playerId;
        public string carId;
    }

    [Serializable]
    struct CarExitPayload
    {
        public string type;
        public string playerId;
    }
}
