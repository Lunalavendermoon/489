// GameManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Normal, CountdownToSuper, Super, GameOver, EndScreen }

    [Header("Mode Toggles")]
    [Tooltip("Practice mode: only dots + curves. Disables bullets and Super Mode.")]
    public bool dotsAndCurvesOnlyMode = false;

    [Tooltip("Normal mode toggle: allow Super Mode to trigger.")]
    public bool enableSuperMode = true;

    [Tooltip("Toggle to enable/disable enemy bullets.")]
    public bool enableBullets = true;

    [Tooltip("Toggle to enable/disable special (batch) notes.")]
    public bool enableSpecialDots = true;

    [Header("Scoring")]
    public int score;
    public int combo;
    public int comboBreakPenalty = 1;
    [Tooltip("Maximum combo levels that contribute to score bonus.")]
    public int comboMax = 10;

    [Tooltip("Multiplier added per combo level (e.g. 0.05 = +5% per level). Actual multiplier = 1 + comboLevel * comboStep.")]
    public float comboStepMultiplier = 0.05f;

    [Tooltip("How many points to lose when you get hit by a bullet.")]
    public int scorePenaltyOnHit = 120;

    [Header("Difficulty Ramp")]
    public float difficultyTimeScale = 0.015f; // bullet speed slowly increases over time

    [Header("Super Mode")]
    public float superCountdownSeconds = 3f;
    public float superDurationSeconds = 10f;
    public int superClickPoints = 40;
    public bool superPausesSpawns = true;
    public AudioSource supermodeAudioSource;
    public float superClickCooldown = 0.15f;
    [Tooltip("Sprite shown when Super click hits from UP direction.")]
    public Sprite supermodeSpriteUp;
    [Tooltip("Sprite shown when Super click hits from DOWN direction.")]
    public Sprite supermodeSpriteDown;
    [Tooltip("Sprite shown when Super click hits from LEFT direction.")]
    public Sprite supermodeSpriteLeft;
    [Tooltip("Sprite shown when Super click hits from RIGHT direction.")]
    public Sprite supermodeSpriteRight;

    [Header("State")]
    public GameState state = GameState.Normal;

    [Header("Refs")]
    public BeatManager beat;
    public UIManager ui;
    public SpawnManager spawns;
    public BulletManager bullets;
    public CoreController core;
    public EffectsManager fx;
    public Tutorial tutorial;

    private float superCountdownTimer;
    private float superTimer;
    private bool supermodeFromSongEnd = false;
    private float superClickCooldownTimer = 0f;
    private bool supermodeCollisionActive = false;
    private bool pendingSuperCountdown = false;
    private float pendingSuperCountdownDelay = 0.3f;

    public float Difficulty01 => Mathf.Clamp01(Time.timeSinceLevelLoad * difficultyTimeScale);

    private void Start()
    {
        RestartGame();
    }

    private void Update()
    {
        if (state == GameState.GameOver || state == GameState.EndScreen) return;

        // Update super click cooldown
        if (superClickCooldownTimer > 0f)
        {
            superClickCooldownTimer -= Time.deltaTime;
        }

        // Handle pending supermode countdown (delayed to ensure audio is done)
        // Use unscaledDeltaTime so this works even when tutorial pauses the game
        if (pendingSuperCountdown)
        {
            pendingSuperCountdownDelay -= Time.unscaledDeltaTime;
            if (pendingSuperCountdownDelay <= 0f)
            {
                pendingSuperCountdown = false;
                RequestSuperCountdown();
            }
        }

        // Super state machine (disabled in practice mode)
        if (!dotsAndCurvesOnlyMode)
        {
            if (state == GameState.CountdownToSuper)
            {
                // Don't advance countdown if tutorial is paused
                if (tutorial == null || !tutorial.isPausedForTutorial)
                {
                    superCountdownTimer -= Time.deltaTime;
                    ui?.SetSuperCountdown(superCountdownTimer);

                    if (superCountdownTimer <= 0f)
                        EnterSuperMode();
                }
            }
            else if (state == GameState.Super)
            {
                superTimer -= Time.deltaTime;
                ui?.SetSuperTimer(superTimer / Mathf.Max(0.01f, superDurationSeconds));

                if (superTimer <= 0f)
                    ExitSuperMode();
            }
        }

        ui?.SetScore(score);
        ui?.SetCombo(combo, GetComboMultiplier());
    }

    public void AddScore(int amount)
    {
        score += Mathf.Max(0, amount);
        ui?.SetScore(score);
    }

    public void SubtractScore(int amount)
    {
        score = Mathf.Max(0, score - Mathf.Max(0, amount));
        ui?.SetScore(score);
    }

    // Increment combo for a perfect deposit.  Number increases indefinitely,
    // multiplier calculation will cap at comboMax inside GetComboMultiplier.
    public void OnPerfectDeposit(bool isSpecial = false)
    {
        combo = combo + 1;
        ui?.SetCombo(combo, GetComboMultiplier());
        tutorial?.OnPerfectDeposit(isSpecial);
    }

    // Reset combo to zero when a non-perfect deposit occurs.
    public void ResetCombo()
    {
        combo = 0;
        ui?.SetCombo(combo, GetComboMultiplier());
    }

    // Deprecated compatibility method: adds raw amount (use with care)
    public void AddCombo(int amount)
    {
        combo = Mathf.Max(0, combo + amount);
        ui?.SetCombo(combo, GetComboMultiplier());
    }

    // Returns the score multiplier based on current combo level.
    public float GetComboMultiplier()
    {
        int level = Mathf.Clamp(combo, 0, comboMax);
        return 1f + level * comboStepMultiplier;
    }

    // Call this when a bullet hits the player
    public void OnPlayerHit()
    {
        if (dotsAndCurvesOnlyMode) return;
        if (state == GameState.GameOver) return;

        SubtractScore(scorePenaltyOnHit);
        fx?.PlayHitSfx();
        fx?.ShakeSmall();
        ui?.FlashDamage();
        tutorial?.OnPlayerHit();
    }

    public void RequestSuperCountdown()
    {
        if (dotsAndCurvesOnlyMode) return;
        if (!enableSuperMode) return;
        if (state != GameState.Normal) return;

        state = GameState.CountdownToSuper;
        superCountdownTimer = superCountdownSeconds;

        if (superPausesSpawns)
        {
            spawns?.SetPaused(true);
            bullets?.SetPaused(true);
            bullets?.DespawnAllBullets();
        }

        // Start supermode audio when countdown begins
        if (supermodeAudioSource != null)
        {
            supermodeAudioSource.Play();
        }

        ui?.ShowSuperCountdown(true);
        ui?.SetSuperCountdown(superCountdownTimer);

        // let the tutorial system know that a super countdown has started
        tutorial?.OnSuperCountdownStart();
    }

    private void EnterSuperMode()
    {
        if (dotsAndCurvesOnlyMode) return;

        state = GameState.Super;
        superTimer = superDurationSeconds;

        ui?.ShowSuperCountdown(false);
        ui?.ShowSuperUI(true);
        ui?.SetSuperTimer(1f);

        fx?.SuperStartBurst();

        // notify tutorial that super mode actually began
        tutorial?.OnSuperModeStart();
    }

    private void ExitSuperMode() 
    {
        if (dotsAndCurvesOnlyMode) return;

        // Reset collision state
        supermodeCollisionActive = false;

        if (supermodeFromSongEnd)
        {
            // Stop supermode audio
            if (supermodeAudioSource != null && supermodeAudioSource.isPlaying)
            {
                supermodeAudioSource.Stop();
            }

            // Song ended, show end screen instead of returning to normal
            supermodeFromSongEnd = false;
            state = GameState.EndScreen;
            ui?.ShowSuperUI(false);
            ui?.ShowEndScreen(true, score, UIManager.GetGrade(score));
        }
        else
        {
            // Stop supermode audio only if regular supermode (not song end)
            if (supermodeAudioSource != null && supermodeAudioSource.isPlaying)
            {
                supermodeAudioSource.Stop();
            }

            state = GameState.Normal;
            // inform the tutorial that super mode ended so it can clear text
            tutorial?.OnSuperModeEnd();
            ui?.ShowSuperUI(false);
            ui?.ClearJudgment();
            core?.ResetFill();

            if (superPausesSpawns)
            {
                spawns?.SetPaused(false);
                bullets?.SetPaused(false);
            }
        }
    }

    private void GameOver()
    {
        state = GameState.GameOver;
        spawns?.SetPaused(true);
        bullets?.SetPaused(true);
        bullets?.DespawnAllBullets();

        ui?.ShowGameOver(true, score);
    }

    public void RestartGame()
    {
        state = GameState.Normal;
        score = 0;
        combo = 0;

        spawns?.ResetAll();
        bullets?.DespawnAllBullets();
        core?.ResetFill();

        ui?.ShowGameOver(false, 0);
        ui?.ShowSuperUI(false);
        ui?.ShowSuperCountdown(false);
        ui?.ClearJudgment();

        ApplyModeRules();

        spawns?.SetPaused(false);
        bullets?.SetPaused(false);
    }

    private void ApplyModeRules()
    {
        if (spawns != null)
        {
            // IMPORTANT: dots should be allowed in BOTH modes.
            spawns.spawnDots = true;
            // Apply special dots toggle
            spawns.spawnSpecialDots = enableSpecialDots;
        }

        if (dotsAndCurvesOnlyMode)
        {
            enableSuperMode = false;

            if (bullets != null)
            {
                bullets.spawnBullets = false;
                bullets.DespawnAllBullets();
            }

            ui?.ShowSuperUI(false);
            ui?.ShowSuperCountdown(false);
        }
        else
        {
            // Apply bullets toggle
            if (bullets != null) bullets.spawnBullets = enableBullets;
        }
    }

    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Super mode core click
    public void TrySuperClickCore(Vector2 cursorWorld, float coreRadius, Vector2 corePos)
    {
        if (state != GameState.Super) return;

        bool cursorInCore = Vector2.Distance(cursorWorld, corePos) <= coreRadius;

        // Trigger hit only on collision enter (transition from outside to inside)
        if (cursorInCore && !supermodeCollisionActive)
        {
            AddScore(superClickPoints);
            ui?.ShowJudgment("PERFECT", superClickPoints);
            fx?.PulseClickPop();
            fx?.PlayDepositSfx(true);
            CoreDirection direction = GetCoreDirectionForCollision(cursorWorld, corePos);
            fx?.TriggerPerfectPostFX(direction);
            fx?.shake?.Shake(0.4f, 0.12f);
            
            // Set sprite based on collision direction
            Sprite directionSprite = GetSupermodeSprite(direction);
            if (directionSprite != null && fx?.coreSpriteRenderer != null)
            {
                fx.coreSpriteRenderer.sprite = directionSprite;
            }
            
            supermodeCollisionActive = true;
        }
        else if (!cursorInCore)
        {
            // Cursor left the core, ready for next collision
            supermodeCollisionActive = false;
        }
    }

    /// <summary>
    /// Determines which directional area of the core was hit based on a collision point.
    /// Divides the core into 4 quadrants: Up, Down, Left, Right.
    /// </summary>
    private CoreDirection GetCoreDirectionForCollision(Vector2 collisionPoint, Vector2 corePos)
    {
        Vector2 delta = collisionPoint - corePos;

        // Use absolute values to determine which axis is more dominant
        float absDeltaX = Mathf.Abs(delta.x);
        float absDeltaY = Mathf.Abs(delta.y);

        if (absDeltaY > absDeltaX)
        {
            // More vertical - check if up or down
            return delta.y > 0 ? CoreDirection.Up : CoreDirection.Down;
        }
        else
        {
            // More horizontal - check if left or right
            return delta.x > 0 ? CoreDirection.Right : CoreDirection.Left;
        }
    }

    private Sprite GetSupermodeSprite(CoreDirection direction)
    {
        switch (direction)
        {
            case CoreDirection.Up:
                return supermodeSpriteUp;
            case CoreDirection.Down:
                return supermodeSpriteDown;
            case CoreDirection.Left:
                return supermodeSpriteLeft;
            case CoreDirection.Right:
                return supermodeSpriteRight;
            default:
                return supermodeSpriteDown;
        }
    }

    public void EndSongRun()
    {
        if (state == GameState.EndScreen || state == GameState.GameOver) return;

        // Mark that we're starting supermode from song end
        supermodeFromSongEnd = true;

        // Schedule supermode countdown to begin after a delay (ensures audio fully stops)
        pendingSuperCountdown = true;
        pendingSuperCountdownDelay = 0.3f;
    }

    public void ResetPauseAccumulatedTime()
    {
        // Reset timers that may have accumulated while paused to prevent unwanted state transitions
        superClickCooldownTimer = 0f;
        pendingSuperCountdown = false;
        pendingSuperCountdownDelay = 0f;
        superCountdownTimer = superCountdownSeconds;
    }
}
 