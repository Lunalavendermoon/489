using UnityEngine;

public class BeatTarget : PooledObject
{
    [Header("Visuals")]
    public LineRenderer outerRing;
    public SpriteRenderer centerDot;

    [Tooltip("Outer ring radius at spawn.")]
    [Min(0.05f)] public float ringStartRadius = 1.2f;

    [Tooltip("Outer ring radius when timing converges.")]
    [Min(0.01f)] public float ringEndRadius = 0.18f;

    [Min(12)] public int ringSegments = 64;
    [Min(0.001f)] public float ringWidth = 0.04f;

    public Color ringColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
    public Color centerColor = new Color(1f, 0.68f, 0.2f, 1f);

    [Header("Hit Zone")]
    [Tooltip("Cursor must enter this radius to attempt a beat hit.")]
    [Min(0.05f)] public float hitZoneRadius = 0.55f;

    private BeatManager beat;
    private float spawnTime;
    private float targetTime;
    private float despawnTime;
    private bool configured;

    public float TargetTime => targetTime;

    private void Awake()
    {
        if (outerRing == null)
            outerRing = GetComponentInChildren<LineRenderer>();

        if (centerDot == null)
            centerDot = GetComponentInChildren<SpriteRenderer>();

        ConfigureVisual();
        DrawRing(ringStartRadius);
    }

    public override void OnSpawn()
    {
        base.OnSpawn();

        configured = false;
        spawnTime = 0f;
        targetTime = 0f;
        despawnTime = 0f;

        ConfigureVisual();
        DrawRing(ringStartRadius);
    }

    public void Configure(BeatManager beatManager, float spawnAtTime, float resolveAtTime, float lateGraceSeconds)
    {
        beat = beatManager;
        spawnTime = spawnAtTime;
        targetTime = Mathf.Max(spawnAtTime, resolveAtTime);
        despawnTime = targetTime + Mathf.Max(0.01f, lateGraceSeconds);
        configured = true;

        DrawRing(ringStartRadius);
    }

    public bool IsExpired(float now)
    {
        return configured && now >= despawnTime;
    }

    public bool IsCursorInHitZone(Vector2 cursorPos)
    {
        return Vector2.Distance(cursorPos, transform.position) <= hitZoneRadius;
    }

    public void DespawnSelf()
    {
        var poolRef = GetComponent<PoolRef>();
        if (poolRef != null) poolRef.Despawn();
        else OnDespawn();
    }

    private void Update()
    {
        if (!IsActive || !configured || beat == null)
            return;

        float now = beat.CurrentTime;
        float t = Mathf.InverseLerp(spawnTime, Mathf.Max(spawnTime + 0.0001f, targetTime), now);
        float radius = Mathf.Lerp(ringStartRadius, ringEndRadius, Mathf.Clamp01(t));
        DrawRing(radius);
    }

    private void ConfigureVisual()
    {
        if (outerRing != null)
        {
            outerRing.useWorldSpace = false;
            outerRing.positionCount = Mathf.Max(12, ringSegments) + 1;
            outerRing.startWidth = ringWidth;
            outerRing.endWidth = ringWidth;
            outerRing.startColor = ringColor;
            outerRing.endColor = ringColor;
        }

        if (centerDot != null)
            centerDot.color = centerColor;
    }

    private void DrawRing(float radius)
    {
        if (outerRing == null)
            return;

        int segments = Mathf.Max(12, ringSegments);
        if (outerRing.positionCount != segments + 1)
            outerRing.positionCount = segments + 1;

        float clampedRadius = Mathf.Max(0.01f, radius);
        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            outerRing.SetPosition(i, new Vector3(Mathf.Cos(a) * clampedRadius, Mathf.Sin(a) * clampedRadius, 0f));
        }
    }

    private void OnValidate()
    {
        ringStartRadius = Mathf.Max(0.05f, ringStartRadius);
        ringEndRadius = Mathf.Clamp(ringEndRadius, 0.01f, ringStartRadius);
        ringSegments = Mathf.Max(12, ringSegments);
        ringWidth = Mathf.Max(0.001f, ringWidth);
        hitZoneRadius = Mathf.Max(0.05f, hitZoneRadius);
    }
}
