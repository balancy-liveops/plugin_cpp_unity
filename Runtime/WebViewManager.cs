using Balancy.Models;
using Balancy.WebView;
using UnityEngine;

namespace Balancy
{
    internal class WebViewManager
    {
        public static void Init()
        {
            BalancyWebView.Instance.OnMessage = OnMessageReceived;
        }

        private static string OnMessageReceived(string msg)
        {
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
