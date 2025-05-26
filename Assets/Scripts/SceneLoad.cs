using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour
{
    [SerializeField] private string _sceneName;

    public void LoadScene()
    {
        SceneManager.LoadSceneAsync(_sceneName);
    }
}