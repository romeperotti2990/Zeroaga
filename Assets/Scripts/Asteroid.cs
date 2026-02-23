using UnityEngine;

public class Asteroid : MonoBehaviour
{
    public Vector2 velocity;
    public float rotationSpeed;
    public float mass = 1f;
    public int health = 3;
    public int minHealth = 2;
    public float healthPerUnitScale = 1.6f;
    public float minMoveSpeed = 2f;
    public float maxMoveSpeed = 5f;
    public float collisionRestitution = 0.9f;
    public float collisionSpinImpulse = 60f;
    public int splitCount = 2;  // Number of smaller asteroids to spawn when destroyed
    public float minSplitScale = 0.7f;  // Minimum scale before asteroid just dies without splitting
    public float splitSeparation = 0.8f;
    public float splitPushSpeed = 8f;
    public float hitPushStrength = 64f;
    public GameObject explosionParticlePrefab;  // Particle effect when asteroid is destroyed
    public GameObject hitEffectPrefab; // Particle effect when asteroid is hit

    [HideInInspector] public Rigidbody2D rb;

    public float maxDistanceFromPlayer = 30f;  // Max distance before bouncing back
    private Transform playerTransform;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Set up Rigidbody2D if it exists
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Random initial velocity if not set
        if (velocity == Vector2.zero)
        {
            velocity = Random.insideUnitCircle * Random.Range(minMoveSpeed, maxMoveSpeed);
        }

        // Random rotation speed
        rotationSpeed = Random.Range(-30f, 30f);

        float scale = transform.localScale.x;
        if (scale <= minSplitScale)
        {
            health = 1;
        }
        else
        {
            health = Mathf.Max(minHealth, Mathf.CeilToInt(scale * healthPerUnitScale));
        }
    }

    void Update()
    {
        // Check distance from player and bounce back if too far
        if (playerTransform != null)
        {
            float distanceFromPlayer = Vector2.Distance(transform.position, playerTransform.position);
            if (distanceFromPlayer > maxDistanceFromPlayer)
            {
                // Redirect velocity towards player
                Vector2 directionToPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
                velocity = directionToPlayer * velocity.magnitude;
            }
        }

        // Move asteroid
        if (rb != null)
        {
            rb.MovePosition(rb.position + velocity * Time.deltaTime);
        }
        else
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
        }

        // Rotate asteroid
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if hit by projectile
        Projectile projectile = collision.gameObject.GetComponent<Projectile>();
        if (projectile != null)
        {
            Vector2 hitPoint = transform.position;
            if (collision.contactCount > 0)
            {
                hitPoint = collision.GetContact(0).point;
            }

            Vector2 pushDir = (Vector2)transform.position - (Vector2)projectile.transform.position;
            if (pushDir.sqrMagnitude < 0.0001f)
            {
                pushDir = projectile.transform.up;
            }
            pushDir.Normalize();
            float massFactor = Mathf.Max(0.1f, mass);
            velocity += pushDir * (hitPushStrength / massFactor);

            TakeDamage(1, hitPoint);
            Destroy(collision.gameObject);  // Destroy the projectile
            return;
        }

        // Check if colliding with another asteroid
        Asteroid otherAsteroid = collision.gameObject.GetComponent<Asteroid>();
        if (otherAsteroid != null)
        {
            // Elastic collision response with restitution
            Vector2 direction = (transform.position - collision.transform.position).normalized;
            Vector2 relativeVelocity = velocity - otherAsteroid.velocity;
            float velAlongNormal = Vector2.Dot(relativeVelocity, direction);

            // Only resolve if asteroids are moving towards each other
            if (velAlongNormal > 0f) return;

            float invMassA = mass > 0f ? 1f / mass : 0f;
            float invMassB = otherAsteroid.mass > 0f ? 1f / otherAsteroid.mass : 0f;
            float impulseScalar = -(1f + collisionRestitution) * velAlongNormal / (invMassA + invMassB);
            Vector2 impulse = impulseScalar * direction;

            velocity += impulse * invMassA;
            otherAsteroid.velocity -= impulse * invMassB;

            // Add spin based on impact strength
            float spin = Mathf.Clamp(impulseScalar, 0f, collisionSpinImpulse);
            rotationSpeed += Random.Range(-spin, spin);
            otherAsteroid.rotationSpeed += Random.Range(-spin, spin);
        }
    }

    void TakeDamage(int damage, Vector2 hitPoint)
    {
        if (hitEffectPrefab != null)
        {
            GameObject hit = Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
            Destroy(hit, 2f);  // Clean up particles after 2 seconds
        }

        health -= damage;
        if (health <= 0)
        {
            Split();
            Destroy(gameObject);
        }
    }

    void Split()
    {
        // Don't split if at/below minimum size - explode into particles instead
        if (transform.localScale.x <= minSplitScale)
        {
            SpawnExplosion();
            return;
        }

        // Spawn smaller asteroids
        Vector2 baseDir = Random.insideUnitCircle.normalized;
        if (baseDir.magnitude < 0.1f)
        {
            baseDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        }

        float angleStep = splitCount > 0 ? 360f / splitCount : 360f;
        for (int i = 0; i < splitCount; i++)
        {
            // Spawn at the same position to avoid visible separation
            float angle = angleStep * i;
            Vector2 splitDir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * baseDir);
            Vector3 spawnPos = transform.position;

            GameObject newAsteroid = Instantiate(gameObject, spawnPos, Quaternion.identity);
            float childScale = Mathf.Max(minSplitScale, transform.localScale.x * 0.6f);
            newAsteroid.transform.localScale = Vector3.one * childScale;

            Asteroid asteroidScript = newAsteroid.GetComponent<Asteroid>();
            if (asteroidScript != null)
            {
                // Make sure the script is enabled
                asteroidScript.enabled = true;

                // Make sure collider is enabled
                Collider2D col = newAsteroid.GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = true;
                }

                // Make sure rigidbody is enabled
                Rigidbody2D rbNew = newAsteroid.GetComponent<Rigidbody2D>();
                if (rbNew != null)
                {
                    rbNew.simulated = true;
                }

                // Set velocity with a strong outward push
                asteroidScript.velocity = velocity * 0.3f + splitDir * splitPushSpeed;
                asteroidScript.mass = mass * 0.6f;
                asteroidScript.rotationSpeed = Random.Range(-50f, 50f);

                // Force rigidbody update
                if (asteroidScript.rb != null)
                {
                    asteroidScript.rb.WakeUp();
                    asteroidScript.rb.linearVelocity = asteroidScript.velocity;
                }
            }
        }
    }

    void SpawnExplosion()
    {
        if (explosionParticlePrefab != null)
        {
            GameObject explosion = Instantiate(explosionParticlePrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 2f);  // Clean up particles after 2 seconds
        }
    }
}
