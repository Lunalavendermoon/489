using Yarn.Unity;
using UnityEngine;

public class AudioCommands : MonoBehaviour
{
    // Added "static" right here!
    [YarnCommand("play_sound")]
    public static void PlaySound(string soundName)
    {
        AudioManager.Instance.PlayEvent(soundName); 
    }
}