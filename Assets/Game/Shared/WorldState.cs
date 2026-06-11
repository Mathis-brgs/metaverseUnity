using System;
using System.Collections.Generic;

/// <summary>
/// État autoritaire du monde, partagé entre le serveur Unity et la logique de jeu.
/// Source unique — remplace l'ancien Assets/Demos/MetaVerse/WorldState.cs et le doublon Server/WorldState.cs.
/// </summary>
[Serializable]
public class PlayerState
{
    public string Id;
    public string Name;
    public string Character = "barbarian";
    public float X;
    public float Y;
    public float Z;
    public float RotY;
    public int Score;

    // Voiture actuellement conduite par ce joueur (null si à pied).
    public string InCarId;

    // Temps Unity (Time.time) jusqu'auquel le joueur est étourdi / au sol (KO réseau).
    public float StunnedUntil;
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
public class CarState
{
    public string Id;
    public float X;
    public float Y;
    public float Z;
    public float RotY;
    public string DriverId; // null si vide
}

[Serializable]
public class WorldState
{
    public int MaxPlayers = 4;
    public Dictionary<string, PlayerState> Players = new Dictionary<string, PlayerState>();
    public Dictionary<string, BonusState> Bonuses = new Dictionary<string, BonusState>();
    public Dictionary<string, CarState> Cars = new Dictionary<string, CarState>();

    int nextPlayerIndex = 1;

    public string GeneratePlayerId()
    {
        if (Players.Count >= MaxPlayers)
            return null;

        string playerId;
        int attempts = 0;
        do
        {
            attempts++;
            if (attempts > MaxPlayers + 1)
                return null;

            playerId = "p" + nextPlayerIndex;
            nextPlayerIndex++;
        } while (Players.ContainsKey(playerId));

        Players[playerId] = new PlayerState
        {
            Id = playerId,
            Name = "Joueur " + playerId.Substring(1),
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
        if (!Bonuses.TryGetValue(bonusId, out BonusState bonus))
        {
            bonus = new BonusState { Id = bonusId };
            Bonuses[bonusId] = bonus;
        }

        bonus.X = x;
        bonus.Y = y;
        bonus.Z = z;
    }

    public bool TryCollectBonus(string bonusId, string byPlayerId)
    {
        if (!Bonuses.TryGetValue(bonusId, out BonusState bonus))
            return false;

        if (bonus.IsCollected)
            return false;

        bonus.IsCollected = true;

        if (Players.TryGetValue(byPlayerId, out PlayerState player))
            player.Score++;

        return true;
    }

    public void AddOrUpdateCar(string carId, float x, float y, float z, float rotY)
    {
        if (!Cars.TryGetValue(carId, out CarState car))
        {
            car = new CarState { Id = carId };
            Cars[carId] = car;
        }

        car.X = x;
        car.Y = y;
        car.Z = z;
        car.RotY = rotY;
    }

    /// <summary>Assigne un conducteur à une voiture si elle est libre. Renvoie false si occupée/déjà en voiture.</summary>
    public bool TryEnterCar(string carId, string playerId)
    {
        if (!Cars.TryGetValue(carId, out CarState car))
            return false;
        if (!string.IsNullOrEmpty(car.DriverId))
            return false;
        if (!Players.TryGetValue(playerId, out PlayerState player))
            return false;
        if (!string.IsNullOrEmpty(player.InCarId))
            return false;

        car.DriverId = playerId;
        player.InCarId = carId;
        return true;
    }

    public bool TryExitCar(string playerId)
    {
        if (!Players.TryGetValue(playerId, out PlayerState player))
            return false;
        if (string.IsNullOrEmpty(player.InCarId))
            return false;

        if (Cars.TryGetValue(player.InCarId, out CarState car) && car.DriverId == playerId)
            car.DriverId = null;

        player.InCarId = null;
        return true;
    }

    public void RemovePlayer(string playerId)
    {
        if (Players.TryGetValue(playerId, out PlayerState player) && !string.IsNullOrEmpty(player.InCarId))
        {
            if (Cars.TryGetValue(player.InCarId, out CarState car) && car.DriverId == playerId)
                car.DriverId = null;
        }

        Players.Remove(playerId);
    }

    PlayerState GetOrCreatePlayer(string playerId)
    {
        if (Players.TryGetValue(playerId, out PlayerState player))
            return player;

        player = new PlayerState { Id = playerId, Name = playerId };
        Players[playerId] = player;
        return player;
    }
}
