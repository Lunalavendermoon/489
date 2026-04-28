using System.Collections.Generic;
using UnityEngine;

public class BulletManager : MonoBehaviour
{
    [System.Serializable]
    public class BulletPhase
    {
        [Header("Time Window")]
        [Min(0f)] public float startTime = 0f;
        [Min(0f)] public float endTime = 10f;

        [Header("Spawn Rules")]
        public bool spawnBullets = true;

        [Tooltip("Spawn a bullet batch every N seconds during this phase.")]
        [Min(0.01f)] public float bulletIntervalSeconds = 0.65f;

        [Tooltip("How many bullets to spawn each interval.")]
        [Min(1)] public int bulletsPerInterval = 1;

        [Header("Speed")]
        [Tooltip("If true, use this phase's speed instead of the global default.")]
        public bool overrideSpeed = false;

        public float baseSpeed = 5.5f;
        public float extraSpeedAtMaxDifficulty = 5.5f;

        public bool Contains(float t)
        {
            return t >= startTime && t < endTime;
        }
    }

    [Header("Debug / Mode Toggles")]
    public bool spawnBulletsGlobally = true;

    [Header("Refs")]
    public Camera cam;
    public GameManager gm;

    [Header("Pool")]
    public Bullet bulletPrefab;
    public int prewarm = 48;

    [Header("Default Spawn Settings")]
    public float defaultBulletIntervalSeconds = 0.65f;
    public int defaultBulletsPerInterval = 1;
    public float spawnOffscreenPadding = 0.7f;

    [Header("Default Speed")]
    public float baseSpeed = 5.5f;
    public float extraSpeedAtMaxDifficulty = 5.5f;

    [Header("Phases")]
    public List<BulletPhase> phases = new List<BulletPhase>();

    [Header("Runtime")]
    [SerializeField] private float timelineTime;
    [SerializeField] private int activePhaseIndex = -1;

    private ObjectPool<Bullet> pool;
    private bool paused;
    private float timer;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (bulletPrefab != null)
            pool = new ObjectPool<Bullet>(bulletPrefab, prewarm, transform);

        SortPhases();
    }

    private void Update()
    {
        if (!spawnBulletsGlobally) return;
        if (paused) return;
        if (pool == null || cam == null || gm == null) return;

        if (gm.state == GameManager.GameState.GameOver || gm.state == GameManager.GameState.EndScreen)
            return;

        timelineTime += Time.deltaTime;

        BulletPhase phase = GetActivePhase(timelineTime);

        // No active phase = no spawning
        if (phase == null || !phase.spawnBullets)
            return;

        float interval = Mathf.Max(0.01f, phase.bulletIntervalSeconds);
        int count = Mathf.Max(1, phase.bulletsPerInterval);

        timer += Time.deltaTime;

        while (timer >= interval)
        {
            timer -= interval;

            for (int i = 0; i < count; i++)
                SpawnBullet(phase);
        }
    }

    private void SpawnBullet(BulletPhase phase)
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        int edge = Random.Range(0, 4);
        Vector2 pos = Vector2.zero;
        Vector2 dir = Vector2.right;
        float t = Random.value;

        if (edge == 0)
        {
            pos = new Vector2(-w - spawnOffscreenPadding, Mathf.Lerp(-h, h, t));
            dir = Vector2.right;
        }
        else if (edge == 1)
        {
            pos = new Vector2(w + spawnOffscreenPadding, Mathf.Lerp(-h, h, t));
            dir = Vector2.left;
        }
        else if (edge == 2)
        {
            pos = new Vector2(Mathf.Lerp(-w, w, t), -h - spawnOffscreenPadding);
            dir = Vector2.up;
        }
        else
        {
            pos = new Vector2(Mathf.Lerp(-w, w, t), h + spawnOffscreenPadding);
            dir = Vector2.down;
        }

        Vector2 toCenter = (-pos).normalized;
        dir = Vector2.Lerp(dir, toCenter, 0.35f).normalized;

        float phaseBaseSpeed = phase.overrideSpeed ? phase.baseSpeed : baseSpeed;
        float phaseExtraSpeed = phase.overrideSpeed ? phase.extraSpeedAtMaxDifficulty : extraSpeedAtMaxDifficulty;
        float speed = phaseBaseSpeed + (phaseExtraSpeed * gm.Difficulty01);

        Bullet b = pool.Spawn(pos, Quaternion.identity);
        b.Init(dir, speed, 0f, gm);

        var pr = b.GetComponent<PoolRef>() ?? b.gameObject.AddComponent<PoolRef>();
        pr.despawnAction = () => pool.Despawn(b);
    }

    private BulletPhase GetActivePhase(float t)
    {
        activePhaseIndex = -1;

        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i] != null && phases[i].Contains(t))
            {
                activePhaseIndex = i;
                return phases[i];
            }
        }

        return null;
    }

    private void SortPhases()
    {
        phases.Sort((a, b) => a.startTime.CompareTo(b.startTime));
    }

    public void SetPaused(bool v) => paused = v;

    public void DespawnAllBullets() => pool?.DespawnAllActive();

    public void ResetTimer() => timer = 0f;

    public void ResetTimeline()
    {
        timelineTime = 0f;
        timer = 0f;
        activePhaseIndex = -1;
        DespawnAllBullets();
        SortPhases();
    }
}