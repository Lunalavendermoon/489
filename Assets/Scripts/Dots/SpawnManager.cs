using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public GameManager gm;

    [Header("Dot Pool")]
    public Dot dotPrefab;
    public int dotPrewarm = 32;

    [Header("Dot Spawns")]
    public bool spawnDots = true;
    public float dotSpawnPerSecond = 1.6f;
    public float dotSpawnMinRadius = 1.6f;
    public float dotSpawnMaxRadius = 4.8f;

    [Header("Special Dots (Batch)")]
    public bool spawnSpecialDots = true;

    [Header("Health Dots")]
    [Tooltip("If true, health dots are maintained on screen at all times.")]
    public bool spawnHealthDots = true;

    [Tooltip("Maximum number of health dots active at once.")]
    public int maxHealthDots = 5;

    [Tooltip("Starting health assigned to spawned health dots.")]
    [Min(1)] public int healthDotStartingHealth = 3;

    [Tooltip("How many special dots appear at once on the field.")]
    public int specialBatchCount = 3;

    [Tooltip("Delay before a new special batch spawns after the previous batch is fully eliminated.")]
    public float specialRespawnDelay = 0.25f;

    [Tooltip("If true, if the special batch becomes incomplete (missing any dot), clear and respawn the whole batch.")]
    public bool enforceFullSpecialBatch = true;

    [Tooltip("How often (seconds) we validate the special batch. Lower = more aggressive, higher = less CPU.")]
    public float specialWatchdogInterval = 0.15f;

    [Header("Spawn Bounds Padding")]
    public float screenPadding = 0.6f;

    [Header("Spawn Exclusion")]
    [Tooltip("Optional reference to the CoreController - exclusion is centered on this if provided, otherwise world origin.")]
    public CoreController core;

    [Tooltip("Radius around the center where no dots will spawn.")]
    public float centerExclusionRadius = 0.8f;

    [Tooltip("How many attempts to avoid the exclusion zone before accepting a point (prevents infinite loops).")]
    public int exclusionMaxAttempts = 12;

    private ObjectPool<Dot> dotPool;

    private float dotAcc;
    private bool paused;

    private readonly HashSet<Dot> activeNormalDots = new HashSet<Dot>();
    private readonly HashSet<Dot> activeSpecialDots = new HashSet<Dot>();
    private readonly HashSet<Dot> activeHealthDots = new HashSet<Dot>();
    private float specialRespawnTimer = 0f;

    private bool timedAllowNormalDots = true;
    private bool timedAllowSpecialDots = true;
    private bool timedAllowHealthDots = true;
    private bool timedOverrideDotSpawnRate = false;
    private float timedDotSpawnPerSecond = 0f;
    private bool timedOverrideMaxHealthDots = false;
    private int timedMaxHealthDots = 0;

    // Guards
    private bool isClearingSpecials = false;
    private bool suppressSpecialLossClear = false;

    // Watchdog timer
    private float watchdogTimer = 0f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (dotPrefab != null) dotPool = new ObjectPool<Dot>(dotPrefab, dotPrewarm, transform);
    }

    private void Update()
    {
        if (paused) return;
        if (cam == null) return;

        if (gm != null && (gm.state == GameManager.GameState.GameOver || gm.state == GameManager.GameState.EndScreen))
            return;

        // Normal dots
        if (ShouldSpawnNormalDots())
        {
            dotAcc += Time.deltaTime * GetEffectiveDotSpawnPerSecond();
            while (dotAcc >= 1f)
            {
                dotAcc -= 1f;
                SpawnDot(isSpecial: false);
            }
        }

        // Health dot pool management
        if (ShouldSpawnHealthDots())
        {
            CleanupHealthSet();
            while (activeHealthDots.Count < GetEffectiveMaxHealthDots())
                SpawnHealthDot();
        }

        // Special batch logic + watchdog
        if (ShouldSpawnSpecialDots())
        {
            // Watchdog: periodically validate batch consistency
            watchdogTimer -= Time.deltaTime;
            if (watchdogTimer <= 0f)
            {
                watchdogTimer = Mathf.Max(0.02f, specialWatchdogInterval);
                ValidateSpecialBatch();
            }

            // Spawn only when none exist
            if (CountAliveSpecialDots() == 0)
            {
                specialRespawnTimer -= Time.deltaTime;
                if (specialRespawnTimer <= 0f)
                {
                    SpawnSpecialBatch();
                }
            }
            else
            {
                // while specials exist, keep timer reset so it doesn't spawn mid-batch
                specialRespawnTimer = specialRespawnDelay;
            }
        }
    }

    private void SpawnDot(bool isSpecial)
    {
        if (dotPool == null) return;

        Vector2 pos = isSpecial
            ? RandomPointInScreenBounds(screenPadding)
            : RandomPointInAnnulus(dotSpawnMinRadius, dotSpawnMaxRadius);

        var d = dotPool.Spawn(pos, Quaternion.identity);
        d.Init(cam);
        d.SetSpecial(isSpecial);
        d.ConfigureHealthDot(false);

        // Pool hook
        var pr = d.GetComponent<PoolRef>() ?? d.gameObject.AddComponent<PoolRef>();
        pr.despawnAction = () =>
        {
            if (d != null)
                UntrackDot(d);

            dotPool.Despawn(d);

            // If a special dot despawned unexpectedly, batch might become incomplete.
            // The watchdog will handle respawn; we keep logic centralized there.
        };

        if (isSpecial)
            activeSpecialDots.Add(d);
        else
            activeNormalDots.Add(d);
    }

    private void SpawnHealthDot()
    {
        if (dotPool == null) return;

        Vector2 pos = RandomPointInScreenBounds(screenPadding);

        var d = dotPool.Spawn(pos, Quaternion.identity);
        d.Init(cam);
        d.SetSpecial(false);
        d.ConfigureHealthDot(true, healthDotStartingHealth);

        var pr = d.GetComponent<PoolRef>() ?? d.gameObject.AddComponent<PoolRef>();
        pr.despawnAction = () =>
        {
            if (d != null)
                UntrackDot(d);

            dotPool.Despawn(d);
        };

        activeHealthDots.Add(d);
    }

    public void SetTimedSpawnRules(bool allowNormalDots, bool allowSpecialDots, bool allowHealthDots)
    {
        timedAllowNormalDots = allowNormalDots;
        timedAllowSpecialDots = allowSpecialDots;
        timedAllowHealthDots = allowHealthDots;
    }

    public void SetTimedOverrides(bool overrideDotSpawnRate, float dotSpawnRate, bool overrideHealthDotCount, int healthDotCount)
    {
        timedOverrideDotSpawnRate = overrideDotSpawnRate;
        timedDotSpawnPerSecond = Mathf.Max(0f, dotSpawnRate);
        timedOverrideMaxHealthDots = overrideHealthDotCount;
        timedMaxHealthDots = Mathf.Max(0, healthDotCount);
    }

    public void ResetTimedSpawnRules()
    {
        timedAllowNormalDots = true;
        timedAllowSpecialDots = true;
        timedAllowHealthDots = true;
        timedOverrideDotSpawnRate = false;
        timedDotSpawnPerSecond = 0f;
        timedOverrideMaxHealthDots = false;
        timedMaxHealthDots = 0;
    }

    public void ClearDisallowedFreeDots(bool allowNormalDots, bool allowSpecialDots, bool allowHealthDots)
    {
        if (!allowNormalDots)
            DespawnFreeDots(activeNormalDots);

        if (!allowSpecialDots)
            DespawnFreeDots(activeSpecialDots);

        if (!allowHealthDots)
            DespawnFreeDots(activeHealthDots);
    }

    private void CleanupHealthSet()
    {
        CleanupDotSet(activeHealthDots, dot => dot.IsHealthDot);
    }

    private void SpawnSpecialBatch()
    {
        if (isClearingSpecials) return;

        // Make sure tracking is clean
        CleanupSpecialSet();

        if (activeSpecialDots.Count > 0) return;

        int count = Mathf.Max(1, specialBatchCount);
        for (int i = 0; i < count; i++)
            SpawnDot(isSpecial: true);

        // after spawning, give watchdog time to see them
        watchdogTimer = Mathf.Max(0.02f, specialWatchdogInterval);
    }

    /// <summary>
    /// Call from CoreController while batch-depositing specials so we don't treat it like a loss.
    /// </summary>
    public void SuppressSpecialLossClear(bool v)
    {
        suppressSpecialLossClear = v;
    }

    /// <summary>
    /// Strong consistency check:
    /// If we have 1..(specialBatchCount-1) alive specials, clear them all and respawn later.
    /// </summary>
    private void ValidateSpecialBatch()
    {
        if (!enforceFullSpecialBatch) return;
        if (suppressSpecialLossClear) return;
        if (isClearingSpecials) return;

        int alive = CountAliveSpecialDots();

        // If the batch is incomplete but not empty -> reset it
        int desired = Mathf.Max(1, specialBatchCount);
        if (alive > 0 && alive < desired)
        {
            DespawnAllSpecialDots();
        }
    }

    private int CountAliveSpecialDots()
    {
        CleanupSpecialSet();
        return activeSpecialDots.Count;
    }

    private void CleanupSpecialSet()
    {
        CleanupDotSet(activeSpecialDots, dot => dot.IsSpecial);
    }

    private void DespawnAllSpecialDots()
    {
        if (isClearingSpecials) return;

        isClearingSpecials = true;

        CleanupSpecialSet();

        Dot[] remaining = new Dot[activeSpecialDots.Count];
        activeSpecialDots.CopyTo(remaining);

        for (int i = 0; i < remaining.Length; i++)
        {
            Dot d = remaining[i];
            if (d == null) continue;
            d.DespawnSelf();
        }

        activeSpecialDots.Clear();

        // Respawn delay before next batch
        specialRespawnTimer = Mathf.Max(0f, specialRespawnDelay);

        isClearingSpecials = false;
    }

    public void SetPaused(bool v) => paused = v;

    public void ResetAll()
    {
        dotPool?.DespawnAllActive();
        dotAcc = 0f;

        activeNormalDots.Clear();
        activeSpecialDots.Clear();
        activeHealthDots.Clear();
        specialRespawnTimer = 0f;

        isClearingSpecials = false;
        suppressSpecialLossClear = false;

        watchdogTimer = 0f;
    }

    private bool ShouldSpawnNormalDots()
    {
        return spawnDots && timedAllowNormalDots;
    }

    private bool ShouldSpawnSpecialDots()
    {
        return spawnSpecialDots && timedAllowSpecialDots;
    }

    private bool ShouldSpawnHealthDots()
    {
        return spawnHealthDots && timedAllowHealthDots;
    }

    private float GetEffectiveDotSpawnPerSecond()
    {
        return timedOverrideDotSpawnRate ? timedDotSpawnPerSecond : dotSpawnPerSecond;
    }

    private int GetEffectiveMaxHealthDots()
    {
        return timedOverrideMaxHealthDots ? timedMaxHealthDots : maxHealthDots;
    }

    private void CleanupNormalSet()
    {
        CleanupDotSet(activeNormalDots, dot => !dot.IsSpecial && !dot.IsHealthDot);
    }

    private void CleanupDotSet(HashSet<Dot> trackedDots, System.Predicate<Dot> isExpectedType)
    {
        if (trackedDots.Count == 0) return;

        Dot[] arr = new Dot[trackedDots.Count];
        trackedDots.CopyTo(arr);

        for (int i = 0; i < arr.Length; i++)
        {
            Dot d = arr[i];
            if (d == null || !d.gameObject.activeInHierarchy || !isExpectedType(d))
                trackedDots.Remove(d);
        }
    }

    private void DespawnFreeDots(HashSet<Dot> trackedDots)
    {
        if (trackedDots.Count == 0) return;

        Dot[] arr = new Dot[trackedDots.Count];
        trackedDots.CopyTo(arr);

        for (int i = 0; i < arr.Length; i++)
        {
            Dot d = arr[i];
            if (d == null)
            {
                trackedDots.Remove(d);
                continue;
            }

            if (!d.gameObject.activeInHierarchy)
            {
                trackedDots.Remove(d);
                continue;
            }

            if (d.IsCarried)
                continue;

            d.DespawnSelf();
        }
    }

    private void UntrackDot(Dot dot)
    {
        activeNormalDots.Remove(dot);
        activeSpecialDots.Remove(dot);
        activeHealthDots.Remove(dot);
    }

    private Vector2 RandomPointInAnnulus(float rMin, float rMax)
    {
        Vector2 center = (core != null) ? core.CorePosition : Vector2.zero;

        // Ensure the exclusion radius is respected by using a minimum radius
        float minR = Mathf.Max(rMin, centerExclusionRadius);

        // If exclusion radius makes spawning impossible within the annulus, fall back to the middle radius
        if (minR >= rMax)
        {
            float r = Mathf.Clamp((rMin + rMax) * 0.5f, Mathf.Epsilon, Mathf.Max(rMax - 0.001f, Mathf.Epsilon));
            float a = Random.Range(0f, Mathf.PI * 2f);
            return center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        float rr = Random.Range(minR, rMax);
        float aa = Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector2(Mathf.Cos(aa) * rr, Mathf.Sin(aa) * rr);
    }

    private Vector2 RandomPointInScreenBounds(float padding)
    {
        Vector2 center = (core != null) ? core.CorePosition : Vector2.zero;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        Vector2 p = Vector2.zero;
        int attempts = 0;
        while (attempts < Mathf.Max(1, exclusionMaxAttempts))
        {
            float x = Random.Range(-w + padding, w - padding);
            float y = Random.Range(-h + padding, h - padding);
            p = center + new Vector2(x, y);

            if (Vector2.Distance(p, center) >= centerExclusionRadius)
                return p;

            attempts++;
        }

        // If we failed to find a point outside the exclusion after several tries, return last sample.
        return p;
    }
}
