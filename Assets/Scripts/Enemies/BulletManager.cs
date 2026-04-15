// BulletManager.cs
using UnityEngine;

public class BulletManager : MonoBehaviour
{
    [Header("Debug / Mode Toggles")]
    public bool spawnBullets = true;

    [Header("Refs")]
    public Camera cam;
    public GameManager gm;

    [Header("Pool")]
    public Bullet bulletPrefab;
    public int prewarm = 48;

    [Header("Spawning (Fixed Interval)")]
    [Tooltip("Spawn a bullet batch every N seconds.")]
    public float bulletIntervalSeconds = 0.65f;

    [Tooltip("How many bullets to spawn each interval (small burst).")]
    public int bulletsPerInterval = 1;

    public float spawnOffscreenPadding = 0.7f;

    [Header("Speed")]
    public float baseSpeed = 5.5f;
    public float extraSpeedAtMaxDifficulty = 5.5f;

    private ObjectPool<Bullet> pool;
    private bool paused;
    private float timer;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (bulletPrefab != null) pool = new ObjectPool<Bullet>(bulletPrefab, prewarm, transform);
    }

    private void Update()
    {
        if (!spawnBullets) return;
        if (paused) return;
        if (pool == null || cam == null || gm == null) return;

        // Only stop spawning on run end states
        if (gm.state == GameManager.GameState.GameOver || gm.state == GameManager.GameState.EndScreen)
            return;

        timer += Time.deltaTime;

        // Catch up cleanly if there's a lag spike
        while (timer >= bulletIntervalSeconds)
        {
            timer -= bulletIntervalSeconds;

            for (int i = 0; i < bulletsPerInterval; i++)
                SpawnBullet();
        }
    }

    private void SpawnBullet()
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        // pick a random edge
        int edge = Random.Range(0, 4);
        Vector2 pos = Vector2.zero;
        Vector2 dir = Vector2.right;

        float t = Random.value;

        if (edge == 0) { pos = new Vector2(-w - spawnOffscreenPadding, Mathf.Lerp(-h, h, t)); dir = Vector2.right; }
        if (edge == 1) { pos = new Vector2(w + spawnOffscreenPadding, Mathf.Lerp(-h, h, t)); dir = Vector2.left; }
        if (edge == 2) { pos = new Vector2(Mathf.Lerp(-w, w, t), -h - spawnOffscreenPadding); dir = Vector2.up; }
        if (edge == 3) { pos = new Vector2(Mathf.Lerp(-w, w, t), h + spawnOffscreenPadding); dir = Vector2.down; }

        // small aim variance toward center
        Vector2 toCenter = (-pos).normalized;
        dir = Vector2.Lerp(dir, toCenter, 0.35f).normalized;

        float speed = baseSpeed + (extraSpeedAtMaxDifficulty * gm.Difficulty01);

        Bullet b = pool.Spawn(pos, Quaternion.identity);

        // Keep your Bullet.Init call if you want, but "damage" should no longer be used for HP.
        // Ideally update Bullet so on hit it calls gm.OnPlayerHit().
        b.Init(dir, speed, 0f, gm);

        var pr = b.GetComponent<PoolRef>() ?? b.gameObject.AddComponent<PoolRef>();
        pr.despawnAction = () => pool.Despawn(b);
    }

    public void SetPaused(bool v) => paused = v;

    public void DespawnAllBullets() => pool?.DespawnAllActive();

    public void ResetTimer() => timer = 0f;
}
