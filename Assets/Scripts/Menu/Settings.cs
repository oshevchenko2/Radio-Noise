using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements.Experimental;

public class Settings : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] _resolutions;
    private readonly List<Resolution> _filteredResolutions = new();
    private int _defaultIndex = 1;

    void Awake()
    {
        LoadSettings();
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= _filteredResolutions.Count) return;

        Resolution resolution = _filteredResolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionWidth", resolution.width);
        PlayerPrefs.SetInt("ResolutionHeight", resolution.height);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ResetSettings()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        LoadSettings();
    }

    private void LoadSettings()
    {
        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new();
        _filteredResolutions.Clear();

        int savedWidth = PlayerPrefs.GetInt("ResolutionWidth", Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt("ResolutionHeight", Screen.currentResolution.height);
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;

        _defaultIndex = 0;
        for (int i = 0; i < _resolutions.Length; i++)
        {
            string option = _resolutions[i].width + " x " + _resolutions[i].height;
            if (!options.Contains(option))
            {
                options.Add(option);
                _filteredResolutions.Add(_resolutions[i]);

                if (_resolutions[i].width == savedWidth && _resolutions[i].height == savedHeight)
                {
                    _defaultIndex = _filteredResolutions.Count - 1;
                }
            }
        }
        Resolution nativeResolution = Screen.currentResolution;
        for (int i = 0; i < _filteredResolutions.Count; i++)
        {
            if (_filteredResolutions[i].width == nativeResolution.width && _filteredResolutions[i].height == nativeResolution.height)
            {
                _defaultIndex = i;
                savedWidth = nativeResolution.width;
                savedHeight = nativeResolution.height;
                break;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = _defaultIndex;
        resolutionDropdown.RefreshShownValue();

        fullscreenToggle.isOn = isFullscreen;
        Screen.SetResolution(savedWidth, savedHeight, isFullscreen);

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            ResetSettings();
        }
    }
}
