using UnityEngine;

public class CharacterScore : MonoBehaviour
{
    public int Score = 0;
    public TMPro.TMP_Text TxtScore;

    NetworkManager _net;

    void Start()
    {
        _net = FindFirstObjectByType<NetworkManager>();
        if (_net != null)
            _net.OnBonusTaken.AddListener(OnBonusTaken);
        UpdateScoreText();
    }

    void OnDestroy()
    {
        if (_net != null)
            _net.OnBonusTaken.RemoveListener(OnBonusTaken);
    }

    void OnBonusTaken(BonusTakenMessage msg)
    {
        if (_net == null || msg.byPlayerId != _net.MyPlayerId) return;
        Score = msg.newScore;
        UpdateScoreText();
    }

    public void AddScore(int points) {
      Score += points;
      UpdateScoreText();
    }

    void UpdateScoreText() {
      if (TxtScore == null) { return; }
      TxtScore.text = Score.ToString();
    }
}
