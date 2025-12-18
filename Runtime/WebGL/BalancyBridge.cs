using UnityEngine;

namespace Balancy.WebGL
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Bridge GameObject for receiving JavaScript callbacks via SendMessage
    /// </summary>
    public class BalancyBridge : MonoBehaviour
    {
        private static BalancyBridge _instance;

        /// <summary>
        /// Get or create the singleton instance
        /// </summary>
        public static BalancyBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Create a persistent GameObject for the bridge
                    var go = new GameObject("BalancyBridge");
                    _instance = go.AddComponent<BalancyBridge>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[Balancy] BalancyBridge GameObject created");
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initialize the bridge (called automatically on first access)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeBridge()
        {
            // Ensure instance is created
            var bridge = Instance;
            Debug.Log("[Balancy] BalancyBridge initialized and ready for callbacks");
        }

        /// <summary>
        /// Called by JavaScript via SendMessage when unzip completes
        /// </summary>
        public void OnWebGLUnzipCompleted(string message)
        {
            Debug.Log($"[BalancyBridge] Received unzip completion: {message}");
            UnzipBridge.OnWebGLUnzipCompleted(message);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
#endif
}
