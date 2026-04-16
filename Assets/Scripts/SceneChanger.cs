using UnityEngine;
using UnityEngine.SceneManagement; 
using Yarn.Unity; 

public class SceneChanger : MonoBehaviour
{
    // Added "static" here to make it a global command!
    [YarnCommand("LoadScene")]
    public static void MoveToScene(string sceneName) 
    {
        // Added a Debug log so we can see if Yarn is actually trying!
        Debug.Log("Yarn is attempting to load: " + sceneName); 
        SceneManager.LoadScene(sceneName);
    }
}