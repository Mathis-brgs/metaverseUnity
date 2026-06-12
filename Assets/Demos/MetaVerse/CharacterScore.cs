using UnityEngine;

public class CharacterScore : MonoBehaviour
{
    public int Score = 0;
    public TMPro.TMP_Text TxtScore;

    NetworkManager _net;
    string _displayName = "";
    bool _nameSetExternally;

    void Start()
    {
        _net = FindFirstObjectByType<NetworkManager>();
        if (_net != null)
        {
            _net.OnBonusTaken.AddListener(OnBonusTaken);
            // Joueur local uniquement : les distants reçoivent déjà leur nom via RemotePlayerManager.
            if (!_nameSetExternally && !string.IsNullOrEmpty(_net.PlayerName))
                SetDisplayName(_net.PlayerName);
        }
        RefreshLabel();
    }

    void OnDestroy()
    {
        if (_net != null)
            _net.OnBonusTaken.RemoveListener(OnBonusTaken);
    }

    /// <summary>Appelé par RemotePlayerManager pour afficher le pseudo du joueur distant.</summary>
    public void SetDisplayName(string name)
    {
        _displayName = name;
        _nameSetExternally = true;
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (TxtScore == null) return;
        // Affiche le pseudo au-dessus de la tête ; le score est dans le HUD scoreboard.
        TxtScore.text = _displayName;
    }

    void OnBonusTaken(BonusTakenMessage msg)
    {
        if (_net == null || msg.byPlayerId != _net.MyPlayerId) return;
        Score = msg.newScore;
        // Score non affiché ici (géré par ScorePanelHUD).
    }

    public void AddScore(int points)
    {
        Score += points;
    }
}
