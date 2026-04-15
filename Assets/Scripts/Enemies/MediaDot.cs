using System.Collections.Generic;
using UnityEngine;

public class MediaDot : MonoBehaviour
{
    [Header("Refs")]
    public CoreController core; 
    public PlayerCursorController cursor;
    public GameManager gm;

    [Header("Spiral Movement")]
    [Min(0.01f)] public float inwardSpeed = 2.2f;
    public float angularSpeedDeg = 90f;

    [Header("Collision")]
    [Min(0.01f)] public float cursorCollisionRadius = 0.35f;
    [Min(0.01f)] public float dotCollisionRadius = 0.3f;

    [Header("Penalty")]
    [Min(0)] public int scorePenaltyOnCoreEnter = 300;

    [Header("Debug")]
    [SerializeField] private float currentRadius;
    [SerializeField] private float currentAngleDeg;
    [SerializeField] private bool initialized;
    [SerializeField] private bool reachedCore;

    public void Initialize(
        CoreController coreRef,
        PlayerCursorController cursorRef,
        GameManager gmRef,
        float startRadius,
        float startAngleDeg,
        float inwardSpeedValue,
        float angularSpeedDegValue,
        int scorePenalty)
    {
        core = coreRef;
        cursor = cursorRef;
        gm = gmRef;

        currentRadius = Mathf.Max(0.01f, startRadius);
        currentAngleDeg = startAngleDeg;
        inwardSpeed = Mathf.Max(0.01f, inwardSpeedValue);
        angularSpeedDeg = angularSpeedDegValue;
        scorePenaltyOnCoreEnter = Mathf.Max(0, scorePenalty);

        reachedCore = false;
        initialized = true;

        UpdateWorldPosition();
    }

    private void Update()
    {
        if (!initialized || core == null || gm == null)
            return;

        if (gm.state != GameManager.GameState.Normal)
            return;

        AdvanceSpiral();

        if (CheckCursorCollision())
        {
            DestroyMedia();
            return;
        }

        if (CheckCarriedDotCollision())
        {
            DestroyMedia();
            return;
        }

        if (CheckReachedCore())
        {
            EnterCore();
            return;
        }
    }

    private void AdvanceSpiral()
    {
        currentRadius -= inwardSpeed * Time.deltaTime;
        currentAngleDeg += angularSpeedDeg * Time.deltaTime;
        UpdateWorldPosition();
    }

    private void UpdateWorldPosition()
    {
        Vector2 corePos = core.CorePosition;
        float rad = currentAngleDeg * Mathf.Deg2Rad;

        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;
        transform.position = corePos + offset;
    }

    private bool CheckCursorCollision()
    {
        if (cursor == null) return false;

        float dist = Vector2.Distance(transform.position, cursor.transform.position);
        return dist <= cursorCollisionRadius;
    }

    private bool CheckCarriedDotCollision()
    {
        if (cursor == null)
            return false;

        IReadOnlyList<Dot> carried = cursor.CarriedDots;
        if (carried == null || carried.Count == 0)
            return false;

        Vector2 mediaPos = transform.position;

        for (int i = 0; i < carried.Count; i++)
        {
            Dot d = carried[i];
            if (d == null || !d.gameObject.activeInHierarchy)
                continue;

            float dist = Vector2.Distance(mediaPos, d.transform.position);
            if (dist <= dotCollisionRadius)
                return true;
        }

        return false;
    }

    private bool CheckReachedCore()
    {
        return currentRadius <= core.coreRadius;
    }

    private void EnterCore()
    {
        if (reachedCore) return;
        reachedCore = true;

        gm.SubtractScore(scorePenaltyOnCoreEnter);
        DestroyMedia();
    }

    private void DestroyMedia()
    {
        Destroy(gameObject);
    }
}