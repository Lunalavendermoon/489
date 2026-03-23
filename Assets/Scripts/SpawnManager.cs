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

    private readonly HashSet<Dot> activeSpecialDots = new HashSet<Dot>();
    private float specialRespawnTimer = 0f;

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
        if (spawnDots)
        {
            dotAcc += Time.deltaTime * dotSpawnPerSecond;
            while (dotAcc >= 1f)
            {
                dotAcc -= 1f;
                SpawnDot(isSpecial: false);
            }
        }

        // Special batch logic + watchdog
        if (spawnSpecialDots)
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

        // Pool hook
        var pr = d.GetComponent<PoolRef>() ?? d.gameObject.AddComponent<PoolRef>();
        pr.despawnAction = () =>
        {
            // Remove first (safe even if already removed)
            if (d != null) activeSpecialDots.Remove(d);

            dotPool.Despawn(d);

            // If a special dot despawned unexpectedly, batch might become incomplete.
            // The watchdog will handle respawn; we keep logic centralized there.
        };

        if (isSpecial)
            activeSpecialDots.Add(d);
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
        // Remove null / inactive / non-special from the set (pool edge cases)
        if (activeSpecialDots.Count == 0) return;

        // Copy to avoid modifying during iteration
        Dot[] arr = new Dot[activeSpecialDots.Count];
        activeSpecialDots.CopyTo(arr);

        for (int i = 0; i < arr.Length; i++)
        {
            Dot d = arr[i];
            if (d == null)
            {
                activeSpecialDots.Remove(d);
                continue;
            }

            if (!d.gameObject.activeInHierarchy)
            {
                activeSpecialDots.Remove(d);
                continue;
            }

            if (!d.IsSpecial)
            {
                activeSpecialDots.Remove(d);
                continue;
            }
        }
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

        activeSpecialDots.Clear();
        specialRespawnTimer = 0f;

        isClearingSpecials = false;
        suppressSpecialLossClear = false;

        watchdogTimer = 0f;
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
