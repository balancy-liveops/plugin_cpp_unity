using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Balancy.Network
{
    public class UnityWebSocketBridge : MonoBehaviour
    {
        private static UnityWebSocketBridge _instance;
        
        // Delegate types for callbacks FROM C++ - using aliases to LibraryMethods types
        private delegate void ConnectRequestDelegate(int connectionId, string url, string authDataJson);
        private delegate void DisconnectRequestDelegate(int connectionId);
        private delegate void SubscribeEventDelegate(int connectionId, string eventName);
        private delegate void SendAckDelegate(int connectionId, int ackId, string responseData);
        private delegate void SendMessageDelegate(int connectionId, string eventName, string data);

        // Native plugin function imports - Registration
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterWSConnectRequestCallback(Balancy.LibraryMethods.WebSocket.ConnectRequestDelegate callback);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterWSDisconnectRequestCallback(Balancy.LibraryMethods.WebSocket.DisconnectRequestDelegate callback);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterWSSubscribeEventCallback(Balancy.LibraryMethods.WebSocket.SubscribeEventDelegate callback);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterWSSendAckCallback(Balancy.LibraryMethods.WebSocket.SendAckDelegate callback);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyRegisterWSSendMessageCallback(Balancy.LibraryMethods.WebSocket.SendMessageDelegate callback);

        // Native plugin function imports - Events TO C++
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyHandleWSConnectionStatusChanged(int connectionId, bool connected, string errorMessage);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyHandleWSSocketIOEvent(int connectionId, string eventName, string eventData, bool needsAck, int ackId);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyHandleWSAckResponse(int connectionId, int ackId, string responseData);
        
        [DllImport(Balancy.LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void balancyHandleWSSocketIOError(int connectionId, int errorCode, string errorMessage);

        // Active connections tracking
        private Dictionary<int, WebSocketConnection> _activeConnections = new Dictionary<int, WebSocketConnection>();
        private static UnityMainThreadDispatcher _mainThreadInstance;

        public static void Initialize()
        {
            if (_instance != null) return;

            var guid = Guid.NewGuid().ToString();
            var go = new GameObject("Balancy_WebSocketBridge_" + guid);

            if (Application.isPlaying)
            {
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
            }
            else
                go.hideFlags = HideFlags.HideAndDontSave;
            
            _instance = go.AddComponent<UnityWebSocketBridge>();
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();

            // Register C# callbacks with the native plugin
            balancyRegisterWSConnectRequestCallback(StaticOnConnectRequest);
            balancyRegisterWSDisconnectRequestCallback(StaticOnDisconnectRequest);
            balancyRegisterWSSubscribeEventCallback(StaticOnSubscribeEvent);
            balancyRegisterWSSendAckCallback(StaticOnSendAck);
            balancyRegisterWSSendMessageCallback(StaticOnSendMessage);
            
            Debug.Log("UnityWebSocketBridge initialized");
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
            
            Debug.Log("UnityWebSocketBridge cleared");
        }
        
        // Method to manually clean up resources
        private void CleanupResources()
        {
            balancyRegisterWSConnectRequestCallback(null);
            balancyRegisterWSDisconnectRequestCallback(null);
            balancyRegisterWSSubscribeEventCallback(null);
            balancyRegisterWSSendAckCallback(null);
            balancyRegisterWSSendMessageCallback(null);
            
            foreach (var connection in _activeConnections.Values)
            {
                connection.Dispose();
            }
            _activeConnections.Clear();
        }

        // Clean up resources when the application exits
        private void OnDestroy()
        {
            CleanupResources();
        }

        // Static callback handlers FROM C++
        [AOT.MonoPInvokeCallback(typeof(Balancy.LibraryMethods.WebSocket.ConnectRequestDelegate))]
        private static void StaticOnConnectRequest(int connectionId, string url, string authDataJson)
        {
            _mainThreadInstance.Enqueue(() =>
            {
                if (_instance != null)
                    _instance.OnConnectRequest(connectionId, url, authDataJson);
                else
                    Debug.LogError("UnityWebSocketBridge instance not initialized.");
            });
        }
        
        [AOT.MonoPInvokeCallback(typeof(Balancy.LibraryMethods.WebSocket.DisconnectRequestDelegate))]
        private static void StaticOnDisconnectRequest(int connectionId)
        {
            _mainThreadInstance.Enqueue(() =>
            {
                _instance?.OnDisconnectRequest(connectionId);
            });
        }

        [AOT.MonoPInvokeCallback(typeof(Balancy.LibraryMethods.WebSocket.SubscribeEventDelegate))]
        private static void StaticOnSubscribeEvent(int connectionId, string eventName)
        {
            _mainThreadInstance.Enqueue(() =>
            {
                _instance?.OnSubscribeEvent(connectionId, eventName);
            });
        }

        [AOT.MonoPInvokeCallback(typeof(Balancy.LibraryMethods.WebSocket.SendAckDelegate))]
        private static void StaticOnSendAck(int connectionId, int ackId, string responseData)
        {
            _mainThreadInstance.Enqueue(() =>
            {
                _instance?.OnSendAck(connectionId, ackId, responseData);
            });
        }
        
        [AOT.MonoPInvokeCallback(typeof(Balancy.LibraryMethods.WebSocket.SendMessageDelegate))]
        private static void StaticOnSendMessage(int connectionId, string eventName, string data)
        {
            _mainThreadInstance.Enqueue(() =>
            {
                _instance?.OnSendMessage(connectionId, eventName, data);
            });
        }

        // Implementation methods
        private async void OnConnectRequest(int connectionId, string url, string authDataJson)
        {
            try
            {
                Debug.Log($"WebSocket connect request: ID={connectionId}, URL={url}");
                
                var connection = new WebSocketConnection(connectionId, this);
                _activeConnections[connectionId] = connection;
                
                await connection.ConnectAsync(url, authDataJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebSocket connect failed: {ex.Message}");
                balancyHandleWSConnectionStatusChanged(connectionId, false, ex.Message);
            }
        }

        private void OnDisconnectRequest(int connectionId)
        {
            Debug.Log($"WebSocket disconnect request: ID={connectionId}");
            
            if (_activeConnections.TryGetValue(connectionId, out var connection))
            {
                connection.Dispose();
                _activeConnections.Remove(connectionId);
            }
        }

        private void OnSubscribeEvent(int connectionId, string eventName)
        {
            Debug.Log($"WebSocket subscribe event: ID={connectionId}, Event={eventName}");
            
            if (_activeConnections.TryGetValue(connectionId, out var connection))
            {
                connection.SubscribeToEvent(eventName);
            }
        }

        private void OnSendAck(int connectionId, int ackId, string responseData)
        {
            Debug.Log($"WebSocket send ack: ID={connectionId}, AckId={ackId}");
            
            if (_activeConnections.TryGetValue(connectionId, out var connection))
            {
                connection.SendAcknowledgment(ackId, responseData);
            }
        }
        
        private void OnSendMessage(int connectionId, string eventName, string data)
        {
            Debug.Log($"WebSocket send message: ID={connectionId}, Event={eventName}");
            
            if (_activeConnections.TryGetValue(connectionId, out var connection))
            {
                connection.SendMessage(eventName, data);
            }
        }

        // Methods to notify C++ (called by WebSocketConnection)
        public void NotifyConnectionStatusChanged(int connectionId, bool connected, string errorMessage = "")
        {
            balancyHandleWSConnectionStatusChanged(connectionId, connected, errorMessage);
        }

        public void NotifySocketIOEvent(int connectionId, string eventName, string eventData, bool needsAck, int ackId)
        {
            balancyHandleWSSocketIOEvent(connectionId, eventName, eventData, needsAck, ackId);
        }

        public void NotifyAckResponse(int connectionId, int ackId, string responseData)
        {
            balancyHandleWSAckResponse(connectionId, ackId, responseData);
        }

        public void NotifySocketIOError(int connectionId, int errorCode, string errorMessage)
        {
            balancyHandleWSSocketIOError(connectionId, errorCode, errorMessage);
        }
    }
}
