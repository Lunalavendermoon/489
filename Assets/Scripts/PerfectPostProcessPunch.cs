using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerfectPostProcessPunch : MonoBehaviour
{
    [Header("References")]
    public Volume globalVolume;

    [Header("Punch Settings")]
    public float duration = 0.18f;
    public float cooldown = 0.08f;

    [Tooltip("How strong the peak effect is.")]
    public float intensity = 1.0f;

    [Header("Bloom")]
    public float bloomAddAtPeak = 2.0f;

    [Header("Chromatic Aberration")]
    public float chromaAtPeak = 0.35f;

    [Header("Vignette")]
    public float vignetteAddAtPeak = 0.25f;

    [Header("Lens Distortion (optional)")]
    public float lensDistortionAtPeak = -0.22f;

    [Header("Exposure Pop (optional)")]
    public float exposureAddAtPeak = 0.35f;

    private Bloom bloom;
    private ChromaticAberration chroma;
    private Vignette vignette;
    private LensDistortion lens;
    private ColorAdjustments color;

    private float baseBloom;
    private float baseChroma;
    private float baseVignette;
    private float baseLens;
    private float baseExposure;

    private Coroutine routine;
    private float lastTime = -999f;

    private void Awake()
    {
        CacheOverrides();
    }

    private void OnValidate()
    {
        // Keeps it resilient if you assign the volume later
        if (Application.isPlaying == false)
            CacheOverrides();
    }

    private void CacheOverrides()
    {
        if (globalVolume == null || globalVolume.profile == null) return;

        globalVolume.profile.TryGet(out bloom);
        globalVolume.profile.TryGet(out chroma);
        globalVolume.profile.TryGet(out vignette);
        globalVolume.profile.TryGet(out lens);
        globalVolume.profile.TryGet(out color);

        // Cache baselines so we return cleanly after the punch
        if (bloom != null) baseBloom = bloom.intensity.value;
        if (chroma != null) baseChroma = chroma.intensity.value;
        if (vignette != null) baseVignette = vignette.intensity.value;
        if (lens != null) baseLens = lens.intensity.value;
        if (color != null) baseExposure = color.postExposure.value;
    }

    public void TriggerPerfectPunch()
    {
        if (Time.unscaledTime - lastTime < cooldown) return;
        lastTime = Time.unscaledTime;

        if (globalVolume == null || globalVolume.profile == null) return;

        // If overrides weren't found at Awake (profile added later), try again
        if (bloom == null && chroma == null && vignette == null && lens == null && color == null)
            CacheOverrides();

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        float t = 0f;
        float dur = Mathf.Max(0.05f, duration);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / dur);

            // Nice quick “hit” curve: fast up, fast down
            float upDown = Mathf.Sin(x * Mathf.PI);     // 0 -> 1 -> 0
            float k = upDown * intensity;

            if (bloom != null) bloom.intensity.value = baseBloom + bloomAddAtPeak * k;
            if (chroma != null) chroma.intensity.value = baseChroma + chromaAtPeak * k;
            if (vignette != null) vignette.intensity.value = baseVignette + vignetteAddAtPeak * k;
            if (lens != null) lens.intensity.value = baseLens + lensDistortionAtPeak * k;
            if (color != null) color.postExposure.value = baseExposure + exposureAddAtPeak * k;

            yield return null;
        }

        // Restore baselines
        if (bloom != null) bloom.intensity.value = baseBloom;
        if (chroma != null) chroma.intensity.value = baseChroma;
        if (vignette != null) vignette.intensity.value = baseVignette;
        if (lens != null) lens.intensity.value = baseLens;
        if (color != null) color.postExposure.value = baseExposure;

        routine = null;
    }
}
