using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PointPickup : MonoBehaviour
{
    public int points = 1;
    public Transform target;
    public float attractionRadius = 16f;
    public float collectDistance = 0.75f;
    public float lifeTime = 300f;
    public Vector2 initialVelocity = Vector2.zero;
    public Color pickupColor = Color.white;
    public Color pickupFlashColor = Color.black;
    public float projectilePushStrength = 4f;
    public float attractionSpeed = 10f;
    public float attractionAcceleration = 140f;
    public float attractionMaxSpeed = 32f;
    public float attractionMinSpeed = 10f;
    public float slideDeceleration = 1.75f;
    public float initialSpinSpeed = 120f;
    public float flashSpeed = 6f;
    public float collectShrinkDuration = 0.18f;

    Rigidbody2D _rb;
    Vector2 _velocity;
    float _spinSpeed;
    float _flashTimer;
    bool _isCollecting;
    float _collectTimer;
    Vector3 _collectStartScale;
    SpriteRenderer _spriteRenderer;
    SpriteRenderer _outlineRenderer;
    static Sprite _triangleSprite;

    void Start()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = false;
        }

        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody2D>();
        }

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        ConfigureVisuals();

        _velocity = initialVelocity;
        _spinSpeed = Random.Range(-initialSpinSpeed, initialSpinSpeed);
        _collectStartScale = transform.localScale;

        if (lifeTime > 0f)
        {
            Destroy(gameObject, lifeTime);
        }
    }

    void ConfigureVisuals()
    {
        if (_triangleSprite == null)
        {
            _triangleSprite = CreateTriangleSprite();
        }

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        _spriteRenderer.sprite = _triangleSprite;
        _spriteRenderer.sortingOrder = 20;

        Transform outlineTransform = transform.Find("Outline");
        if (outlineTransform == null)
        {
            GameObject outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one * 1.12f;

            _outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            _outlineRenderer = outlineTransform.GetComponent<SpriteRenderer>();
            if (_outlineRenderer == null)
            {
                _outlineRenderer = outlineTransform.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        _outlineRenderer.sprite = _triangleSprite;
        _outlineRenderer.color = Color.black;
        _outlineRenderer.sortingOrder = 19;
        _spriteRenderer.color = pickupColor;

        ParticleSystem particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.enabled = false;
            }
        }
    }

    void Update()
    {
        if (_isCollecting)
        {
            UpdateCollectAnimation();
            return;
        }

        if (target == null)
        {
            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                target = playerController.transform;
            }
        }

        if (target != null)
        {
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            if (distance <= collectDistance)
            {
                Collect();
                return;
            }

            float snapDistance = Mathf.Max(collectDistance * 2.5f, 2f);
            if (distance <= snapDistance)
            {
                _velocity = toTarget.normalized * attractionMaxSpeed;
                Collect();
                return;
            }

            if (distance <= attractionRadius)
            {
                float proximityT = 1f - Mathf.Clamp01(distance / attractionRadius);
                float desiredSpeed = Mathf.Lerp(attractionMinSpeed, attractionMaxSpeed, proximityT * proximityT);
                Vector2 desiredVelocity = toTarget.normalized * desiredSpeed;
                _velocity = Vector2.MoveTowards(_velocity, desiredVelocity, attractionAcceleration * Time.deltaTime);
            }
            else
            {
                _velocity = Vector2.zero;
            }
        }
        else
        {
            _velocity = Vector2.zero;
        }

        _flashTimer += Time.deltaTime * flashSpeed;
        float flashPhase = Mathf.PingPong(_flashTimer, 1f);
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = Color.Lerp(pickupFlashColor, pickupColor, flashPhase);
        }

        if (_rb != null)
        {
            _rb.linearVelocity = _velocity;
        }
        else
        {
            transform.position += (Vector3)(_velocity * Time.deltaTime);
        }

        transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        if (collision.collider.GetComponentInParent<PlayerController>() != null)
        {
            Collect();
            return;
        }

        Projectile projectile = collision.collider.GetComponent<Projectile>();
        if (projectile != null)
        {
            PushAwayFromProjectile(projectile);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() != null)
        {
            Collect();
        }
    }

    void PushAwayFromProjectile(Projectile projectile)
    {
        Vector2 fromProjectile = (Vector2)transform.position - (Vector2)projectile.transform.position;
        if (fromProjectile.sqrMagnitude < 0.0001f)
        {
            fromProjectile = projectile.transform.up;
        }

        fromProjectile.Normalize();
        _velocity += fromProjectile * projectilePushStrength;
        _spinSpeed += Random.Range(-25f, 25f);

        if (_velocity.sqrMagnitude > 25f)
        {
            _velocity = _velocity.normalized * 5f;
        }
    }

    static Sprite CreateTriangleSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = Color.white;

        float halfBase = 0.36f;
        float height = halfBase * Mathf.Sqrt(3f);
        Vector2 top = new Vector2(0.5f, 0.5f + (height * 0.5f));
        Vector2 left = new Vector2(0.5f - halfBase, 0.5f - (height * 0.5f));
        Vector2 right = new Vector2(0.5f + halfBase, 0.5f - (height * 0.5f));

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                texture.SetPixel(x, y, PointInTriangle(p, top, left, right) ? fill : clear);
            }
        }

        AddTriangleBorder(texture, Color.black, top, left, right);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    static void AddTriangleBorder(Texture2D texture, Color borderColor, Vector2 top, Vector2 left, Vector2 right)
    {
        int size = texture.width;
        int borderThickness = 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                if (!PointInTriangle(p, top, left, right))
                {
                    continue;
                }

                bool nearEdge = false;
                for (int oy = -borderThickness; oy <= borderThickness && !nearEdge; oy++)
                {
                    for (int ox = -borderThickness; ox <= borderThickness; ox++)
                    {
                        int nx = Mathf.Clamp(x + ox, 0, size - 1);
                        int ny = Mathf.Clamp(y + oy, 0, size - 1);
                        Vector2 np = new Vector2((nx + 0.5f) / size, (ny + 0.5f) / size);
                        if (!PointInTriangle(np, top, left, right))
                        {
                            nearEdge = true;
                            break;
                        }
                    }
                }

                if (nearEdge)
                {
                    texture.SetPixel(x, y, borderColor);
                }
            }
        }
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNeg && hasPos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    void Collect()
    {
        if (_isCollecting)
        {
            return;
        }

        ScoreManager.Instance.AddPoints(points);
        _isCollecting = true;
        _collectTimer = 0f;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.enabled = false;
        }
    }

    void UpdateCollectAnimation()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        _collectTimer += Time.deltaTime;
        float t = collectShrinkDuration > 0f ? Mathf.Clamp01(_collectTimer / collectShrinkDuration) : 1f;
        float easedT = Mathf.SmoothStep(0f, 1f, t);

        transform.position = Vector3.Lerp(transform.position, target.position, easedT * 0.35f);
        transform.localScale = Vector3.Lerp(_collectStartScale, Vector3.zero, easedT);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = Color.Lerp(pickupColor, Color.clear, easedT);
        }

        if (_outlineRenderer != null)
        {
            _outlineRenderer.color = Color.Lerp(Color.black, Color.clear, easedT);
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}