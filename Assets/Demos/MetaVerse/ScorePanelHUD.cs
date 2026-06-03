using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScorePanelHUD : MonoBehaviour
{
    public Vector2 PanelPosition = new Vector2(18f, -18f);
    public Vector2 PanelSize = new Vector2(230f, 260f);
    public float RefreshInterval = 0.15f;
    public float PickupMessageDuration = 2f;
    public int MaxDisplayedPlayers = 10;

    Text scoreText;
    Text pickupMessageText;
    float nextRefreshTime;
    float pickupMessageUntil;
    readonly StringBuilder builder = new StringBuilder();
    readonly List<PlayerScoreLine> scoreLines = new List<PlayerScoreLine>();
    readonly Dictionary<string, PlayerScoreLine> networkScoreLines = new Dictionary<string, PlayerScoreLine>();
    NetworkManager networkManager;
    bool useNetworkScores;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateScorePanel()
    {
      if (FindFirstObjectByType<ScorePanelHUD>() != null) { return; }

      GameObject root = new GameObject("Score Panel HUD");
      root.AddComponent<ScorePanelHUD>();
    }

    void Awake()
    {
      BuildPanel();
      RegisterNetworkManager();
      Refresh();
    }

    void OnDestroy()
    {
      UnregisterNetworkManager();
    }

    void Update()
    {
      UpdatePickupMessage();

      if (Time.time < nextRefreshTime) { return; }

      nextRefreshTime = Time.time + RefreshInterval;
      Refresh();
    }

    public static void ShowPickupMessage(CharacterController player)
    {
      ScorePanelHUD hud = FindFirstObjectByType<ScorePanelHUD>();
      if (hud == null) {
        GameObject root = new GameObject("Score Panel HUD");
        hud = root.AddComponent<ScorePanelHUD>();
      }

      hud.ShowPickupMessageInternal(player);
    }

    void BuildPanel()
    {
      Canvas canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 100;

      CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920f, 1080f);

      gameObject.AddComponent<GraphicRaycaster>();

      GameObject panelObject = new GameObject("Panel");
      panelObject.transform.SetParent(transform, false);

      RectTransform panelTransform = panelObject.AddComponent<RectTransform>();
      panelTransform.anchorMin = new Vector2(0f, 1f);
      panelTransform.anchorMax = new Vector2(0f, 1f);
      panelTransform.pivot = new Vector2(0f, 1f);
      panelTransform.anchoredPosition = PanelPosition;
      panelTransform.sizeDelta = PanelSize;

      Image panelImage = panelObject.AddComponent<Image>();
      panelImage.color = new Color(0f, 0f, 0f, 0.58f);

      GameObject textObject = new GameObject("Score Text");
      textObject.transform.SetParent(panelObject.transform, false);

      RectTransform textTransform = textObject.AddComponent<RectTransform>();
      textTransform.anchorMin = Vector2.zero;
      textTransform.anchorMax = Vector2.one;
      textTransform.offsetMin = new Vector2(14f, 10f);
      textTransform.offsetMax = new Vector2(-14f, -10f);

      scoreText = textObject.AddComponent<Text>();
      scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      scoreText.fontSize = 24;
      scoreText.color = Color.white;
      scoreText.alignment = TextAnchor.MiddleLeft;
      scoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
      scoreText.verticalOverflow = VerticalWrapMode.Overflow;

      GameObject messageObject = new GameObject("Pickup Message");
      messageObject.transform.SetParent(transform, false);

      RectTransform messageTransform = messageObject.AddComponent<RectTransform>();
      messageTransform.anchorMin = new Vector2(0.5f, 1f);
      messageTransform.anchorMax = new Vector2(0.5f, 1f);
      messageTransform.pivot = new Vector2(0.5f, 1f);
      messageTransform.anchoredPosition = new Vector2(0f, -22f);
      messageTransform.sizeDelta = new Vector2(520f, 44f);

      pickupMessageText = messageObject.AddComponent<Text>();
      pickupMessageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      pickupMessageText.fontSize = 28;
      pickupMessageText.color = Color.white;
      pickupMessageText.alignment = TextAnchor.MiddleCenter;
      pickupMessageText.horizontalOverflow = HorizontalWrapMode.Overflow;
      pickupMessageText.verticalOverflow = VerticalWrapMode.Overflow;
      pickupMessageText.enabled = false;
    }

    void Refresh()
    {
      scoreLines.Clear();

      if (useNetworkScores) {
        foreach (PlayerScoreLine scoreLine in networkScoreLines.Values) {
          scoreLines.Add(scoreLine);
        }
      } else {
        CharacterScore[] scores = FindObjectsByType<CharacterScore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (CharacterScore score in scores) {
          CharacterController controller = score.GetComponentInParent<CharacterController>();
          if (controller == null) {
            controller = score.GetComponentInChildren<CharacterController>();
          }

          if (controller == null) { continue; }

          AddOrUpdateScoreLine(GetPlayerDisplayName(controller), score.Score);
        }
      }

      scoreLines.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));

      builder.Clear();
      builder.Append("Joueurs : ");
      builder.Append(scoreLines.Count);
      builder.Append(" / ");
      builder.Append(MaxDisplayedPlayers);

      int lineCount = Mathf.Min(scoreLines.Count, MaxDisplayedPlayers);
      for (int i = 0; i < lineCount; i++) {
        builder.AppendLine();
        builder.Append(scoreLines[i].Name);
        builder.Append(" : ");
        builder.Append(scoreLines[i].Score);
      }

      scoreText.text = builder.ToString();
    }

    void ShowPickupMessageInternal(CharacterController player)
    {
      if (pickupMessageText == null || player == null) { return; }

      pickupMessageText.text = GetPlayerDisplayName(player.Player) + " a ramassé un cube";
      pickupMessageText.enabled = true;
      pickupMessageUntil = Time.time + PickupMessageDuration;
    }

    void UpdatePickupMessage()
    {
      if (pickupMessageText == null || !pickupMessageText.enabled) { return; }

      if (Time.time >= pickupMessageUntil) {
        pickupMessageText.enabled = false;
      }
    }

    void RegisterNetworkManager()
    {
      networkManager = FindFirstObjectByType<NetworkManager>();
      if (networkManager == null) { return; }

      if (networkManager.OnInitState != null) {
        networkManager.OnInitState.AddListener(HandleInitState);
      }
      if (networkManager.OnPlayerJoin != null) {
        networkManager.OnPlayerJoin.AddListener(HandlePlayerJoin);
      }
      if (networkManager.OnPlayerLeft != null) {
        networkManager.OnPlayerLeft.AddListener(HandlePlayerLeft);
      }
      if (networkManager.OnBonusTaken != null) {
        networkManager.OnBonusTaken.AddListener(HandleBonusTaken);
      }
    }

    void UnregisterNetworkManager()
    {
      if (networkManager == null) { return; }

      if (networkManager.OnInitState != null) {
        networkManager.OnInitState.RemoveListener(HandleInitState);
      }
      if (networkManager.OnPlayerJoin != null) {
        networkManager.OnPlayerJoin.RemoveListener(HandlePlayerJoin);
      }
      if (networkManager.OnPlayerLeft != null) {
        networkManager.OnPlayerLeft.RemoveListener(HandlePlayerLeft);
      }
      if (networkManager.OnBonusTaken != null) {
        networkManager.OnBonusTaken.RemoveListener(HandleBonusTaken);
      }
    }

    void HandleInitState(InitStateMessage message)
    {
      useNetworkScores = true;
      networkScoreLines.Clear();

      if (message == null || message.players == null) { return; }

      foreach (NetPlayerSnapshot player in message.players) {
        SetNetworkScoreLine(player.id, player.name, player.score);
      }
    }

    void HandlePlayerJoin(PlayerJoinMessage message)
    {
      if (message == null) { return; }

      useNetworkScores = true;
      SetNetworkScoreLine(message.id, message.name, 0);
    }

    void HandlePlayerLeft(PlayerLeftMessage message)
    {
      if (message == null) { return; }

      networkScoreLines.Remove(message.id);
    }

    void HandleBonusTaken(BonusTakenMessage message)
    {
      if (message == null) { return; }

      useNetworkScores = true;
      PlayerScoreLine scoreLine;
      if (!networkScoreLines.TryGetValue(message.byPlayerId, out scoreLine)) {
        SetNetworkScoreLine(message.byPlayerId, message.byPlayerId, message.newScore);
        return;
      }

      scoreLine.Score = message.newScore;
      networkScoreLines[message.byPlayerId] = scoreLine;
    }

    void SetNetworkScoreLine(string playerId, string playerName, int score)
    {
      if (string.IsNullOrEmpty(playerId)) { return; }
      if (string.IsNullOrEmpty(playerName)) {
        playerName = playerId;
      }

      networkScoreLines[playerId] = new PlayerScoreLine {
        Name = playerName,
        Score = score,
        SortKey = GetPlayerSortKey(playerId),
      };
    }

    void AddOrUpdateScoreLine(string playerName, int score)
    {
      for (int i = 0; i < scoreLines.Count; i++) {
        if (scoreLines[i].Name != playerName) { continue; }

        PlayerScoreLine existingLine = scoreLines[i];
        existingLine.Score += score;
        scoreLines[i] = existingLine;
        return;
      }

      scoreLines.Add(new PlayerScoreLine {
        Name = playerName,
        Score = score,
        SortKey = GetPlayerSortKey(playerName),
      });
    }

    int GetPlayerSortKey(string playerName)
    {
      string digits = "";
      for (int i = 0; i < playerName.Length; i++) {
        if (char.IsDigit(playerName[i])) {
          digits += playerName[i];
        }
      }

      int sortKey;
      if (int.TryParse(digits, out sortKey)) {
        return sortKey;
      }

      return int.MaxValue;
    }

    string GetPlayerDisplayName(CharacterController player)
    {
      if (player == null) { return "Joueur"; }
      return GetPlayerDisplayName(player.Player);
    }

    string GetPlayerDisplayName(CharacterPlayer player)
    {
      switch (player) {
        case CharacterPlayer.Player1:
          return "Joueur 1";
        case CharacterPlayer.Player2:
          return "Joueur 2";
        default:
          return "Joueur";
      }
    }

    struct PlayerScoreLine
    {
      public string Name;
      public int Score;
      public int SortKey;
    }
}
