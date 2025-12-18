using UnityEngine;
using System.Runtime.InteropServices;

namespace Balancy.WebGL
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Loads and injects the JSZip library into the page
    /// </summary>
    public static class JSZipLoader
    {
        [DllImport("__Internal")]
        private static extern void InjectJavaScript(string jsCode);

        private static bool _isLoaded = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadJSZip()
        {
            if (_isLoaded)
                return;

            // Load JSZip from Resources
            // Note: Resources.Load doesn't need file extensions
            var jsAsset = Resources.Load<TextAsset>("WebGL/jszip.min.js");
            if (jsAsset != null)
            {
                Debug.Log("[Balancy] Loading JSZip library from Resources...");
                InjectJavaScript(jsAsset.text);
                _isLoaded = true;
                Debug.Log("[Balancy] JSZip library loaded and injected");
            }
            else
            {
                Debug.LogError("[Balancy] Failed to load jszip.min.js.txt from Resources/WebGL/");
                Debug.LogError("[Balancy] Make sure the file exists at Assets/Balancy/WebView/Resources/WebGL/jszip.min.js.txt");
            }
        }
    }
#endif
}
