using UnityEngine;

public class OpenWeb : MonoBehaviour
{
    [SerializeField] private string url = "";

    public void OpenSite()
    {
        Application.OpenURL(url);
    }
}
