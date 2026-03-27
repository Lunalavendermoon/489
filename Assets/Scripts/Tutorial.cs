using UnityEngine;
using TMPro;

public class Tutorial : MonoBehaviour
{
    public enum TutorialState
    {
        CollectDots,
        WaitAfterCollect,
        DepositDots,
        WaitAfterDeposit,
        SpecialDots,
        IntroduceBullets,
        ExplainCombo,
        ExplainSuperMode,
        SuperModeHappening,
        Complete
    }

    [Header("Refs")]
    public GameManager gm;
    public PlayerCursorController cursor;
    public UIManager ui;
    public TMP_Text tutorialText;
    public EffectsManager fx;

    [Header("Settings")]
    public bool enableTutorial = true;

    // State tracking
    private TutorialState currentState = TutorialState.CollectDots;
    private float stateTimer = 0f;
    private bool hasPerfectedFirstDeposit = false;
    private bool hasSpecialDeposited = false;
    private float timeSinceLastDamage = 0f;
    private bool supermodeHappened = false;

    // Pause/resume behavior for tutorial text
    public bool isPausedForTutorial = false;
    private string lastTutorialText = "";
    private bool lastTutorialTextBlocks = false;
    private int resumeFrameCount = -999;
    private bool tutorialPausedBeatAudio = false;

    private void Start()
    {
        if (!enableTutorial) return;

        // Start tutorial with dots-only: disable bullets and special dots until introduced
        if (gm != null)
        {
            gm.enableBullets = false;
            gm.enableSpecialDots = false;
            if (gm.bullets != null) gm.bullets.spawnBullets = false;
            if (gm.spawns != null) gm.spawns.spawnSpecialDots = false;
            // Ensure GameManager has a reference to this tutorial instance
            gm.tutorial = this;
        }

        UpdateTutorialText("Hold down your mouse and hover over Rumors to collect them. You can collect a max of 10 at once.");
    }

    private void Update()
    {
        if (!enableTutorial || gm == null) return;
        if (currentState == TutorialState.Complete) return;

        if (isPausedForTutorial)
        {
            // wait until user clicks/taps to resume
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                ResumeFromTutorialPause();
            }
            return;
        }

        stateTimer -= Time.deltaTime;

        // Track time since last damage
        timeSinceLastDamage += Time.deltaTime;

