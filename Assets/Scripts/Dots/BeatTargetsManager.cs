using UnityEngine;

public class BeatTargetsManager : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public GameManager gm;
    public BeatManager beat;
    public PlayerCursorController cursor;
    public UIManager ui;
    public EffectsManager fx;

    [Header("Beat Targets")]
    public BeatTarget beatTargetPrefab;
    [Min(1)] public int prewarm = 8;
    [Min(1)] public int perfectsToTrigger = 5;
    [Min(1)] public int beatsPerSequence = 5;
    [Min(1)] public int spawnEveryNBeats = 1;
    [Min(0.01f)] public float spawnPadding = 0.8f;

    [Header("Scoring")]
    [Min(0)] public int beatPerfectPoints = 450;
    [Min(0)] public int beatGreatPoints = 250;

    [Header("Timing")]
    [Tooltip("If true, beat hit success requires GREAT or PERFECT. If false, only PERFECT succeeds.")]
    public bool successOnGreatOrPerfect = true;

    [Header("Debug")]
    [SerializeField] private bool sequenceActive;
    [SerializeField] private int perfectsTowardTrigger;
    [SerializeField] private int beatsSpawned;
    [SerializeField] private int beatsResolved;

    private ObjectPool<BeatTarget> beatPool;
    private BeatTarget activeBeat;
    private bool cursorWasInside;
    private float nextSpawnTime;

    public bool IsSequenceActive => sequenceActive;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (beatTargetPrefab != null)
            beatPool = new ObjectPool<BeatTarget>(beatTargetPrefab, prewarm, transform);
    }

    private void Update()
    {
        if (!IsFeatureAvailable())
        {
            if (sequenceActive || perfectsTowardTrigger > 0 || activeBeat != null)
                ResetBeatsState();

            return;
        }

        if (gm != null && gm.state != GameManager.GameState.Normal)
        {
            if (gm.state == GameManager.GameState.GameOver || gm.state == GameManager.GameState.EndScreen)
                ResetBeatsState();

            return;
        }

        if (!sequenceActive)
            return;

        int maxBeats = Mathf.Max(1, beatsPerSequence);
        float now = beat.CurrentTime;

        if (activeBeat == null)
        {
            if (beatsResolved >= maxBeats)
            {
                EndSequence();
                return;
            }

            if (beatsSpawned < maxBeats && now >= nextSpawnTime)
                SpawnNextBeatTarget(now);

            return;
        }

        if (activeBeat.IsExpired(now))
        {
            ResolveActiveBeat(false, "MISS", 0);
            return;
        }

        TryHandleBeatAttempt(now);
    }

    public void RegisterNormalPerfectDeposit()
    {
        if (!IsFeatureAvailable()) return;
        if (sequenceActive) return;

        perfectsTowardTrigger++;
        if (perfectsTowardTrigger >= Mathf.Max(1, perfectsToTrigger))
            StartSequence();
    }

    public void ResetBeatsState()
    {
        sequenceActive = false;
        perfectsTowardTrigger = 0;
        beatsSpawned = 0;
        beatsResolved = 0;
        cursorWasInside = false;

        if (activeBeat != null)
        {
            activeBeat.DespawnSelf();
            activeBeat = null;
        }

        beatPool?.DespawnAllActive();
    }

    private void StartSequence()
    {
        if (!IsFeatureAvailable()) return;
        if (sequenceActive) return;

        sequenceActive = true;
        perfectsTowardTrigger = 0;
        beatsSpawned = 0;
        beatsResolved = 0;
        nextSpawnTime = beat != null ? beat.CurrentTime : 0f;

        SpawnNextBeatTarget(nextSpawnTime);
    }

    private void EndSequence()
    {
        sequenceActive = false;
        beatsSpawned = 0;
        beatsResolved = 0;
        cursorWasInside = false;
        activeBeat = null;
        nextSpawnTime = 0f;
    }

    private void SpawnNextBeatTarget(float spawnTime)
    {
        if (!sequenceActive) return;
        if (beatPool == null || beat == null) return;

        int maxBeats = Mathf.Max(1, beatsPerSequence);
        if (beatsSpawned >= maxBeats)
        {
            EndSequence();
            return;
        }

        float beatDuration = beat.BeatDuration;
        int n = Mathf.Max(1, spawnEveryNBeats);
        float currentIndex = Mathf.Floor(spawnTime / beatDuration);
        float hitBeatIndex = currentIndex + n;
        float hitTime = hitBeatIndex * beatDuration;
        if (hitTime <= spawnTime)
            hitTime = spawnTime + beatDuration * n;

        Vector2 pos = RandomPointInScreenBounds();

        BeatTarget spawned = beatPool.Spawn(pos, Quaternion.identity);
        spawned.Configure(beat, spawnTime, hitTime, beat.greatWindow);

        var pr = spawned.GetComponent<PoolRef>() ?? spawned.gameObject.AddComponent<PoolRef>();
        pr.despawnAction = () =>
        {
            if (activeBeat == spawned)
                activeBeat = null;

            beatPool.Despawn(spawned);
        };

        activeBeat = spawned;
        beatsSpawned++;
        nextSpawnTime = hitTime;
        cursorWasInside = false;
    }

    private void TryHandleBeatAttempt(float now)
    {
        if (activeBeat == null || cursor == null)
            return;

        Vector2 cursorPos = cursor.transform.position;
        bool inHitZone = activeBeat.IsCursorInHitZone(cursorPos);

        if (inHitZone && !cursorWasInside)
        {
            Dot consumedDot;
            if (TryConsumeCarriedDot(out consumedDot))
            {
                string judge;
                bool perfect;
                bool great;
                EvaluateBeatTiming(now, activeBeat.TargetTime, out judge, out perfect, out great);

                bool success = perfect || (successOnGreatOrPerfect && great);
                int points = 0;

                if (success)
                {
                    points = perfect ? beatPerfectPoints : beatGreatPoints;
                    gm?.AddScore(points);
                    ui?.ShowJudgment($"BEAT {judge}", points);
                    fx?.PlayDepositSfx(perfect);

                    if (perfect)
                    {
                        fx?.DepositPop(true);
                        fx?.TriggerPerfectPostFX();
                    }
                    else
                    {
                        fx?.DepositPop(false);
                    }
                }
                else
                {
                    ui?.ShowJudgment("BEAT MISS", 0);
                    fx?.PlayDepositSfx(false);
                    fx?.DepositPop(false);
                }

                consumedDot.DespawnSelf();
                ResolveActiveBeat(success, judge, points);
                cursorWasInside = inHitZone;
                return;
            }
        }

        cursorWasInside = inHitZone;
    }

    private bool TryConsumeCarriedDot(out Dot consumed)
    {
        consumed = null;
        if (cursor == null) return false;

        var carried = cursor.CarriedDots;
        if (carried == null || carried.Count == 0) return false;

        for (int i = carried.Count - 1; i >= 0; i--)
        {
            Dot d = carried[i];
            if (d == null || !d.gameObject.activeInHierarchy) continue;

            // Keep special dots reserved for special batch handling.
            if (d.IsSpecial) continue;

            cursor.RemoveCarriedDot(d);
            d.SetCarried(false);
            consumed = d;
            return true;
        }

        return false;
    }

    private void EvaluateBeatTiming(float now, float targetTime, out string judge, out bool perfect, out bool great)
    {
        float dt = Mathf.Abs(now - targetTime);
        float perfectWindow = beat != null ? Mathf.Max(0.001f, beat.perfectWindow) : 0.10f;
        float greatWindow = beat != null ? Mathf.Max(perfectWindow, beat.greatWindow) : 0.18f;

        perfect = dt <= perfectWindow;
        great = dt <= greatWindow;

        if (perfect) judge = "PERFECT";
        else if (great) judge = "GREAT";
        else judge = "MISS";
    }

    private void ResolveActiveBeat(bool hit, string judge, int points)
    {
        if (activeBeat != null)
            activeBeat.DespawnSelf();

        beatsResolved++;

        int maxBeats = Mathf.Max(1, beatsPerSequence);
        if (beatsResolved >= maxBeats)
        {
            EndSequence();
            return;
        }

    }

    private bool IsFeatureAvailable()
    {
        if (gm == null || beat == null || beatTargetPrefab == null)
            return false;

        return gm.enableBeats;
    }

    private Vector2 RandomPointInScreenBounds()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector2.zero;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        float x = Random.Range(-w + spawnPadding, w - spawnPadding);
        float y = Random.Range(-h + spawnPadding, h - spawnPadding);
        return new Vector2(x, y);
    }
}
