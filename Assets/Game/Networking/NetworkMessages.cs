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
public class NetPlayerPosition
{
    public string id;
    public float x;
    public float y;
    public float z;
    public float rotY;
}
