using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineFollow))]
public class CameraMouseOrbit : MonoBehaviour
{
    public float MouseSensitivity = 0.2f;
    public float RotationSmoothTime = 0.08f;
    public float MinPitch = 15f;
    public float MaxPitch = 75f;
    public bool HideCursorWhileDragging = true;

    CinemachineFollow follow;
    float yaw;
    float pitch;
    float targetYaw;
    float targetPitch;
    float yawVelocity;
    float pitchVelocity;
    float distance;

    void Awake()
    {
        follow = GetComponent<CinemachineFollow>();
        ReadCurrentOffset();
    }

    void Update()
    {
        if (Mouse.current == null || follow == null) { return; }

        if (CharacterController.InputFrozen)
        {
            SetCursorDragging(false);
            return;
        }

        bool dragging = Mouse.current.leftButton.isPressed;
        SetCursorDragging(dragging);

        if (!dragging) { return; }

        Vector2 delta = Mouse.current.delta.ReadValue();
        targetYaw += delta.x * MouseSensitivity;
        targetPitch = Mathf.Clamp(targetPitch - delta.y * MouseSensitivity, MinPitch, MaxPitch);
    }

    void LateUpdate()
    {
        if (follow == null) { return; }

        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, RotationSmoothTime);
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, RotationSmoothTime);
        ApplyOffset();
    }

    void OnDisable()
    {
        SetCursorDragging(false);
    }

    void ReadCurrentOffset()
    {
        Vector3 offset = follow.FollowOffset;
        distance = Mathf.Max(0.1f, offset.magnitude);

        Vector2 flatOffset = new Vector2(offset.x, offset.z);
        float horizontalDistance = Mathf.Max(0.01f, flatOffset.magnitude);

        yaw = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(Mathf.Atan2(offset.y, horizontalDistance) * Mathf.Rad2Deg, MinPitch, MaxPitch);
        targetYaw = yaw;
        targetPitch = pitch;
    }

    void ApplyOffset()
    {
        float pitchRad = pitch * Mathf.Deg2Rad;
        float yawRad = yaw * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(pitchRad) * distance;

        follow.FollowOffset = new Vector3(
            Mathf.Sin(yawRad) * horizontalDistance,
            Mathf.Sin(pitchRad) * distance,
            -Mathf.Cos(yawRad) * horizontalDistance
        );
    }

    void SetCursorDragging(bool dragging)
    {
        if (!HideCursorWhileDragging) { return; }

        Cursor.lockState = dragging ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !dragging;
    }
}
