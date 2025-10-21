using UnityEngine;
using System.Runtime.InteropServices;

namespace Balancy.WebGL
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Loads and injects the IndexedDB helper JavaScript into the page
    /// </summary>
    public static class IndexedDBLoader
    {
        [DllImport("__Internal")]
        private static extern void InjectJavaScript(string jsCode);

        private static bool _isLoaded = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadIndexedDBHelper()
        {
            if (_isLoaded)
                return;

            // Resources.Load doesn't need file extension for TextAssets
            var jsAsset = Resources.Load<TextAsset>("WebGL/IndexedDBFileHelper.js");
            if (jsAsset != null)
            {
                Debug.Log("[Balancy] Loading IndexedDB helper from Resources...");
                InjectJavaScript(jsAsset.text);
                _isLoaded = true;
                Debug.Log("[Balancy] IndexedDB helper loaded and injected");
            }
            else
            {
                Debug.LogError("[Balancy] Failed to load IndexedDBFileHelper.js.txt from Resources/WebGL/");
            }
        }
    }
#endif
}
