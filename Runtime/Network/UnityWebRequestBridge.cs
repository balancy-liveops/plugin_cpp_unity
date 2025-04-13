using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Balancy.Network
{
    public class UnityWebRequestBridge : MonoBehaviour
    {
        // Keep the instance alive
        private static UnityWebRequestBridge _instance;

        // Delegate types that match the C++ callback signatures
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void WebRequestCallbackDelegate(int requestId, string url, string method, string body, string headersJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FileLoadCallbackDelegate(int requestId, string url);

        // Native plugin function imports
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterWebRequestCallback(WebRequestCallbackDelegate callback);

        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterFileLoadCallback(FileLoadCallbackDelegate callback);

        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyHandleWebRequestComplete(int requestId, bool success, int errorCode, IntPtr data, int dataSize);

        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyHandleFileLoadComplete(int requestId, bool success, int errorCode, IntPtr data, int dataSize);

        // Active requests tracking
        private Dictionary<int, UnityWebRequest> _activeRequests = new Dictionary<int, UnityWebRequest>();

        // Initialize the bridge
        public static void Initialize()
        {
            if (_instance != null) return;

            // Create a GameObject to host our MonoBehaviour
            var go = new GameObject("Balancy_WebRequestBridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnityWebRequestBridge>();

            // Register C# callbacks with the native plugin
            balancyRegisterWebRequestCallback(StaticOnWebRequestReceived);
            balancyRegisterFileLoadCallback(StaticOnFileLoadReceived);
        }

        // Clean up resources when the application exits
        private void OnDestroy()
        {
            balancyRegisterWebRequestCallback(null);
            balancyRegisterFileLoadCallback(null);
            foreach (var request in _activeRequests.Values)
            {
                request.Dispose();
            }
            _activeRequests.Clear();
        }

        // Called by the native plugin when a web request needs to be sent
        [AOT.MonoPInvokeCallback(typeof(WebRequestCallbackDelegate))]
        private static void StaticOnWebRequestReceived(int requestId, string url, string method, string body, string headersJson)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (_instance != null)
                    _instance.OnWebRequestReceived(requestId, url, method, body, headersJson);
                else
                    Debug.LogError("UnityWebRequestBridge instance not initialized.");
            });
        }
        
        private void OnWebRequestReceived(int requestId, string url, string method, string body, string headersJson)
        {
            // Convert parameters to C# values
            Debug.Log($"Received web request: ID={requestId}, URL={url}, Method={method}");

            // Start the coroutine to process the request
            StartCoroutine(ProcessWebRequest(requestId, url, method, body, headersJson));
        }

        // Called by the native plugin when a file needs to be loaded
        [AOT.MonoPInvokeCallback(typeof(FileLoadCallbackDelegate))]
        private static void StaticOnFileLoadReceived(int requestId, string url)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (_instance != null)
                    _instance.OnFileLoadReceived(requestId, url);
                else
                    Debug.LogError("UnityWebRequestBridge instance not initialized.");
            });
        }
        
        private void OnFileLoadReceived(int requestId, string url)
        {
            Debug.Log($"Received file load request: ID={requestId}, URL={url}");

            // Start the coroutine to process the file load
            StartCoroutine(ProcessFileLoad(requestId, url));
        }

        // Coroutine to handle a web request
        private IEnumerator ProcessWebRequest(int requestId, string url, string method, string body, string headersJson)
        {
            // Create the request
            UnityWebRequest webRequest = new UnityWebRequest(url, method);

            // Add request body if present
            if (!string.IsNullOrEmpty(body))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.uploadHandler.contentType = "application/json";
            }

            // Set download handler
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // Add headers if present
            if (!string.IsNullOrEmpty(headersJson))
            {
                try
                {
                    Dictionary<string, string> headers = ParseHeaders(headersJson);
                    foreach (var header in headers)
                        webRequest.SetRequestHeader(header.Key, header.Value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing headers JSON: {ex.Message}");
                }
            }

            // Track the request
            _activeRequests[requestId] = webRequest;

            // Send the request
            yield return webRequest.SendWebRequest();

            // Process the response
            bool success = !webRequest.isNetworkError && !webRequest.isHttpError;
            int errorCode = (int)webRequest.responseCode;
            byte[] data = webRequest.downloadHandler.data;

            // Convert data to a native pointer
            IntPtr dataPtr = IntPtr.Zero;
            int dataSize = 0;

            if (data != null && data.Length > 0)
            {
                dataSize = data.Length;
                dataPtr = Marshal.AllocHGlobal(dataSize);
                Marshal.Copy(data, 0, dataPtr, dataSize);
            }

            try
            {
                // Send the result back to the native plugin
                balancyHandleWebRequestComplete(requestId, success, errorCode, dataPtr, dataSize);
            }
            finally
            {
                // Clean up
                if (dataPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(dataPtr);
                }

                _activeRequests.Remove(requestId);
                webRequest.Dispose();
            }
        }
        
        private Dictionary<string, string> ParseHeaders(string json)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return headers;

            string trimmed = json.Trim('{', '}');
            var pairs = trimmed.Split(',');

            foreach (var pair in pairs)
            {
                var keyValue = pair.Split(':');
                if (keyValue.Length == 2)
                {
                    string key = keyValue[0].Trim().Trim('"');
                    string value = keyValue[1].Trim().Trim('"');
                    headers[key] = value;
                }
            }

            return headers;
        }


        // Coroutine to handle a file load
        private IEnumerator ProcessFileLoad(int requestId, string url)
        {
            // Create the request
            UnityWebRequest webRequest = UnityWebRequest.Get(url);

            // Track the request
            _activeRequests[requestId] = webRequest;

            // Send the request
            yield return webRequest.SendWebRequest();

            // Process the response
            bool success = !webRequest.isNetworkError && !webRequest.isHttpError;
            int errorCode = (int)webRequest.responseCode;
            byte[] data = webRequest.downloadHandler.data;

            // Convert data to a native pointer
            IntPtr dataPtr = IntPtr.Zero;
            int dataSize = 0;

            if (data != null && data.Length > 0)
            {
                dataSize = data.Length;
                dataPtr = Marshal.AllocHGlobal(dataSize);
                Marshal.Copy(data, 0, dataPtr, dataSize);
            }

            try
            {
                // Send the result back to the native plugin
                balancyHandleFileLoadComplete(requestId, success, errorCode, dataPtr, dataSize);
            }
            finally
            {
                // Clean up
                if (dataPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(dataPtr);
                }

                _activeRequests.Remove(requestId);
                webRequest.Dispose();
            }
        }

        // Cancel a request if needed
        public static void CancelRequest(int requestId)
        {
            if (_instance != null && _instance._activeRequests.TryGetValue(requestId, out var request))
            {
                request.Abort();
                _instance._activeRequests.Remove(requestId);
                request.Dispose();
            }
        }
    }
}