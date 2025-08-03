using UnityEngine;
using UnityEngine.Events;

public class Button : MonoBehaviour
{
    [SerializeField] private UnityEvent _onClick;

    public void OnMouseDown()
    {
        _onClick.Invoke();
    }
}
