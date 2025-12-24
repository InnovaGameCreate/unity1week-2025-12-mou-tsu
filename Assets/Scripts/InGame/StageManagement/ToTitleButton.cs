using UnityEngine;
using UnityEngine.SceneManagement;

public class ToTitle : MonoBehaviour
{
    [SerializeField] private string titleSceneName = "Title";

    public void GoToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }
}
