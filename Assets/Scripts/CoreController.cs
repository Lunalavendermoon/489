using UnityEngine;

public class CoreController : MonoBehaviour
{
    [Header("Core")]
    public Transform coreTransform;
    public float coreRadius = 0.8f;

    [Header("Fill")]
    public int fillMax = 8;
    public int dotFillAmount = 1;

    [Header("Rewards (Normal Dots)")]
    public int dotBasePoints = 100;
    public int dotBonusPoints = 150;

    [Header("Perfect Flat Bonus (Score)")]
    public int dotPerfectFlatBonus = 200;

    [Header("Special Dots (Batch Rule)")]
    [Tooltip("How many SPECIAL dots must be carried before they can be deposited as a batch.")]
    public int specialRequiredCount = 3;

    [Tooltip("Special dot reward multiplier vs normal dot rewards.")]
    public float specialRewardMultiplier = 1.8f;

    [Tooltip("Flat bonus applied ONCE when a special batch deposit is PERFECT.")]
    public int specialPerfectFlatBonus = 400;

    [Tooltip("Fill added once per special batch deposit.")]
    public int specialBatchFillAmount = 2;

    [Header("Timing")]
    public float maxWindowSeconds = 0.28f;

    [Header("Deposit Batch UI (Normal Dots)")]
    [Tooltip("Deposits within this time window get combined into one UI pop (useful for chain-follow dots).")]
    public float depositBatchWindow = 0.14f;

    [Header("Refs")]
    public GameManager gm;
    public BeatManager beat;
    public PlayerCursorController cursor;
    public UIManager ui;
    public EffectsManager fx;
    public SpawnManager spawns;

    private int fill;

    // Batched UI state (groups NORMAL dot deposits across multiple frames)
    private float batchTimer = 0f;
    private int batchPoints = 0;
    private float batchHP = 0f;
    private int batchCount = 0;
    private string batchBestJudge = null;
    private bool batchAnyPerfect = false;

    public Vector2 CorePosition => (coreTransform != null) ? (Vector2)coreTransform.position : (Vector2)transform.position;

    private Vector2 GetCursorWorldPos()
    {
        // If you have a cursor transform on PlayerCursorController, use that instead.
        // Otherwise use mouse position -> world.
        if (cursor != null && cursor.transform != null)
            return cursor.transform.position;

        if (Camera.main == null) return Vector2.zero;
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(wp.x, wp.y);
    }

    /// <summary>
    /// Determines which directional area of the core was hit based on a collision point.
    /// Divides the core into 4 quadrants: Up, Down, Left, Right.
    /// </summary>
    private CoreDirection GetCoreDirectionForCollision(Vector2 collisionPoint)
    {
        Vector2 corePos = CorePosition;
        Vector2 delta = collisionPoint - corePos;

        // Use absolute values to determine which axis is more dominant
        float absDeltaX = Mathf.Abs(delta.x);
        float absDeltaY = Mathf.Abs(delta.y);

        if (absDeltaY > absDeltaX)
        {
            // More vertical - check if up or down
            return delta.y > 0 ? CoreDirection.Up : CoreDirection.Down;
        }
        else
        {
            // More horizontal - check if left or right
            return delta.x > 0 ? CoreDirection.Right : CoreDirection.Left;
        }
    }

    private void Awake()
    {
        if (coreTransform == null) coreTransform = transform;
    }

