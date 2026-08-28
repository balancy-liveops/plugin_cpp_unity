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
        private static extern void balancyHandleFileLoadComplete(int requestId, bool success, int errorCode, IntPtr data, int dataSize, string contentType);

        // Active requests tracking
        private Dictionary<int, UnityWebRequest> _activeRequests = new Dictionary<int, UnityWebRequest>();
        private bool _resourcesCleaned;

        private static volatile bool _isStopped = false;
        private static int _generation;

        public static bool IsStopped => _isStopped;

#if UNITY_EDITOR
        // HttpClient for Editor mode - use a dictionary to manage different clients with different timeouts
        private static Dictionary<int, HttpClient> _httpClients = new Dictionary<int, HttpClient>();
        private static readonly object _httpClientLock = new object();
#endif
        
        private static UnityMainThreadDispatcher _mainThreadInstance;

        // Initialize the bridge
        public static void Initialize()
        {
            if (_instance != null) return;

            _isStopped = false;
            _generation++;

            var guid = Guid.NewGuid().ToString();
            var go = new GameObject("Balancy_WebRequestBridge_" + guid);

            if (Application.isPlaying)
            {
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
            }
            else
                go.hideFlags = HideFlags.HideAndDontSave;
            
            _instance = go.AddComponent<UnityWebRequestBridge>();
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();

            // Register C# callbacks with the native plugin
            balancyRegisterWebRequestCallback(StaticOnWebRequestReceived);
            balancyRegisterFileLoadCallback(StaticOnFileLoadReceived);
        }

        public static void Clear()
        {
            _isStopped = true;
            _generation++;

            if (_instance == null) return;

            _instance.StopAllCoroutines();
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
            if (_resourcesCleaned) return;
            _resourcesCleaned = true;

            InvokeNative(() => balancyRegisterWebRequestCallback(null));
            InvokeNative(() => balancyRegisterFileLoadCallback(null));
            
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
            var generation = _generation;
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                if (!_isStopped && generation == _generation && _instance != null)
                    _instance.OnWebRequestReceived(requestId, url, method, body, headersJson, timeoutSeconds);
                else if (!_isStopped && generation == _generation)
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
            var generation = _generation;
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                if (!_isStopped && generation == _generation && _instance != null)
                    _instance.OnFileLoadReceived(requestId, url, timeoutSeconds);
                else if (!_isStopped && generation == _generation)
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
            var normalizedTimeout = NormalizeHttpTimeout(timeoutSeconds);
            var cacheKey = timeoutSeconds <= 0 ? 0 : timeoutSeconds;
            lock (_httpClientLock)
            {
                // Use the timeout as a key to get a client with that timeout
                if (_httpClients.TryGetValue(cacheKey, out HttpClient client))
                {
                    return client;
                }
                
                // Create a new client with the specified timeout
                client = new HttpClient
                {
                    Timeout = normalizedTimeout
                };
                
                _httpClients[cacheKey] = client;
                return client;
            }
        }

        private static TimeSpan NormalizeHttpTimeout(int timeoutSeconds) =>
            timeoutSeconds <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(timeoutSeconds);

        // Process web request with HttpClient for Editor mode
        private async void ProcessWebRequestWithHttpClient(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds)
        {
            try
            {
                var httpClient = GetHttpClient(timeoutSeconds);
                using (var request = new HttpRequestMessage(new HttpMethod(method), url))
                {
                    if (!string.IsNullOrEmpty(body) &&
                        (method == "POST" || method == "PUT" || method == "PATCH"))
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrEmpty(headersJson))
                    {
                        Dictionary<string, string> headers = ParseHeaders(headersJson);
                        foreach (var header in headers)
                        {
                            if (string.Equals(header.Key, "content-type",
                                    StringComparison.OrdinalIgnoreCase) && request.Content != null)
                                continue;

                            if (!header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            else if (request.Content != null)
                                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    using (var response = await httpClient.SendAsync(request))
                    {
                        if (_isStopped) return;

                        byte[] data = await response.Content.ReadAsByteArrayAsync();
                        bool success = response.IsSuccessStatusCode;
                        int errorCode = (int)response.StatusCode;
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
                            if (!_isStopped)
                                InvokeNative(() => balancyHandleWebRequestComplete(requestId, success, errorCode, dataPtr, dataSize));
                        }
                        finally
                        {
                            if (dataPtr != IntPtr.Zero)
                                Marshal.FreeHGlobal(dataPtr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isStopped) return;

                Debug.LogError($"HTTP request failed: {ex.Message}");

                // Create error message as byte array
                string errorMessage = JsonUtility.ToJson(new WebRequestError { error = ex.Message });
                byte[] errorData = Encoding.UTF8.GetBytes(errorMessage);

                // Convert to native pointer
                IntPtr dataPtr = Marshal.AllocHGlobal(errorData.Length);
                Marshal.Copy(errorData, 0, dataPtr, errorData.Length);

                try
                {
                    // Send failure back to native plugin
                    if (!_isStopped)
                        InvokeNative(() => balancyHandleWebRequestComplete(requestId, false, 0, dataPtr, errorData.Length));
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
            try
            {
                var httpClient = GetHttpClient(timeoutSeconds);
                using (var response = await httpClient.GetAsync(url))
                {
                    if (_isStopped) return;

                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    bool success = response.IsSuccessStatusCode;
                    int errorCode = (int)response.StatusCode;
                    string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
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
                        if (!_isStopped)
                            InvokeNative(() => balancyHandleFileLoadComplete(requestId, success, errorCode,
                                dataPtr, dataSize, contentType));
                    }
                    finally
                    {
                        if (dataPtr != IntPtr.Zero)
                            Marshal.FreeHGlobal(dataPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isStopped) return;

                Debug.LogError($"File download failed: {ex.Message}");

                // Send failure back to native plugin
                if (!_isStopped)
                    InvokeNative(() => balancyHandleFileLoadComplete(requestId, false, 0, IntPtr.Zero, 0, ""));
            }
        }
#endif

        // Coroutine to handle a web request (for runtime)
        private IEnumerator ProcessWebRequest(int requestId, string url, string method, string body, string headersJson, int timeoutSeconds)
        {
            // Create the request
            UnityWebRequest webRequest = new UnityWebRequest(url, method);

            // Set timeout
            webRequest.timeout = NormalizeUnityTimeout(timeoutSeconds);

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
                    webRequest.Dispose();
                    ReportWebRequestFailure(requestId, ex.Message);
                    yield break;
                }
            }

            // Track the request
            _activeRequests[requestId] = webRequest;

            // Send the request
            yield return webRequest.SendWebRequest();

            if (_isStopped)
            {
                _activeRequests.Remove(requestId);
                webRequest.Dispose();
                yield break;
            }

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
            // Ensure consistency: if dataPtr is Zero, dataSize must be 0
            if (dataPtr == IntPtr.Zero)
            {
                dataSize = 0;
            }

            try
            {
                // Send the result back to the native plugin
                if (!_isStopped)
                    InvokeNative(() => balancyHandleWebRequestComplete(requestId, success, errorCode, dataPtr, dataSize));
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
        
        private static Dictionary<string, string> ParseHeaders(string json)
        {
            var headers = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return headers;

            var index = 0;
            SkipWhitespace(json, ref index);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
                return headers;

            while (true)
            {
                var key = ParseJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                Expect(json, ref index, ':');
                SkipWhitespace(json, ref index);
                var value = ParseJsonString(json, ref index);
                headers[key] = value;
                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}')) break;
                Expect(json, ref index, ',');
                SkipWhitespace(json, ref index);
            }

            SkipWhitespace(json, ref index);
            if (index != json.Length)
                throw new FormatException("Unexpected data after headers JSON object");
            return headers;
        }

        private static string ParseJsonString(string json, ref int index)
        {
            Expect(json, ref index, '"');
            var value = new StringBuilder();
            while (index < json.Length)
            {
                var character = json[index++];
                if (character == '"') return value.ToString();
                if (character != '\\')
                {
                    value.Append(character);
                    continue;
                }

                if (index >= json.Length) throw new FormatException("Incomplete JSON escape");
                switch (json[index++])
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new FormatException("Incomplete Unicode escape");
                        if (!ushort.TryParse(json.Substring(index, 4),
                                System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                            throw new FormatException("Invalid Unicode escape");
                        value.Append((char)codePoint);
                        index += 4;
                        break;
                    default: throw new FormatException("Invalid JSON escape");
                }
            }
            throw new FormatException("Unterminated JSON string");
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private static bool TryConsume(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected) return false;
            index++;
            return true;
        }

        private static void Expect(string json, ref int index, char expected)
        {
            if (!TryConsume(json, ref index, expected))
                throw new FormatException($"Expected '{expected}' at offset {index}");
        }

        private static int NormalizeUnityTimeout(int timeoutSeconds) => Math.Max(0, timeoutSeconds);

        // Coroutine to handle a file load (for runtime)
        private IEnumerator ProcessFileLoad(int requestId, string url, int timeoutSeconds)
        {
            // Create the request
            UnityWebRequest webRequest = UnityWebRequest.Get(url);
            
            // Set timeout
            webRequest.timeout = NormalizeUnityTimeout(timeoutSeconds);

            // Track the request
            _activeRequests[requestId] = webRequest;

            // Send the request
            yield return webRequest.SendWebRequest();

            if (_isStopped)
            {
                _activeRequests.Remove(requestId);
                webRequest.Dispose();
                yield break;
            }

            // Process the response
            bool success = webRequest.result == UnityWebRequest.Result.Success;
            int errorCode = (int)webRequest.responseCode;
            byte[] data = webRequest.downloadHandler.data;

            // Extract Content-Type header - this is the key part!
            string contentType = "";
            if (success && webRequest.GetResponseHeaders() != null)
            {
                webRequest.GetResponseHeaders().TryGetValue("Content-Type", out contentType);
                if (contentType == null) contentType = "";

                // Clean up content type (remove charset and other parameters)
                int semicolonIndex = contentType.IndexOf(';');
                if (semicolonIndex >= 0)
                {
                    contentType = contentType.Substring(0, semicolonIndex).Trim();
                }
            }

            // Convert data to a native pointer
            IntPtr dataPtr = IntPtr.Zero;
            int dataSize = 0;

            if (data != null && data.Length > 0)
            {
                dataSize = data.Length;
                dataPtr = Marshal.AllocHGlobal(dataSize);
                Marshal.Copy(data, 0, dataPtr, dataSize);
            }
            // Ensure consistency: if dataPtr is Zero, dataSize must be 0
            if (dataPtr == IntPtr.Zero)
            {
                dataSize = 0;
            }

            try
            {
                // Send the result back to the native plugin
                if (!_isStopped)
                    InvokeNative(() => balancyHandleFileLoadComplete(requestId, success, errorCode, dataPtr, dataSize, contentType));
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

        private static void InvokeNative(Action callback)
        {
            try { callback(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ReportWebRequestFailure(int requestId, string message)
        {
            if (_isStopped) return;
            var errorData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                new WebRequestError { error = message ?? "Request failed" }));
            var dataPtr = Marshal.AllocHGlobal(errorData.Length);
            try
            {
                Marshal.Copy(errorData, 0, dataPtr, errorData.Length);
                if (!_isStopped)
                    InvokeNative(() => balancyHandleWebRequestComplete(
                        requestId, false, 0, dataPtr, errorData.Length));
            }
            finally
            {
                Marshal.FreeHGlobal(dataPtr);
            }
        }

        [Serializable]
        private class WebRequestError
        {
            public string error;
        }
    }
}
