using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Écran de connexion auto-construit.
/// Permet de choisir un nom et un personnage avant de rejoindre le serveur.
/// </summary>
public class ConnectionUI : MonoBehaviour
{
    static readonly string[] CharacterNames = { "barbarian", "druid", "engineer", "knight", "mage", "rogue" };

    NetworkManager _net;
    RemotePlayerManager _rpm;
    InputField _nameInput;
    string _selectedCharacter = "barbarian";
    Button[] _charButtons;
    GameObject _panel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<ConnectionUI>() != null) return;
        var go = new GameObject("Connection UI");
        go.AddComponent<ConnectionUI>();
    }

    void Awake()
    {
        _net = FindFirstObjectByType<NetworkManager>();
        _rpm = FindFirstObjectByType<RemotePlayerManager>();
        BuildUI();
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        // Fond semi-transparent
        _panel = CreateRect(transform, "Panel");
        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        Stretch(_panel.GetComponent<RectTransform>());

        // Titre
        var title = CreateText(_panel.transform, "MetaVerse", 64);
        Place(title, 0.5f, 0.82f, 600, 80);

        // Label nom
        var nameLabel = CreateText(_panel.transform, "Nom du joueur", 24);
        Place(nameLabel, 0.5f, 0.68f, 400, 40);

        // Champ nom
        var inputGo = CreateRect(_panel.transform, "NameInput");
        Place(inputGo, 0.5f, 0.60f, 400, 50);
        inputGo.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
        _nameInput = inputGo.AddComponent<InputField>();
        var inputText = CreateText(inputGo.transform, "", 26);
        Place(inputText, 0.5f, 0.5f, 380, 46);
        inputText.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
        _nameInput.textComponent = inputText.GetComponent<Text>();
        _nameInput.text = _net != null ? _net.PlayerName : "Joueur";

        // Label perso
        var charLabel = CreateText(_panel.transform, "Choisir un personnage", 24);
        Place(charLabel, 0.5f, 0.49f, 600, 40);

        // Boutons perso
        _charButtons = new Button[CharacterNames.Length];
        float startX = 0.5f - (CharacterNames.Length - 1) * 0.07f;
        for (int i = 0; i < CharacterNames.Length; i++)
        {
            int idx = i;
            var btnGo = CreateRect(_panel.transform, CharacterNames[i]);
            Place(btnGo, startX + i * 0.14f, 0.38f, 160, 60);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = i == 0 ? new Color(0.2f, 0.6f, 1f) : new Color(0.3f, 0.3f, 0.4f);
            var btn = btnGo.AddComponent<Button>();
            var label = CreateText(btnGo.transform, CharacterNames[i], 18);
            Place(label, 0.5f, 0.5f, 150, 56);
            _charButtons[i] = btn;
            btn.onClick.AddListener(() => SelectCharacter(idx));
        }

        // Bouton Rejoindre
        var connectGo = CreateRect(_panel.transform, "ConnectBtn");
        Place(connectGo, 0.5f, 0.25f, 300, 60);
        connectGo.AddComponent<Image>().color = new Color(0.1f, 0.7f, 0.3f);
        var connectBtn = connectGo.AddComponent<Button>();
        var connectLabel = CreateText(connectGo.transform, "Rejoindre", 28);
        Place(connectLabel, 0.5f, 0.5f, 290, 56);
        connectBtn.onClick.AddListener(OnConnect);
    }

    void SelectCharacter(int index)
    {
        _selectedCharacter = CharacterNames[index];
        for (int i = 0; i < _charButtons.Length; i++)
        {
            var img = _charButtons[i].GetComponent<Image>();
            img.color = i == index ? new Color(0.2f, 0.6f, 1f) : new Color(0.3f, 0.3f, 0.4f);
        }
    }

    void OnConnect()
    {
        if (_net == null) return;
        string playerName = _nameInput != null ? _nameInput.text.Trim() : "Joueur";
        if (string.IsNullOrEmpty(playerName)) playerName = "Joueur";

        _net.PlayerName = playerName;
        _net.SelectedCharacter = _selectedCharacter;
        _net.Connect(playerName);

        Destroy(gameObject);
    }

    // --- Helpers UI ---

    GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    GameObject CreateText(Transform parent, string content, int fontSize)
    {
        var go = CreateRect(parent, "Text");
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return go;
    }

    void Place(GameObject go, float anchorX, float anchorY, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
