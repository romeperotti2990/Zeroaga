using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{

    Vector2 moveDir;
    float rotateAxis = 0f;
    public float speed = 5f;
    public float rotateSpeed = 5f;
    // World boundary settings
    public bool useWorldBounds = true;
    // If true, uses a circular bound defined by worldLimitRadius. If false, uses the Rect `worldBounds`.
    public bool useCircularWorldBounds = false;
    public Rect worldBounds = new Rect(-20f, -20f, 40f, 40f);
    public float worldLimitRadius = 20f;
    // Option to base the world limit on the AsteroidSpawner (giant circular world)
    public bool useAsteroidSpawnerBounds = true;
    // follow the spawner's transform as the center of the circle
    public bool followAsteroidSpawnerCenter = true;
    // computed or inspector-overridable center for circular bounds
    public Vector2 worldBoundsCenter = Vector2.zero;
    private AsteroidSpawner asteroidSpawnerRef;
    // Visualization options
    public bool showBoundaryInScene = true;
    public bool showBoundaryInGame = false;
    public int boundarySegments = 64;
    public float boundaryLineWidth = 0.08f;
    private LineRenderer boundaryRenderer;

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

        // If requested, find the AsteroidSpawner and use its large field radius as the circular world limit
        if (useAsteroidSpawnerBounds)
        {
            AsteroidSpawner sp = FindObjectOfType<AsteroidSpawner>();
            if (sp != null)
            {
                asteroidSpawnerRef = sp;
                useWorldBounds = true;
                useCircularWorldBounds = true;
                worldLimitRadius = sp.largeFieldRadius;
                worldBoundsCenter = sp.transform.position;
            }
        }

        // If requested, create a LineRenderer to visualize the boundary in-game
        if (showBoundaryInGame)
        {
            GameObject go = new GameObject("BoundaryRenderer");
            go.transform.SetParent(transform, false);
            boundaryRenderer = go.AddComponent<LineRenderer>();
            boundaryRenderer.loop = true;
            boundaryRenderer.useWorldSpace = true;
            boundaryRenderer.positionCount = Mathf.Max(3, boundarySegments);
            boundaryRenderer.startWidth = boundaryLineWidth;
            boundaryRenderer.endWidth = boundaryLineWidth;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.cyan;
            boundaryRenderer.material = mat;
            UpdateBoundaryRenderer();
        }
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

        // Update bounds center/radius from AsteroidSpawner if requested
        if (useAsteroidSpawnerBounds && asteroidSpawnerRef == null)
        {
            asteroidSpawnerRef = FindObjectOfType<AsteroidSpawner>();
        }

        if (asteroidSpawnerRef != null && followAsteroidSpawnerCenter)
        {
            worldBoundsCenter = asteroidSpawnerRef.transform.position;
            worldLimitRadius = asteroidSpawnerRef.largeFieldRadius;
            useCircularWorldBounds = true;
            useWorldBounds = true;
        }

        // Enforce world boundaries so the player can't move outside the playable area
        if (useWorldBounds)
        {
            Vector3 clamped = transform.position;
            if (useCircularWorldBounds)
            {
                Vector2 center = worldBoundsCenter; // use the computed/inspector center
                Vector2 offset = (Vector2)clamped - center;
                float dist = offset.magnitude;
                if (dist > worldLimitRadius)
                {
                    Vector2 limited = offset.normalized * worldLimitRadius;
                    clamped = new Vector3(center.x + limited.x, center.y + limited.y, clamped.z);
                    transform.position = clamped;
                    moveDir = Vector2.zero;
                }
            }
            else
            {
                float minX = worldBounds.xMin;
                float maxX = worldBounds.xMax;
                float minY = worldBounds.yMin;
                float maxY = worldBounds.yMax;
                float newX = Mathf.Clamp(clamped.x, minX, maxX);
                float newY = Mathf.Clamp(clamped.y, minY, maxY);
                if (newX != clamped.x || newY != clamped.y)
                {
                    transform.position = new Vector3(newX, newY, clamped.z);
                    moveDir = Vector2.zero;
                }
            }
        }

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

        // update boundary renderer if active
        if (showBoundaryInGame && boundaryRenderer != null)
        {
            UpdateBoundaryRenderer();
        }

    }

    void OnDrawGizmos()
    {
        if (!showBoundaryInScene || !useWorldBounds || !useCircularWorldBounds)
            return;

        // Prefer asteroid spawner center if available
        Vector2 center = worldBoundsCenter;
        if (asteroidSpawnerRef == null)
        {
            asteroidSpawnerRef = FindObjectOfType<AsteroidSpawner>();
        }
        if (asteroidSpawnerRef != null && followAsteroidSpawnerCenter)
        {
            center = asteroidSpawnerRef.transform.position;
        }

        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        Gizmos.DrawWireSphere(new Vector3(center.x, center.y, transform.position.z), worldLimitRadius);
    }


    void UpdateBoundaryRenderer()
    {
        if (boundaryRenderer == null) return;

        Vector2 center = worldBoundsCenter;
        if (asteroidSpawnerRef != null && followAsteroidSpawnerCenter)
        {
            center = asteroidSpawnerRef.transform.position;
        }

        int segs = Mathf.Max(3, boundarySegments);
        boundaryRenderer.positionCount = segs;
        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / segs * Mathf.PI * 2f;
            float x = Mathf.Cos(t) * worldLimitRadius + center.x;
            float y = Mathf.Sin(t) * worldLimitRadius + center.y;
            boundaryRenderer.SetPosition(i, new Vector3(x, y, transform.position.z));
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


