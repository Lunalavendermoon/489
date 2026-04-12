using UnityEngine;

public class PlayAudioScript : MonoBehaviour
{
    public string WwiseEventID;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioManager.Instance.PlayEvent("StartGameAudio");
        AudioManager.Instance.PlayEvent(WwiseEventID);
    }
}
