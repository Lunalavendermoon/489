using UnityEngine;

public class CoreDrift : MonoBehaviour
{
    [Header("Drift Area")]
    [Tooltip("World-space center the core drifts around (usually (0,0)).")]
    public Vector2 homeCenter = Vector2.zero;

    [Tooltip("Max distance from homeCenter the core can wander.")]
    public float driftRadius = 1.2f;

    [Header("Motion")]
    [Tooltip("How fast the core moves toward its current target.")]
    public float driftSpeed = 0.7f;

    [Tooltip("How quickly it steers (higher = snappier).")]
    public float steerLerp = 4f;

    [Tooltip("Pick a new wander target every X seconds (randomized a bit).")]
    public float retargetInterval = 1.8f;

    [Tooltip("Random +/- added to retarget interval.")]
    public float retargetJitter = 0.6f;

    [Tooltip("If core reaches within this distance of target, retarget early.")]
    public float arriveRadius = 0.10f;

    [Header("Optional")]
    [Tooltip("If true, keep Z fixed (recommended for 2D).")]
    public bool lockZ = true;
    public float lockedZ = 0f;

    private Vector2 target;
    private Vector2 velocity;
    private float timer;

    private void OnEnable()
    {
        // Start from current position but clamp inside the drift area.
        Vector2 p = transform.position;
        Vector2 clamped = ClampToCircle(p, homeCenter, driftRadius);
        transform.position = new Vector3(clamped.x, clamped.y, lockZ ? lockedZ : transform.position.z);

        PickNewTarget(true);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        timer -= dt;
        Vector2 pos = transform.position;

        // Retarget if timer elapsed or we reached near target
        if (timer <= 0f || Vector2.Distance(pos, target) <= arriveRadius)
            PickNewTarget(false);

        // Steer toward target
        Vector2 to = target - pos;
        Vector2 desired = (to.sqrMagnitude < 0.0001f) ? Vector2.zero : to.normalized * driftSpeed;

        velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-steerLerp * dt));
        Vector2 next = pos + velocity * dt;

        // Hard clamp inside drift circle to guarantee confinement
        next = ClampToCircle(next, homeCenter, driftRadius);

        transform.position = new Vector3(next.x, next.y, lockZ ? lockedZ : transform.position.z);
    }

    private void PickNewTarget(bool immediate)
    {
        // Random point inside circle
        Vector2 offset = Random.insideUnitCircle * driftRadius;
        target = homeCenter + offset;

        float interval = Mathf.Max(0.1f, retargetInterval + Random.Range(-retargetJitter, retargetJitter));
        timer = immediate ? interval : interval;
    }

    private Vector2 ClampToCircle(Vector2 p, Vector2 center, float radius)
    {
        Vector2 d = p - center;
        float r = Mathf.Max(0.0001f, radius);
        if (d.sqrMagnitude > r * r)
            p = center + d.normalized * r;
        return p;
    }
}
