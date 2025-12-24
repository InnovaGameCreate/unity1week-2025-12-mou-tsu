using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    void Awake()
    {
        // すでに存在していたら自分を消す
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーンをまたいで維持
        DontDestroyOnLoad(gameObject);
    }
}
