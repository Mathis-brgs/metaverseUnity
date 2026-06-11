using UnityEngine;

/// <summary>
/// Représentation physique autoritaire d'un joueur connecté, côté serveur.
/// Reçoit les intentions de déplacement (INPUT), applique la physique (collisions murs/obstacles)
/// et réécrit la position résultante dans le <see cref="WorldState"/> pour le broadcast STATE.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ServerPlayerProxy : MonoBehaviour
{
    public float WalkSpeed = 3f;
    public float TurnSpeed = 720f;
    [Tooltip("Layer des personnages (doit matcher Bonus.CollisionLayers).")]
    public int CharacterLayer = 6;

    public string PlayerId { get; private set; }

    static int _spawnIndex;

    PlayerState _state;
    WorldState _world;
    Rigidbody _rb;
    Vector3 _inputDir;     // direction monde voulue (x,z dans [-1,1])
    float _targetRotY;
    bool _hasInput;

    public void Init(PlayerState state, WorldState world)
    {
        _state = state;
        _world = world;
        PlayerId = state.Id;

        gameObject.layer = CharacterLayer;

        var col = GetComponent<Collider>();
        if (col == null)
        {
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.radius = 0.4f;
        }

        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rb.interpolation = RigidbodyInterpolation.None;

        Vector3 spawnBase = new Vector3(250.406921f, 0.318325f, 248.204315f);
        Vector3 spawn = spawnBase + new Vector3((_spawnIndex % 4) * 1.5f, 0f, (_spawnIndex / 4) * 1.5f);
        _spawnIndex++;
        transform.position = spawn;
        _targetRotY = 0f;

        WriteBack();
    }

    public void SetInput(float ix, float iz, float rotY)
    {
        // Mode INPUT : le serveur simule la physique (collisions) → Rigidbody dynamique.
        if (_rb != null && _rb.isKinematic) _rb.isKinematic = false;

        _inputDir = new Vector3(ix, 0f, iz);
        if (_inputDir.sqrMagnitude > 1f) _inputDir.Normalize();
        _targetRotY = rotY;
        _hasInput = true;
    }

    /// <summary>
    /// Place directement le proxy à une position (client legacy MOVE). Utilise rb.position
    /// pour conserver la détection des triggers de bonus.
    /// </summary>
    public void ApplyDirectPosition(Vector3 position, float rotY)
    {
        // Mode legacy : pas de gravité ni simulation, on suit exactement la position du client.
        if (_rb != null)
        {
            if (!_rb.isKinematic) _rb.isKinematic = true;
            _rb.position = position;
            _rb.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
        transform.position = position;
        transform.eulerAngles = new Vector3(0f, rotY, 0f);
        _hasInput = false;
        WriteBack();
    }

    void FixedUpdate()
    {
        if (_state == null || _rb == null) return;

        // En voiture : le proxy ne bouge pas, la voiture fait autorité.
        if (!string.IsNullOrEmpty(_state.InCarId))
        {
            _rb.linearVelocity = Vector3.zero;
            return;
        }

        _rb.angularVelocity = Vector3.zero;

        if (_hasInput && _inputDir.sqrMagnitude > 0.0001f)
        {
            Vector3 movement = _inputDir * WalkSpeed * Time.fixedDeltaTime;
            _rb.MovePosition(_rb.position + movement);

            Quaternion target = Quaternion.LookRotation(_inputDir.normalized, Vector3.up);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, target, TurnSpeed * Time.fixedDeltaTime));
        }

        WriteBack();
    }

    void WriteBack()
    {
        if (_state == null) return;
        Vector3 p = transform.position;
        _state.X = p.x;
        _state.Y = p.y;
        _state.Z = p.z;
        _state.RotY = transform.eulerAngles.y;
    }
}
