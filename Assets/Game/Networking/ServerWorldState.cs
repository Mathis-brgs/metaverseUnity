using System.Collections.Generic;

public class SrvPlayerState
{
    public string Id;
    public string Name;
    public string Character;
    public float X, Y, Z, RotY;
    public int   Score;
    public float Speed;
}

public class SrvBonusState
{
    public string Id;
    public float  X, Y, Z;
    public bool   IsCollected;
}

public class SrvWorldState
{
    public readonly Dictionary<string, SrvPlayerState> Players =
        new Dictionary<string, SrvPlayerState>();
    public readonly Dictionary<string, SrvBonusState> Bonuses =
        new Dictionary<string, SrvBonusState>();

    public void AddOrUpdateBonus(string id, float x, float y, float z)
    {
        if (!Bonuses.TryGetValue(id, out var b))
        {
            b = new SrvBonusState { Id = id };
            Bonuses[id] = b;
        }
        b.X = x; b.Y = y; b.Z = z;
    }

    public bool TryCollectBonus(string bonusId, string byPlayerId)
    {
        if (!Bonuses.TryGetValue(bonusId, out var b) || b.IsCollected) return false;
        b.IsCollected = true;
        if (Players.TryGetValue(byPlayerId, out var p)) p.Score++;
        return true;
    }
}
