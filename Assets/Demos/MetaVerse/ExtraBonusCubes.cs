using UnityEngine;

public class ExtraBonusCubes : MonoBehaviour
{
    public int BonusCount = 8;
    public float Radius = 8f;
    public float CubeHeight = 1.164f;
    public LayerMask CollisionLayers = 1 << 6;

    static Material cubeMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateExtraBonuses()
    {
      if (FindFirstObjectByType<ExtraBonusCubes>() != null) { return; }

      GameObject spawner = new GameObject("Extra Bonus Cubes");
      spawner.AddComponent<ExtraBonusCubes>();
    }

    void Start()
    {
      Bonus referenceBonus = FindFirstObjectByType<Bonus>();
      if (referenceBonus == null) { return; }

      Vector3 center = referenceBonus.transform.position;
      int points = referenceBonus.Points;
      LayerMask collisionLayers = referenceBonus.CollisionLayers;
      int bonusLayer = referenceBonus.gameObject.layer;

      for (int i = 0; i < BonusCount; i++) {
        float angle = i * Mathf.PI * 2f / BonusCount;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Radius;
        CreateBonusCube(center + offset, points, collisionLayers, bonusLayer, i + 1);
      }
    }

    void CreateBonusCube(Vector3 position, int points, LayerMask collisionLayers, int bonusLayer, int index)
    {
      GameObject bonusObject = new GameObject("Bonus Extra " + index);
      bonusObject.layer = bonusLayer;
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
