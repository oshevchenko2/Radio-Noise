// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using DG.Tweening;
using UnityEngine;

public class ButtonAnim : MonoBehaviour
{
    [SerializeField] private Transform[] button;
    [SerializeField] private Ease easeType = Ease.OutQuad;
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private Vector3 localPosition;

    void Start ()
    {
        float delayBetweenObjects = 0.1f;
        for(int i = 0; i < button.Length; i++)
        {
            if(button[i] == null)
            {
                continue;
            }
            button[i].DOLocalMove(button[i].localPosition + localPosition, moveDuration)
                .SetDelay(i * delayBetweenObjects)
                .SetEase(easeType);
        }
    }
}
