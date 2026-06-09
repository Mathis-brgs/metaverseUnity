using Unity.Cinemachine;
using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (ServerMode.Active) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = cam.transform.position - transform.position;
        dir = dir.ProjectOntoPlane(Vector3.up).normalized;

        transform.LookAt(transform.position - dir, Vector3.up);
    }
}
