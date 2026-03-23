// CurveString.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CurveString : PooledObject
{
    [Header("Curve")]
    public int samples = 50;
    public int controlPoints = 4; // 3 or 4 works well
    public float minLength = 2.2f;
    public float maxLength = 5.5f;

    [Header("Start Marker")]
    public Collider2D startMarker;

    [Header("Visual")]
    public float width = 0.08f;

    private LineRenderer lr;
    private readonly List<Vector3> localPoints = new List<Vector3>();

    private bool tracing;
    private int traceIndex;
    private bool captured;

    public bool IsCaptured => captured;
    public bool CanStartTrace => IsActive && !tracing && !captured;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.useWorldSpace = false; // IMPORTANT for carrying by moving transform
            lr.positionCount = 0;
            lr.startWidth = width;
            lr.endWidth = width;
        }
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        tracing = false;
        captured = false;
        traceIndex = 0;

        GenerateRandomCurve();
        ApplyLine();
        if (lr != null)
        {
            lr.startWidth = width;
            lr.endWidth = width;
        }

        UpdateStartMarkerPos();

        // Ensure marker is on same layer as curve so curveMask sees it
        if (startMarker != null)
            startMarker.gameObject.layer = gameObject.layer;
    }

    public void BeginTrace()
    {
        tracing = true;
        captured = false;
        traceIndex = 0;
    }

    public bool TraceStep(Vector2 cursorWorld, float tolerance)
    {
        if (!tracing || captured || localPoints.Count < 2) return false;

        // advance along sampled points
        Vector3 targetWorld = transform.TransformPoint(localPoints[traceIndex]);
        float dist = Vector2.Distance(cursorWorld, targetWorld);

        if (dist <= tolerance)
        {
            // allow skipping forward quickly
            int safety = 0;
            while (traceIndex < localPoints.Count - 1)
            {
                Vector3 w = transform.TransformPoint(localPoints[traceIndex]);
                if (Vector2.Distance(cursorWorld, w) > tolerance) break;
                traceIndex++;
                safety++;
                if (safety > 8) break;
            }

            if (traceIndex >= localPoints.Count - 1)
            {
                Capture();
            }
            return true;
        }

        return false;
    }

    private void Capture()
    {
        tracing = false;
        captured = true;
        // Optional: widen a bit to feel “grabbed”
        if (lr != null)
        {
            lr.startWidth = width * 1.15f;
            lr.endWidth = width * 1.15f;
        }
    }

    public void FailTrace()
    {
        tracing = false;
        captured = false;
        traceIndex = 0;
        if (lr != null)
        {
            lr.startWidth = width;
            lr.endWidth = width;
        }
    }

    public void DropCaptured()
    {
        // Dropping returns to idle state; keep curve on field.
        captured = false;
        tracing = false;
        traceIndex = 0;
        if (lr != null)
        {
            lr.startWidth = width;
            lr.endWidth = width;
        }
    }

    public void FollowCursor(Vector2 cursorWorld, float lerp)
    {
        Vector3 target = new Vector3(cursorWorld.x, cursorWorld.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-lerp * Time.deltaTime));
    }

    public bool IsCursorOnStart(Vector2 cursorWorld, float radius)
    {
        if (startMarker == null) return false;
        return Vector2.Distance(cursorWorld, startMarker.transform.position) <= radius;
    }

    public void DespawnSelf()
    {
        var poolRef = GetComponent<PoolRef>();
        if (poolRef != null) poolRef.Despawn();
        else OnDespawn();
    }

    private void GenerateRandomCurve()
    {
        localPoints.Clear();

        // Build random control points in LOCAL space
        // We'll place transform at a random world pos and generate a curve around it.
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

        float len = Random.Range(minLength, maxLength);

        Vector3 p0 = (-dir * len * 0.5f);
        Vector3 p3 = (dir * len * 0.5f);

        Vector2 perp = new Vector2(-dir.y, dir.x);
        float bend = Random.Range(0.6f, 1.4f);

        Vector3 p1 = p0 + (Vector3)(dir * len * 0.25f) + (Vector3)(perp * bend);
        Vector3 p2 = p3 - (Vector3)(dir * len * 0.25f) + (Vector3)(perp * -bend);

        // sample cubic bezier
        for (int i = 0; i < samples; i++)
        {
            float t = (samples <= 1) ? 1f : i / (float)(samples - 1);
            Vector3 pt = CubicBezier(p0, p1, p2, p3, t);
            localPoints.Add(pt);
        }
    }

    private Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return (u * u * u) * a +
               (3f * u * u * t) * b +
               (3f * u * t * t) * c +
               (t * t * t) * d;
    }

    private void ApplyLine()
    {
        if (lr == null) return;

        lr.positionCount = localPoints.Count;
        for (int i = 0; i < localPoints.Count; i++)
            lr.SetPosition(i, localPoints[i]);

        lr.startWidth = width;
        lr.endWidth = width;
    }

    private void UpdateStartMarkerPos()
    {
        if (startMarker == null || localPoints.Count == 0) return;

        // Ensure marker is a child so it moves with the curve
        if (startMarker.transform.parent != transform)
            startMarker.transform.SetParent(transform, true);

        // Put it at the first point in LOCAL space
        startMarker.transform.localPosition = localPoints[0];
    }

}
