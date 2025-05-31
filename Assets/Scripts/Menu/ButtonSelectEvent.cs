using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonSelectEvent : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private UnityEvent _onHoverEnter;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _onHoverEnter.Invoke();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _onHoverEnter.Invoke();
    }
}