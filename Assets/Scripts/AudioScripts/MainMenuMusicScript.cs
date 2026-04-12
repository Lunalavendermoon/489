using UnityEngine;

public class MainMenuMusicScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //AkSoundEngine.PostEvent("StartGameAudio", gameObject);
        //AkSoundEngine.PostEvent("SetMusicToMainTheme", gameObject);

        AudioManager.Instance.PlayEvent("StartGameAudio");
        AudioManager.Instance.PlayEvent("SetMusicToMainTheme");
    }
}
