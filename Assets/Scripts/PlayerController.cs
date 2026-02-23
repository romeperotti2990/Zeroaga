using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{

    Vector2 moveDir;
    float rotateAxis = 0f;
    public float speed = 5f;
    public float rotateSpeed = 5f;

    // Zoom
    public Camera targetCamera;
    public float zoomedOutSize = 12f;
    public float zoomedOutFov = 60f;
    public float zoomTransitionSpeed = 80f;
    bool isZoomedOut = false;
    float defaultOrthoSize = 5f;
    float defaultFov = 60f;
    float targetOrthoSize = 10f;
    float targetFov = 60f;

    // Visual tilt
    public Transform visualTransform;  // Assign the Visual child in the Inspector
    public float tiltAmount = 15f;  // Max tilt angle in degrees when moving

    // Shooting
    public GameObject projectilePrefab;
    public float fireCooldown = 0.1f;
    // shooting state/timer
    bool isShooting = false;
    float fireTimer = 0f;
    // it's serialized so frick editing it in here i guess
    public Vector2[] fireOffsets = new Vector2[4] {
        new Vector2(0.75f, 0.75f),
        new Vector2(-0.75f, 0.75f),
        new Vector2(0.8f, -0.75f),
        new Vector2(-0.8f, -0.75f)
    };

    // Alternative callback signature if using InputAction callbacks
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled)
            moveDir = Vector2.zero;
        else
            moveDir = ctx.ReadValue<Vector2>();
    }

    // Simple 1D axis handler for keyboard rotate (Q/E) or any float axis
    // stores the axis value so Update can apply smooth rotation
    public void OnRotate(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled) { rotateAxis = 0f; return; }
        try { rotateAxis = ctx.ReadValue<float>(); } catch { rotateAxis = 0f; }
    }

    // InputAction callback for hold-to-fire. Use Started to start and Canceled to stop.
    public void OnFire(InputAction.CallbackContext ctx)
    {
        bool pressed = false;
        try { pressed = ctx.ReadValue<float>() > 0f; } catch { }
        if (pressed)
        {
            if (!isShooting) { isShooting = true; fireTimer = 0f; Fire(); }
        }
        else
        {
            isShooting = false;
            fireTimer = 0f;
        }
    }

    public void OnZoom(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
        {
            return;
        }

        isZoomedOut = !isZoomedOut;
        SetZoomTarget();
    }

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        CacheDefaultZoom();
        SetZoomTarget();
    }

    // Spawn Projectiles at fire points
    void Fire()
    {
        if (projectilePrefab == null) { Debug.LogError("Assign projectilePrefab in the Inspector"); return; }
        foreach (var off in fireOffsets)
        {
            // convert local offset to world position so offsets rotate/move with the player
            Vector3 spawnPos = transform.TransformPoint(new Vector3(off.x, off.y, 0f));
            GameObject instance = Instantiate(projectilePrefab, spawnPos, transform.rotation);
            instance.SetActive(true);
        }
    }

    void Update()
    {
        // 2D movement relative to the player's facing (local space)
        Vector3 pos = transform.position;
        Vector3 localMove = transform.right * moveDir.x + transform.up * moveDir.y;
        pos += localMove * speed * Time.deltaTime;
        transform.position = pos;

        // 2D rotation on z axis (apply axis set by OnRotate)
        if (!Mathf.Approximately(rotateAxis, 0f))
        {
            transform.Rotate(0f, 0f, -rotateAxis * rotateSpeed * Time.deltaTime);
        }

        // Cosmetic tilt on the visual child (banking effect)
        if (visualTransform != null)
        {
            // Calculate target tilt based on input direction
            float targetTiltY = -moveDir.x * tiltAmount;  // Left/right strafe
            float targetTiltX = moveDir.y * 45f;   // Up/down movement (reversed, max 75 degrees)

            // Get current rotation
            Vector3 currentRotation = visualTransform.localEulerAngles;
            float currentY = currentRotation.y;
            float currentX = currentRotation.x;

            // Normalize angles to -180 to 180 range
            if (currentY > 180f) currentY -= 360f;
            if (currentX > 180f) currentX -= 360f;

            // Smoothly lerp to target tilts
            float newY = Mathf.Lerp(currentY, targetTiltY, Time.deltaTime * 8f);
            float newX = Mathf.Lerp(currentX, targetTiltX, Time.deltaTime * 8f);
            visualTransform.localRotation = Quaternion.Euler(newX, newY, 0f);
        }

        UpdateZoom();

        // handle shooting while held with cooldown
        if (isShooting)
        {
            fireTimer += Time.deltaTime;
            if (fireTimer >= fireCooldown)
            {
                Fire();
                fireTimer -= fireCooldown;
            }
        }
    }

    void CacheDefaultZoom()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (targetCamera.orthographic)
        {
            defaultOrthoSize = targetCamera.orthographicSize;
        }
        else
        {
            defaultFov = targetCamera.fieldOfView;
        }
    }

    void SetZoomTarget()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (targetCamera.orthographic)
        {
            targetOrthoSize = isZoomedOut ? zoomedOutSize : defaultOrthoSize;
        }
        else
        {
            targetFov = isZoomedOut ? zoomedOutFov : defaultFov;
        }
    }

    void UpdateZoom()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (targetCamera.orthographic)
        {
            float nextSize = Mathf.MoveTowards(targetCamera.orthographicSize, targetOrthoSize, zoomTransitionSpeed * Time.deltaTime);
            targetCamera.orthographicSize = nextSize;
        }
        else
        {
            float nextFov = Mathf.MoveTowards(targetCamera.fieldOfView, targetFov, zoomTransitionSpeed * Time.deltaTime);
            targetCamera.fieldOfView = nextFov;
        }
    }
}


