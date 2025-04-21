using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy.WebView
{
    public class BalancyWebView : MonoBehaviour
    {
        // Singleton instance
        private static BalancyWebView _instance;
        
        // Events
        public event Action<string> OnMessage;
        public event Action<bool> OnLoadCompleted;
        public event Action<bool> OnCacheCompleted;
        
        // Native plugin functions
        #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool _BalancyWebViewOpen(string url, int width, int height);
        
        [DllImport("__Internal")]
        private static extern void _BalancyWebViewClose();
        
        [DllImport("__Internal")]
        private static extern bool _BalancyWebViewSendMessage(string message);
        
        [DllImport("__Internal")]
        private static extern void _BalancyWebViewSetOfflineCacheEnabled(bool enabled);
        #endif
        
        // Initialize singleton
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        // Get or create instance
        public static BalancyWebView Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("BalancyWebView");
                    _instance = go.AddComponent<BalancyWebView>();
                }
                return _instance;
            }
        }
        
        // Open WebView with URL
        public bool OpenWebView(string url, int width = 0, int height = 0)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return _BalancyWebViewOpen(url, width, height);
            #else
            Debug.Log($"[BalancyWebView] Opening WebView with URL: {url}");
            return false;
            #endif
        }
        
        // Close WebView
        public void CloseWebView()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _BalancyWebViewClose();
            #else
            Debug.Log("[BalancyWebView] Closing WebView");
            #endif
        }
        
        // Send message to WebView
        public bool SendMessage(string message)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return _BalancyWebViewSendMessage(message);
            #else
            Debug.Log($"[BalancyWebView] Sending message: {message}");
            return false;
            #endif
        }
        
        // Enable/disable offline caching
        public void SetOfflineCacheEnabled(bool enabled)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _BalancyWebViewSetOfflineCacheEnabled(enabled);
            #else
            Debug.Log($"[BalancyWebView] Set offline cache enabled: {enabled}");
            #endif
        }
        
        // Called by native code when message is received
        private void OnMessageReceived(string message)
        {
            OnMessage?.Invoke(message);
        }
        
        // Called by native code when load is completed
        private void OnLoadCompletedReceived(string success)
        {
            bool isSuccess = success == "true";
            OnLoadCompleted?.Invoke(isSuccess);
        }
        
        // Called by native code when cache is completed
        private void OnCacheCompletedReceived(string success)
        {
            bool isSuccess = success == "true";
            OnCacheCompleted?.Invoke(isSuccess);
        }
    }
}