using System.Collections.Generic;
using UnityEngine;

public class BombController : MonoBehaviour
{
    [System.Serializable]
    public class AttackWindow
    {
        [Min(0f)] public float startTime = 5f;
        [Min(0.01f)] public float duration = 0.35f;

        public float EndTime => startTime + duration;

        public bool Contains(float t)
        {
            return t >= startTime && t < EndTime;
        }
    }

    [Header("Refs")]
    public BeatManager beat;
    public GameManager gm;
    public PlayerCursorController cursor;
    public BombManager bombManager;
    public SpriteRenderer spriteRenderer;

    [Header("Activation")]
    [Tooltip("Time since run start when this bomb becomes active.")]
    [Min(0f)] public float activateAtTime = 0f;

    [Header("Timer")]
    [Min(0.1f)] public float maxCountdown = 12f;
    [Min(0f)] public float criticalThreshold = 3f;
    [SerializeField] private float currentCountdown;

    [Header("Manual Attack Windows")]
    [Tooltip("Times since run start when this bomb can be attacked.")]
    public List<AttackWindow> attackWindows = new List<AttackWindow>();

    [Header("Attack")]
    [Min(0.01f)] public float hitRadius = 0.55f;
    [Min(0.1f)] public float timeRestoreOnHit = 4f;
    [Min(0)] public int nextDepositBonusOnHit = 100;

    [Header("Explosion Penalty")]
    [Min(0)] public int explosionScorePenalty = 1000;
    public bool resetComboOnExplode = true;

    [Header("Visuals")]
    [Tooltip("Base color when the bomb has lots of time left.")]
    public Color normalColor = Color.white;

    [Tooltip("Color shown while the bomb is attackable.")]
    public Color openColor = new Color(1f, 0.8f, 0.2f, 1f);

    [Tooltip("Color the bomb gradually shifts toward as time runs out.")]
    public Color dangerColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Pulse")]
    [Tooltip("Base scale when idle.")]
    public float minScaleMultiplier = 1f;

    [Tooltip("Scale multiplier at strongest pulse.")]
    public float maxScaleMultiplier = 1.06f;

    [Tooltip("Extra target scale while attackable.")]
    public float attackableScaleMultiplier = 1.2f;

    [Tooltip("How quickly the scale eases toward its target.")]
    public float scaleEaseSpeed = 10f;

    [Header("State")]
    [SerializeField] private bool isActiveBomb = false;
    [SerializeField] private bool isAttackable = false;
    [SerializeField] private bool lowWarningTriggered = false;

    private Vector3 baseScale;
    private float currentScaleMultiplier = 1f;

    public bool IsActiveBomb => isActiveBomb;
    public bool IsAttackable => isAttackable;
    public float ActivateAtTime => activateAtTime;
    public float CurrentCountdown => currentCountdown;
    public float Countdown01 => Mathf.Clamp01(currentCountdown / Mathf.Max(0.01f, maxCountdown));

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        baseScale = transform.localScale;
        currentScaleMultiplier = minScaleMultiplier;

