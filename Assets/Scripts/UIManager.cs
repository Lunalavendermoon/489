using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Bars")]

    public Slider superSlider;
    public Image coreFillImage; // radial fill
    public Slider coreFillSlider; // optional alternative

    [Header("Text")]
    public TMP_Text scoreText;
    public TMP_Text comboText;
    public TMP_Text judgmentText;
    public TMP_Text countdownText;

    [Header("Special UI")]
    [Tooltip("Shows special progress, e.g. SPECIAL 2/3 or SPECIAL READY")]
    public TMP_Text specialStatusText;

    [Tooltip("Shows points gained when SPECIAL batch is deposited, e.g. SPECIAL +1200")]
    public TMP_Text specialScoreText;

    [Tooltip("How fast the special score pop fades out.")]
    public float specialScoreFadeSpeed = 3.5f;

    [Tooltip("How long the special score pop stays fully visible before fading.")]
    public float specialScoreHold = 0.25f;

    private float specialScoreHoldTimer = 0f;

    [Header("Panels")]
    public GameObject gameOverPanel;
    public TMP_Text finalScoreText;

    [Header("Super UI")]
    public GameObject superUIRoot;

    [Header("End Screen UI")]
    public GameObject endPanel;
    public TMP_Text endScoreText;
    public TMP_Text endGradeText;

    [Header("Damage Flash (optional)")]
    public Image damageFlash;
    public float flashFadeSpeed = 6f;

    private void Awake()
    {
        // Ensure special texts start hidden/clean
        if (specialStatusText != null)
            specialStatusText.text = "";

        if (specialScoreText != null)
        {
            specialScoreText.text = "";
            SetTMPAlpha(specialScoreText, 0f);
        }
    }

    private void Update()
    {
        // Damage flash fade (optional)
        if (damageFlash != null && damageFlash.color.a > 0f)
        {
            Color c = damageFlash.color;
            c.a = Mathf.MoveTowards(c.a, 0f, flashFadeSpeed * Time.deltaTime);
            damageFlash.color = c;
        }

        // Special score pop fade
        if (specialScoreText != null)
        {
            if (specialScoreHoldTimer > 0f)
            {
                specialScoreHoldTimer -= Time.deltaTime;
            }
            else
            {
                float a = specialScoreText.color.a;
                if (a > 0f)
                {
                    a = Mathf.MoveTowards(a, 0f, specialScoreFadeSpeed * Time.deltaTime);
                    SetTMPAlpha(specialScoreText, a);
                }
            }
        }
    }

    public void SetCoreFill(float fill01)
    {
        if (coreFillImage != null) coreFillImage.fillAmount = fill01;
        if (coreFillSlider != null) coreFillSlider.value = fill01;
    }

    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = $"Score: {score:n0}";
    }

    public void SetCombo(int combo, float comboMultiplier = 1f)
    {
        if (comboText == null) return;

        if (combo <= 0)
        {
            comboText.text = "Combo: 0";
            return;
        }

        // Show combo level and bonus percent (e.g. +25%)
        int bonusPct = Mathf.RoundToInt((comboMultiplier - 1f) * 100f);
        if (bonusPct > 0)
            comboText.text = $"Combo: {combo} (+{bonusPct}%)";
        else
            comboText.text = $"Combo: {combo}";
    }

    /// <summary>
    /// General judgment pop (no HP display anymore)
    /// </summary>
    public void ShowJudgment(string judge, int points, float unusedHp = 0f)
    {
        if (judgmentText == null) return;
        judgmentText.text = $"{judge}\n+{points:n0}";
    }

    public void ClearJudgment()
    {
        if (judgmentText != null) judgmentText.text = "";
    }

    // -------------------------
    // SPECIAL UI
    // -------------------------

    /// <summary>
    /// Show progress like "SPECIAL 2/3" or "SPECIAL READY"
    /// </summary>
    public void SetSpecialStatus(int carried, int required)
    {
        if (specialStatusText == null) return;

        required = Mathf.Max(1, required);
        carried = Mathf.Clamp(carried, 0, required);

        if (carried >= required)
            specialStatusText.text = "SPECIAL READY";
        else
            specialStatusText.text = $"SPECIAL {carried}/{required}";
    }

    public void ClearSpecialStatus()
    {
        if (specialStatusText != null) specialStatusText.text = "";
    }

    /// <summary>
    /// Call when special batch deposits successfully.
    /// Shows "SPECIAL +XXXX" briefly.
    /// </summary>
    public void ShowSpecialScore(int pointsGained)
    {
        if (specialScoreText == null) return;

        specialScoreText.text = $"SPECIAL +{pointsGained:n0}";
        SetTMPAlpha(specialScoreText, 1f);
        specialScoreHoldTimer = Mathf.Max(0.01f, specialScoreHold);
    }

    // -------------------------
    // Super mode UI (keep if still used)
    // -------------------------

    public void ShowSuperCountdown(bool show)
    {
        if (countdownText != null) countdownText.gameObject.SetActive(show);
    }

    public void SetSuperCountdown(float secondsLeft)
    {
        if (countdownText == null) return;
        int s = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
        countdownText.text = $"SUPER IN {s}...";
    }

    public void ShowSuperUI(bool show)
    {
        if (superUIRoot != null) superUIRoot.SetActive(show);
        if (superSlider != null) superSlider.gameObject.SetActive(show);
    }

    public void SetSuperTimer(float t01)
    {
        if (superSlider != null) superSlider.value = Mathf.Clamp01(t01);
    }

    // -------------------------
    // End/game over
    // -------------------------

    public void ShowGameOver(bool show, int finalScore)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(show);
        if (finalScoreText != null) finalScoreText.text = $"Final Score: {finalScore:n0}";
    }

    public void FlashDamage()
    {
        if (damageFlash == null) return;
        Color c = damageFlash.color;
        c.a = 0.35f;
        damageFlash.color = c;
    }

    public void ShowEndScreen(bool show, int finalScore, string grade)
    {
        if (endPanel) endPanel.SetActive(show);
        if (endScoreText) endScoreText.text = $"Score: {finalScore:n0}";
        if (endGradeText) endGradeText.text = $"Grade: {grade}";
    }

    public void HideEndScreen()
    {
        if (endPanel != null) endPanel.SetActive(false);
    }

    public static string GetGrade(int score)
    {
        if (score >= 90000) return "SSS";
        if (score >= 85000) return "SS";
        if (score >= 80000) return "S";
        if (score >= 70000) return "A";
        if (score >= 60000) return "B";
        if (score >= 50000) return "C";
        return "D";
    }

    // -------------------------
    // Helpers
    // -------------------------

    private void SetTMPAlpha(TMP_Text t, float a)
    {
        if (t == null) return;
        Color c = t.color;
        c.a = Mathf.Clamp01(a);
        t.color = c;
    }
}
