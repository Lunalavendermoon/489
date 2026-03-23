// EffectsManager.cs
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// Represents the 4 directional areas of the core collider.
/// </summary>
public enum CoreDirection
{
    Up,
    Down,
    Left,
    Right
}

public class EffectsManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("Used for normal deposit sounds.")]
    public AudioSource normalSource;

    [Tooltip("Used for PERFECT deposit sounds.")]
    public AudioSource perfectSource;

    [Tooltip("Used for negative/hit sounds (e.g. bullet hits). If null, normalSource is used.")]
    public AudioSource negativeSource;

    [Header("Deposit Clips")]
    [Tooltip("Played when a deposit is NOT perfect.")]
    public AudioClip depositClip;

    [Tooltip("Played when a deposit IS perfect.")]
    public AudioClip depositPerfectClip;

    [Header("Negative/Hurt Clips")]
    [Tooltip("Played when the player/cursor is hit by a bullet.")]
    public AudioClip hitClip;

    [Header("Cooldowns")]
    [Tooltip("Cooldown between normal deposit sounds.")]
    public float normalCooldown = 0.08f;

    [Tooltip("Cooldown between PERFECT deposit sounds (prevents perfect spam from overriding itself).")]
    public float perfectCooldown = 0.10f;

    [Tooltip("Cooldown between negative/hit sounds.")]
    public float hitCooldown = 0.10f;

    [Tooltip("If true, a PERFECT will stop any currently playing normal deposit sound for clarity.")]
    public bool perfectStopsNormal = true;

    private float lastNormalTime = -999f;
    private float lastPerfectTime = -999f;
    private float lastHitTime = -999f;

    [Header("Perfect Post Processing Flash")]
    public Volume perfectVolume;               // assign your Global Volume here
    public float perfectFlashIn = 0.04f;       // ramp up time
    public float perfectHold = 0.05f;          // hold at full weight
    public float perfectFlashOut = 0.10f;      // ramp down time
    public float perfectFlashCooldown = 0.08f; // prevents constant retrigger spam

    private Coroutine perfectFlashRoutine;
    private float lastPerfectFlashTime = -999f;

    [Header("Perfect Core Sprite Swap")]
    [Tooltip("SpriteRenderer on the Core GameObject. If null, you can drag Core here or it will try to find one.")]
    public SpriteRenderer coreSpriteRenderer;

    [Tooltip("Normal sprite for the core (restored after perfect flash). If left null, we cache the sprite at runtime.")]
    public Sprite coreNormalSprite;

    [Tooltip("Sprite shown during perfect flash - UP direction.")]
    public Sprite corePerfectSpriteUp;

    [Tooltip("Sprite shown during perfect flash - DOWN direction.")]
    public Sprite corePerfectSpriteDown;

    [Tooltip("Sprite shown during perfect flash - LEFT direction.")]
    public Sprite corePerfectSpriteLeft;

    [Tooltip("Sprite shown during perfect flash - RIGHT direction.")]
    public Sprite corePerfectSpriteRight;

    private bool cachedCoreNormal = false;
    private Sprite currentPerfectSprite = null;

    [Header("Camera Shake")]
    public CameraShake2D shake;
    public float smallShake = 0.3f;
    public float bigShake = 0.6f;

    private void Awake()
    {
        // If user didn't assign sources, try to find/create sensible defaults.
        if (normalSource == null)
            normalSource = GetComponent<AudioSource>();

        if (perfectSource == null)
        {
            // Create a second AudioSource for perfect deposits if not provided
            perfectSource = gameObject.AddComponent<AudioSource>();

            // Copy key settings from normal source if available (keeps volume/mixer consistent)
            if (normalSource != null)
            {
                perfectSource.outputAudioMixerGroup = normalSource.outputAudioMixerGroup;
                perfectSource.spatialBlend = normalSource.spatialBlend; // 0 = 2D, 1 = 3D
                perfectSource.volume = normalSource.volume;
                perfectSource.pitch = normalSource.pitch;
                perfectSource.loop = false;
                perfectSource.playOnAwake = false;
            }
        }

        // Setup negative/hit source if not assigned
        if (negativeSource == null)
        {
            negativeSource = gameObject.AddComponent<AudioSource>();
            if (normalSource != null)
            {
                negativeSource.outputAudioMixerGroup = normalSource.outputAudioMixerGroup;
                negativeSource.spatialBlend = normalSource.spatialBlend;
                negativeSource.volume = normalSource.volume;
                negativeSource.pitch = normalSource.pitch;
                negativeSource.loop = false;
                negativeSource.playOnAwake = false;
            }
        }

        // Auto-find camera shake if not assigned
        if (shake == null)
        {
            if (Camera.main != null)
                shake = Camera.main.GetComponent<CameraShake2D>();
            if (shake == null)
                shake = FindObjectOfType<CameraShake2D>();
        }

        if (shake == null)
            Debug.LogWarning("[EffectsManager] CameraShake2D not found. No shake will occur.");

        // Auto-find core sprite renderer if not assigned (optional convenience)
        if (coreSpriteRenderer == null)
        {
            // Try find by tag first (recommended): tag your core object "Core"
            GameObject coreGO = GameObject.FindWithTag("Core");
            if (coreGO != null) coreSpriteRenderer = coreGO.GetComponentInChildren<SpriteRenderer>();

            // Fallback: first SpriteRenderer in scene with name containing "core"
            if (coreSpriteRenderer == null)
            {
                var srs = FindObjectsOfType<SpriteRenderer>();
                for (int i = 0; i < srs.Length; i++)
                {
                    if (srs[i] != null && srs[i].name.ToLower().Contains("core"))
                    {
                        coreSpriteRenderer = srs[i];
                        break;
                    }
                }
            }
        }

        CacheCoreNormalSpriteIfNeeded();
    }

    private void CacheCoreNormalSpriteIfNeeded()
    {
        if (cachedCoreNormal) return;
        if (coreSpriteRenderer == null) return;

        if (coreNormalSprite == null)
            coreNormalSprite = coreSpriteRenderer.sprite;

        cachedCoreNormal = true;
    }

    /// <summary>
    /// Plays deposit sound with separate cooldowns for normal vs perfect.
    /// PERFECT can optionally cut any currently playing normal sound, but is also throttled so it doesn't override itself.
    /// </summary>
    public void PlayDepositSfx(bool perfect)
    {
        float now = Time.unscaledTime;

        if (perfect)
        {
            if (now - lastPerfectTime < perfectCooldown) return;
            if (depositPerfectClip == null || perfectSource == null) return;

            if (perfectStopsNormal && normalSource != null)
                normalSource.Stop();

            perfectSource.PlayOneShot(depositPerfectClip);
            lastPerfectTime = now;
        }
        else
        {
            if (now - lastNormalTime < normalCooldown) return;
            if (depositClip == null || normalSource == null) return;

            normalSource.PlayOneShot(depositClip);
            lastNormalTime = now;
        }
    }

    /// <summary>
    /// Plays a negative/hurt sound when the player is hit by a bullet.
    /// Throttled with hitCooldown to avoid spamming.
    /// </summary>
    public void PlayHitSfx()
    {
        float now = Time.unscaledTime;
        if (now - lastHitTime < hitCooldown) return;
        if (hitClip == null) return;

        AudioSource src = negativeSource != null ? negativeSource : normalSource;
        if (src == null) return;

        src.PlayOneShot(hitClip);
        lastHitTime = now;
    }

    public void PickupPop() { /* stub */ }
    public void TraceStartPop() { /* stub */ }
    public void CapturePop() { shake?.Shake(smallShake, 0.08f); }
    public void FailPop() { shake?.Shake(smallShake, 0.06f); }

    public void DepositPop(bool perfect)
    {
        Debug.Log($"[EffectsManager] DepositPop called. perfect={perfect} shakeNull={(shake == null)}");
        if (perfect) shake?.Shake(bigShake, 0.12f);
        else shake?.Shake(smallShake, 0.08f);
    }

    public void ShakeSmall() => shake?.Shake(smallShake, 0.08f);
    public void SuperStartBurst() => shake?.Shake(bigShake, 0.18f);
    public void PulseClickPop() { /* stub */ }

    /// <summary>
    /// Triggers perfect post FX + core sprite swap for the same duration as the flash.
    /// directionOrDefault defaults to Down if not provided (for backwards compatibility).
    /// </summary>
    public void TriggerPerfectPostFX(CoreDirection? direction = null)
    {
        if (perfectVolume == null && corePerfectSpriteUp == null && corePerfectSpriteDown == null 
            && corePerfectSpriteLeft == null && corePerfectSpriteRight == null) 
            return;

        // cooldown prevents strobing if many perfects happen in a row
        if (Time.unscaledTime - lastPerfectFlashTime < perfectFlashCooldown)
            return;

        lastPerfectFlashTime = Time.unscaledTime;

        // Determine which sprite to use based on direction
        currentPerfectSprite = GetPerfectSpriteForDirection(direction ?? CoreDirection.Down);

        if (perfectFlashRoutine != null)
            StopCoroutine(perfectFlashRoutine);

        perfectFlashRoutine = StartCoroutine(PerfectFlashRoutine());
    }

    private Sprite GetPerfectSpriteForDirection(CoreDirection direction)
    {
        switch (direction)
        {
            case CoreDirection.Up:
                return corePerfectSpriteUp;
            case CoreDirection.Down:
                return corePerfectSpriteDown;
            case CoreDirection.Left:
                return corePerfectSpriteLeft;
            case CoreDirection.Right:
                return corePerfectSpriteRight;
            default:
                return corePerfectSpriteDown;
        }
    }

    private IEnumerator PerfectFlashRoutine()
    {
        CacheCoreNormalSpriteIfNeeded();

        // --- Immediately switch core sprite to perfect (based on direction) ---
        if (coreSpriteRenderer != null && currentPerfectSprite != null)
            coreSpriteRenderer.sprite = currentPerfectSprite;

        // If no volume assigned, still keep sprite swap for the same computed duration
        if (perfectVolume != null)
            perfectVolume.weight = 0f;

        // Ramp in
        float t = 0f;
        float inDur = Mathf.Max(0.001f, perfectFlashIn);
        while (t < inDur)
        {
            t += Time.unscaledDeltaTime;
            if (perfectVolume != null)
                perfectVolume.weight = Mathf.Clamp01(t / inDur);
            yield return null;
        }
        if (perfectVolume != null) perfectVolume.weight = 1f;

        // Hold
        if (perfectHold > 0f)
            yield return new WaitForSecondsRealtime(perfectHold);

        // Ramp out
        t = 0f;
        float outDur = Mathf.Max(0.001f, perfectFlashOut);
        while (t < outDur)
        {
            t += Time.unscaledDeltaTime;
            if (perfectVolume != null)
                perfectVolume.weight = 1f - Mathf.Clamp01(t / outDur);
            yield return null;
        }
        if (perfectVolume != null) perfectVolume.weight = 0f;

        // --- Restore core sprite ---
        if (coreSpriteRenderer != null && coreNormalSprite != null)
            coreSpriteRenderer.sprite = coreNormalSprite;

        currentPerfectSprite = null;
        perfectFlashRoutine = null;
    }
}
