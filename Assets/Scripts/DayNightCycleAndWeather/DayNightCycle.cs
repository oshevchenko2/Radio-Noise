using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class DayNightCycle : MonoBehaviour
{
    [SerializeField] private Light _mainLight;
    [SerializeField] private Volume _skyVolume;

    private Exposure _exposure;

    [SerializeField, Range(0, 24)] private float _timeOfDay = 12f;
    [SerializeField] private float _dayDurationInMinutes = 10f;

    [SerializeField] private float _maxSunIntensity = 120000f;
    [SerializeField] private float _minMoonIntensity = 0.2f;

    [SerializeField] private AnimationCurve _sunCurve = AnimationCurve.EaseInOut(0f, 0f, 0.25f, 1f); 
    [SerializeField] private AnimationCurve _exposureCurve = AnimationCurve.Linear(0f, -4f, 1f, 1f);
    
    [SerializeField] private Gradient _lightColor;

    void Start()
    {
        _skyVolume.profile.TryGet(out _exposure);

        _lightColor = new Gradient
        {
            colorKeys = new GradientColorKey[]
            {
                new(new Color(0.2f, 0.3f, 0.5f), 0f),
                new(new Color(1f, 0.5f, 0.2f), 0.2f),
                new(Color.white, 0.4f),
                new(new Color(1f, 0.5f, 0.2f), 0.8f),
                new(new Color(0.2f, 0.3f, 0.5f), 1f)
            }
        };
    }

    void Update()
    {
        UpdateTime();
        UpdateLighting();
    }

    void UpdateTime()
    {
        _timeOfDay += 24f / (_dayDurationInMinutes * 60f) * Time.deltaTime;
        if (_timeOfDay > 24f)
            _timeOfDay -= 24f;
    }

    void UpdateLighting()
    {
        float normalizedTime = _timeOfDay / 24f;
        float sunRotation = normalizedTime * 360f - 90f;

        _mainLight.transform.rotation = Quaternion.Euler(sunRotation, 170f, 0);

        float sunFactor = _sunCurve.Evaluate(normalizedTime);
        _mainLight.intensity = Mathf.Lerp(_minMoonIntensity, _maxSunIntensity, sunFactor);

        _mainLight.color = _lightColor.Evaluate(normalizedTime);

        if (_exposure != null)
        {
            _exposure.fixedExposure.value = _exposureCurve.Evaluate(normalizedTime);
        }
    }
}
