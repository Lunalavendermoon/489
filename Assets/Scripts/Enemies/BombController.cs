using System.Collections.Generic;
using UnityEngine;

public class BombController : MonoBehaviour
{
    [Header("Refs")]
    public BeatManager beat;
    public GameManager gm;
    public PlayerCursorController cursor;
    public BombManager bombManager;
    public SpriteRenderer spriteRenderer;

    [Header("Timer")]
    [Min(0.1f)] public float maxCountdown = 12f;
    [Min(0f)] public float criticalThreshold = 3f;
    [SerializeField] private float currentCountdown;

    [Header("Beat Attack Window")]
    [Tooltip("Bomb is attackable within this many seconds from the beat peak.")]
    [Min(0.01f)] public float attackWindowSeconds = 0.30f;

    [Header("Attack Reward")]
    [Tooltip("How much time gets restored on a successful hit.")]
    [Min(0.1f)] public float timeRestoreOnHit = 4f;

    [Tooltip("Flat score bonus granted to the player's next core deposit.")]
    [Min(0)] public int nextDepositBonusOnHit = 100;

    [Header("Explosion Penalty")]
    [Min(0)] public int explosionScorePenalty = 1000;
    public bool resetComboOnExplode = true;

    [Header("Visuals")]
    public Color normalColor = Color.white;
    public Color openColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Color criticalColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("State")]
    [SerializeField] private bool isActiveBomb = false;
    [SerializeField] private bool isAttackable = false;

    public bool IsActiveBomb => isActiveBomb;
    public bool IsAttackable => isAttackable;
    public float CurrentCountdown => currentCountdown;
    public float Countdown01 => Mathf.Clamp01(currentCountdown / Mathf.Max(0.01f, maxCountdown));

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        DeactivateBomb();
    }

    private void Update()
    {
        if (!isActiveBomb) return;
        if (gm == null || gm.state != GameManager.GameState.Normal) return;
        if (beat == null) return;

        currentCountdown -= Time.deltaTime;

        isAttackable = beat.DistanceToPeakSeconds <= attackWindowSeconds;
        UpdateVisual();

        TryHandleAttack();

        if (currentCountdown <= 0f)
            ExplodeAndReset();
    }

    public void ActivateBomb()
    {
        isActiveBomb = true;
        currentCountdown = maxCountdown;
        isAttackable = false;
        UpdateVisual();
        gameObject.SetActive(true);
    }

    public void DeactivateBomb()
    {
        isActiveBomb = false;
        isAttackable = false;
        currentCountdown = maxCountdown;
        UpdateVisual();
        gameObject.SetActive(false);
    }

    public void ResetBomb()
    {
        if (!isActiveBomb) return;
        currentCountdown = maxCountdown;
        isAttackable = false;
        UpdateVisual();
    }

    private void TryHandleAttack()
    {
        if (!isAttackable) return;
        if (cursor == null) return;

        IReadOnlyList<Dot> carried = cursor.CarriedDots;
        if (carried == null || carried.Count == 0) return;

        Vector2 bombPos = transform.position;

        for (int i = carried.Count - 1; i >= 0; i--)
        {
            Dot d = carried[i];
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            if (d.IsSpecial) continue;

            float dist = Vector2.Distance(d.transform.position, bombPos);
            if (dist > 0.55f) continue;

            // Successful bomb hit
            cursor.RemoveCarriedDot(d);
            d.SetCarried(false);
            d.DespawnSelf();

            currentCountdown = Mathf.Min(maxCountdown, currentCountdown + timeRestoreOnHit);
            bombManager?.GrantNextDepositBonus(nextDepositBonusOnHit);

            // Prevent multiple hits in same open window
            isAttackable = false;
            UpdateVisual();
            return;
        }
    }

    private void ExplodeAndReset()
    {
        gm?.SubtractScore(explosionScorePenalty);

        if (resetComboOnExplode)
            gm?.ResetCombo();

        currentCountdown = maxCountdown;
        isAttackable = false;
        UpdateVisual();
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
            spriteRenderer.color = openColor;
        else if (currentCountdown <= criticalThreshold)
            spriteRenderer.color = criticalColor;
        else
            spriteRenderer.color = normalColor;
    }
}