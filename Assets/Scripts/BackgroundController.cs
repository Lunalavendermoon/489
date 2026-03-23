using UnityEngine;
using UnityEngine.UI; 
using Yarn.Unity;

public class BackgroundController : MonoBehaviour
{
    public Image displayScreen;      // The Projector Screen (UI Image)
    public Sprite[] backgroundImages; // The stack of Photos (Sprites)

    // This listens for the command: <<scene [Name]>>
    [YarnCommand("scene")]
    public void SwitchBackground(string locationName)
    {
        // 1. Loop through our stack of photos
        foreach (Sprite photo in backgroundImages)
        {
            // 2. If we find one with the matching name...
            if (photo.name == locationName)
            {
                // 3. ...put it on the screen!
                displayScreen.sprite = photo;
                return; 
            }
        }
        Debug.LogError("Help! I can't find a background named: " + locationName);
    }
}
