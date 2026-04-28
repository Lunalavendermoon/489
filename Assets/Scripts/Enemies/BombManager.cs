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

    [Header("Progression")]
    [Tooltip("Seconds since run start when each additional bomb becomes active.")]
    public List<float> bombUnlockTimes = new List<float>() { 20f, 50f, 90f };

    [Header("Deposit Multiplier")]
    [Tooltip("Each active bomb increases deposit score by this multiplier amount.")]
    [Min(0f)] public float depositBonusPerActiveBomb = 0.05f;

    [Header("Next Deposit Bonus")]
    [SerializeField] private bool hasStoredNextDepositBonus = false;
    [SerializeField] private int storedNextDepositBonus = 0;

    [Header("Runtime")]
    [SerializeField] private float elapsedTime;
    [SerializeField] private int activeBombCount;

    public int ActiveBombCount => activeBombCount;
    public bool HasStoredNextDepositBonus => hasStoredNextDepositBonus;
    public int StoredNextDepositBonus => storedNextDepositBonus;

    public float CurrentDepositMultiplier
    {
        get
        {
            return 1f + (activeBombCount * depositBonusPerActiveBomb);
        }
    }

    private void Awake()
    {
        SortUnlockTimes();
        WireBombRefs();
        ResetBombSystem();
    }

    private void Update()
    {
        if (gm == null || gm.state != GameManager.GameState.Normal)
            return;

        elapsedTime += Time.deltaTime;
        UpdateActiveBombsFromTimeline();
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

    private void SortUnlockTimes()
    {
        bombUnlockTimes.Sort();
    }

    private void UpdateActiveBombsFromTimeline()
    {
        int targetActive = 0;

        for (int i = 0; i < bombUnlockTimes.Count; i++)
        {
            if (elapsedTime >= bombUnlockTimes[i])
                targetActive++;
        }

        targetActive = Mathf.Clamp(targetActive, 0, bombs.Count);

        if (targetActive == activeBombCount)
            return;

        for (int i = 0; i < bombs.Count; i++)
        {
            BombController bomb = bombs[i];
            if (bomb == null) continue;

            if (i < targetActive)
            {
                if (!bomb.IsActiveBomb)
                    bomb.ActivateBomb();
            }
            else
            {
                if (bomb.IsActiveBomb)
                    bomb.DeactivateBomb();
            }
        }

        activeBombCount = targetActive;
    }

    public void GrantNextDepositBonus(int amount)
    {
        if (amount <= 0) return;

        hasStoredNextDepositBonus = true;
        storedNextDepositBonus = amount;
    }

    public int ConsumeNextDepositBonus()
    {
        if (!hasStoredNextDepositBonus)
            return 0;

        int bonus = storedNextDepositBonus;
        hasStoredNextDepositBonus = false;
        storedNextDepositBonus = 0;
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