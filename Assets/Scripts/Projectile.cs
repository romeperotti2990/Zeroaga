using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    // If true the projectile will still be destroyed after `lifetime` seconds
    // when no collision occurs. Set to false to keep until collision.
    public bool despawnAfterTime = false;
    public float lifetime = 1.5f;
    Rigidbody2D rb;

    void Start()
    {
        // Optionally despawn after time; by default we keep the projectile until it hits something
        if (despawnAfterTime)
        {
            Destroy(gameObject, lifetime);
        }
        
        // Ensure there's a Rigidbody2D for reliable 2D trigger/collision callbacks
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        transform.position += transform.up * speed * Time.deltaTime;
        // Move using the Rigidbody2D when available so collisions/triggers are detected reliably
        if (rb != null)
        {
            Vector2 next = rb.position + (Vector2)(transform.up * speed * Time.deltaTime);
            rb.MovePosition(next);
        }
        else
        {
            transform.position += transform.up * speed * Time.deltaTime;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // If this is the world border, ignore enter events (we want to destroy when exiting)
        if (other.CompareTag("WorldBorder"))
        {
            return;
        }

        // Destroy on any other trigger collision (asteroids, enemies, etc.)
        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Also handle non-trigger collisions
        if (collision.collider != null && collision.collider.CompareTag("WorldBorder"))
            return;
        Destroy(gameObject);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // If a projectile leaves the playable circle (hits the border edge), destroy it
        if (other.CompareTag("WorldBorder"))
        {
            Destroy(gameObject);
        }
    }
}
