using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AK.Wwise.Bank soundBank;

    private bool isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>("AudioManager");

            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                Debug.LogError("AudioManager prefab not found in Resources!");
            }
        }
    }

    private void Awake()
    {
        Debug.Log("AudioManager Awake");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudio();
    }

    private void Start()
    {
        AudioManager.Instance.PlayEvent("StartGameAudio");
    }

    private void InitializeAudio()
    {
        if (isInitialized) return;

        if (soundBank != null)
        {
            Debug.Log("Loading SoundBank...");
            soundBank.Load();
            isInitialized = true;
        }
        else
        {
            Debug.LogError("SoundBank not assigned in AudioManager!");
        }
    }

    public void PlayEvent(string eventName)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("AudioManager not ready yet.");
            return;
        }

        AkSoundEngine.PostEvent(eventName, gameObject);
    }
}