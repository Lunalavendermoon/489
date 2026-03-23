using UnityEngine;

public class Dot : PooledObject
{
    [Header("Feel")]
    public float carriedScale = 1.15f;

    [Tooltip("Optional: tint normal dots.")]
    public Color normalTint = Color.white;

    [Tooltip("Optional: tint special dots to stand out.")]
    public Color specialTint = new Color(1f, 0.8f, 0.25f, 1f);

    [Tooltip("Optional: slightly larger special dots.")]
    public float specialScaleMult = 1.10f;
    [Header("Special Dot Overrides")]
    [Tooltip("If true, special dots override motion/lifetime values below.")]
    public bool useSpecialOverrides = true;

    public float specialGuaranteedOnScreenTime = 8.0f;
    public float specialMaxLifetime = 14.0f;
    public float specialDriftSpeed = 1.6f;

    [Tooltip("If assigned, tint will be applied here.")]
    public SpriteRenderer spriteRenderer;

    [Header("Autonomous Motion")]
    public bool enableAutonomousMotion = true;

    [Tooltip("Time (seconds) dot will try to remain on-screen (wander inside bounds).")]
    public float guaranteedOnScreenTime = 3.5f;

    [Tooltip("Total lifetime cap (seconds). Dot despawns even if still visible.")]
    public float maxLifetime = 8.0f;

    [Tooltip("Wander speed while staying on-screen.")]
    public float wanderSpeed = 1.8f;

    [Tooltip("How quickly dot steers toward its wander target.")]
    public float steerLerp = 6f;

    [Tooltip("How close to target before picking a new target.")]
    public float targetReachRadius = 0.25f;

    [Tooltip("After guaranteed time ends, dot drifts outward at this speed.")]
    public float driftSpeed = 2.6f;

    [Tooltip("Extra bounds padding used for 'keep on screen' and 'offscreen check'.")]
    public float boundsPadding = 0.6f;

    private bool carried;
    private Vector3 defaultScale;

    private Camera cam;
    private float life;
    private float keepTimer;

    private Vector2 velocity;
    private Vector2 wanderTarget;
    private bool drifting;
    private float defaultGuaranteedOnScreenTime;
    private float defaultMaxLifetime;
    private float defaultDriftSpeed;

    [SerializeField] private bool isSpecial = false;
    public bool IsSpecial => isSpecial;


    // ✅ Allow pickup even if special; special logic happens at deposit time
    public bool CanPickup => IsActive && !carried;

    private void Awake()
    {
        defaultScale = transform.localScale;
        defaultGuaranteedOnScreenTime = guaranteedOnScreenTime;
        defaultMaxLifetime = maxLifetime;
        defaultDriftSpeed = driftSpeed;
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        ApplyVisual();
    }

    public override void OnSpawn()
    {
        base.OnSpawn();

        carried = false;
        drifting = false;

        transform.localScale = defaultScale;

        life = 0f;
        keepTimer = 0f;

        velocity = Vector2.zero;

        // IMPORTANT: pooling safety — reset special flag by default.
        // The spawner should call SetSpecial(true) after spawning special dots.
        SetSpecial(false); // important: pooling reset
        isSpecial = false;
        ApplyVisual();

        if (cam == null) cam = Camera.main;
        PickNewWanderTarget();
    }

    /// <summary>Call after spawning so bounds use the correct camera.</summary>
    public void Init(Camera camera)
    {
        cam = camera != null ? camera : Camera.main;
        PickNewWanderTarget();
    }

    /// <summary>Spawner calls this to mark dots as special/normal.</summary>
    public void SetSpecial(bool v)
    {
        isSpecial = v;
        if (!useSpecialOverrides)
            return;

        if (isSpecial)
        {
            guaranteedOnScreenTime = specialGuaranteedOnScreenTime;
            maxLifetime = specialMaxLifetime;
            driftSpeed = specialDriftSpeed;
        }
        else
        {
            guaranteedOnScreenTime = defaultGuaranteedOnScreenTime;
            maxLifetime = defaultMaxLifetime;
            driftSpeed = defaultDriftSpeed;
        }
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        // Tint
        if (spriteRenderer != null)
            spriteRenderer.color = isSpecial ? specialTint : normalTint;

        // Size (only when not carried; carried scaling is applied in SetCarried)
        if (!carried)
            transform.localScale = defaultScale * (isSpecial ? specialScaleMult : 1f);
    }

    private void Update()
    {
        if (!IsActive) return;
        if (!enableAutonomousMotion) return;
        if (carried) return; // Player controls it while carried

        life += Time.deltaTime;
        keepTimer += Time.deltaTime;

        if (life >= maxLifetime)
        {
            DespawnSelf();
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (!drifting)
        {
            // Phase 1: wander but remain guaranteed on-screen
            WanderInsideBounds();

            if (keepTimer >= guaranteedOnScreenTime)
                BeginDriftOff();
        }
        else
        {
            // Phase 2: drift outward; despawn when clearly offscreen
            transform.position += (Vector3)(velocity * Time.deltaTime);

            if (IsOffscreen(boundsPadding * 1.5f))
                DespawnSelf();
        }
    }

    private void WanderInsideBounds()
    {
        Vector2 pos = transform.position;
        Vector2 toTarget = wanderTarget - pos;

        if (toTarget.magnitude <= targetReachRadius)
            PickNewWanderTarget();

        Vector2 desiredVel = (toTarget.sqrMagnitude < 0.0001f) ? Vector2.zero : toTarget.normalized * wanderSpeed;
        velocity = Vector2.Lerp(velocity, desiredVel, 1f - Mathf.Exp(-steerLerp * Time.deltaTime));

        transform.position += (Vector3)(velocity * Time.deltaTime);

        // Hard clamp -> guarantee stays visible
        ClampToScreen(boundsPadding);
    }

    private void BeginDriftOff()
    {
        drifting = true;

        Vector2 pos = transform.position;
        Vector2 outward = (pos.sqrMagnitude < 0.001f) ? Random.insideUnitCircle.normalized : pos.normalized;
        Vector2 jitter = Random.insideUnitCircle * 0.25f;

        Vector2 dir = (outward + jitter).normalized;
        velocity = dir * driftSpeed;
    }

    private void PickNewWanderTarget()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        float x = Random.Range(-w + boundsPadding, w - boundsPadding);
        float y = Random.Range(-h + boundsPadding, h - boundsPadding);

        wanderTarget = new Vector2(x, y);
    }

    private void ClampToScreen(float pad)
    {
        if (cam == null) return;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, -w + pad, w - pad);
        p.y = Mathf.Clamp(p.y, -h + pad, h - pad);
        transform.position = p;
    }

    private bool IsOffscreen(float pad)
    {
        if (cam == null) return false;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        Vector3 p = transform.position;
        return (p.x < -w - pad || p.x > w + pad || p.y < -h - pad || p.y > h + pad);
    }

    public void SetCarried(bool v)
    {
        carried = v;

        // Keep special scale feel while carried
        float baseMult = isSpecial ? specialScaleMult : 1f;
        transform.localScale = v ? defaultScale * baseMult * carriedScale : defaultScale * baseMult;
    }

    public void FollowCursor(Vector2 cursorWorld, float lerp)
    {
        Vector3 target = new Vector3(cursorWorld.x, cursorWorld.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-lerp * Time.deltaTime));
    }

    public void DespawnSelf()
    {
        var poolRef = GetComponent<PoolRef>();
        if (poolRef != null) poolRef.Despawn();
        else OnDespawn();
    }
}