    private void Update()
    {
        if (gm == null || gm.state != GameManager.GameState.Normal) return;
        if (cursor == null) return;

        // ---- Flush batched NORMAL DOT UI if the window expires ----
        if (batchCount > 0)
        {
            batchTimer -= Time.deltaTime;
            if (batchTimer <= 0f)
            {
                string label = batchBestJudge ?? "OK";
                if (batchCount > 1) label += $" x{batchCount}";

                ui?.ShowJudgment(label, batchPoints, batchHP);

                // Normal batch pop (small). Perfect dots already big-pop immediately per-dot.
                fx?.DepositPop(false);

                ResetBatch();
            }
        }

        var carried = cursor.CarriedDots;
        if (carried == null || carried.Count == 0) return;

        bool cursorInCore = Vector2.Distance(GetCursorWorldPos(), CorePosition) <= coreRadius;

        // ---- SPECIAL DOTS: deposit ALL at once only if you have all required ----
        if (cursorInCore)
        {
            int carriedSpecial = CountCarriedSpecialDots();
            int required = Mathf.Max(1, specialRequiredCount);

            if (carriedSpecial >= required)
            {
                // Deposit exactly "required" specials as one batch
                DepositSpecialBatch(required);

                // If we triggered super countdown, stop processing this frame
                if (gm.state != GameManager.GameState.Normal)
                    return;
            }
        }

        // ---- NORMAL DOT DEPOSITS (batched over time window) ----
        // If cursor is in core, deposit ALL carried NORMAL dots immediately.
        // Otherwise, fall back to per-dot position checks.
        for (int i = carried.Count - 1; i >= 0; i--)
        {
            Dot d = carried[i];
            if (d == null || !d.gameObject.activeInHierarchy) continue;

            // Special dots are handled only by batch deposit (never individually).
            if (d.IsSpecial) continue;

            bool dotInCore = Vector2.Distance(d.transform.position, CorePosition) <= coreRadius;
            if (cursorInCore || dotInCore)
            {
                DepositNormalDot_NoUI(d, out int pts, out float hp, out string judge, out bool isPerfect);

                // Accumulate for combined UI
                batchPoints += pts;
                batchHP += hp;
                batchCount++;
                batchAnyPerfect |= isPerfect;

                if (batchBestJudge == null || JudgeRank(judge) > JudgeRank(batchBestJudge))
                    batchBestJudge = judge;

                // Extend batching window each time a dot deposits
                batchTimer = depositBatchWindow;

                // If core fill triggered countdown/super, stop further deposits
                if (gm.state != GameManager.GameState.Normal)
                    break;
            }
        }
    }

