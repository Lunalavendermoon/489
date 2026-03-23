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
            UpdateTutorialText("Collect all special(green) Rumors on-screen (there's 5!) and deposit them all together to score massive points.");
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
            UpdateTutorialText("Countdown active! When super mode begins you'll be invulnerable. Smack him repeatedly to earn massive points!");
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
            UpdateTutorialText("Super mode is active! Smack him as many times as possible!!!");
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

    // Called from GameManager when a countdown to super begins.  We want
    // to explain the mechanic while the countdown UI is visible.
    public void OnSuperCountdownStart()
    {
        if (!enableTutorial) return;
        UpdateTutorialText("Super mode is coming! You'll be invincible and earn a big score bonus. Get ready to smash everything!");
    }

    // Called when the game actually enters super mode.  Give the player a
    // brief reminder while it is active.
    public void OnSuperModeStart()
    {
        if (!enableTutorial) return;
        UpdateTutorialText("Super mode active! Collect dots and avoid damage to keep it going.");
    }

    // Optionally clear any lingering text when super mode ends.
    public void OnSuperModeEnd()
    {
        if (!enableTutorial) return;
        // only clear if we're not in another tutorial sequence
        if (currentState == TutorialState.SuperModeHappening ||
            currentState == TutorialState.ExplainSuperMode)
        {
            UpdateTutorialText("");
        }
    }

    private void AdvanceState(TutorialState nextState, float delay)
    {
        currentState = nextState;
        stateTimer = delay;
    }

    private void UpdateTutorialText(string text)
    {
        if (tutorialText != null)
        {
            tutorialText.text = text;
        }
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
