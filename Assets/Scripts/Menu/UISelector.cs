using UnityEngine;
using UnityEngine.EventSystems;

public class UISelector : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button defaultButton;

    void Start()
    {
        EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }
}