    private int CountCarriedSpecialDots()
    {
        var carried = cursor.CarriedDots;
        if (carried == null) return 0;

        int count = 0;
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i] != null && carried[i].IsSpecial)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Deposits exactly "required" SPECIAL dots as a single batch:
    ///  - one judgment
    ///  - one sound
    ///  - one UI pop
    ///  - one fill increment (specialBatchFillAmount)
    /// </summary>
    private void DepositSpecialBatch(int required)
    {
        if (gm == null || beat == null || cursor == null) return;

        required = Mathf.Max(1, required);

        // One timing evaluation for the whole batch
        float acc01;
        string judge = beat.Judgment(out acc01, maxWindowSeconds);
        float curveAcc = acc01 * acc01;

        bool isPerfect = (judge == "PERFECT");

        // Compute per-dot special rewards
        int perBasePts = Mathf.RoundToInt(dotBasePoints * specialRewardMultiplier);
        int perBonusPts = Mathf.RoundToInt(dotBonusPoints * specialRewardMultiplier);

        int totalPoints = 0;
        float totalHP = 0f;

        // Remove and despawn exactly 'required' special dots from carried list
        int remaining = required;

        // Iterate backwards so RemoveCarriedDot is safe
        for (int i = cursor.CarriedDots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            spawns?.SuppressSpecialLossClear(true);
            Dot d = cursor.CarriedDots[i];
            if (d == null || !d.IsSpecial) continue;

            cursor.RemoveCarriedDot(d);
            d.SetCarried(false);

            int pts = perBasePts + Mathf.RoundToInt(perBonusPts * curveAcc);

            totalPoints += pts;
            d.DespawnSelf();
            remaining--;
            spawns?.SuppressSpecialLossClear(false);
        }

        // Flat bonus ONCE for perfect special-batch
        if (isPerfect)
        {
            totalPoints += specialPerfectFlatBonus;
            CoreDirection direction = GetCoreDirectionForCollision(GetCursorWorldPos());
            fx?.TriggerPerfectPostFX(direction);
            fx?.DepositPop(true);

            // Increment combo and apply combo multiplier to the batch score
            gm?.OnPerfectDeposit(true);
            float mul = gm != null ? gm.GetComboMultiplier() : 1f;
            totalPoints = Mathf.RoundToInt(totalPoints * mul);
        }
        else
        {
            fx?.DepositPop(false);
            // Non-perfect resets combo
            gm?.ResetCombo();
        }

        fx?.PlayDepositSfx(isPerfect);

        gm?.AddScore(totalPoints);

        // Show one UI for the batch
        ui?.ShowJudgment($"SPECIAL {judge} x{required}", totalPoints, totalHP);
        ui?.ShowSpecialScore(totalPoints);
        // Fill once per batch
        fill += specialBatchFillAmount;
        UpdateFillUI();

        if (fill >= fillMax)
        {
            fill = fillMax;
            UpdateFillUI();
            // Disabled: auto-supermode trigger. Supermode now only triggers at song end.
            // gm.RequestSuperCountdown();
        }
    }

    /// <summary>
    /// Deposits a NORMAL dot, awards score/HP/fill, despawns it.
    /// DOES NOT show UI (handled by batching).
    /// </summary>
    private void DepositNormalDot_NoUI(Dot dot, out int pointsAwarded, out float hpAwarded, out string judge, out bool isPerfect)
    {
        pointsAwarded = 0;
        hpAwarded = 0f;
        judge = "OK";
        isPerfect = false;

        if (gm == null || beat == null || dot == null) return;

        // Remove from carry list first so it can't score again
        cursor.RemoveCarriedDot(dot);
        dot.SetCarried(false);

        float acc01;
        judge = beat.Judgment(out acc01, maxWindowSeconds);
        float curveAcc = acc01 * acc01;

        pointsAwarded = dotBasePoints + Mathf.RoundToInt(dotBonusPoints * curveAcc);

        isPerfect = (judge == "PERFECT");
        if (isPerfect)
        {
            pointsAwarded += dotPerfectFlatBonus;
            CoreDirection direction = GetCoreDirectionForCollision(dot.transform.position);
            fx?.TriggerPerfectPostFX(direction);
            fx?.DepositPop(true); // big shake immediately for perfect

            // Combo: increment then apply multiplier to this dot's points
            gm?.OnPerfectDeposit(false);
            float mul = gm != null ? gm.GetComboMultiplier() : 1f;
            pointsAwarded = Mathf.RoundToInt(pointsAwarded * mul);
        }
        else
        {
            // Non-perfect resets combo
            gm?.ResetCombo();
        }
        fx?.PlayDepositSfx(isPerfect);

        fx?.PlayDepositSfx(isPerfect);

        gm?.AddScore(pointsAwarded);

        fill += dotFillAmount;
        UpdateFillUI();

        dot.DespawnSelf();

        if (fill >= fillMax)
        {
            fill = fillMax;
            UpdateFillUI();
            // Disabled: auto-supermode trigger. Supermode now only triggers at song end.
            // gm.RequestSuperCountdown();
        }
    }

    private int JudgeRank(string j)
    {
        switch (j)
        {
            case "PERFECT": return 3;
            case "GREAT": return 2;
            case "OK": return 1;
            default: return 0; // EARLY/LATE, OFF, etc.
        }
    }

    private void ResetBatch()
    {
        batchTimer = 0f;
        batchPoints = 0;
        batchHP = 0f;
        batchCount = 0;
        batchBestJudge = null;
        batchAnyPerfect = false;
    }

    private void UpdateFillUI()
    {
        ui?.SetCoreFill(Mathf.Clamp01(fill / (float)Mathf.Max(1, fillMax)));
    }

    public void ResetFill()
    {
        fill = 0;
        UpdateFillUI();
        ResetBatch();
    }
}
