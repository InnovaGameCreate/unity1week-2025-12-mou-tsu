using System.Runtime.InteropServices;
using UnityEngine;

public class ExitFullScreenButton : MonoBehaviour
{
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ExitBrowserFullscreen();
#endif

    // Button OnClick に登録
    public void ExitFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ExitBrowserFullscreen();
#else
        // Editor/Standalone用の保険
        Screen.fullScreen = false;
#endif
    }
}
