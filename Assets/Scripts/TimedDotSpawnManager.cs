using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TimedDotSpawnPhase
{
    public string phaseName = "Phase";
    [Min(0f)] public float startTimeSeconds = 0f;
    public bool hasEndTime = false;
    [Min(0f)] public float endTimeSeconds = 20f;
    public bool allowNormalDots = true;
    public bool allowSpecialDots = true;
    public bool allowHealthDots = true;
    public bool overrideNormalSpawnRate = false;
    [Min(0f)] public float normalSpawnPerSecond = 1.6f;
    public bool overrideMaxHealthDots = false;
    [Min(0)] public int maxHealthDots = 5;

    public bool Contains(float elapsedSeconds)
    {
        if (elapsedSeconds < startTimeSeconds)
            return false;

        if (hasEndTime && elapsedSeconds >= endTimeSeconds)
            return false;

        return true;
    }
}

public class TimedDotSpawnManager : MonoBehaviour
{
    [Header("Refs")]
    public SpawnManager spawns;
    public GameManager gm;

    [Header("Timeline")]
    public bool enableTimedPhases = true;
    [Tooltip("Checks for released disallowed dots every frame so carried dots are removed as soon as they are dropped.")]
    public bool clearDisallowedDotsContinuously = true;
    public List<TimedDotSpawnPhase> phases = new List<TimedDotSpawnPhase>();

    [Header("Debug")]
    [SerializeField] private int currentPhaseIndex = -1;
    [SerializeField] private string currentPhaseName = "None";
    [SerializeField] private float timelineStartTime = 0f;

    public float ElapsedTimeSeconds => Mathf.Max(0f, Time.timeSinceLevelLoad - timelineStartTime);
    public int CurrentPhaseIndex => currentPhaseIndex;
    public string CurrentPhaseName => currentPhaseName;

    private void Awake()
    {
        if (spawns == null) spawns = GetComponent<SpawnManager>();
    }

    private void OnEnable()
    {
        ResetTimeline();
    }

    private void Update()
    {
        if (spawns == null)
            return;

        if (!enableTimedPhases)
        {
            if (currentPhaseIndex != -1)
            {
                currentPhaseIndex = -1;
                currentPhaseName = "Disabled";
                spawns.ResetTimedSpawnRules();
            }

            return;
        }

        if (gm != null && (gm.state == GameManager.GameState.GameOver || gm.state == GameManager.GameState.EndScreen))
            return;

        int nextPhaseIndex = GetActivePhaseIndex(ElapsedTimeSeconds);
        if (nextPhaseIndex != currentPhaseIndex)
            ApplyPhase(nextPhaseIndex);

        if (clearDisallowedDotsContinuously && currentPhaseIndex >= 0)
            ApplyCleanup(phases[currentPhaseIndex]);
    }

    public void ResetTimeline()
    {
        timelineStartTime = Time.timeSinceLevelLoad;
        currentPhaseIndex = -1;
        currentPhaseName = "None";

        if (spawns != null)
            spawns.ResetTimedSpawnRules();

        if (enableTimedPhases)
            ApplyPhase(GetActivePhaseIndex(ElapsedTimeSeconds));
    }

    private int GetActivePhaseIndex(float elapsedSeconds)
    {
        if (phases == null || phases.Count == 0)
            return -1;

        for (int i = 0; i < phases.Count; i++)
        {
            TimedDotSpawnPhase phase = phases[i];
            if (phase != null && phase.Contains(elapsedSeconds))
                return i;
        }

        return -1;
    }

    private void ApplyPhase(int phaseIndex)
    {
        currentPhaseIndex = phaseIndex;

        if (phaseIndex < 0 || phaseIndex >= phases.Count)
        {
            currentPhaseName = "Default";
            spawns.ResetTimedSpawnRules();
            return;
        }

        TimedDotSpawnPhase phase = phases[phaseIndex];
        currentPhaseName = string.IsNullOrWhiteSpace(phase.phaseName) ? $"Phase {phaseIndex + 1}" : phase.phaseName;

        spawns.SetTimedSpawnRules(phase.allowNormalDots, phase.allowSpecialDots, phase.allowHealthDots);
        spawns.SetTimedOverrides(
            phase.overrideNormalSpawnRate,
            phase.normalSpawnPerSecond,
            phase.overrideMaxHealthDots,
            phase.maxHealthDots);

        ApplyCleanup(phase);
    }

    private void ApplyCleanup(TimedDotSpawnPhase phase)
    {
        if (phase == null || spawns == null)
            return;

        spawns.ClearDisallowedFreeDots(phase.allowNormalDots, phase.allowSpecialDots, phase.allowHealthDots);
    }

    private void OnValidate()
    {
        if (phases == null)
            return;

        for (int i = 0; i < phases.Count; i++)
        {
            TimedDotSpawnPhase phase = phases[i];
            if (phase == null)
                continue;

            phase.startTimeSeconds = Mathf.Max(0f, phase.startTimeSeconds);
            phase.endTimeSeconds = Mathf.Max(phase.startTimeSeconds, phase.endTimeSeconds);
            phase.normalSpawnPerSecond = Mathf.Max(0f, phase.normalSpawnPerSecond);
            phase.maxHealthDots = Mathf.Max(0, phase.maxHealthDots);
        }
    }
}