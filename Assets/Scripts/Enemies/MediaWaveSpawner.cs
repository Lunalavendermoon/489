using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MediaWaveSpawner : MonoBehaviour
{
    [System.Serializable]
    public class MediaWave
    {
        [Header("Timing")] 
        [Min(0f)] public float spawnAtTime = 5f;

        [Header("Trail Layout")]
        [Min(1)] public int trailCount = 3;
        [Min(1)] public int dotsPerTrail = 6;

        [Tooltip("Distance between dots within the same trail. Smaller = denser.")]
        [Min(0.05f)] public float radialSpacing = 0.5f;

        [Tooltip("Initial radius from the core for the first dot in each trail.")]
        [Min(0.1f)] public float startRadius = 7f;

        [Tooltip("Angular offset applied to each next trail.")]
        public float angleOffsetDeg = 0f;

        [Header("Spawn Rate")]
        [Tooltip("Delay between spawning each dot in a trail.")]
        [Min(0f)] public float spawnInterval = 0.08f;

        [Header("Movement")]
        [Min(0.01f)] public float inwardSpeed = 2.2f;
        public float angularSpeedDeg = 90f;

        [Tooltip("If true, alternate spiral direction every other trail.")]
        public bool alternateTrailDirection = true;

        [Header("Penalty")]
        [Min(0)] public int scorePenaltyOnCoreEnter = 300;
    }

    [Header("Refs")]
    public MediaDot mediaPrefab;
    public CoreController core;
    public PlayerCursorController cursor;
    public GameManager gm;

    [Header("Schedule")]
    public List<MediaWave> waves = new List<MediaWave>();

    [Header("Runtime")]
    [SerializeField] private float elapsedTime;
    [SerializeField] private int nextWaveIndex;
    [SerializeField] private List<MediaDot> activeMedia = new List<MediaDot>();

    private Coroutine spawnRoutine;

    private void Awake()
    {
        if (core == null) core = FindObjectOfType<CoreController>();
        if (cursor == null && core != null) cursor = core.cursor;
        if (gm == null && core != null) gm = core.gm;

        SortWaves();
    }

    private void Update()
    {
        if (gm == null || gm.state != GameManager.GameState.Normal)
            return;

        elapsedTime += Time.deltaTime;

        while (nextWaveIndex < waves.Count && elapsedTime >= waves[nextWaveIndex].spawnAtTime)
        {
            MediaWave wave = waves[nextWaveIndex];
            nextWaveIndex++;

            if (spawnRoutine != null)
                StopCoroutine(spawnRoutine);

            spawnRoutine = StartCoroutine(SpawnWaveRoutine(wave));
        }
    }

    private IEnumerator SpawnWaveRoutine(MediaWave wave)
    {
        int trailCount = Mathf.Max(1, wave.trailCount);
        int dotsPerTrail = Mathf.Max(1, wave.dotsPerTrail);

        float baseAngleStep = 360f / trailCount;

        for (int dotIndex = 0; dotIndex < dotsPerTrail; dotIndex++)
        {
            for (int trailIndex = 0; trailIndex < trailCount; trailIndex++)
            {
                float baseAngle = trailIndex * baseAngleStep + wave.angleOffsetDeg;
                float spawnRadius = wave.startRadius + (dotIndex * wave.radialSpacing);

                float angularSpeed = wave.angularSpeedDeg;
                if (wave.alternateTrailDirection && (trailIndex % 2 == 1))
                    angularSpeed *= -1f;

                SpawnMedia(
                    spawnRadius,
                    baseAngle,
                    wave.inwardSpeed,
                    angularSpeed,
                    wave.scorePenaltyOnCoreEnter
                );
            }

            if (wave.spawnInterval > 0f)
                yield return new WaitForSeconds(wave.spawnInterval);
        }

        spawnRoutine = null;
    }

    private void SpawnMedia(
        float startRadius,
        float startAngleDeg,
        float inwardSpeed,
        float angularSpeedDeg,
        int penalty)
    {
        if (mediaPrefab == null || core == null || gm == null)
            return;

        MediaDot media = Instantiate(mediaPrefab, transform);
        media.Initialize(
            core,
            cursor,
            gm,
            startRadius,
            startAngleDeg,
            inwardSpeed,
            angularSpeedDeg,
            penalty
        );

        activeMedia.Add(media);
    }

    private void SortWaves()
    {
        waves.Sort((a, b) => a.spawnAtTime.CompareTo(b.spawnAtTime));
    }

    public void ResetSpawner()
    {
        elapsedTime = 0f;
        nextWaveIndex = 0;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        for (int i = activeMedia.Count - 1; i >= 0; i--)
        {
            if (activeMedia[i] != null)
                Destroy(activeMedia[i].gameObject);
        }

        activeMedia.Clear();
        SortWaves();
    }
}