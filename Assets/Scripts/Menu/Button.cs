using DG.Tweening;
using Unity.Entities.UniversalDelegates;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Button : MonoBehaviour
{
    [SerializeField] private UnityEvent _onClick;

    public void OnMouseDown()
    {
        _onClick.Invoke();
    }
}
