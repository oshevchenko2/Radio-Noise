// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using System.Runtime.InteropServices;
using UnityEngine;

public class LicenseChecker : MonoBehaviour
{
    [DllImport("LicensePlugin", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int CheckLicense(string streamingAssetsPath);

    void Awake()
    {
        string path = Application.streamingAssetsPath;
        int result = CheckLicense(path);
        if (result == 0)
        {
            Debug.LogError("License not found");
            Application.Quit();
        }
        else if (result == 1)
        {
            Debug.Log("License found, but changed. It may be a modificated version or may be a game bug");
        }
        else if (result == 2)
        {
            Debug.Log("License found");
        }
    }
}
