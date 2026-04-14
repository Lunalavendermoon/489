using UnityEngine;
using UnityEngine.SceneManagement; // Needed to change scenes
using Yarn.Unity;                  // Needed to talk to Yarn

public class SceneDirector : MonoBehaviour
{
    // This tells Yarn to listen for the command <<load_scene [Name]>>
    [YarnCommand("load_scene")]
    public void LoadNewScene(string sceneName)
    {
        Debug.Log("Yarn is loading scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }
}