using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;
using System.Collections;
using System.Collections.Generic;

public class ActorManager : MonoBehaviour
{
    [Header("Characters")]
    public List<GameObject> characters;

    [Header("Backgrounds")]
    public SpriteRenderer backgroundScreen;
    public List<Sprite> backgroundImages;

    [Header("Audio")]
    public AudioSource audioPlayer;
    public List<AudioClip> soundEffects;

    [Header("Effects")]
    public CanvasGroup blackCurtain; 

    private void Awake()
    {
        DialogueRunner runner = FindFirstObjectByType<DialogueRunner>();

        if (runner != null)
        {
            runner.AddCommandHandler<string>("show", ShowCharacter);
            runner.AddCommandHandler<string>("hide", HideCharacter);
            runner.AddCommandHandler<string, string>("play_anim", PlayAnimation);
            runner.AddCommandHandler<string>("background", SetBackground);
            runner.AddCommandHandler<string>("play_sfx", PlaySFX);

            // --- THE FIX: We now accept <string, float> ---
            // This allows <<fade_in "anything" 2.0>>
            runner.AddCommandHandler<string, float>("fade_in", FadeIn);
            runner.AddCommandHandler<string, float>("fade_out", FadeOut);
            // ---------------------------------------------
        }
    }

    // --- UPDATED FADER LOGIC ---
    // Now accepts a name (ignored for now) and a time
    public void FadeIn(string targetName, float time)
    {
        if (blackCurtain != null) StartCoroutine(DoFade(1, 0, time)); 
    }

    public void FadeOut(string targetName, float time)
    {
        if (blackCurtain != null) StartCoroutine(DoFade(0, 1, time)); 
    }

    private IEnumerator DoFade(float start, float end, float duration)
    {
        float counter = 0f;
        while (counter < duration)
        {
            counter += Time.deltaTime;
            blackCurtain.alpha = Mathf.Lerp(start, end, counter / duration);
            yield return null;
        }
        blackCurtain.alpha = end; 
    }
    // ---------------------------

    // (Standard functions remain the same)
    public void PlaySFX(string soundName)
    {
        AudioClip sfx = soundEffects.Find(x => x.name == soundName);
        if (sfx != null && audioPlayer != null) audioPlayer.PlayOneShot(sfx);
    }
    public void SetBackground(string imageName)
    {
        Sprite newBg = backgroundImages.Find(x => x.name == imageName);
        if (newBg != null && backgroundScreen != null) backgroundScreen.sprite = newBg;
    }
    public void ShowCharacter(string name) { FindActor(name)?.SetActive(true); }
    public void HideCharacter(string name) { FindActor(name)?.SetActive(false); }
    public void PlayAnimation(string charName, string trigName) 
    {
        GameObject t = FindActor(charName);
        if (t) t.GetComponent<Animator>()?.SetTrigger(trigName);
    }
    private GameObject FindActor(string name) { return characters.Find(c => c.name == name); }
}
