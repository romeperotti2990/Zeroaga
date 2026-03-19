using UnityEngine;

public class AsteroidSpawner : MonoBehaviour
{
    public GameObject asteroidPrefab; // Prefab used when spawning asteroids
    public float safeRad = 3f; // Safety bubble (asteroids won't spawn inside this around the player)
    public Vector2 minSize = new Vector2(1f, 1f); // Minimum asteroid scale
    public Vector2 maxSize = new Vector2(2.5f, 2.5f); // Maximum asteroid scale
    public int asteroidCount = 200; // Number of asteroids to create in the large field
    public float asteroidRadius = 500f; // Radius to distribute asteroids in the large field

    [Header("Player randomization")]
    public float worldRadius = 2000f;
    public int playerSpawnAttempts = 200;
    [Range(0.05f, 1f)] public float centerFraction = 0.5f;

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

        StartCoroutine(RandomizePlayerDelayed());

        SpawnLargeAsteroidField(asteroidCount, asteroidRadius);
        largeFieldSpawned = true;
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


    void TryRandomizePlayerPosition()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
            }
            else
            {
                Debug.LogWarning("TryRandomizePlayerPosition: no player Transform found (tag 'Player')");
                return;
            }
        }

        Asteroid[] asteroids = FindObjectsOfType<Asteroid>();

        // Limit randomization to inside the playable large field so PlayerController won't immediately clamp to the edge.
        float maxAllowedRadius = worldRadius;
        if (asteroidRadius > safeRad + 1f)
        {
            maxAllowedRadius = Mathf.Min(worldRadius, asteroidRadius - safeRad - 1f);
        }

        // bias toward center by using a fraction of the allowed radius
        float spawnRadius = Mathf.Max(0.01f, maxAllowedRadius * Mathf.Clamp01(centerFraction));

        for (int i = 0; i < playerSpawnAttempts; i++)
        {
            Vector2 candidate = Random.insideUnitCircle * spawnRadius;
            bool valid = true;

            foreach (Asteroid a in asteroids)
            {
                if (a == null) continue;
                float d = Vector2.Distance(candidate, a.transform.position);
                if (d < safeRad + (a.transform.localScale.x * 0.5f))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                playerTransform.position = new Vector3(candidate.x, candidate.y, playerTransform.position.z);
                Debug.Log($"TryRandomizePlayerPosition: placed player at {playerTransform.position} after {i + 1} attempts (spawnRadius {spawnRadius}, maxAllowed {maxAllowedRadius})");
                return;
            }
        }

        // Fallback: place at origin if no safe spot found
        playerTransform.position = Vector3.zero;
        Debug.LogWarning("TryRandomizePlayerPosition: failed to find safe spot, placed player at origin");
    }

    System.Collections.IEnumerator RandomizePlayerDelayed()
    {
        // wait a frame for other initialization (PlayerController, etc.)
        yield return null;
        TryRandomizePlayerPosition();

        // if player still near origin, try once more after a short delay
        if (playerTransform != null && (playerTransform.position.x == 0f && playerTransform.position.y == 0f))
        {
            yield return new WaitForSeconds(0.05f);
            TryRandomizePlayerPosition();
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
            if (radius <= safeRad)
            {
                dist = radius; // place at edge if radius too small
            }
            else
            {
                dist = Random.Range(safeRad, radius);
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
            }
        }

        Debug.Log("Finished spawning large asteroid field");
    }
}
