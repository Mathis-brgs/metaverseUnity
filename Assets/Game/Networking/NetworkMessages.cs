using System;

/// <summary>DTOs JsonUtility — champs alignés sur planning/protocol.md (A).</summary>
[Serializable]
public class NetHeader
{
    public string type;
}

[Serializable]
public class InitStateMessage
{
    public string type;
    public string playerId;
    public NetPlayerSnapshot[] players;
    public NetBonusSnapshot[] bonuses;
    public NetCarSnapshot[] cars;
}

[Serializable]
public class PlayerJoinMessage
{
    public string type;
    public string id;
    public string name;
    public string character;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class PlayerLeftMessage
{
    public string type;
    public string id;
}

[Serializable]
public class StateMessage
{
    public string type;
    public NetPlayerPosition[] players;
    public NetCarPosition[] cars;
}

[Serializable]
public class BonusTakenMessage
{
    public string type;
    public string bonusId;
    public string byPlayerId;
    public int newScore;
}

[Serializable]
public class CarEnteredMessage
{
    public string type;
    public string carId;
    public string driverId;
}

[Serializable]
public class CarExitedMessage
{
    public string type;
    public string carId;
}

[Serializable]
public class ErrorMessage
{
    public string type;
    public string message;
}

// --- Messages entrants (client → serveur), parsés par le serveur Unity ---

[Serializable]
public class JoinMessage
{
    public string type;
    public string name;
    public string character;
    public float x;
    public float y;
    public float z;
    public float rotY;
}

[Serializable]
public class MoveMessage
{
    public string type;
    public string id;
    public float x;
    public float y;
    public float z;
    public float rotY;
}

[Serializable]
public class InputMessage
{
    public string type;
    public string id;
    public float ix;
    public float iz;
    public float rotY;
}

[Serializable]
public class TakeMessage
{
    public string type;
    public string playerId;
    public string bonusId;
}

[Serializable]
public class CarEnterMessage
{
    public string type;
    public string playerId;
    public string carId;
}

[Serializable]
public class CarExitMessage
{
    public string type;
    public string playerId;
}

[Serializable]
public class NetPlayerSnapshot
{
    public string id;
    public string name;
    public string character;
    public float x;
    public float y;
    public float z;
    public float rotY;
    public int score;
    public string inCarId;
}

[Serializable]
public class NetBonusSnapshot
{
    public string id;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class NetCarSnapshot
{
    public string id;
    public float x;
    public float y;
    public float z;
    public float rotY;
    public string driverId;
}

[Serializable]
public class NetPlayerPosition
{
    public string id;
    public float x;
    public float y;
    public float z;
    public float rotY;
    public string inCarId;
}

[Serializable]
public class NetCarPosition
{
    public string id;
    public float x;
    public float y;
    public float z;
    public float rotY;
    public string driverId;
}
