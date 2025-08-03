using UnityEngine;
using System.Collections;

public class TextFadeCycle : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float visibleDuration = 1f;
    [SerializeField] private float invisibleDuration = 1f;

    private CanvasGroup _canvasGroup;
    [SerializeField] private bool _fastAnimation = false;

    private Coroutine _fadeCoroutine;

    void Start()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        StartCoroutine(FadeLoop());
    }

    private IEnumerator FadeLoop()
    {
        while (true)
        {
            float speedMultiplier = _fastAnimation ? 0.1f : 1f;

            yield return StartCoroutine(Fade(0f, 1f, fadeDuration * speedMultiplier));
            yield return new WaitForSeconds(visibleDuration * speedMultiplier);

            yield return StartCoroutine(Fade(1f, 0f, fadeDuration * speedMultiplier));
            yield return new WaitForSeconds(invisibleDuration * speedMultiplier);
        }
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        _canvasGroup.alpha = endAlpha;
    }

    public void SetFastAnimation(bool enabled)
    {
        if (_fastAnimation != enabled)
        {
            _fastAnimation = enabled;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeLoop());
        }
    }
}
