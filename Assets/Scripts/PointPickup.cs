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
    public float projectilePushStrength = 4f;
    public float attractionAcceleration = 140f;
    public float attractionMaxSpeed = 32f;
    public float attractionMinSpeed = 10f;
    public float initialSpinSpeed = 120f;
    public float flashSpeed = 6f;
    public float collectShrinkDuration = 0.18f;
    public float visualScale = 0.75f;

    public Color pickupColor = Color.white;
    public Color pickupFlashColor = Color.black;

    Rigidbody2D _rb;
    Vector2 _velocity;
    float _spinSpeed;
    float _flashTimer;
    bool _isCollecting;
    float _collectTimer;
    Vector3 _collectStartScale;
    Transform _visualRoot;
    SpriteRenderer[] _visualRenderers;

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

        SetupVisuals();

        _velocity = initialVelocity;
        _spinSpeed = Random.Range(-initialSpinSpeed, initialSpinSpeed);
        _collectStartScale = transform.localScale;

        if (lifeTime > 0f)
        {
            Destroy(gameObject, lifeTime);
        }
    }

    void SetupVisuals()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bool hasChildVisuals = false;

        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] != null && childRenderers[i].transform != transform)
            {
                hasChildVisuals = true;
                break;
            }
        }

        if (hasChildVisuals)
        {
            _visualRenderers = childRenderers;
            _visualRoot = FindVisualRootFromRenderers(childRenderers);
            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }
        }
        else if (rootRenderer != null)
        {
            _visualRoot = new GameObject("VisualRoot").transform;
            _visualRoot.SetParent(transform, false);
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one;

            SpriteRenderer copiedRenderer = _visualRoot.gameObject.AddComponent<SpriteRenderer>();
            CopyRendererSettings(rootRenderer, copiedRenderer);
            rootRenderer.enabled = false;
            _visualRenderers = new[] { copiedRenderer };
        }
        else
        {
            _visualRoot = new GameObject("VisualRoot").transform;
            _visualRoot.SetParent(transform, false);
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one;

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(_visualRoot, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            spriteRenderer.sortingOrder = 20;
            _visualRenderers = new[] { spriteRenderer };
        }

        ApplyVisualScale();
        ApplyVisualColor(pickupColor);
    }

    void CopyRendererSettings(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.sharedMaterial = source.sharedMaterial;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.maskInteraction = source.maskInteraction;
        target.enabled = true;
    }

    Transform FindVisualRootFromRenderers(SpriteRenderer[] renderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].transform != transform)
            {
                return renderers[i].transform.parent != null ? renderers[i].transform.parent : renderers[i].transform;
            }
        }

        return null;
    }

    void ApplyVisualScale()
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);
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

        if (_rb != null)
        {
            _rb.linearVelocity = _velocity;
        }

        _flashTimer += Time.deltaTime * flashSpeed;
        float flashPhase = Mathf.PingPong(_flashTimer, 1f);
        ApplyVisualColor(Color.Lerp(pickupFlashColor, pickupColor, flashPhase));

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
        if (_visualRoot != null)
        {
            _visualRoot.localScale = Vector3.one * Mathf.Lerp(visualScale, 0f, easedT);
        }

        ApplyVisualColor(Color.Lerp(pickupColor, Color.clear, easedT));

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    void ApplyVisualColor(Color color)
    {
        if (_visualRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _visualRenderers.Length; i++)
        {
            if (_visualRenderers[i] != null)
            {
                _visualRenderers[i].color = color;
            }
        }
    }
}
