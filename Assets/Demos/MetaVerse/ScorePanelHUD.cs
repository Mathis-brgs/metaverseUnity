using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ScorePanelHUD : MonoBehaviour
{
    public Vector2 PanelPosition = new Vector2(18f, -18f);
    public Vector2 PanelSize = new Vector2(190f, 78f);
    public float RefreshInterval = 0.15f;
    public float PickupMessageDuration = 2f;

    Text scoreText;
    Text pickupMessageText;
    float nextRefreshTime;
    float pickupMessageUntil;
    readonly StringBuilder builder = new StringBuilder();

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
      Refresh();
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
      int player1Score = 0;
      int player2Score = 0;

      CharacterScore[] scores = FindObjectsByType<CharacterScore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
      foreach (CharacterScore score in scores) {
        CharacterController controller = score.GetComponentInParent<CharacterController>();
        if (controller == null) {
          controller = score.GetComponentInChildren<CharacterController>();
        }

        if (controller == null) { continue; }

        switch (controller.Player) {
          case CharacterPlayer.Player1:
            player1Score += score.Score;
            break;
          case CharacterPlayer.Player2:
            player2Score += score.Score;
            break;
        }
      }

      builder.Clear();
      builder.Append("Joueur 1 : ");
      builder.Append(player1Score);
      builder.AppendLine();
      builder.Append("Joueur 2 : ");
      builder.Append(player2Score);

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
}
