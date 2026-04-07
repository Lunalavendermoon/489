using UnityEngine;

public class OptionsPresenterWatcher : MonoBehaviour
{
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private GameObject lastLineText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        bool shouldShow = false;
        foreach(Transform child in optionsContainer)
        {
            var childGameObject = child.gameObject;
            if (childGameObject.activeSelf)
            {
                shouldShow = true; break;
            
            }
        }
        lastLineText.SetActive(shouldShow);
     
    }
}
