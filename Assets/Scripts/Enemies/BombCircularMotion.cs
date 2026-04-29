using UnityEngine;

public class BombCircularMotion : MonoBehaviour
{
    [Header("Orbit Center")]
    [Tooltip("If assigned, the bomb orbits around this transform. If not, it uses the world position set below.")]
    public Transform centerTarget;

    [Tooltip("Used only if Center Target is not assigned.")]
    public Vector2 worldCenter = Vector2.zero;

    [Header("Orbit")]
    [Min(0.01f)] public float radius = 2f;

    [Tooltip("Degrees per second. Positive = counterclockwise, negative = clockwise.")]
    public float angularSpeedDeg = 45f;

    [Tooltip("Starting angle in degrees.")]
    public float startAngleDeg = 0f;

    [Header("Options")]
    [Tooltip("If true, recomputes the base center every frame from Center Target.")]
    public bool followMovingCenter = true;

    private float currentAngleDeg;
    private Vector2 cachedCenter;

    private void Awake()
    {
        currentAngleDeg = startAngleDeg;
        cachedCenter = GetCenter();
        UpdatePositionImmediate();
    }

    private void OnEnable()
    {
        currentAngleDeg = startAngleDeg;
        cachedCenter = GetCenter();
        UpdatePositionImmediate();
    }

    private void Update()
    {
        if (followMovingCenter || centerTarget != null)
            cachedCenter = GetCenter();

        currentAngleDeg += angularSpeedDeg * Time.deltaTime;
        UpdatePositionImmediate();
    }

    private Vector2 GetCenter()
    {
        if (centerTarget != null)
            return centerTarget.position;

        return worldCenter;
    }

    private void UpdatePositionImmediate()
    {
        float rad = currentAngleDeg * Mathf.Deg2Rad;

        Vector2 offset = new Vector2(
            Mathf.Cos(rad) * radius,
            Mathf.Sin(rad) * radius
        );

        transform.position = cachedCenter + offset;
    }

    public void SetAngle(float angleDeg)
    {
        currentAngleDeg = angleDeg;
        UpdatePositionImmediate();
    }

    public void SetCenter(Vector2 newCenter)
    {
        cachedCenter = newCenter;
        worldCenter = newCenter;
        UpdatePositionImmediate();
    }

    public void SetRadius(float newRadius)
    {
        radius = Mathf.Max(0.01f, newRadius);
        UpdatePositionImmediate();
    }
}