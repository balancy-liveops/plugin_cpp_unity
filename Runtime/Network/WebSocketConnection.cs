using System;
using System.Collections.Generic;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Balancy.Network
{
    // Helper classes for JsonUtility parsing
    [System.Serializable]
    public class SocketIOAuthData
    {
        public string token;
        public string userId;
        public string gameId;
        public int environment;
        public string deviceId;
    }

    // Simple JSON array parser for Socket.IO messages
    public static class SimpleJsonParser
    {
        public static string[] ParseSocketIOEvent(string jsonArray)
        {
            // Parse: ["eventName", data, ackId]
            // Remove brackets and split by comma, handling nested objects
            
            if (!jsonArray.StartsWith("[") || !jsonArray.EndsWith("]"))
                return null;
                
            string content = jsonArray.Substring(1, jsonArray.Length - 2);
            var parts = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            bool inString = false;
            bool escapeNext = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                
                if (escapeNext)
                {
                    current.Append(c);
                    escapeNext = false;
                    continue;
                }
                
                if (c == '\\')
                {
                    escapeNext = true;
                    current.Append(c);
                    continue;
                }
                
                if (c == '"')
                {
                    inString = !inString;
                    current.Append(c);
                    continue;
                }
                
                if (!inString)
                {
                    if (c == '{' || c == '[')
                    {
                        depth++;
                    }
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        parts.Add(current.ToString().Trim());
                        current.Clear();
                        continue;
                    }
                }
                
                current.Append(c);
            }
            
            if (current.Length > 0)
            {
                parts.Add(current.ToString().Trim());
            }
            
            return parts.ToArray();
        }
        
        public static string UnquoteString(string quotedString)
        {
            if (quotedString.StartsWith("\"") && quotedString.EndsWith("\""))
            {
                return quotedString.Substring(1, quotedString.Length - 2);
            }
            return quotedString;
        }
        
        public static bool TryParseInt(string str, out int result)
        {
            return int.TryParse(str, out result);
        }
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    public class WebSocketConnection : IDisposable
    {
        private readonly int _connectionId;
        private readonly UnityWebSocketBridge _bridge;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;
        private bool _disposed;
        private string _originalUrl;
        private string _originalAuthData;
        private SocketIOAuthData _authData;
        private bool _handshakeCompleted;
        private Task _receiveLoopTask;
        private Task _healthCheckLoopTask;
        
        // Socket.IO specific
        private HashSet<string> _subscribedEvents = new HashSet<string>();
        private Dictionary<int, TaskCompletionSource<string>> _pendingAcks = new Dictionary<int, TaskCompletionSource<string>>();
        private int _nextAckId = 1;
        private DateTime _lastPing = DateTime.UtcNow;
        private const int PING_INTERVAL_MS = 25000; // 25 seconds
        private const int PING_TIMEOUT_MS = 5000;   // 5 seconds
        private const int RECONNECT_DELAY_MS = 5000; // 5 seconds
        private const int MAX_RECONNECT_ATTEMPTS = 50; // Maximum reconnection attempts
        private int _reconnectAttempts = 0;
        private bool _isReconnecting = false;

        public WebSocketConnection(int connectionId, UnityWebSocketBridge bridge)
        {
            _connectionId = connectionId;
            _bridge = bridge;
        }

        public async Task ConnectAsync(string url, string authDataJson)
        {
            if (_disposed) return;

            try
            {
                // FIXED: Properly cleanup any existing connection first
                if (_webSocket != null)
                {
                    Debug.Log($"Cleaning up existing WebSocket before new connection (ID: {_connectionId})");
                    try
                    {
                        _cancellationTokenSource?.Cancel();
                        
                        // Wait for background tasks to complete
                        if (_receiveLoopTask != null)
                        {
                            await Task.WhenAny(_receiveLoopTask, Task.Delay(2000));
                        }
                        if (_healthCheckLoopTask != null)
                        {
                            await Task.WhenAny(_healthCheckLoopTask, Task.Delay(2000));
                        }
                        
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                        }
                        
                        _webSocket.Dispose();
                        _cancellationTokenSource?.Dispose();
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.LogWarning($"Error during pre-connection cleanup (ID: {_connectionId}): {cleanupEx.Message}");
                    }
                }
                
                _originalUrl = url;
                _originalAuthData = authDataJson;
                _handshakeCompleted = false;
                _isConnected = false;
                
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                // Convert HTTP URL to WebSocket URL if needed
                string wsUrl = url;
                if (url.StartsWith("http://"))
                    wsUrl = url.Replace("http://", "ws://");
                else if (url.StartsWith("https://"))
                    wsUrl = url.Replace("https://", "wss://");

                // Parse auth data first
                if (!string.IsNullOrEmpty(authDataJson))
                {
                    try
                    {
                        _authData = JsonUtility.FromJson<SocketIOAuthData>(authDataJson);
                        Debug.Log($"🔍 Parsed auth data: gameId='{_authData.gameId}', env={_authData.environment}, userId='{_authData.userId}', hasToken={!string.IsNullOrEmpty(_authData.token)}");
                        
                        // Validate UUID format
                        if (string.IsNullOrEmpty(_authData.gameId))
                        {
                            Debug.LogError("🚨 gameId is empty!");
                            _bridge?.NotifyConnectionStatusChanged(_connectionId, false, "gameId is empty", this);
                            return;
                        }
                        else if (_authData.gameId.Length != 36 || !_authData.gameId.Contains("-"))
                        {
                            Debug.LogError($"🚨 gameId '{_authData.gameId}' is not a valid UUID format (should be 36 chars with dashes)!");
                            _bridge?.NotifyConnectionStatusChanged(_connectionId, false, $"gameId '{_authData.gameId}' is not a valid UUID format", this);
                            return;
                        }
                        
                        // Validate environment
                        if (_authData.environment < 0 || _authData.environment > 2)
                        {
                            Debug.LogError($"🚨 environment '{_authData.environment}' is invalid (should be 0, 1, or 2)!");
                            _bridge?.NotifyConnectionStatusChanged(_connectionId, false, $"environment '{_authData.environment}' is invalid", this);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to parse auth data: {ex.Message}");
                        _bridge?.NotifyConnectionStatusChanged(_connectionId, false, "Invalid auth data: " + ex.Message, this);
                        return;
                    }
                }

                // Build Socket.IO URL EXACTLY like C++ does
                var uriBuilder = new UriBuilder(wsUrl);
                if (!uriBuilder.Path.EndsWith("/"))
                    uriBuilder.Path += "/";
                uriBuilder.Path += "socket.io/";
                
                // Build Socket.IO URL WITHOUT auth data (only basic Socket.IO parameters)
                var queryParams = new List<string>
                {
                    "EIO=4",
                    "transport=websocket"
                };
                
                uriBuilder.Query = string.Join("&", queryParams);

                // Remove Authorization header as it might conflict with Socket.IO auth
                // (Socket.IO expects auth data in specific auth message, not in headers)

                Debug.Log($"Connecting to WebSocket: {uriBuilder.Uri}");
                await _webSocket.ConnectAsync(uriBuilder.Uri, _cancellationTokenSource.Token);
                
                _isConnected = true;
                _reconnectAttempts = 0; // Reset reconnection counter on successful connection
                _bridge?.NotifyConnectionStatusChanged(_connectionId, true, "", this);

                // Start background tasks and track them
                _receiveLoopTask = Task.Run(ReceiveLoop);
                _healthCheckLoopTask = Task.Run(HealthCheckLoop);

                Debug.Log($"WebSocket connected successfully (ID: {_connectionId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebSocket connection failed (ID: {_connectionId}): {ex.Message}");
                _bridge?.NotifyConnectionStatusChanged(_connectionId, false, ex.Message, this);
                
                // Only attempt to reconnect if not already in reconnection loop
                if (!_isReconnecting)
                {
                    await AttemptReconnect(ex.Message);
                }
                else
                {
                    // Re-throw to let AttemptReconnect handle it
                    throw;
                }
            }
        }

        public void SubscribeToEvent(string eventName)
        {
            _subscribedEvents.Add(eventName);
            Debug.Log($"Subscribed to event: {eventName} (ID: {_connectionId})");
        }

        public async void SendMessage(string eventName, string data)
        {
            if (!_isConnected || _disposed || !_handshakeCompleted) return;

            try
            {
                // Socket.IO event format: "42[eventName,data]"
                string message = $"42[\"{eventName}\",{data}]";
                await SendRawMessage(message);
                Debug.Log($"Sent message: {eventName} (ID: {_connectionId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send message: {ex.Message}");
            }
        }

        public async void SendAcknowledgment(int ackId, string responseData)
        {
            if (!_isConnected || _disposed || !_handshakeCompleted) return;

            try
            {
                // Socket.IO v4 ACK format: 43<ackId><data>
                // responseData should already be a JSON array
                string message = $"43{ackId}{responseData}";
                await SendRawMessage(message);
                Debug.Log($"Sent acknowledgment for ackId: {ackId} (ID: {_connectionId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send acknowledgment: {ex.Message}");
            }
        }

        private async Task SendRawMessage(string message)
        {
            if (!_isConnected || _disposed) return;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource.Token
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send raw message: {ex.Message}");
                await HandleDisconnection("Send message failed: " + ex.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];

            while (_isConnected && !_disposed && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationTokenSource.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleDisconnection("Connection closed by server");
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WebSocket receive error (ID: {_connectionId}): {ex.Message}");
                    _bridge?.NotifySocketIOError(_connectionId, -1, ex.Message, this);
                    await HandleDisconnection("Receive error: " + ex.Message);
                    break;
                }
            }
        }

        private async Task ProcessMessage(string message)
        {
            try
            {
                Debug.Log($"Received message (ID: {_connectionId}): {message}");
                
                // Socket.IO message format parsing
                if (message.StartsWith("0"))
                {
                    // Connection message: "0{\"sid\":\"...\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":5000}"
                    Debug.Log($"Socket.IO connected (ID: {_connectionId})");
                    
                    // FIXED: Send CONNECT with auth data instead of simple "40"
                    await SendConnectWithAuth();
                    return;
                }
                else if (message.StartsWith("2"))
                {
                    // Ping message
                    await SendRawMessage("3"); // Pong response
                    _lastPing = DateTime.UtcNow; // Update ping time when we respond
                    Debug.Log($"Ping received, sent pong (ID: {_connectionId})");
                    return;
                }
                else if (message.StartsWith("3"))
                {
                    // Pong message (response to our ping)
                    _lastPing = DateTime.UtcNow;
                    Debug.Log($"Pong received (ID: {_connectionId})");
                    return;
                }
                else if (message.StartsWith("40"))
                {
                    // CONNECT acknowledgment from server
                    Debug.Log($"Server acknowledged connection (ID: {_connectionId})");
                    _handshakeCompleted = true;
                    return;
                }
                else if (message.StartsWith("41"))
                {
                    // DISCONNECT message from server
                    Debug.LogWarning($"Server sent disconnect (ID: {_connectionId}): {message}");
                    await HandleDisconnection("Server initiated disconnect: " + message);
                    return;
                }
                else if (message.StartsWith("42"))
                {
                    // Event message: "42[eventName,data]" or "4234[eventName,data]" (with ack ID)
                    string afterType = message.Substring(2);
                    
                    // Check if there's an ack ID after "42"
                    int ackId = 0;
                    bool hasAckId = false;
                    string jsonPart = afterType;
                    
                    // Find where the JSON array starts
                    int jsonStart = afterType.IndexOf('[');
                    if (jsonStart > 0)
                    {
                        // There's an ack ID before the JSON
                        string ackIdStr = afterType.Substring(0, jsonStart);
                        if (int.TryParse(ackIdStr, out ackId))
                        {
                            hasAckId = true;
                            jsonPart = afterType.Substring(jsonStart); // Start from '['
                            Debug.Log($"📧 Parsed ack ID: {ackId} from message: {message} (ID: {_connectionId})");
                        }
                    }
                    
                    Debug.Log($"jsonPart: {jsonPart} (ID: {_connectionId})");
                    var eventParts = SimpleJsonParser.ParseSocketIOEvent(jsonPart);
                    
                    if (eventParts != null && eventParts.Length >= 1)
                    {
                        string eventName = SimpleJsonParser.UnquoteString(eventParts[0]);
                        string eventData = eventParts.Length > 1 ? eventParts[1] : "{}";
                        
                        Debug.Log($"📧 Event: {eventName}, Data: {eventData}, AckId: {(hasAckId ? ackId.ToString() : "none")} (ID: {_connectionId})");
                        
                        // Handle auth:token specially - setup subscriptions after successful auth
                        if (eventName == "auth:token" && eventData.Contains("\"success\":true"))
                        {
                            Debug.Log($"🔑 Authentication successful! Setting up event subscriptions (ID: {_connectionId})");
                            
                            // Subscribe to key events like C++ code does
                            SubscribeToEvent("system:connection");
                            SubscribeToEvent("system:ping");
                            SubscribeToEvent("profile:updated");
                            
                            // Send acknowledgment if needed
                            if (hasAckId)
                            {
                                // Socket.IO v4 ACK format: 43<ackId><data>
                                await SendRawMessage($"43{ackId}[{{\"received\":true}}]");
                                Debug.Log($"Sent auth:token ack response (ID: {_connectionId})");
                            }
                            return;
                        }
                        
                        // Handle system:ping specially - handle COMPLETELY inline like C++ SocketIOWebSocketHandler does
                        if (eventName == "system:ping")
                        {
                            _lastPing = DateTime.UtcNow;
                            
                            if (hasAckId)
                            {
                                await SendRawMessage($"43{ackId}[]");
                            }
                            return;
                        }
                        
                        // Handle profile:updated specially - send ACK inline then notify C++ WITHOUT ack requirement
                        if (eventName == "profile:updated")
                        {
                            // Send acknowledgment IMMEDIATELY inline, BEFORE notifying C++
                            if (hasAckId)
                            {
                                await SendRawMessage($"43{ackId}[]");
                            }
                            
                            // Now notify C++ WITHOUT ack requirement (needsAck=false, ackId=0)
                            _bridge?.NotifySocketIOEvent(_connectionId, eventName, eventData, false, 0, this);
                            return;
                        }
                        
                        // For other events, check subscription
                        if (_subscribedEvents.Contains(eventName) || _subscribedEvents.Count == 0)
                        {
                            _bridge?.NotifySocketIOEvent(_connectionId, eventName, eventData, hasAckId, ackId, this);
                        }
                        else
                        {
                            Debug.Log($"Ignoring unsubscribed event: {eventName} (ID: {_connectionId})");
                            
                            // Still send ack if needed to prevent server disconnect
                            if (hasAckId)
                            {
                                // Socket.IO v4 ACK format: 43<ackId><data>
                                await SendRawMessage($"43{ackId}[]");
                                Debug.Log($"Sent automatic ack for ignored event: {eventName} (ID: {_connectionId})");
                            }
                        }
                    }
                }
                else if (message.StartsWith("43"))
                {
                    // Ack response: "43[ackId,responseData]"
                    string jsonPart = message.Substring(2);
                    var ackParts = SimpleJsonParser.ParseSocketIOEvent(jsonPart);
                    
                    if (ackParts != null && ackParts.Length >= 2)
                    {
                        if (SimpleJsonParser.TryParseInt(ackParts[0], out int ackId))
                        {
                            string responseData = ackParts[1];
                            _bridge?.NotifyAckResponse(_connectionId, ackId, responseData, this);
                        }
                    }
                }
                else if (message.StartsWith("44"))
                {
                    // ERROR message: "44{\"message\":\"...\",\"data\":{...}}"
                    string errorJson = message.Substring(2);
                    Debug.LogError($"🚨 Socket.IO Error (ID: {_connectionId}): {errorJson}");
                    
                    try
                    {
                        // Try to parse error details
                        var errorData = JsonUtility.FromJson<SocketIOError>(errorJson);
                        if (errorData != null)
                        {
                            Debug.LogError($"🚨 Error Code: {errorData.data?.code}, Message: {errorData.message}");
                            
                            // If it's a Bad handshake error, log additional details
                            if (errorData.data?.code == 1002)
                            {
                                Debug.LogError($"🚨 Bad handshake error detected. Check auth parameters:");
                                Debug.LogError($"   - gameId: '{_authData?.gameId}' (should be UUID)");
                                Debug.LogError($"   - env: {_authData?.environment} (should be 0, 1, or 2)");
                                Debug.LogError($"   - userId: '{_authData?.userId}'");
                                Debug.LogError($"   - token: '{(_authData?.token?.Length > 10 ? _authData.token.Substring(0, 10) + "..." : _authData?.token)}'");
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError($"Failed to parse error details: {parseEx.Message}");
                    }
                    
                    // Notify bridge about the error
                    _bridge?.NotifySocketIOError(_connectionId, 44, errorJson, this);
                    await HandleDisconnection("Socket.IO Error: " + errorJson);
                    return;
                }
                else
                {
                    Debug.Log($"Unknown message type (ID: {_connectionId}): {message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to process message '{message}' (ID: {_connectionId}): {ex.Message}");
            }
        }

        private async Task HealthCheckLoop()
        {
            while (_isConnected && !_disposed)
            {
                try
                {
                    await Task.Delay(PING_INTERVAL_MS, _cancellationTokenSource.Token);
                    
                    // FIXED: Don't send manual pings - just monitor server activity like C++ does
                    if (_isConnected && !_disposed && _handshakeCompleted)
                    {
                        var timeSinceLastActivity = (DateTime.UtcNow - _lastPing).TotalMilliseconds;
                        
                        // Just monitor health like C++ CheckConnectionHealth() does
                        if (timeSinceLastActivity > 60000) // 60 seconds like C++
                        {
                            Debug.LogWarning($"No server activity for {timeSinceLastActivity}ms - connection might be lost (ID: {_connectionId})");
                            await HandleDisconnection("No server activity - connection health check failed");
                            break;
                        }
                        else if (timeSinceLastActivity > 35000) // 35 seconds like C++
                        {
                            Debug.LogWarning($"Server activity delay detected: {timeSinceLastActivity}ms since last activity (ID: {_connectionId})");
                        }
                        else
                        {
                            Debug.Log($"Connection healthy - last server activity: {timeSinceLastActivity}ms ago (ID: {_connectionId})");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Health check error (ID: {_connectionId}): {ex.Message}");
                    break;
                }
            }
        }

        private async Task HandleDisconnection(string reason = "")
        {
            if (!_isConnected) return;

            _isConnected = false;
            _handshakeCompleted = false;
            Debug.Log($"WebSocket disconnected (ID: {_connectionId}): {reason}");
            _bridge?.NotifyConnectionStatusChanged(_connectionId, false, reason, this);

            // FIXED: Properly cleanup before reconnecting
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                }
                
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error during cleanup (ID: {_connectionId}): {ex.Message}");
            }

            // Attempt to reconnect
            await AttemptReconnect(reason);
        }

        private async Task AttemptReconnect(string reason)
        {
            if (_disposed || _isReconnecting)
                return;

            _isReconnecting = true;

            try
            {
                while (_reconnectAttempts < MAX_RECONNECT_ATTEMPTS && !_disposed)
                {
                    _reconnectAttempts++;
                    
                    // Calculate exponential backoff delay: min(1000 * 2^attempt, 10000)
                    int delay = Math.Min(RECONNECT_DELAY_MS * (int)Math.Pow(2, Math.Min(_reconnectAttempts - 1, 3)), 10000);
                    
                    Debug.Log($"Reconnection attempt #{_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS} after {delay}ms delay (ID: {_connectionId})");
                    await Task.Delay(delay);
                    
                    if (_disposed)
                        break;

                    try
                    {
                        await ConnectAsync(_originalUrl, _originalAuthData);
                        
                        // If we reach here without exception, connection was successful
                        if (_isConnected)
                        {
                            Debug.Log($"Reconnection successful after {_reconnectAttempts} attempts (ID: {_connectionId})");
                            _reconnectAttempts = 0;
                            _isReconnecting = false;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Reconnection attempt #{_reconnectAttempts} failed (ID: {_connectionId}): {ex.Message}");
                        // Continue to next attempt
                    }
                }

                if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
                {
                    Debug.LogError($"Max reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached. Giving up (ID: {_connectionId})");
                    _bridge?.NotifyConnectionStatusChanged(_connectionId, false, $"Max reconnection attempts reached: {reason}", this);
                }
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        private async Task SendConnectWithAuth()
        {
            if (_authData == null)
            {
                // Send simple connect if no auth data
                await SendRawMessage("40");
                Debug.Log($"Sent simple connect without auth (ID: {_connectionId})");
                return;
            }

            try
            {
                // Socket.IO v4 CONNECT with auth data format: "40{\"auth\":{...}}"
                // This matches the C++ implementation which sends auth data in the auth object
                // Use same field names as C++ code (snake_case)
                var authJson = "{"
                    + $"\"game_id\":\"{_authData.gameId}\","
                    + $"\"user_id\":\"{_authData.userId}\","
                    + $"\"env\":{_authData.environment},"
                    + $"\"token\":\"{_authData.token}\"";
                
                // Add device_id only if it's provided
                if (!string.IsNullOrEmpty(_authData.deviceId))
                {
                    authJson += $",\"device_id\":\"{_authData.deviceId}\"";
                }
                
                authJson += "}";

                Debug.Log($"🔐 Sending connect with auth (ID: {_connectionId}, userId='{_authData.userId}')");
                
                // Try both formats - first without "auth" wrapper
                string connectMessage = $"40{authJson}";
                await SendRawMessage(connectMessage);
                Debug.Log($"Connect with auth sent (without auth wrapper) (ID: {_connectionId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send connect with auth (ID: {_connectionId}): {ex.Message}");
                // Fallback to simple connect
                await SendRawMessage("40");
                Debug.Log($"Sent fallback simple connect (ID: {_connectionId})");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _isConnected = false;
            _handshakeCompleted = false;
            _isReconnecting = false;
            _reconnectAttempts = 0;

            _cancellationTokenSource?.Cancel();
            
            try
            {
                _webSocket?.Abort();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error during WebSocket close (ID: {_connectionId}): {ex.Message}");
            }
            
            _webSocket?.Dispose();
            _cancellationTokenSource?.Dispose();

            _subscribedEvents.Clear();
            _pendingAcks.Clear();

            Debug.Log($"WebSocket connection disposed (ID: {_connectionId})");
        }
    }

    // Helper classes for parsing Socket.IO error messages
    [System.Serializable]
    public class SocketIOError
    {
        public string message;
        public SocketIOErrorData data;
    }

    [System.Serializable]
    public class SocketIOErrorData
    {
        public int code;
        public string message;
        public SocketIOErrorMeta[] meta;
    }

    [System.Serializable]
    public class SocketIOErrorMeta
    {
        public string property;
        public SocketIOErrorConstraints constraints;
    }

    [System.Serializable]
    public class SocketIOErrorConstraints
    {
        public string isUuid;
        public string isEnum;
        public string isNumber;
    }
#else
    // WebGL builds use EmscriptenWebSocketHandler (C++ -> JavaScript bridge)
    // This stub prevents compilation errors but won't be used
    public class WebSocketConnection : IDisposable
    {
        public WebSocketConnection(int connectionId, UnityWebSocketBridge bridge) { }
        public void Dispose() { }
    }
#endif
}
