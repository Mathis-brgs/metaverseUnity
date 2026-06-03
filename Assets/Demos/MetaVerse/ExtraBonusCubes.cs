using UnityEngine;

public class ExtraBonusCubes : MonoBehaviour
{
    public int BonusCount = 8;
    public int RandomSeed = 12345;
    public Vector2 AreaCenter = new Vector2(250f, 255f);
    public Vector2 AreaSize = new Vector2(42f, 42f);
    public float MinimumDistanceBetweenCubes = 4f;
    public float CubeHeight = 1.164f;
    public int ExtraBonusPoints = 1;
    public LayerMask CollisionLayers = 1 << 6;

    static Material cubeMaterial;
    const string GeneratedRootName = "Generated Bonus Cubes";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateExtraBonuses()
    {
      if (FindFirstObjectByType<ExtraBonusCubes>() != null) { return; }

      GameObject spawner = new GameObject("Extra Bonus Cubes");
      spawner.AddComponent<ExtraBonusCubes>();
    }

    void Start()
    {
      RebuildCubes();
    }

    void OnValidate()
    {
      BonusCount = Mathf.Max(0, BonusCount);
      AreaSize = new Vector2(Mathf.Max(0f, AreaSize.x), Mathf.Max(0f, AreaSize.y));
      MinimumDistanceBetweenCubes = Mathf.Max(0f, MinimumDistanceBetweenCubes);
      CubeHeight = Mathf.Max(0f, CubeHeight);
      ExtraBonusPoints = Mathf.Max(1, ExtraBonusPoints);

      // La generation se fait au Play pour eviter de modifier la scene pendant le chargement Unity.
    }

    void RebuildCubes()
    {
      ClearGeneratedCubes();
      BuildCubes();
    }

    void EnsureCubes()
    {
      Transform existingRoot = transform.Find(GeneratedRootName);
      if (existingRoot != null && existingRoot.childCount > 0) { return; }

      BuildCubes();
    }

    void BuildCubes()
    {
      Bonus referenceBonus = FindReferenceBonus();
      if (referenceBonus == null) { return; }

      int points = ExtraBonusPoints;
      LayerMask collisionLayers = referenceBonus.CollisionLayers;
      int bonusLayer = referenceBonus.gameObject.layer;

      Random.InitState(RandomSeed);
      Vector3[] positions = new Vector3[BonusCount];
      int placedCount = 0;
      int attempts = 0;
      int maxAttempts = Mathf.Max(40, BonusCount * 25);

      while (placedCount < BonusCount && attempts < maxAttempts) {
        attempts++;
        Vector3 position = GetRandomPosition(referenceBonus.transform.position.y);
        if (!CanPlaceAt(position, positions, placedCount)) { continue; }

        positions[placedCount] = position;
        placedCount++;
      }

      for (int i = 0; i < placedCount; i++) {
        CreateBonusCube(positions[i], points, collisionLayers, bonusLayer, i + 1);
      }
    }

    Vector3 GetRandomPosition(float height)
    {
      float x = AreaCenter.x + Random.Range(-AreaSize.x * 0.5f, AreaSize.x * 0.5f);
      float z = AreaCenter.y + Random.Range(-AreaSize.y * 0.5f, AreaSize.y * 0.5f);
      return new Vector3(x, height, z);
    }

    bool CanPlaceAt(Vector3 position, Vector3[] existingPositions, int placedCount)
    {
      float minDistanceSqr = MinimumDistanceBetweenCubes * MinimumDistanceBetweenCubes;
      for (int i = 0; i < placedCount; i++) {
        Vector3 diff = position - existingPositions[i];
        diff.y = 0f;
        if (diff.sqrMagnitude < minDistanceSqr) {
          return false;
        }
      }

      return true;
    }

    Bonus FindReferenceBonus()
    {
      Bonus[] bonuses = FindObjectsByType<Bonus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
      foreach (Bonus bonus in bonuses) {
        if (!bonus.transform.IsChildOf(transform)) {
          return bonus;
        }
      }

      return null;
    }

    void ClearGeneratedCubes()
    {
      Transform existingRoot = transform.Find(GeneratedRootName);
      if (existingRoot == null) { return; }

      Destroy(existingRoot.gameObject);
    }

    void CreateBonusCube(Vector3 position, int points, LayerMask collisionLayers, int bonusLayer, int index)
    {
      Transform generatedRoot = GetGeneratedRoot();
      GameObject bonusObject = new GameObject("Bonus Extra " + index);
      bonusObject.layer = bonusLayer;
      bonusObject.transform.SetParent(generatedRoot, false);
      bonusObject.transform.position = position;

      BoxCollider trigger = bonusObject.AddComponent<BoxCollider>();
      trigger.isTrigger = true;
      trigger.size = new Vector3(1.4807739f, 1.9355191f, 1.5086975f);
      trigger.center = new Vector3(0.0104522705f, 1.0582217f, -0.017410278f);

      Rotate rotate = bonusObject.AddComponent<Rotate>();
      rotate.RotateSpeed = 80f;
      rotate.RotateAxis = Vector3.up;
      rotate.RotateSpace = Space.Self;

      Bonus bonus = bonusObject.AddComponent<Bonus>();
      bonus.CollisionLayers = collisionLayers;
      bonus.Points = points;

      GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube.name = "Cube";
      cube.layer = bonusLayer;
      cube.transform.SetParent(bonusObject.transform, false);
      cube.transform.localPosition = new Vector3(0f, CubeHeight, 0f);
      cube.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
      cube.transform.localScale = Vector3.one;

      Collider cubeCollider = cube.GetComponent<Collider>();
      if (cubeCollider != null) {
        Destroy(cubeCollider);
      }

      MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
      renderer.sharedMaterial = GetCubeMaterial();
    }

    Transform GetGeneratedRoot()
    {
      Transform generatedRoot = transform.Find(GeneratedRootName);
      if (generatedRoot != null) { return generatedRoot; }

      GameObject root = new GameObject(GeneratedRootName);
      root.transform.SetParent(transform, false);
      root.transform.localPosition = Vector3.zero;
      root.transform.localRotation = Quaternion.identity;
      root.transform.localScale = Vector3.one;
      return root.transform;
    }

    static Material GetCubeMaterial()
    {
      if (cubeMaterial != null) { return cubeMaterial; }

      Shader shader = Shader.Find("Universal Render Pipeline/Lit");
      if (shader == null) {
        shader = Shader.Find("Standard");
      }

      cubeMaterial = new Material(shader);
      cubeMaterial.name = "Runtime Bonus Cube";
      cubeMaterial.color = new Color(1f, 0f, 0.84f, 0.72f);
      return cubeMaterial;
    }
}
