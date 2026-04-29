using System;
using System.Collections.Generic;
using UnityEngine;

public class BeatManager : MonoBehaviour
{
    [System.Serializable]
    
    public class PulseSpriteTarget
    {
        public Transform target;

        [Tooltip("Scale multiplier at the start of the beat.")]
        public float minScaleMultiplier = 1f;

        [Tooltip("Scale multiplier near the beat peak.")]
        public float maxScaleMultiplier = 1.12f;

        [NonSerialized] public Vector3 baseScale;
        [NonSerialized] public bool initialized;
    }

    [Header("Timing")]
    [Range(40f, 240f)] public float bpm = 120f;

    [Tooltip("Manual song length in seconds, used when not relying on AudioSource clip length.")]
    [Min(0.01f)] public float manualSongLengthSeconds = 120f;

    [Header("Windows (seconds)")]
    public float perfectWindow = 0.10f;
    public float greatWindow = 0.18f;
    public float okWindow = 0.28f;

    [Header("Pulse Visual")]
    public LineRenderer pulseLine;
    public int pulseSegments = 64;
    public float pulseRMin = 0.5f;
    public float pulseRMax = 4.2f;
    public float peakFlashScale = 1.12f;

    [Header("Sprite Pulse")]
    public List<PulseSpriteTarget> pulseSprites = new List<PulseSpriteTarget>();

    [Header("Core Reference")]
    public Transform coreTransform;

    [Header("Song End")]
    public GameManager gm;
    public event Action OnPeak;

    public float BeatDuration => 60f / Mathf.Max(1f, bpm);

    private float lastBeatIndex = -1f;
    private float songTimer = 0f;
    private bool songStarted = false;
    private bool songEndedTriggered = false;

    public float CurrentTime
    {
        get { return songTimer; }
    }

    public float BeatPhase01
    {
        get
        {
            float t = CurrentTime;
            float d = BeatDuration;
            float phase = t % d;
            return Mathf.Clamp01(phase / d);
        }
    }

    public float DistanceToPeakSeconds
    {
        get
        {
            float t = CurrentTime;
            float d = BeatDuration;
            float phase = t % d;

            float dist0 = Mathf.Abs(phase - 0f);
            float distD = Mathf.Abs(phase - d);
            return Mathf.Min(dist0, distD);
        }
    }

    public float Accuracy01(float maxWindow)
    {
        float dist = DistanceToPeakSeconds;
        return Mathf.Clamp01(1f - (dist / Mathf.Max(0.0001f, maxWindow)));
    }

    public string Judgment(out float acc01, float maxWindow)
    {
        acc01 = Accuracy01(maxWindow);

        float dist = DistanceToPeakSeconds;
        if (dist <= perfectWindow) return "PERFECT";
        if (dist <= greatWindow) return "GREAT";
        if (dist <= okWindow) return "OK";
        return dist < (BeatDuration * 0.5f) ? "EARLY/LATE" : "OFF";
    }

    private void Awake()
    {
        if (pulseLine != null)
        {
            pulseLine.positionCount = pulseSegments + 1;
            pulseLine.useWorldSpace = true;
        }

        CachePulseSpriteBaseScales();
    }

    private void OnEnable()
    {
        CachePulseSpriteBaseScales();
    }

    private void Update()
    {
        UpdateSongClock();
        CheckSongEnd();
        AnimatePulse();
        AnimatePulseSprites();
        HandlePeakEvent();
    }

    private void UpdateSongClock()
    {
        songTimer += Time.deltaTime;
        songStarted = true;
    }

    private void CheckSongEnd()
    {
        if (songEndedTriggered) return;

        if (songStarted && songTimer >= manualSongLengthSeconds)
        {
            songEndedTriggered = true;
            gm?.EndSongRun();
        }
    }

    private void HandlePeakEvent()
    {
        float t = CurrentTime;
        float d = BeatDuration;
        float beatIndex = Mathf.Floor(t / d);

        if (BeatPhase01 >= 0.92f && beatIndex != lastBeatIndex)
        {
            lastBeatIndex = beatIndex;
            OnPeak?.Invoke();
        }
    }

    private void AnimatePulse()
    {
        if (pulseLine == null) return;

        Vector3 center = Vector3.zero;
        if (coreTransform != null) center = coreTransform.position;

        float phase = BeatPhase01;
        float r = Mathf.Lerp(pulseRMin, pulseRMax, phase);

        float flash = (phase >= 0.97f) ? peakFlashScale : 1f;
        r *= flash;

        for (int i = 0; i <= pulseSegments; i++)
        {
            float a = (i / (float)pulseSegments) * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            pulseLine.SetPosition(i, center + p);
        }
    }

    private void AnimatePulseSprites()
    {
        if (pulseSprites == null || pulseSprites.Count == 0)
            return;

        float phase = BeatPhase01;
        float pulse01 = Mathf.Sin(phase * Mathf.PI);

        for (int i = 0; i < pulseSprites.Count; i++)
        {
            PulseSpriteTarget entry = pulseSprites[i];
            if (entry == null || entry.target == null)
                continue;

            if (!entry.initialized)
            {
                entry.baseScale = entry.target.localScale;
                entry.initialized = true;
            }

            float scaleMul = Mathf.Lerp(entry.minScaleMultiplier, entry.maxScaleMultiplier, pulse01);
            entry.target.localScale = entry.baseScale * scaleMul;
        }
    }

    private void CachePulseSpriteBaseScales()
    {
        if (pulseSprites == null) return;

        for (int i = 0; i < pulseSprites.Count; i++)
        {
            PulseSpriteTarget entry = pulseSprites[i];
            if (entry == null || entry.target == null)
                continue;

            entry.baseScale = entry.target.localScale;
            entry.initialized = true;
        }
    }

    public void ResetSongClock()
    {
        songTimer = 0f;
        songStarted = false;
        songEndedTriggered = false;
        lastBeatIndex = -1f;

        CachePulseSpriteBaseScales();
    }
}