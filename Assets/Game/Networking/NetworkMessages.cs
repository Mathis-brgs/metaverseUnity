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

[Serializable]
public class BonusSpawnMessage
{
    public string type;
    public string bonusId;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class GameOverMessage
{
    public string type;
    public string winnerId;
    public string winnerName;
    public int winnerScore;
    public float spawnX;
    public float spawnY;
    public float spawnZ;
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
public class PlayerHitMessage
{
    public string type;
    public string attackerId;
    public string targetId;
    public float attackerX;
    public float attackerZ;
    public int knockDown;  // 0 = coup normal, 1 = KO
    public int hitIndex;   // numéro de coup dans le combo (pour Hit_A / Hit_B)
}

[Serializable]
public class AttackMessage
{
    public string type;
    public string attackerId;
    public string targetId;
}

[Serializable]
public class InputMessage
{
    public string type;
    public string id;
    public float ix;
    public float iz;
    public float rotY;
    public float y;
    public bool inCar;
    public float carX;
    public float carY;
    public float carZ;
    public float carRotY;
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
    /// <summary>
    /// true = bonus déjà collecté sur le serveur.
    /// false (ou absent) = bonus actif.
    /// Le client n'utilise ce flag que pour cacher les bonus EXPLICITEMENT collectés ;
    /// un bonus absent de la liste (non connu du serveur) reste visible côté client.
    /// </summary>
    public bool collected;
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
