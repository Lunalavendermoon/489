using Yarn.Unity;
using UnityEngine;

public class AudioCommands : MonoBehaviour
{
    [YarnCommand("play_sound")]
    public void PlaySound(string soundName)
    {
        AudioManager.Instance.PlayEvent("soundName");
    }
}