using System;
using System.Collections.Generic;

[Serializable]
public class PlayerState
{
    public string Id;
    public string Name;
    public float X;
    public float Y;
    public float Z;
    public float RotY;
    public int Score;
    public string SkinId;
}

[Serializable]
public class BonusState
{
    public string Id;
    public float X;
    public float Y;
    public float Z;
    public bool IsCollected;
}

[Serializable]
public class WorldState
{
    public int MaxPlayers = CharacterSkinCatalog.SkinCount;
    public Dictionary<string, PlayerState> Players = new Dictionary<string, PlayerState>();
    public Dictionary<string, BonusState> Bonuses = new Dictionary<string, BonusState>();

    int nextPlayerIndex = 1;
    readonly List<int> availableSkinIndexes = new List<int>();

    public string GeneratePlayerId()
    {
      if (Players.Count >= MaxPlayers) {
        return null;
      }

      string playerId;
      int attempts = 0;
      do {
        attempts++;
        if (attempts > MaxPlayers) {
          return null;
        }

        if (nextPlayerIndex > MaxPlayers) {
          nextPlayerIndex = 1;
        }

        playerId = "p" + nextPlayerIndex;
        nextPlayerIndex++;
      } while (Players.ContainsKey(playerId));

      Players[playerId] = new PlayerState {
        Id = playerId,
        Name = "Joueur " + playerId.Substring(1),
        SkinId = TakeRandomSkinId(),
      };

      return playerId;
    }

    public void UpdatePlayerPosition(string playerId, float x, float y, float z, float rotY)
    {
      PlayerState player = GetOrCreatePlayer(playerId);
      player.X = x;
      player.Y = y;
      player.Z = z;
      player.RotY = rotY;
    }

    public void UpdatePlayerScore(string playerId, int score)
    {
      PlayerState player = GetOrCreatePlayer(playerId);
      player.Score = score;
    }

    public void AddOrUpdateBonus(string bonusId, float x, float y, float z)
    {
      BonusState bonus;
      if (!Bonuses.TryGetValue(bonusId, out bonus)) {
        bonus = new BonusState {
          Id = bonusId,
        };
        Bonuses[bonusId] = bonus;
      }

      bonus.X = x;
      bonus.Y = y;
      bonus.Z = z;
    }

    public bool TryCollectBonus(string bonusId, string byPlayerId)
    {
      BonusState bonus;
      if (!Bonuses.TryGetValue(bonusId, out bonus)) {
        return false;
      }

      if (bonus.IsCollected) {
        return false;
      }

      bonus.IsCollected = true;

      PlayerState player;
      if (Players.TryGetValue(byPlayerId, out player)) {
        player.Score++;
      }

      return true;
    }

    PlayerState GetOrCreatePlayer(string playerId)
    {
      PlayerState player;
      if (Players.TryGetValue(playerId, out player)) {
        return player;
      }

      player = new PlayerState {
        Id = playerId,
        Name = playerId,
        SkinId = TakeRandomSkinId(),
      };
      Players[playerId] = player;
      return player;
    }

    string TakeRandomSkinId()
    {
      RefillAvailableSkinsIfNeeded();
      if (availableSkinIndexes.Count == 0) {
        return CharacterSkinCatalog.GetSkin(0).Id;
      }

      int listIndex = UnityEngine.Random.Range(0, availableSkinIndexes.Count);
      int skinIndex = availableSkinIndexes[listIndex];
      availableSkinIndexes.RemoveAt(listIndex);
      return CharacterSkinCatalog.GetSkin(skinIndex).Id;
    }

    void RefillAvailableSkinsIfNeeded()
    {
      if (availableSkinIndexes.Count > 0) { return; }

      for (int i = 0; i < CharacterSkinCatalog.SkinCount; i++) {
        availableSkinIndexes.Add(i);
      }

      foreach (PlayerState player in Players.Values) {
        int skinIndex = CharacterSkinCatalog.GetSkinIndex(player.SkinId);
        if (skinIndex >= 0) {
          availableSkinIndexes.Remove(skinIndex);
        }
      }
    }
}
