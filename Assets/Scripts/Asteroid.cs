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
    // Safety clamp to prevent runaway speeds after impulses
    public float maxVelocity = 12f;
    public GameObject explosionParticlePrefab;  // Particle effect when asteroid is destroyed
    public GameObject hitEffectPrefab; // Particle effect when asteroid is hit
    public GameObject pointPickupPrefab; // Collectible that homes into the player and awards points
    public int basePointValue = 1;
    public float pointValuePerScale = 2f;

    [HideInInspector] public Rigidbody2D rb;

    public float maxDistanceFromPlayer = 30f;  // Max distance before bouncing back
    private Transform playerTransform;
    // World border info (optional). If a GameObject with tag 'WorldBorder' exists and has a CircleCollider2D,
    // we'll use it as a hard boundary fallback so asteroids can't pass through.
    private bool haveWorldBorder = false;
    private Vector2 worldBorderCenter = Vector2.zero;
    private float worldBorderRadius = 0f;

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

        // Try to find a world border collider by tag and cache its center/radius
        GameObject border = GameObject.FindGameObjectWithTag("WorldBorder");
        if (border != null)
        {
            CircleCollider2D col = border.GetComponent<CircleCollider2D>();
            if (col != null)
            {
                haveWorldBorder = true;
                worldBorderCenter = border.transform.position;
                // account for transform scale
                float scale = border.transform.lossyScale.x;
                worldBorderRadius = col.radius * Mathf.Abs(scale);
            }
        }

        float localScale = transform.localScale.x;
        if (localScale <= minSplitScale)
        {
            health = 1;
        }
        else
        {
            health = Mathf.Max(minHealth, Mathf.CeilToInt(localScale * healthPerUnitScale));
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

        // Move asteroid with optional world-border fallback to reflect when crossing the border
        Vector2 currentPos = transform.position;
        Vector2 nextPos = currentPos + velocity * Time.deltaTime;

        if (haveWorldBorder)
        {
            float distNext = Vector2.Distance(nextPos, worldBorderCenter);
            if (distNext > worldBorderRadius)
            {
                // reflect velocity against border normal at the exit point
                Vector2 normal = (nextPos - worldBorderCenter).normalized;
                velocity = Vector2.Reflect(velocity, normal) * collisionRestitution;
                velocity = Vector2.ClampMagnitude(velocity, maxVelocity);
                // recompute nextPos using reflected velocity
                nextPos = currentPos + velocity * Time.deltaTime;
                // small nudge inward so asteroid doesn't stick outside
                nextPos = (Vector2)worldBorderCenter + (nextPos - worldBorderCenter).normalized * (worldBorderRadius - 0.01f);
                rotationSpeed += Random.Range(-collisionSpinImpulse, collisionSpinImpulse) * 0.05f;
            }
        }

        if (rb != null)
        {
            rb.MovePosition(nextPos);
        }
        else
        {
            transform.position = (Vector3)nextPos;
        }

        // Rotate asteroid
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Bounce off world border (support both trigger and collision setups)
        if (collision != null && collision.gameObject != null && collision.gameObject.CompareTag("WorldBorder"))
        {
            Vector2 normal;
            if (collision.contactCount > 0)
            {
                normal = collision.GetContact(0).normal;
            }
            else
            {
                // Fallback for no contact info: use vector from border center to asteroid
                normal = ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;
            }

                // Reflect velocity and apply restitution
                velocity = Vector2.Reflect(velocity, normal) * collisionRestitution;

                // Clamp after reflect
                velocity = Vector2.ClampMagnitude(velocity, maxVelocity);

                // Nudge asteroid slightly along its new direction so it doesn't stick in the border
                if (velocity.sqrMagnitude > 0.0001f)
                {
                    transform.position += (Vector3)(velocity.normalized * 0.1f);
                }

                // Add some random spin from the bounce
                rotationSpeed += Random.Range(-collisionSpinImpulse, collisionSpinImpulse) * 0.1f;
            return;
        }

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

            // Clamp velocity to prevent runaway speeds from hits
            velocity = Vector2.ClampMagnitude(velocity, maxVelocity);

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

            // Clamp both asteroids' velocities to keep collisions stable
            velocity = Vector2.ClampMagnitude(velocity, maxVelocity);
            otherAsteroid.velocity = Vector2.ClampMagnitude(otherAsteroid.velocity, maxVelocity);

            // Add spin based on impact strength
            float spin = Mathf.Clamp(impulseScalar, 0f, collisionSpinImpulse);
            rotationSpeed += Random.Range(-spin, spin);
            otherAsteroid.rotationSpeed += Random.Range(-spin, spin);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || other.gameObject == null) return;

        if (other.gameObject.CompareTag("WorldBorder"))
        {
            // For trigger borders, approximate normal from border center to asteroid
            Vector2 normal = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
            velocity = Vector2.Reflect(velocity, normal) * collisionRestitution;
            velocity = Vector2.ClampMagnitude(velocity, maxVelocity);
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.position += (Vector3)(velocity.normalized * 0.1f);
            }
            rotationSpeed += Random.Range(-collisionSpinImpulse, collisionSpinImpulse) * 0.1f;
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
            SpawnPointPickup();
            Split();
            Destroy(gameObject);
        }
    }

    void SpawnPointPickup()
    {
        int pointValue = Mathf.Max(basePointValue, Mathf.RoundToInt(transform.localScale.x * pointValuePerScale));

        GameObject pickup = null;
        if (pointPickupPrefab != null)
        {
            pickup = Instantiate(pointPickupPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            pickup = new GameObject("PointPickup");
            pickup.transform.position = transform.position;

            CircleCollider2D collider2D = pickup.AddComponent<CircleCollider2D>();
            collider2D.isTrigger = true;
            collider2D.radius = 0.15f;

            Rigidbody2D body = pickup.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            PointPickup pointPickup = pickup.AddComponent<PointPickup>();
            pointPickup.points = pointValue;
        }

        PointPickup pickupScript = pickup.GetComponent<PointPickup>();
        if (pickupScript != null)
        {
            pickupScript.points = pointValue;
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
            Vector3 spawnPos = transform.position + (Vector3)(splitDir * 0.1f);

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

                // Set velocity with a strong outward push (clamped)
                Vector2 childVel = velocity * 0.3f + splitDir * splitPushSpeed;
                asteroidScript.velocity = Vector2.ClampMagnitude(childVel, maxVelocity);
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
