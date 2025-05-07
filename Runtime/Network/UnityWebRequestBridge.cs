using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
#endif

namespace Balancy.Network
{
    public class UnityWebRequestBridge : MonoBehaviour
    {
        // Keep the instance alive
        private static UnityWebRequestBridge _instance;

        // Delegate types that match the C++ callback signatures
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void WebRequestCallbackDelegate(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FileLoadCallbackDelegate(int requestId, string url, int timeoutSeconds);

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

#if UNITY_EDITOR
        // HttpClient for Editor mode - use a dictionary to manage different clients with different timeouts
        private static Dictionary<int, HttpClient> _httpClients = new Dictionary<int, HttpClient>();
        private static readonly object _httpClientLock = new object();
#endif

        // Initialize the bridge
        public static void Initialize()
        {
            if (_instance != null) return;

            var go = new GameObject("Balancy_WebRequestBridge");
            
            if (Application.isPlaying)
            {
                go.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(go);
            }
            else
            {
                // In Editor mode, just hide in hierarchy but allow normal destruction
                go.hideFlags = HideFlags.HideInHierarchy;
            }
            
            _instance = go.AddComponent<UnityWebRequestBridge>();

            // Register C# callbacks with the native plugin
            balancyRegisterWebRequestCallback(StaticOnWebRequestReceived);
            balancyRegisterFileLoadCallback(StaticOnFileLoadReceived);
        }

        public static void Clear()
        {
            if (_instance == null) return;
            
            _instance.CleanupResources();
            
            if (Application.isPlaying)
                Destroy(_instance.gameObject);
            else
                DestroyImmediate(_instance.gameObject);
                
            _instance = null;
        }
        
        // Method to manually clean up resources
        private void CleanupResources()
        {
            balancyRegisterWebRequestCallback(null);
            balancyRegisterFileLoadCallback(null);
            
            foreach (var request in _activeRequests.Values)
            {
                request.Dispose();
            }
            _activeRequests.Clear();

#if UNITY_EDITOR
            // Dispose all HttpClients
            lock (_httpClientLock)
            {
                foreach (var client in _httpClients.Values)
                {
                    client.Dispose();
                }
                _httpClients.Clear();
            }
#endif
        }

        // Clean up resources when the application exits
        private void OnDestroy()
        {
            CleanupResources();
        }

        // Called by the native plugin when a web request needs to be sent
        [AOT.MonoPInvokeCallback(typeof(WebRequestCallbackDelegate))]
        private static void StaticOnWebRequestReceived(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (_instance != null)
                    _instance.OnWebRequestReceived(requestId, url, method, body, headersJson, timeoutSeconds);
                else
                    Debug.LogError("UnityWebRequestBridge instance not initialized.");
            });
        }
        
        private void OnWebRequestReceived(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds)
        {
            // Convert parameters to C# values
            // Debug.Log($"Received web request: ID={requestId}, URL={url}, Method={method}");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Use HttpClient in Editor mode
                ProcessWebRequestWithHttpClient(requestId, url, method, body, headersJson, timeoutSeconds);
                return;
            }
#endif

            // Start the coroutine to process the request (for runtime)
            StartCoroutine(ProcessWebRequest(requestId, url, method, body, headersJson, timeoutSeconds));
        }

        // Called by the native plugin when a file needs to be loaded
        [AOT.MonoPInvokeCallback(typeof(FileLoadCallbackDelegate))]
        private static void StaticOnFileLoadReceived(int requestId, string url, int timeoutSeconds)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (_instance != null)
                    _instance.OnFileLoadReceived(requestId, url, timeoutSeconds);
                else
                    Debug.LogError("UnityWebRequestBridge instance not initialized.");
            });
        }
        
        private void OnFileLoadReceived(int requestId, string url, int timeoutSeconds)
        {
            // Debug.Log($"Received file load request: ID={requestId}, URL={url}");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Use HttpClient in Editor mode
                ProcessFileLoadWithHttpClient(requestId, url, timeoutSeconds);
                return;
            }
#endif

            // Start the coroutine to process the file load (for runtime)
            StartCoroutine(ProcessFileLoad(requestId, url, timeoutSeconds));
        }

