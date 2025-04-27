using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy.WebView
{
    public class BalancyWebViewNative
    {
        // Delegate types for callbacks
        public delegate void MessageCallback(string message);
        public delegate void LoadCompletedCallback(bool success);
        public delegate void CacheCompletedCallback(bool success);
        
        // C function imports
        #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [DllImport("balancy_webview")]
        private static extern bool OpenWebView(string url, int width, int height, bool transparent);
        
        [DllImport("balancy_webview")]
        private static extern void CloseWebView();
        
        [DllImport("balancy_webview")]
        private static extern bool SendWebViewMessage(string message);
        
        [DllImport("balancy_webview")]
        private static extern void SetWebViewOfflineCacheEnabled(bool enable);
        
        [DllImport("balancy_webview")]
        private static extern void RegisterWebViewMessageCallback(MessageCallbackDelegate callback);
        
        [DllImport("balancy_webview")]
        private static extern void RegisterWebViewLoadCompletedCallback(LoadCompletedCallbackDelegate callback);
        
        [DllImport("balancy_webview")]
        private static extern void RegisterWebViewCacheCompletedCallback(CacheCompletedCallbackDelegate callback);
        
        // Texture functions
        [DllImport("balancy_webview")]
        private static extern IntPtr GetWebViewTexture();
        
        [DllImport("balancy_webview")]
        private static extern int GetWebViewTextureWidth();
        
        [DllImport("balancy_webview")]
        private static extern int GetWebViewTextureHeight();
        
        [DllImport("balancy_webview")]
        private static extern bool UpdateWebViewTexture();
        #elif UNITY_IOS
        [DllImport("__Internal")]
        private static extern bool OpenWebView(string url, int width, int height, bool transparent);
        
        [DllImport("__Internal")]
        private static extern void CloseWebView();
        
        [DllImport("__Internal")]
        private static extern bool SendWebViewMessage(string message);
        
        [DllImport("__Internal")]
        private static extern void SetWebViewOfflineCacheEnabled(bool enable);
        
        [DllImport("__Internal")]
        private static extern void RegisterWebViewMessageCallback(MessageCallbackDelegate callback);
        
        [DllImport("__Internal")]
        private static extern void RegisterWebViewLoadCompletedCallback(LoadCompletedCallbackDelegate callback);
        
        [DllImport("__Internal")]
        private static extern void RegisterWebViewCacheCompletedCallback(CacheCompletedCallbackDelegate callback);
        
        // Texture functions (not used on iOS)
        private static IntPtr GetWebViewTexture() => IntPtr.Zero;
        private static int GetWebViewTextureWidth() => 0;
        private static int GetWebViewTextureHeight() => 0;
        private static bool UpdateWebViewTexture() => false;
        #else
        // Implement stubs for other platforms
        private static bool OpenWebView(string url, int width, int height, bool transparent) => false;
        private static void CloseWebView() {}
        private static bool SendWebViewMessage(string message) => false;
        private static void SetWebViewOfflineCacheEnabled(bool enable) {}
        private static void RegisterWebViewMessageCallback(MessageCallbackDelegate callback) {}
        private static void RegisterWebViewLoadCompletedCallback(LoadCompletedCallbackDelegate callback) {}
        private static void RegisterWebViewCacheCompletedCallback(CacheCompletedCallbackDelegate callback) {}
        private static IntPtr GetWebViewTexture() => IntPtr.Zero;
        private static int GetWebViewTextureWidth() => 0;
        private static int GetWebViewTextureHeight() => 0;
        private static bool UpdateWebViewTexture() => false;
        #endif
        
        // Callback delegates (must be kept alive to prevent garbage collection)
        private delegate void MessageCallbackDelegate(string message);
        private delegate void LoadCompletedCallbackDelegate(bool success);
        private delegate void CacheCompletedCallbackDelegate(bool success);
        
        private static MessageCallbackDelegate s_messageCallback;
        private static LoadCompletedCallbackDelegate s_loadCompletedCallback;
        private static CacheCompletedCallbackDelegate s_cacheCompletedCallback;
        
        // Events
        public static event MessageCallback OnMessage;
        public static event LoadCompletedCallback OnLoadCompleted;
        public static event CacheCompletedCallback OnCacheCompleted;
        
        // Static constructor to register callbacks
        static BalancyWebViewNative()
        {
            // Register callbacks
            s_messageCallback = OnMessageReceived;
            s_loadCompletedCallback = OnLoadCompletedReceived;
            s_cacheCompletedCallback = OnCacheCompletedReceived;
            
            RegisterWebViewMessageCallback(s_messageCallback);
            RegisterWebViewLoadCompletedCallback(s_loadCompletedCallback);
            RegisterWebViewCacheCompletedCallback(s_cacheCompletedCallback);
        }
        
        // Public methods
        public static bool Open(string url, int width = 0, int height = 0, bool transparent = false)
        {
            return OpenWebView(url, width, height, transparent);
        }
        
        public static void Close()
        {
            CloseWebView();
        }
        
        public static bool SendMessage(string message)
        {
            return SendWebViewMessage(message);
        }
        
        public static void SetOfflineCacheEnabled(bool enable)
        {
            SetWebViewOfflineCacheEnabled(enable);
        }
        
        // Texture methods
        public static IntPtr GetTexture()
        {
            return GetWebViewTexture();
        }
        
        public static int GetTextureWidth()
        {
            return GetWebViewTextureWidth();
        }
        
        public static int GetTextureHeight()
        {
            return GetWebViewTextureHeight();
        }
        
        public static bool UpdateTexture()
        {
            return UpdateWebViewTexture();
        }
        
        // Callback receivers
        [AOT.MonoPInvokeCallback(typeof(MessageCallbackDelegate))]
        private static void OnMessageReceived(string message)
        {
            OnMessage?.Invoke(message);
        }
        
        [AOT.MonoPInvokeCallback(typeof(LoadCompletedCallbackDelegate))]
        private static void OnLoadCompletedReceived(bool success)
        {
            OnLoadCompleted?.Invoke(success);
        }
        
        [AOT.MonoPInvokeCallback(typeof(CacheCompletedCallbackDelegate))]
        private static void OnCacheCompletedReceived(bool success)
        {
            OnCacheCompleted?.Invoke(success);
        }
    }
}