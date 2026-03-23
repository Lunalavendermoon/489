using UnityEngine;
using UnityEngine.SceneManagement; // Essential for switching scenes

public class SceneChanger : MonoBehaviour
{
    public void MoveToScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}