using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro;

public class TutorialScreenManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public VideoClip videoClip;
        [TextArea(2, 5)]
        public string caption;
    }

    [Header("Tutorial Data")]
    [SerializeField] private List<TutorialStep> tutorialSteps = new List<TutorialStep>();

    [Header("UI References")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;
    [SerializeField] private Button startGameButton;

    [Header("Scene Settings")]
    [SerializeField] private string gameSceneName = "GameScene";

    private int currentStepIndex = 0;

    private void Start()
    {
        if (tutorialSteps == null || tutorialSteps.Count == 0)
        {
            Debug.LogWarning("No tutorial steps assigned.");
            return;
        }

        leftArrowButton.onClick.AddListener(PreviousStep);
        rightArrowButton.onClick.AddListener(NextStep);
        startGameButton.onClick.AddListener(StartGame);

        ShowStep(0);
    }

    private void ShowStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= tutorialSteps.Count)
            return;

        currentStepIndex = stepIndex;
        TutorialStep step = tutorialSteps[currentStepIndex];

        // Update caption
        if (captionText != null)
            captionText.text = step.caption;

        // Update video
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = step.videoClip;
            videoPlayer.isLooping = true;
            videoPlayer.Play();
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool isFirstStep = currentStepIndex == 0;
        bool isLastStep = currentStepIndex == tutorialSteps.Count - 1;

        if (leftArrowButton != null)
            leftArrowButton.gameObject.SetActive(!isFirstStep);

        if (rightArrowButton != null)
            rightArrowButton.gameObject.SetActive(!isLastStep);

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isLastStep);
    }

    public void NextStep()
    {
        if (currentStepIndex < tutorialSteps.Count - 1)
        {
            ShowStep(currentStepIndex + 1);
        }
    }

    public void PreviousStep()
    {
        if (currentStepIndex > 0)
        {
            ShowStep(currentStepIndex - 1);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}