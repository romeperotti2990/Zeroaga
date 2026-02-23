using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("World Settings")]
    public float worldRadius = 2000f;
    public bool clampPlayerToWorld = true;

    [Header("Player Spawn")]
    public float safeSpawnRadius = 12f; // radius around player with no asteroids
    public int maxSpawnAttempts = 200;
    public bool randomizePlayerOnAwake = true;
    
    [Header("Asteroid Field")]
    public GameObject asteroidPrefab;
    public bool spawnLargeFieldOnce = true;
    public int largeFieldCount = 200;
    public float largeFieldRadius = 500f;
    public float poissonMinDistanceFactor = 0.9f; // 0-1 adjust spacing (lower => denser)
    public int poissonK = 30; // attempts per active point
    [Header("Cluster Field (clumps)")]
    public bool useClusters = true;
    public int clusterCount = 20;
    public int minPerCluster = 5;
    public int maxPerCluster = 20;
    public float clusterRadius = 12f;

    void Awake()
    {
        if (randomizePlayerOnAwake)
        {
            TryRandomizePlayerPosition();
        }
        
        // After player is positioned, optionally spawn a large asteroid field
        if (spawnLargeFieldOnce)
        {
            SpawnLargeAsteroidField(largeFieldCount, largeFieldRadius);
        }
    }

    void LateUpdate()
    {
        if (!clampPlayerToWorld) return;
        Vector3 pos = transform.position;
        Vector2 flat = new Vector2(pos.x, pos.y);
        if (flat.magnitude > worldRadius)
        {
            flat = flat.normalized * worldRadius;
            transform.position = new Vector3(flat.x, flat.y, pos.z);
        }
    }

    void TryRandomizePlayerPosition()
    {
        // Try to find a random position inside worldRadius that has no asteroids within safeSpawnRadius
        Asteroid[] asteroids = FindObjectsOfType<Asteroid>();

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector2 candidate = Random.insideUnitCircle * worldRadius;
            bool valid = true;

            foreach (Asteroid a in asteroids)
            {
                if (a == null) continue;
                float d = Vector2.Distance(candidate, a.transform.position);
                if (d < safeSpawnRadius + (a.transform.localScale.x * 0.5f))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                transform.position = new Vector3(candidate.x, candidate.y, transform.position.z);
                return;
            }
        }

        // Fallback: place at origin if no safe spot found
        transform.position = Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.1f);
        Gizmos.DrawWireSphere(Vector3.zero, worldRadius);

        Gizmos.color = new Color(0f, 1f, 0.25f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, safeSpawnRadius);
    }

    void SpawnLargeAsteroidField(int count, float radius)
    {
        if (asteroidPrefab == null)
        {
            Debug.LogWarning("GameManager: asteroidPrefab not assigned, skipping large field spawn.");
            return;
        }

        Vector3 centerPos = transform.position;
        Debug.Log($"GameManager: Spawning large asteroid field ({count}) around {centerPos} radius {radius}");

        if (useClusters)
        {
            SpawnClusterField(count, radius, centerPos);
            return;
        }

        // Estimate minimum distance to aim for roughly `count` samples in the disk
        float area = Mathf.PI * radius * radius;
        float targetDensity = count > 0 ? (area / count) : 1f;
        float estimatedMinDist = Mathf.Sqrt(targetDensity) * poissonMinDistanceFactor;

        var samples = GeneratePoissonDiskSamples(radius, estimatedMinDist, poissonK);

        // If algorithm produced fewer samples than requested, fall back to random fill for remainder
        int spawned = 0;
        for (int i = 0; i < samples.Count && spawned < count; i++)
        {
            Vector2 s = samples[i];
            Vector3 spawnPos = centerPos + new Vector3(s.x, s.y, 0f);
            // avoid spawning too close to the player
            if (Vector2.Distance(new Vector2(spawnPos.x, spawnPos.y), new Vector2(transform.position.x, transform.position.y)) < safeSpawnRadius)
            {
                Vector2 dir = ((Vector2)spawnPos - (Vector2)transform.position).normalized;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                spawnPos = transform.position + (Vector3)(dir * (safeSpawnRadius + 1f));
            }
            SpawnAsteroidAt(spawnPos);
            spawned++;
        }

        for (int i = spawned; i < count; i++)
        {
            // fill remaining slots randomly inside radius
            Vector2 randDir = Random.insideUnitCircle;
            if (randDir.sqrMagnitude < 0.0001f) randDir = Vector2.right;
            randDir.Normalize();
            float dist = Random.Range(0f, radius);
            Vector3 spawnPos = centerPos + new Vector3(randDir.x * dist, randDir.y * dist, 0f);
            if (Vector2.Distance(new Vector2(spawnPos.x, spawnPos.y), new Vector2(transform.position.x, transform.position.y)) < safeSpawnRadius)
            {
                Vector2 dir = ((Vector2)spawnPos - (Vector2)transform.position).normalized;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                spawnPos = transform.position + (Vector3)(dir * (safeSpawnRadius + 1f));
            }
            SpawnAsteroidAt(spawnPos);
        }
    }

    void SpawnClusterField(int count, float radius, Vector3 centerPos)
    {
        int clusters = Mathf.Max(1, clusterCount);
        // distribute counts roughly evenly with some randomness
        int remaining = count;
        int clustersLeft = clusters;

        for (int c = 0; c < clusters; c++)
        {
            int per = Mathf.Clamp(remaining / clustersLeft, minPerCluster, maxPerCluster);
            // add small randomness
            per = Mathf.Clamp(per + Random.Range(-Mathf.Max(1, per/3), Mathf.Max(1, per/3)), minPerCluster, maxPerCluster);
            remaining -= per;
            clustersLeft--;

            // pick a cluster center inside radius
            Vector2 center;
            int attempts = 0;
            do
            {
                center = Random.insideUnitCircle * radius;
                attempts++;
            } while (attempts < 10 && center.magnitude > radius);

            for (int i = 0; i < per; i++)
            {
                Vector2 offset = Random.insideUnitCircle * clusterRadius * Random.Range(0.3f, 1f);
                Vector2 sample = center + offset;
                // ensure within disk
                if (sample.magnitude > radius)
                {
                    sample = sample.normalized * Random.Range(radius * 0.6f, radius);
                }

                Vector3 spawnPos = centerPos + new Vector3(sample.x, sample.y, 0f);
                // avoid player safe bubble
                if (Vector2.Distance(new Vector2(spawnPos.x, spawnPos.y), new Vector2(transform.position.x, transform.position.y)) < safeSpawnRadius)
                {
                    Vector2 dir = ((Vector2)spawnPos - (Vector2)transform.position).normalized;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                    spawnPos = transform.position + (Vector3)(dir * (safeSpawnRadius + Random.Range(1f, 3f)));
                }

                SpawnAsteroidAt(spawnPos);
            }
        }
    }

    void SpawnAsteroidAt(Vector3 spawnPos)
    {
        GameObject asteroid = Instantiate(asteroidPrefab, spawnPos, Quaternion.identity);
        float scale = Random.Range(1f, 2.5f);
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

    // Bridson's Poisson-disc sampling for a disk centered at origin
    System.Collections.Generic.List<Vector2> GeneratePoissonDiskSamples(float radius, float minDist, int k)
    {
        var samples = new System.Collections.Generic.List<Vector2>();
        if (minDist <= 0f)
        {
            // fallback: random samples
            for (int i = 0; i < largeFieldCount; i++) samples.Add(Random.insideUnitCircle * radius);
            return samples;
        }

        float cellSize = minDist / Mathf.Sqrt(2f);
        int gridSize = Mathf.CeilToInt((radius * 2f) / cellSize);
        var grid = new System.Collections.Generic.List<Vector2?>(new Vector2?[gridSize * gridSize]);

        System.Collections.Generic.List<Vector2> process = new System.Collections.Generic.List<Vector2>();

        // helper to convert sample to grid index
        System.Action<Vector2, System.Action<int,int>> forGrid = (v, action) => {
            int gx = Mathf.FloorToInt((v.x + radius) / cellSize);
            int gy = Mathf.FloorToInt((v.y + radius) / cellSize);
            action(gx, gy);
        };

        // initial sample
        Vector2 first = Random.insideUnitCircle * radius;
        samples.Add(first);
        process.Add(first);
        forGrid(first, (gx, gy) => { if (gx >= 0 && gy >= 0 && gx < gridSize && gy < gridSize) grid[gx + gy * gridSize] = first; });

        while (process.Count > 0)
        {
            int idx = Random.Range(0, process.Count);
            Vector2 point = process[idx];
            bool found = false;

            for (int i = 0; i < k; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(minDist, 2f * minDist);
                Vector2 candidate = point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (candidate.magnitude > radius) continue;

                // check neighbors
                int cgx = Mathf.FloorToInt((candidate.x + radius) / cellSize);
                int cgy = Mathf.FloorToInt((candidate.y + radius) / cellSize);
                bool ok = true;
                for (int nx = cgx - 2; nx <= cgx + 2 && ok; nx++)
                {
                    for (int ny = cgy - 2; ny <= cgy + 2; ny++)
                    {
                        if (nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize) continue;
                        var gval = grid[nx + ny * gridSize];
                        if (gval.HasValue)
                        {
                            if (Vector2.SqrMagnitude(gval.Value - candidate) < minDist * minDist)
                            {
                                ok = false; break;
                            }
                        }
                    }
                }

                if (ok)
                {
                    samples.Add(candidate);
                    process.Add(candidate);
                    if (cgx >= 0 && cgy >= 0 && cgx < gridSize && cgy < gridSize) grid[cgx + cgy * gridSize] = candidate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                process.RemoveAt(idx);
            }

            // stop early if we reached a lot more than needed (safety)
            if (samples.Count >= Mathf.Max(largeFieldCount, 10000)) break;
        }

        return samples;
    }
}

