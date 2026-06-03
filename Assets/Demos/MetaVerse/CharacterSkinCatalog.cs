using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CharacterSkinCatalog
{
    public const int SkinCount = 6;

    static readonly CharacterSkinInfo[] Skins = {
      new CharacterSkinInfo("barbarian", "Barbare", "Assets/Demos/MetaVerse/Prefabs/barbarian.prefab"),
      new CharacterSkinInfo("druid", "Druide", "Assets/Demos/MetaVerse/Prefabs/druid.prefab"),
      new CharacterSkinInfo("engineer", "Ingenieur", "Assets/Demos/MetaVerse/Prefabs/engineer.prefab"),
      new CharacterSkinInfo("knight", "Chevalier", "Assets/Demos/MetaVerse/Prefabs/knight.prefab"),
      new CharacterSkinInfo("mage", "Mage", "Assets/Demos/MetaVerse/Prefabs/mage.prefab"),
      new CharacterSkinInfo("rogue", "Voleur", "Assets/Demos/MetaVerse/Prefabs/rogue.prefab"),
    };

    public static CharacterSkinInfo GetSkin(int index)
    {
      return Skins[Mathf.Clamp(index, 0, Skins.Length - 1)];
    }

    public static int GetSkinIndex(string skinId)
    {
      for (int i = 0; i < Skins.Length; i++) {
        if (Skins[i].Id == skinId) {
          return i;
        }
      }

      return -1;
    }

    public static GameObject LoadPrefab(CharacterSkinInfo skin)
    {
#if UNITY_EDITOR
      return AssetDatabase.LoadAssetAtPath<GameObject>(skin.PrefabPath);
#else
      return null;
#endif
    }
}

public struct CharacterSkinInfo
{
    public readonly string Id;
    public readonly string DisplayName;
    public readonly string PrefabPath;

    public CharacterSkinInfo(string id, string displayName, string prefabPath)
    {
      Id = id;
      DisplayName = displayName;
      PrefabPath = prefabPath;
    }
}