        SortAttackWindows();
        DeactivateBomb();
    }

    private void OnValidate()
    {
        activateAtTime = Mathf.Max(0f, activateAtTime);
        maxCountdown = Mathf.Max(0.1f, maxCountdown);
        criticalThreshold = Mathf.Max(0f, criticalThreshold);
        hitRadius = Mathf.Max(0.01f, hitRadius);
        timeRestoreOnHit = Mathf.Max(0.1f, timeRestoreOnHit);
        maxScaleMultiplier = Mathf.Max(minScaleMultiplier, maxScaleMultiplier);
        attackableScaleMultiplier = Mathf.Max(1f, attackableScaleMultiplier);
        scaleEaseSpeed = Mathf.Max(0.01f, scaleEaseSpeed);
    }

    private void Update()
    {
        if (!isActiveBomb) return;

        AnimatePulse();

        if (gm == null || gm.state != GameManager.GameState.Normal)
        {
            UpdateVisual();
            return;
        }

        currentCountdown -= Time.deltaTime;

        float elapsed = bombManager != null ? bombManager.ElapsedTime : Time.timeSinceLevelLoad;
        isAttackable = IsWithinAnyAttackWindow(elapsed);

        UpdateVisual();
        TryHandleAttack();

        if (!lowWarningTriggered && currentCountdown <= criticalThreshold)
        {
            lowWarningTriggered = true;
            // TODO: Play low bomb warning SFX here
            AudioManager.Instance.PlayEvent("PlaySFXWolfHowl");
        }

        if (currentCountdown <= 0f)
            ExplodeAndReset();
    }

    public void ActivateBomb()
    {
        if (isActiveBomb) return;

        isActiveBomb = true;
        currentCountdown = maxCountdown;
        isAttackable = false;
        lowWarningTriggered = false;
        currentScaleMultiplier = minScaleMultiplier;

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        transform.localScale = baseScale * currentScaleMultiplier;
        UpdateVisual();
    }

    public void DeactivateBomb()
    {
        isActiveBomb = false;
        isAttackable = false;
        lowWarningTriggered = false;
        currentCountdown = maxCountdown;
        currentScaleMultiplier = minScaleMultiplier;

        transform.localScale = baseScale * currentScaleMultiplier;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    public void ResetBomb()
    {
        currentCountdown = maxCountdown;
        isAttackable = false;
        lowWarningTriggered = false;
        currentScaleMultiplier = minScaleMultiplier;

        transform.localScale = baseScale * currentScaleMultiplier;
        UpdateVisual();
    }

    private void SortAttackWindows()
    {
        attackWindows.Sort((a, b) => a.startTime.CompareTo(b.startTime));
    }

    private bool IsWithinAnyAttackWindow(float elapsed)
    {
        for (int i = 0; i < attackWindows.Count; i++)
        {
            AttackWindow w = attackWindows[i];
            if (w != null && w.Contains(elapsed))
                return true;
        }

        return false;
    }

    private void TryHandleAttack()
    {
        if (!isAttackable) return;
        if (cursor == null) return;

        var carried = cursor.CarriedDots;
        if (carried == null || carried.Count == 0) return;

        Vector2 bombPos = transform.position;

        for (int i = carried.Count - 1; i >= 0; i--)
        {
            Dot d = carried[i];
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            if (d.IsSpecial) continue;

            float dist = Vector2.Distance(d.transform.position, bombPos);
            if (dist > hitRadius) continue;

            cursor.RemoveCarriedDot(d);
            d.SetCarried(false);
            d.DespawnSelf();

            currentCountdown = Mathf.Min(maxCountdown, currentCountdown + timeRestoreOnHit);
            bombManager?.GrantNextDepositBonus(nextDepositBonusOnHit);
            Debug.Log($"[Bomb] Successful hit on {name}. Granted next deposit bonus: {nextDepositBonusOnHit}");

            // TODO: Play successful bomb hit / stabilize SFX here
            AudioManager.Instance.PlayEvent("PlaySFXPunch");

            lowWarningTriggered = currentCountdown <= criticalThreshold;
            UpdateVisual();
            return;
        }
    }

    private void ExplodeAndReset()
    {
        gm?.SubtractScore(explosionScorePenalty);

        if (resetComboOnExplode)
            gm?.ResetCombo();

        // TODO: Play bomb explosion SFX here
        AudioManager.Instance.PlayEvent("PlaySFXVineboom");

        currentCountdown = maxCountdown;
        isAttackable = false;
        lowWarningTriggered = false;
        currentScaleMultiplier = minScaleMultiplier;

        UpdateVisual();
    }

    private void AnimatePulse()
    {
        float pulseMul = minScaleMultiplier;

        if (beat != null)
        {
            float phase = beat.BeatPhase01;
            float pulse01 = Mathf.Sin(phase * Mathf.PI);
            pulseMul = Mathf.Lerp(minScaleMultiplier, maxScaleMultiplier, pulse01);
        }

        float targetMul = isAttackable ? pulseMul * attackableScaleMultiplier : pulseMul;

        float lerpT = 1f - Mathf.Exp(-scaleEaseSpeed * Time.deltaTime);
        currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetMul, lerpT);

        transform.localScale = baseScale * currentScaleMultiplier;
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        if (!isActiveBomb)
        {
            spriteRenderer.color = normalColor;
            return;
        }

        if (isAttackable)
        {
            spriteRenderer.color = openColor;
            return;
        }

        // 0 = full timer left, 1 = almost exploded
        float danger01 = 1f - Countdown01;
        danger01 = Mathf.Clamp01(danger01);

        // Optional easing so it stays calmer early and gets redder faster near the end
        danger01 = danger01 * danger01;

        spriteRenderer.color = Color.Lerp(normalColor, dangerColor, danger01);
    }
}