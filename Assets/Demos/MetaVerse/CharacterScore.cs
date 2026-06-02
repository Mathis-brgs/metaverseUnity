using UnityEngine;

public class CharacterScore : MonoBehaviour
{
    public int Score = 0;
    public TMPro.TMP_Text TxtScore;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UpdateScoreText();
    }

    // Update is called once per frame
    void Update()
    {
        
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
