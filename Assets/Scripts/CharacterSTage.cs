using UnityEngine;
using Yarn.Unity;

public class CharacterStage : MonoBehaviour
{
    // This tells Yarn Spinner to listen for the word "appear"
    [YarnCommand("appear")]
    public void Appear() 
    {
        gameObject.SetActive(true); // Turns the character ON
    }

    // This tells Yarn Spinner to listen for the word "disappear"
    [YarnCommand("disappear")]
    public void Disappear() 
    {
        gameObject.SetActive(false); // Turns the character OFF
    }
}

