using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/// <summary>
/// Caméra troisième personne fixée derrière la cible Cinemachine.
/// Suit la rotation du personnage (clavier uniquement — aucun contrôle souris).
/// </summary>
[RequireComponent(typeof(CinemachineFollow))]
public class CameraMouseOrbit : MonoBehaviour
{
    [Header("Suivi")]
    [Tooltip("Plus la valeur est haute, plus la caméra suit le yaw du joueur lentement (ex. 0,2–0,4).")]
    public float CameraYawLag = 0.25f;

    CinemachineFollow follow;

    void Awake()
    {
        follow = GetComponent<CinemachineFollow>();
        if (follow == null)
            return;

        var settings = follow.TrackerSettings;
        settings.BindingMode = BindingMode.LockToTargetWithWorldUp;
        settings.PositionDamping = Vector3.zero;

        // Cinemachine : amortissement élevé = rotation plus lente (moins de secousse).
        float yawDamp = CameraYawLag <= 0f
            ? 0f
            : Mathf.Clamp(CameraYawLag * 14f, 1.5f, 6f);
        settings.RotationDamping = new Vector3(0f, yawDamp, 0f);
        settings.QuaternionDamping = yawDamp;

        follow.TrackerSettings = settings;

        EnsureCursorUnlocked();
    }

    void OnDisable()
    {
        EnsureCursorUnlocked();
    }

    static void EnsureCursorUnlocked()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
