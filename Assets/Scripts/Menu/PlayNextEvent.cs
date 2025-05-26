using UnityEngine;
using UnityEngine.Events;

public class PlayNextEvent : MonoBehaviour
{
    [SerializeField] private UnityEvent _event;

    public void InvokeEvent()
    {
        _event?.Invoke();
    }
}
