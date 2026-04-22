using UnityEngine;

public class SpriteBillboardY : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Header("Rotation")]
    [Range(-180f, 180f)] public float yawOffset;
    public bool reverseFacing;

    [Header("Runtime")]
    public bool disableDuringDialogue = true;

    void LateUpdate()
    {
        if (disableDuringDialogue && DialogueManager.IsDialogueActive) return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        Vector3 toCamera = cam.transform.position - transform.position;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude < 0.0001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        Quaternion offsetRotation = Quaternion.Euler(0f, yawOffset + (reverseFacing ? 180f : 0f), 0f);
        transform.rotation = lookRotation * offsetRotation;
    }
}