        switch (currentState)
        {
            case TutorialState.CollectDots:
                UpdateCollectDots();
                break;
            case TutorialState.WaitAfterCollect:
                UpdateWaitAfterCollect();
                break;
            case TutorialState.DepositDots:
                UpdateDepositDots();
                break;
            case TutorialState.WaitAfterDeposit:
                UpdateWaitAfterDeposit();
                break;
            case TutorialState.SpecialDots:
                UpdateSpecialDots();
                break;
            case TutorialState.IntroduceBullets:
                UpdateIntroduceBullets();
                break;
            case TutorialState.ExplainCombo:
                UpdateExplainCombo();
                break;
            case TutorialState.ExplainSuperMode:
                UpdateExplainSuperMode();
                break;
            case TutorialState.SuperModeHappening:
                UpdateSupermodeHappening();
                break;
        }
    }

    private void UpdateCollectDots()
    {
        // Wait for player to collect 2+ dots
        if (cursor != null && cursor.CarriedDots != null && cursor.CarriedDots.Count >= 2)
        {
            AdvanceState(TutorialState.WaitAfterCollect, 5f);
        }
    }

    private void UpdateWaitAfterCollect()
    {
        // Just wait for timer
        if (stateTimer <= 0f)
        {
            UpdateTutorialText("Drag the Rumors to the center to deposit them. Deposit when the pulse reaches the yellow line to get a PERFECT");
            AdvanceState(TutorialState.DepositDots, 0f);
        }
    }

    private void UpdateDepositDots()
    {
        // This state lasts until player gets a perfect deposit
        // We'll hook into CoreController perfect deposits
        if (hasPerfectedFirstDeposit)
        {
            AdvanceState(TutorialState.WaitAfterDeposit, 5f);
        }
    }

    private void UpdateWaitAfterDeposit()
    {
        if (stateTimer <= 0f)
        {
            UpdateTutorialText("Collect all special(green) Rumors on-screen (there's 5!) to deposit them. Do this to score massive points!");
            // Enable special dot spawns when we move into the special dots tutorial
            if (gm != null && gm.spawns != null)
            {
                gm.enableSpecialDots = true;
                gm.spawns.spawnSpecialDots = true;
            }
            AdvanceState(TutorialState.SpecialDots, 0f);
        }
    }

    private void UpdateSpecialDots()
    {
        // Wait for player to get a perfect special deposit
        if (hasSpecialDeposited)
        {
            // Enable bullets now
            if (gm.bullets != null)
            {
                gm.enableBullets = true;
                gm.bullets.spawnBullets = true;
            }

            UpdateTutorialText("Watch out! Enemies are coming! Avoid them while collecting Rumors.");
            timeSinceLastDamage = 0f;
            AdvanceState(TutorialState.IntroduceBullets, 0f);
        }
    }

    private void UpdateIntroduceBullets()
    {
        // Wait for 10 seconds without getting hit
        if (timeSinceLastDamage >= 10f)
        {
            UpdateTutorialText("Great! Getting consecutive PERFECTs increases your combo. Higher combo = more points. Keep your combo going!");
            AdvanceState(TutorialState.ExplainCombo, 10f);
        }
    }

    private void UpdateExplainCombo()
    {
        if (stateTimer <= 0f)
        {
            // After explaining combo we wait until the countdown begins and
            // then show the player what super mode actually does.  We don't
            // immediately complete the tutorial here.
            UpdateTutorialText("");
            AdvanceState(TutorialState.ExplainSuperMode, 0f);
        }
    }

    private void UpdateExplainSuperMode()
    {
        // While we are waiting for the countdown to start we'll keep the
        // instructional text visible.  When the GameManager flips into the
        // countdown state we switch to the "super happening" state which
        // updates the text a little further.
        if (gm != null && gm.state == GameManager.GameState.CountdownToSuper)
        {
            UpdateTutorialText("Countdown active! When super mode begins you'll be invulnerable. Smack him repeatedly to earn massive points!", false);
            AdvanceState(TutorialState.SuperModeHappening, 0f);
        }
    }

    private void UpdateSupermodeHappening()
    {
        // Once we're past the countdown we want to clear the tutorial text
        // or optionally update it while super is active.  The following
        // logic handles both the Super and Normal cases.
        if (gm == null) return;

        if (gm.state == GameManager.GameState.Super)
        {
            // super is live
            UpdateTutorialText("Super mode is active! Smack him as many times as possible!!!", false);
        }
        else if (gm.state != GameManager.GameState.CountdownToSuper)
        {
            // countdown has finished and we left super, end the tutorial
            UpdateTutorialText("");
            AdvanceState(TutorialState.Complete, 0f);
        }
    }

    // Called from CoreController when a perfect deposit happens
    public void OnPerfectDeposit(bool isSpecial)
    {
        if (!enableTutorial) return;

        // Track special deposits for the SpecialDots state
        if (isSpecial && currentState == TutorialState.SpecialDots)
        {
            hasSpecialDeposited = true;
        }

        // Track regular perfect for the DepositDots state
        if (!isSpecial && currentState == TutorialState.DepositDots)
        {
            hasPerfectedFirstDeposit = true;
        }
    }

    // Called from GameManager when player takes damage
    public void OnPlayerHit()
    {
        if (!enableTutorial) return;
        timeSinceLastDamage = 0f;
    }

    // Called from GameManager when a countdown to super begins.
    // Super-mode tutorial text is driven by the state machine in Update.
    public void OnSuperCountdownStart()
    {
        if (!enableTutorial) return;
    }

    // Called when the game actually enters super mode.
    // Super-mode tutorial text is driven by the state machine in Update.
    public void OnSuperModeStart()
    {
        if (!enableTutorial) return;
    }

    // Called when super mode ends.
    // Cleanup is handled by the tutorial state machine in Update.
    public void OnSuperModeEnd()
    {
        if (!enableTutorial) return;
    }

    private void AdvanceState(TutorialState nextState, float delay)
    {
        currentState = nextState;
        stateTimer = delay;
    }

    private void UpdateTutorialText(string text, bool pauseGameplay = true)
    {
        bool textChanged = text != lastTutorialText;
        bool blockingChanged = pauseGameplay != lastTutorialTextBlocks;

        // If tutorial content or its blocking behavior changed, update pause state.
        if (textChanged || blockingChanged)
        {
            if (!string.IsNullOrEmpty(text) && pauseGameplay)
            {
                PauseForTutorial();
            }
            else
            {
                // no text means tutorial is not blocking and may resume if still paused
                if (isPausedForTutorial)
                {
                    ResumeFromTutorialPause();
                }
            }

            lastTutorialText = text;
            lastTutorialTextBlocks = pauseGameplay;
        }

        if (tutorialText != null)
        {
            tutorialText.text = text;
        }
    }

    private void PauseForTutorial()
    {
        if (isPausedForTutorial) return;

        isPausedForTutorial = true;
        Time.timeScale = 0f;

        // Reset camera shake immediately when pausing
        if (fx != null && fx.shake != null)
        {
            fx.shake.ResetShake();
        }

        // Pause audio so pulse doesn't advance independently
        if (gm != null && gm.beat != null && gm.beat.audioSource != null)
        {
            tutorialPausedBeatAudio = gm.beat.audioSource.isPlaying;
            if (tutorialPausedBeatAudio)
            {
                gm.beat.audioSource.Pause();
            }
        }
    }

    private void ResumeFromTutorialPause()
    {
        isPausedForTutorial = false;
        resumeFrameCount = Time.frameCount;
        Time.timeScale = 1f;

        // Resume audio only if the tutorial was the system that paused it.
        if (tutorialPausedBeatAudio && gm != null && gm.beat != null && gm.beat.audioSource != null)
        {
            gm.beat.audioSource.Play();
        }
        tutorialPausedBeatAudio = false;

        // Reset any lingering camera shake to prevent jitter
        if (fx != null && fx.shake != null)
        {
            fx.shake.ResetShake();
        }

        // Reset GameManager timers to prevent accumulated state triggering unintended transitions
        if (gm != null)
        {
            gm.ResetPauseAccumulatedTime();
        }
    }

    public bool WasJustResumedFromPause()
    {
        return Time.frameCount == resumeFrameCount;
    }

    private void CompleteTutorial()
    {
        currentState = TutorialState.Complete;
        if (tutorialText != null)
        {
            tutorialText.text = "";
        }
    }
}
