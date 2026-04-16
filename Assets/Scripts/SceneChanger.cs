using UnityEngine;
using UnityEngine.SceneManagement; 
using Yarn.Unity; // 1. Open the Yarn dictionary!

public class SceneChanger : MonoBehaviour
{
    // 2. Hand Yarn the walkie-talkie and tell it to listen for "LoadScene"
    [YarnCommand("LoadScene")]
    public void MoveToScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}