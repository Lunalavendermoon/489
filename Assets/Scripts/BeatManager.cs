// BeatManager.cs
using System;
using Unity.VisualScripting;
using UnityEngine;

public class BeatManager : MonoBehaviour
{
    [Header("Timing")]
    [Range(40f, 240f)] public float bpm = 120f;
    public bool useAudioSourceTime = false;
    public AudioSource audioSource;

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

    [Header("Core Reference")]
    public Transform coreTransform;
    public float BeatDuration => 60f / Mathf.Max(1f, bpm);
    [Header("Song End")]
    public bool endOnSongFinish = true;
    public GameManager gm;
    public event Action OnPeak;   // fired once per beat near peak

    private float lastBeatIndex = -1f;
    private bool hasObservedPlaybackStart = false;
    
    public float CurrentTime
    {
        get
        {
            if (useAudioSourceTime && audioSource != null) return audioSource.time;
            return Time.timeSinceLevelLoad;
        }
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
            // Peak at end of interval (phase ~ d). Use nearest to 0/d
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
    }

    private void Update()
    {
        if (endOnSongFinish && audioSource != null && audioSource.clip != null)
        {
            if (audioSource.isPlaying)
            {
                hasObservedPlaybackStart = true;
            }
            else if (hasObservedPlaybackStart && HasAudioReachedClipEnd())
            {
                gm?.EndSongRun();
                endOnSongFinish = false; // Prevent triggering multiple times
                return;
            }
        }
        AnimatePulse();

        // Peak event: once per beat index when phase is near 1 (end)
        float t = CurrentTime;
        float d = BeatDuration;
        float beatIndex = Mathf.Floor(t / d);

        // Fire when entering last ~10% of beat and not fired this beat
        if (BeatPhase01 >= 0.92f && beatIndex != lastBeatIndex)
        {
            lastBeatIndex = beatIndex;
            OnPeak?.Invoke();
        }
    }

    private bool HasAudioReachedClipEnd()
    {
        if (audioSource == null || audioSource.clip == null)
            return false;

        if (audioSource.timeSamples > 0)
            return audioSource.timeSamples >= audioSource.clip.samples - 1;

        return audioSource.time >= audioSource.clip.length - 0.05f;
    }

   private void AnimatePulse()
    {
        if (pulseLine == null) return;

        Vector3 center = Vector3.zero;
        if (coreTransform != null) center = coreTransform.position;

        float phase = BeatPhase01;
        float r = Mathf.Lerp(pulseRMin, pulseRMax, phase);

        // tiny flash near peak
        float flash = (phase >= 0.97f) ? peakFlashScale : 1f;
        r *= flash;

        for (int i = 0; i <= pulseSegments; i++)
        {
            float a = (i / (float)pulseSegments) * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            pulseLine.SetPosition(i, center + p);
        }
    }
}
