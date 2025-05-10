using System;
using Balancy.Models;
using Balancy.WebView;
using UnityEngine;

namespace Balancy
{
    public class RenderViewsManager
    {
        private static BalancyWebView _webView;
        
        internal static void Init()
        {
            BalancyWebView.Instance.OnMessage = OnMessageReceived;
            _webView = BalancyWebView.Instance;
            _webView.OnLoadCompleted += HandleLoadCompleted;
            _webView.OnClosed += HandleWebViewClosed;
            
            _webView.SetTransparentBackground(true);
            _webView.SetFullScreen(true);
            //_webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
            //_webView.SetDebugLogging(true);
        }

        private static void HandleWebViewClosed()
        {
            
        }

        private static void HandleLoadCompleted(bool obj)
        {
            
        }
        
        public static void OpenView(string url)
        {
            if (_webView.IsWebViewOpen())
            {
                Debug.LogError("View is already opened");
                return;
            }
            
            var urlToLoad = url + "?timestamp=" + Guid.NewGuid().ToString();

            bool success = _webView.OpenWebView(urlToLoad);
            
            if (success)
                Debug.Log("Opening View: " + urlToLoad);
            else
                Debug.Log("Failed to open View");
        }

        private static string OnMessageReceived(string msg)
        {
            if (msg == "balancy_close_view")
            {
                Debug.Log("Closing view");
                _webView.CloseWebView();
                return string.Empty;
            }
            
            Debug.Log("Incomming = " + msg);
            var output = RunRequestInTheCorePlugin(msg);
            Debug.Log("output = " + output);
            return output;
        }

        private static string RunRequestInTheCorePlugin(string requestData)
        {
            return JsonBasedObject.GetStringFromIntPtr(LibraryMethods.General.balancyWebViewRequest(requestData));
        }
    }
}
