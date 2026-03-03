using UnityEngine;

public class AsteroidSpawner : MonoBehaviour
{
    public GameObject asteroidPrefab;
    public int minAsteroidCount = 10;  // Minimum asteroids to maintain near player
    public int maxTotalAsteroids = 20;
    public int maxSpawnPerCheck = 3;
    public float spawnRadius = 15f;
    public float minSpawnDistance = 4f;
    // radius around player where asteroids should not spawn
    public float safeSpawnRadius = 3f;
    public float activeRadius = 20f;
    public float repositionDistance = 35f;
    public float spawnBuffer = 6f;
    public float outOfViewPadding = 2f;
    public Camera targetCamera;
    public Vector2 minSize = new Vector2(1f, 1f);
    public Vector2 maxSize = new Vector2(2.5f, 2.5f);
    public float spawnCheckInterval = 2f;  // How often to check asteroid count
    public int maxSpawnAttempts = 12;

    [Header("One-time large field (optional)")]
    public bool spawnLargeFieldOnce = true;
    public int largeFieldCount = 200;
    public float largeFieldRadius = 500f;

    private float spawnCheckTimer = 0f;
    public Transform playerTransform;
    private bool largeFieldSpawned = false;

    void Start()
    {
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        if (spawnLargeFieldOnce)
        {
            SpawnLargeAsteroidField(largeFieldCount, largeFieldRadius);
            largeFieldSpawned = true;
        }
        else
        {
            SpawnAsteroids(minAsteroidCount);
        }
    }

    void Update()
    {
        EnsurePlayerTransform();
        if (playerTransform == null)
        {
            return;
        }

        // If we've already spawned a single large field, skip periodic spawning
        if (spawnLargeFieldOnce && largeFieldSpawned)
        {
            return;
        }

        EnsureCamera();

        spawnCheckTimer += Time.deltaTime;

        if (spawnCheckTimer >= spawnCheckInterval)
        {
            spawnCheckTimer = 0f;

            // Count current asteroids by finding all Asteroid components
            // Only count asteroids within `activeRadius` as "active" for spawning decisions
            Asteroid[] allAsteroids = FindObjectsOfType<Asteroid>();
            int currentCount = 0;
            foreach (Asteroid asteroid in allAsteroids)
            {
                float distanceToPlayer = Vector2.Distance(asteroid.transform.position, playerTransform.position);
                if (distanceToPlayer <= activeRadius)
                {
                    currentCount++;
                }
            }

            Debug.Log($"Total asteroids: {allAsteroids.Length}, Active asteroids: {currentCount}");

            if (currentCount < minAsteroidCount)
            {
                int availableSlots = Mathf.Max(0, maxTotalAsteroids - currentCount);
                int spawnCount = Mathf.Min(minAsteroidCount - currentCount, maxSpawnPerCheck, availableSlots);
                if (spawnCount > 0)
                {
                    Debug.Log($"Spawning {spawnCount} new asteroids!");
                    SpawnAsteroids(spawnCount);
                }
            }
        }
    }

    void SpawnAsteroids(int count)
    {
        if (asteroidPrefab == null)
        {
            Debug.LogError("Assign asteroidPrefab in the Inspector");
            return;
        }

        EnsurePlayerTransform();
        EnsureCamera();

        // Get player position (or use origin if player not found)
        Vector3 centerPos = playerTransform != null ? playerTransform.position : transform.position;

        Debug.Log($"Spawning {count} asteroids at position {centerPos}");

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(centerPos);

            // Spawn asteroid
            GameObject asteroid = Instantiate(asteroidPrefab, spawnPos, Quaternion.identity);

            // Random scale
            float scale = Random.Range(minSize.x, maxSize.x);
            asteroid.transform.localScale = Vector3.one * scale;

            // Set random velocity
            Asteroid asteroidScript = asteroid.GetComponent<Asteroid>();
            if (asteroidScript != null)
            {
                asteroidScript.velocity = Random.insideUnitCircle * Random.Range(0.5f, 2f);
                asteroidScript.mass = scale; // Larger asteroids are heavier
            }
            else
            {
                Debug.LogError("Spawned asteroid doesn't have Asteroid script!");
            }
        }