#if UNITY_EDITOR
        // Get or create an HttpClient with the specified timeout
        private static HttpClient GetHttpClient(int timeoutSeconds)
        {
            lock (_httpClientLock)
            {
                // Use the timeout as a key to get a client with that timeout
                if (_httpClients.TryGetValue(timeoutSeconds, out HttpClient client))
                {
                    return client;
                }
                
                // Create a new client with the specified timeout
                client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                };
                
                _httpClients[timeoutSeconds] = client;
                return client;
            }
        }

        // Process web request with HttpClient for Editor mode
        private async void ProcessWebRequestWithHttpClient(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds)
        {
            // Get a client with the appropriate timeout
            HttpClient httpClient = GetHttpClient(timeoutSeconds);

            try
            {
                // Create request message
                HttpMethod httpMethod = new HttpMethod(method);
                HttpRequestMessage request = new HttpRequestMessage(httpMethod, url);

                // Add request body if present
                if (!string.IsNullOrEmpty(body) && (method == "POST" || method == "PUT" || method == "PATCH"))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                // Add headers if present
                if (!string.IsNullOrEmpty(headersJson))
                {
                    try
                    {
                        Dictionary<string, string> headers = ParseHeaders(headersJson);
                        foreach (var header in headers)
                        {
                            // Skip content type if we've already set it with the request body
                            if (header.Key.ToLower() == "content-type" && request.Content != null)
                                continue;
                                
                            // Some headers can't be set directly on the request
                            if (!header.Key.StartsWith("Content-"))
                                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            else if (request.Content != null)
                                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error parsing headers JSON: {ex.Message}");
                    }
                }

                // Send request
                HttpResponseMessage response = await httpClient.SendAsync(request);
                
                // Read response
                byte[] data = await response.Content.ReadAsByteArrayAsync();
                bool success = response.IsSuccessStatusCode;
                int errorCode = (int)response.StatusCode;

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
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"HTTP request failed: {ex.Message}");
                
                // Create error message as byte array
                string errorMessage = $"{{\"error\":\"{ex.Message}\"}}";
                byte[] errorData = Encoding.UTF8.GetBytes(errorMessage);
                
                // Convert to native pointer
                IntPtr dataPtr = Marshal.AllocHGlobal(errorData.Length);
                Marshal.Copy(errorData, 0, dataPtr, errorData.Length);
                
                try
                {
                    // Send failure back to native plugin
                    balancyHandleWebRequestComplete(requestId, false, 0, dataPtr, errorData.Length);
                }
                finally
                {
                    // Clean up
                    Marshal.FreeHGlobal(dataPtr);
                }
            }
        }

        // Process file load with HttpClient for Editor mode
        private async void ProcessFileLoadWithHttpClient(int requestId, string url, int timeoutSeconds)
        {
            // Get a client with the appropriate timeout
            HttpClient httpClient = GetHttpClient(timeoutSeconds);

            try
            {
                // Download the file
                byte[] data = await httpClient.GetByteArrayAsync(url);
                bool success = true;
                int errorCode = 200; // Assume OK

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
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"File download failed: {ex.Message}");
                
                // Send failure back to native plugin
                balancyHandleFileLoadComplete(requestId, false, 0, IntPtr.Zero, 0);
            }
        }
#endif

        // Coroutine to handle a web request (for runtime)
        private IEnumerator ProcessWebRequest(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds)
        {
            // Create the request
            UnityWebRequest webRequest = new UnityWebRequest(url, method);

            // Set timeout
            webRequest.timeout = timeoutSeconds;

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
            bool success = webRequest.result == UnityWebRequest.Result.Success;
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

        // Coroutine to handle a file load (for runtime)
        private IEnumerator ProcessFileLoad(int requestId, string url, int timeoutSeconds)
        {
            // Create the request
            UnityWebRequest webRequest = UnityWebRequest.Get(url);
            
            // Set timeout
            webRequest.timeout = timeoutSeconds;

            // Track the request
            _activeRequests[requestId] = webRequest;

            // Send the request
            yield return webRequest.SendWebRequest();

            // Process the response
            bool success = webRequest.result == UnityWebRequest.Result.Success;
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