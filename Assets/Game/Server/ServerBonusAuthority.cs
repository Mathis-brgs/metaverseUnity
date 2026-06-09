using System.Collections;
using UnityEngine;

/// <summary>
/// Autorité serveur des bonus. Enregistre les positions réelles des bonus de la scène dans le
/// <see cref="WorldState"/> (fini le hardcode b0/b1/b2) afin que l'INIT_STATE soit exact.
/// La collecte elle-même est déclenchée par les triggers Physics côté serveur (voir Bonus.cs).
/// </summary>
public class ServerBonusAuthority : MonoBehaviour
{
    UnityGameServer _server;

    public void Bind(UnityGameServer server)
    {
        _server = server;
    }

    void Start()
    {
        StartCoroutine(RegisterBonusesDeferred());
    }

    IEnumerator RegisterBonusesDeferred()
    {
        // Laisse le temps aux bonus de la scène + ExtraBonusCubes (AfterSceneLoad) de s'initialiser.
        yield return null;
        RegisterAllBonuses();
        yield return new WaitForSeconds(0.5f);
        RegisterAllBonuses();
    }

    void RegisterAllBonuses()
    {
        if (_server == null) return;

        Bonus[] bonuses = FindObjectsByType<Bonus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var bonus in bonuses)
        {
            string id = bonus.ResolveBonusId();
            if (string.IsNullOrEmpty(id)) continue;

            Vector3 p = bonus.transform.position;
            _server.World.AddOrUpdateBonus(id, p.x, p.y, p.z);
        }

        Debug.Log($"[ServerBonusAuthority] {bonuses.Length} bonus enregistrés dans le WorldState.");
    }
}