        Debug.Log($"Finished spawning {count} asteroids");
    }

    void EnsurePlayerTransform()
    {
        if (playerTransform != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    void EnsureCamera()
    {
        if (targetCamera != null)
        {
            return;
        }

        targetCamera = Camera.main;
    }

    Vector3 GetSpawnPosition(Vector3 centerPos)
    {
        float minRadius = Mathf.Max(0f, minSpawnDistance);
        float outOfViewRadius = GetOutOfViewRadius();
        // Clamp outOfViewRadius so huge camera zooms don't push spawns extremely far away.
        if (outOfViewRadius > spawnRadius)
        {
            outOfViewRadius = spawnRadius;
        }
        minRadius = Mathf.Max(minRadius, outOfViewRadius + outOfViewPadding);
        // ensure we don't spawn inside the player's safe bubble
        minRadius = Mathf.Max(minRadius, safeSpawnRadius);
        float maxRadius = Mathf.Max(minRadius, minRadius + spawnBuffer);

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float radius = Random.Range(minRadius, maxRadius);
            Vector2 randomDir = Random.insideUnitCircle;
            if (randomDir.sqrMagnitude < 0.0001f)
            {
                randomDir = Vector2.right;
            }
            randomDir.Normalize();
            Vector2 randomPos = randomDir * radius;
            Vector3 candidate = centerPos + new Vector3(randomPos.x, randomPos.y, 0f);
            // avoid spawning inside the player's safe bubble and avoid camera view
            if (Vector2.Distance(candidate, centerPos) < safeSpawnRadius)
            {
                continue;
            }
            if (!IsInCameraView(candidate))
            {
                return candidate;
            }
        }

        Vector2 fallbackDir = ((Vector2)centerPos - (Vector2)targetCamera.transform.position).normalized;
        if (fallbackDir.sqrMagnitude < 0.0001f)
        {
            fallbackDir = Vector2.right;
        }
        Vector3 fallbackPos = centerPos + (Vector3)(fallbackDir * minRadius);
        return fallbackPos;
    }

    float GetOutOfViewRadius()
    {
        if (targetCamera == null)
        {
            return 0f;
        }

        if (targetCamera.orthographic)
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
        }

        float distance = Mathf.Abs(targetCamera.transform.position.z - playerTransform.position.z);
        Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        float halfWidthPerspective = Mathf.Abs(topRight.x - bottomLeft.x) * 0.5f;
        float halfHeightPerspective = Mathf.Abs(topRight.y - bottomLeft.y) * 0.5f;
        return Mathf.Sqrt(halfWidthPerspective * halfWidthPerspective + halfHeightPerspective * halfHeightPerspective);
    }

    bool IsInCameraView(Vector3 worldPos)
    {
        if (targetCamera == null)
        {
            return false;
        }

        Vector3 viewportPos = targetCamera.WorldToViewportPoint(worldPos);
        if (viewportPos.z <= 0f)
        {
            return false;
        }

        return viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f;
    }

    void RepositionAsteroid(Asteroid asteroid)
    {
        if (asteroid == null || playerTransform == null)
        {
            return;
        }

        Vector3 spawnPos = GetSpawnPosition(playerTransform.position);
        asteroid.transform.position = spawnPos;
        asteroid.velocity = Random.insideUnitCircle * Random.Range(0.5f, 2f);
        if (asteroid.rb != null)
        {
            asteroid.rb.position = spawnPos;
            asteroid.rb.linearVelocity = asteroid.velocity;
        }
    }

    void SpawnLargeAsteroidField(int count, float radius)
    {
        if (asteroidPrefab == null)
        {
            Debug.LogError("Assign asteroidPrefab in the Inspector");
            return;
        }

        EnsurePlayerTransform();

        Vector3 centerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        Debug.Log($"Spawning large asteroid field: {count} asteroids within radius {radius} around {centerPos}");

        for (int i = 0; i < count; i++)
        {
            Vector2 randDir = Random.insideUnitCircle;
            if (randDir.sqrMagnitude < 0.0001f) randDir = Vector2.right;
            randDir.Normalize();
            float dist;
            if (radius <= safeSpawnRadius)
            {
                dist = radius; // place at edge if radius too small
            }
            else
            {
                dist = Random.Range(safeSpawnRadius, radius);
            }
            Vector3 spawnPos = centerPos + new Vector3(randDir.x * dist, randDir.y * dist, 0f);

            GameObject asteroid = Instantiate(asteroidPrefab, spawnPos, Quaternion.identity);

            float scale = Random.Range(minSize.x, maxSize.x);
            asteroid.transform.localScale = Vector3.one * scale;

            Asteroid asteroidScript = asteroid.GetComponent<Asteroid>();
            if (asteroidScript != null)
            {
                asteroidScript.velocity = Random.insideUnitCircle * Random.Range(0.5f, 2f);
                asteroidScript.mass = scale;
                if (asteroidScript.rb != null)
                {
                    asteroidScript.rb.position = spawnPos;
                    asteroidScript.rb.linearVelocity = asteroidScript.velocity;
                }
            }
        }

        Debug.Log("Finished spawning large asteroid field");
    }
}
