using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class CharacterSkinRandomizer : MonoBehaviour
{
    const string RandomizerName = "Character Skin Randomizer";
    const string RuntimeSkinNamePrefix = "Runtime Skin - ";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateRandomizer()
    {
      if (GameObject.Find(RandomizerName) != null) { return; }

      CharacterController[] players = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
      if (players.Length == 0) { return; }

      GameObject randomizer = new GameObject(RandomizerName);
      randomizer.AddComponent<CharacterSkinRandomizer>();
    }

    void Awake()
    {
      AssignRandomSkins();
    }

    void AssignRandomSkins()
    {
      CharacterController[] players = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
      if (players.Length == 0) { return; }

      List<int> skinIndexes = CreateShuffledSkinIndexes();
      int count = Mathf.Min(players.Length, skinIndexes.Count);

      for (int i = 0; i < count; i++) {
        CharacterSkinInfo skin = CharacterSkinCatalog.GetSkin(skinIndexes[i]);
        ApplySkin(players[i], skin);
      }

      for (int i = CharacterSkinCatalog.SkinCount; i < players.Length; i++) {
        players[i].gameObject.SetActive(false);
      }
    }

    List<int> CreateShuffledSkinIndexes()
    {
      List<int> indexes = new List<int>();
      for (int i = 0; i < CharacterSkinCatalog.SkinCount; i++) {
        indexes.Add(i);
      }

      for (int i = indexes.Count - 1; i > 0; i--) {
        int swapIndex = Random.Range(0, i + 1);
        int current = indexes[i];
        indexes[i] = indexes[swapIndex];
        indexes[swapIndex] = current;
      }

      return indexes;
    }

    void ApplySkin(CharacterController player, CharacterSkinInfo skin)
    {
      GameObject prefab = CharacterSkinCatalog.LoadPrefab(skin);
      if (prefab == null) { return; }

      HideCurrentModel(player.transform);

      GameObject skinObject = Instantiate(prefab, player.transform);
      skinObject.name = RuntimeSkinNamePrefix + skin.DisplayName;
      skinObject.transform.localPosition = Vector3.zero;
      skinObject.transform.localRotation = Quaternion.identity;
      skinObject.transform.localScale = Vector3.one;
      skinObject.AddComponent<RuntimeCharacterSkin>();
      PrepareSkinOnlyObject(skinObject);

      player.SkinId = skin.Id;
      player.RefreshVisualReferences();
    }

    void PrepareSkinOnlyObject(GameObject skinObject)
    {
      CharacterController[] controllers = skinObject.GetComponentsInChildren<CharacterController>(true);
      foreach (CharacterController controller in controllers) {
        controller.enabled = false;
      }

      CharacterScore[] scores = skinObject.GetComponentsInChildren<CharacterScore>(true);
      foreach (CharacterScore score in scores) {
        score.gameObject.SetActive(false);
      }

      Rigidbody[] rigidbodies = skinObject.GetComponentsInChildren<Rigidbody>(true);
      foreach (Rigidbody currentRigidbody in rigidbodies) {
        currentRigidbody.isKinematic = true;
        currentRigidbody.detectCollisions = false;
      }

      Collider[] colliders = skinObject.GetComponentsInChildren<Collider>(true);
      foreach (Collider currentCollider in colliders) {
        currentCollider.enabled = false;
      }
    }

    void HideCurrentModel(Transform playerRoot)
    {
      for (int i = playerRoot.childCount - 1; i >= 0; i--) {
        Transform child = playerRoot.GetChild(i);
        if (child.name.StartsWith(RuntimeSkinNamePrefix)) {
          Destroy(child.gameObject);
        }
      }

      Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
      foreach (Renderer currentRenderer in renderers) {
        if (currentRenderer.GetComponentInParent<CharacterScore>() != null) { continue; }
        if (currentRenderer.GetComponentInParent<RuntimeCharacterSkin>() != null) { continue; }

        if (currentRenderer.GetComponent<HiddenCharacterSkinRenderer>() == null) {
          currentRenderer.gameObject.AddComponent<HiddenCharacterSkinRenderer>();
        }
        currentRenderer.enabled = false;
      }
    }
}

public class RuntimeCharacterSkin : MonoBehaviour
{
}

public class HiddenCharacterSkinRenderer : MonoBehaviour
{
}
