using System;
using UnityEngine;

namespace Balancy.WebView
{
    public class BalancyWebView : MonoBehaviour
    {
        private static BalancyWebView s_instance;
        
        public static BalancyWebView Instance 
        {
            get 
            {
                if (s_instance == null)
                {
                    GameObject go = new GameObject("BalancyWebView");
                    s_instance = go.AddComponent<BalancyWebView>();
                    DontDestroyOnLoad(go);
                }
                return s_instance;
            }
        }
        
        // Events
        public event BalancyWebViewNative.MessageCallback OnMessage;
        public event BalancyWebViewNative.LoadCompletedCallback OnLoadCompleted;
        public event BalancyWebViewNative.CacheCompletedCallback OnCacheCompleted;
        
        #if UNITY_EDITOR_OSX
        private EmbeddedEditorWebView embeddedWebView;
        #endif
        
        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Register for events from native
            BalancyWebViewNative.OnMessage += HandleMessage;
            BalancyWebViewNative.OnLoadCompleted += HandleLoadCompleted;
            BalancyWebViewNative.OnCacheCompleted += HandleCacheCompleted;
            
            #if UNITY_EDITOR_OSX
            // Set up embedded WebView for editor
//             GameObject embeddedGo = new GameObject("EmbeddedWebView");
//             embeddedGo.transform.SetParent(transform);
//             embeddedWebView = embeddedGo.AddComponent<EmbeddedEditorWebView>();
            embeddedWebView = EmbeddedEditorWebView.Instance;

            
            embeddedWebView.OnMessage += (message) => { OnMessage?.Invoke(message); };
            embeddedWebView.OnLoadCompleted += (success) => { OnLoadCompleted?.Invoke(success); };
            embeddedWebView.OnCacheCompleted += (success) => { OnCacheCompleted?.Invoke(success); };
            #endif
        }
        
        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
            
            // Unregister events
            BalancyWebViewNative.OnMessage -= HandleMessage;
            BalancyWebViewNative.OnLoadCompleted -= HandleLoadCompleted;
            BalancyWebViewNative.OnCacheCompleted -= HandleCacheCompleted;
            
            // Close the WebView
            #if !UNITY_EDITOR
            BalancyWebViewNative.Close();
            #else
            if (embeddedWebView != null)
            {
                embeddedWebView.CloseWebView();
            }
            #endif
        }
        
        // Public methods
        public bool OpenWebView(string url, int width = 0, int height = 0, bool transparent = false)
        {
            try
            {
                #if UNITY_EDITOR_OSX
                if (embeddedWebView != null)
                {
                    Debug.Log($"Opening embedded WebView with URL: {url}");
                    return embeddedWebView.OpenWebView(url, width, height, transparent);
                }
                return false;
                #else
                return BalancyWebViewNative.Open(url, width, height, transparent);
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error opening WebView: {ex.Message}");
                return false;
            }
        }
        
        public void CloseWebView()
        {
            try
            {
                #if UNITY_EDITOR_OSX
                if (embeddedWebView != null)
                {
                    embeddedWebView.CloseWebView();
                }
                #else
                BalancyWebViewNative.Close();
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error closing WebView: {ex.Message}");
            }
        }
        
        public bool SendMessage(string message)
        {
            try
            {
                #if UNITY_EDITOR_OSX
                if (embeddedWebView != null)
                {
                    return embeddedWebView.SendMessage(message);
                }
                return false;
                #else
                return BalancyWebViewNative.SendMessage(message);
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending WebView message: {ex.Message}");
                return false;
            }
        }
        
        public void SetOfflineCacheEnabled(bool enable)
        {
            try
            {
                #if UNITY_EDITOR_OSX
                if (embeddedWebView != null)
                {
                    embeddedWebView.SetOfflineCacheEnabled(enable);
                }
                #else
                BalancyWebViewNative.SetOfflineCacheEnabled(enable);
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting WebView cache: {ex.Message}");
            }
        }
        
        // Event handlers
        private void HandleMessage(string message)
        {
            try
            {
                OnMessage?.Invoke(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in WebView message handler: {ex.Message}");
            }
        }
        
        private void HandleLoadCompleted(bool success)
        {
            try
            {
                OnLoadCompleted?.Invoke(success);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in WebView load completed handler: {ex.Message}");
            }
        }
        
        private void HandleCacheCompleted(bool success)
        {
            try
            {
                OnCacheCompleted?.Invoke(success);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in WebView cache completed handler: {ex.Message}");
            }
        }
    }
}