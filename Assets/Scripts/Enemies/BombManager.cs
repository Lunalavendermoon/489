using System.Collections.Generic;
using UnityEngine;

public class BombManager : MonoBehaviour
{
    [Header("Refs")]
    public GameManager gm;
    public BeatManager beat;
    public PlayerCursorController cursor;

    [Header("Bomb List")]
    [Tooltip("Assign fixed bomb instances placed in the scene.")]
    public List<BombController> bombs = new List<BombController>();

    [Header("Deposit Multiplier")]
    [Tooltip("Each active bomb increases deposit score by this multiplier amount.")]
    [Min(0f)] public float depositBonusPerActiveBomb = 0.05f;

    [Header("Next Deposit Bonus")]
    [Tooltip("If greater than 0, caps how much flat bomb bonus can be stored at once. Set to 0 for no cap.")]
    [Min(0)] public int maxStoredNextDepositBonus = 0;

    [SerializeField] private bool hasStoredNextDepositBonus = false;
    [SerializeField] private int storedNextDepositBonus = 0;

    [Header("Runtime")]
    [SerializeField] private float elapsedTime;
    [SerializeField] private int activeBombCount;

    public float ElapsedTime => elapsedTime;
    public int ActiveBombCount => activeBombCount;
    public bool HasStoredNextDepositBonus => hasStoredNextDepositBonus;
    public int StoredNextDepositBonus => storedNextDepositBonus;

    public float CurrentDepositMultiplier
    {
        get { return 1f + (activeBombCount * depositBonusPerActiveBomb); }
    }

    private void Awake()
    {
        WireBombRefs();
        ResetBombSystem();
    }

    private void Update()
    {
        if (gm == null || gm.state != GameManager.GameState.Normal)
            return;

        elapsedTime += Time.deltaTime;
        UpdateBombActivation();
    }

    private void WireBombRefs()
    {
        for (int i = 0; i < bombs.Count; i++)
        {
            BombController bomb = bombs[i];
            if (bomb == null) continue;

            bomb.gm = gm;
            bomb.beat = beat;
            bomb.cursor = cursor;
            bomb.bombManager = this;
        }
    }

    private void UpdateBombActivation()
    {
        int count = 0;

        for (int i = 0; i < bombs.Count; i++)
        {
            BombController bomb = bombs[i];
            if (bomb == null) continue;

            bool shouldBeActive = elapsedTime >= bomb.ActivateAtTime;

            if (shouldBeActive)
            {
                if (!bomb.IsActiveBomb)
                    bomb.ActivateBomb();

                count++;
            }
            else
            {
                if (bomb.IsActiveBomb)
                    bomb.DeactivateBomb();
            }
        }

        activeBombCount = count;
    }

    public void GrantNextDepositBonus(int amount)
    {
        if (amount <= 0) return;

        storedNextDepositBonus += amount;

        if (maxStoredNextDepositBonus > 0)
            storedNextDepositBonus = Mathf.Min(storedNextDepositBonus, maxStoredNextDepositBonus);

        hasStoredNextDepositBonus = storedNextDepositBonus > 0;

        Debug.Log($"[BombManager] Granted next deposit bonus: +{amount}, total stored = {storedNextDepositBonus}");
    }

    public int ConsumeNextDepositBonus()
    {
        if (!hasStoredNextDepositBonus || storedNextDepositBonus <= 0)
        {
            Debug.Log("[BombManager] No stored next deposit bonus to consume.");
            return 0;
        }

        int bonus = storedNextDepositBonus;
        hasStoredNextDepositBonus = false;
        storedNextDepositBonus = 0;

        Debug.Log($"[BombManager] Consumed stacked next deposit bonus: {bonus}");
        return bonus;
    }

    public void ResetBombSystem()
    {
        elapsedTime = 0f;
        activeBombCount = 0;
        hasStoredNextDepositBonus = false;
        storedNextDepositBonus = 0;

        for (int i = 0; i < bombs.Count; i++)
        {
            if (bombs[i] != null)
                bombs[i].DeactivateBomb();
        }
    }
}