# Balancy WebView Request-Response Implementation Guide

This guide explains how the request-response pattern has been implemented in the Balancy WebView system for Unity (macOS runtime). This implementation allows JavaScript code in the WebView to send requests to Unity and receive responses synchronously using Promises.

## Overview

The solution uses a correlation ID pattern to implement a request-response mechanism without using JavaScript evaluation for each response. This approach:

1. Minimizes memory impact
2. Provides a clean Promise-based API in JavaScript
3. Handles errors and timeouts gracefully
4. Works alongside the existing message system

## Architecture

The implementation consists of three main parts:

### 1. JavaScript Layer

- Implements `window.BalancyWebView.sendRequest(action, params)` that returns a Promise
- Generates a unique ID for each request and stores the Promise's resolve/reject callbacks
- Sends the request to Unity with the ID, action, and parameters
- Sets up a timeout to handle abandoned requests
- Provides a response handler that resolves the appropriate Promise when a response is received

### 2. Native Bridge (macOS)

- Parses incoming messages to detect request messages
- Forwards requests to Unity
- Provides a method to send responses back to JavaScript
- Uses a shared instance pattern to allow sending responses from anywhere

### 3. C# Layer

- Provides a registry of request handlers by action type
- Processes requests by extracting parameters and calling the right handler
- Serializes responses as JSON and sends them back via the native bridge
- Handles errors and exceptions

## How It Works

### Request Flow

1. JavaScript calls `window.BalancyWebView.sendRequest(action, params)`
2. A unique ID is generated and the Promise callbacks are stored
3. A message is sent to Unity with the request details
4. The native layer receives the message and forwards it to C#
5. The C# code parses the message and identifies it as a request
6. The appropriate handler is called with the request parameters
7. The handler returns a result object
8. The result is serialized to JSON and sent back to JavaScript
9. The JavaScript response handler receives the result and resolves the Promise
10. The original caller gets the result via the Promise's `then` callback

### Example Usage

#### JavaScript Side

```javascript
// Simple request
window.BalancyWebView.sendRequest('getPlayerInfo')
  .then(playerInfo => {
    console.log('Player info:', playerInfo);
    updatePlayerUI(playerInfo);
  })
  .catch(error => {
    console.error('Failed to get player info:', error);
  });

// Request with parameters
window.BalancyWebView.sendRequest('performAction', {
  actionType: 'purchase',
  amount: 1
})
  .then(result => {
    if (result.success) {
      showSuccessMessage(result.message);
    } else {
      showErrorMessage(result.message);
    }
  })
  .catch(error => {
    showErrorMessage(error.message);
  });

// Using async/await
async function loadGameData() {
  try {
    const playerInfo = await window.BalancyWebView.sendRequest('getPlayerInfo');
    const gameState = await window.BalancyWebView.sendRequest('getGameState');
    
    // Now use both sets of data
    updateUI(playerInfo, gameState);
    return { playerInfo, gameState };
  } catch (error) {
    console.error('Failed to load game data:', error);
    return null;
  }
}
```

#### C# Side

```csharp
// In your initialization code
void InitializeWebView()
{
    BalancyWebView webView = BalancyWebView.Instance;
    
    // Register handlers for different request types
    webView.RegisterRequestHandler("getPlayerInfo", _ => {
        // Return player data (in a real game, get this from your game state)
        return new {
            id = "player123",
            name = "Test Player",
            level = 42,
            xp = 12345,
            coins = 9876,
            gems = 567
        };
    });
    
    webView.RegisterRequestHandler("performAction", parameters => {
        string actionType = parameters["actionType"]?.ToString();
        int amount = parameters["amount"]?.Value<int>() ?? 0;
        
        Debug.Log($"WebView requested action: {actionType} with amount: {amount}");
        
        // Process the action (in a real game, call your game logic)
        bool success = true;
        string message = "Action completed successfully";
        
        return new {
            success,
            message,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    });
    
    // Open the WebView
    webView.OpenWebView("file:///path/to/your/page.html");
}
```

## Advanced Features

### Timeout Handling

The JavaScript implementation includes automatic timeout handling for requests:

```javascript
// Set up timeout to clean up abandoned requests
setTimeout(() => {
    const request = pendingRequests[requestId];
    if (request) {
        delete pendingRequests[requestId];
        reject(new Error(`Request timeout: ${action}`));
    }
}, 10000); // 10 second timeout
```

This ensures that abandoned requests don't leak memory and that the Promise is rejected if Unity doesn't respond in time.

### Error Handling

Errors are properly propagated from C# to JavaScript:

1. If a handler throws an exception, it's caught and sent as an error response
2. If no handler is registered for an action, an error response is sent
3. If response serialization fails, an error is sent

On the JavaScript side, these errors cause the Promise to be rejected, allowing for proper error handling.

### Custom Request Handlers

The system allows registering custom request handlers for any action:

```csharp
// Processing complex data
webView.RegisterRequestHandler("processData", parameters => {
    // Extract data array
    JArray dataArray = parameters["data"] as JArray;
    if (dataArray == null || dataArray.Count == 0)
    {
        return new { success = false, error = "No data provided" };
    }
    
    // Process the data
    double sum = 0;
    foreach (var item in dataArray)
    {
        if (item.Type == JTokenType.Integer || item.Type == JTokenType.Float)
        {
            sum += item.Value<double>();
        }
    }
    
    double average = dataArray.Count > 0 ? sum / dataArray.Count : 0;
    
    // Return the results
    return new {
        success = true,
        results = new {
            count = dataArray.Count,
            sum,
            average,
            processed = DateTime.Now.ToString("o")
        }
    };
});
```

The handler receives the parameters as a `JObject` and can return any serializable object as the result.

## Memory Considerations

This implementation avoids JavaScript evaluation for each response, which prevents the JavaScript context from growing with each message. Instead:

1. The response handler is initialized once when the page loads
2. Responses are sent by calling this handler with the response data
3. The handler resolves the appropriate Promise and cleans up

This means the memory usage remains constant regardless of how many messages are exchanged.

## Future Improvements

1. **Platform Support**: Extend this implementation to iOS, Android, and other platforms
2. **Request Batching**: Allow sending multiple requests in a single message
3. **Streaming Responses**: Support for large responses that need to be streamed
4. **Progress Updates**: Add support for progress updates during long-running operations

## Conclusion

This implementation provides a clean, efficient way to handle request-response communication between JavaScript and Unity. It works alongside the existing message system and provides a modern, Promise-based API in JavaScript while maintaining a clean, type-safe API in C#.
