using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class EnyKeyTrigger : MonoBehaviour
{
    [SerializeField] UnityEvent _event;

    void Update()
    {
        if (Input.anyKeyDown)
        {
            Debug.Log("Pressed AnyKey");
            GetComponent<TextFadeCycle>().SetFastAnimation(true);
            StartCoroutine(Wait4());
        }
    }

    private IEnumerator Wait4()
    {
        yield return new WaitForSeconds(2);
        _event?.Invoke();
    }
}
