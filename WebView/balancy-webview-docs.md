# Balancy WebView Plugin for Unity

## Overview

Balancy WebView is a cross-platform Unity plugin that allows developers to display web content directly within their games and applications. The plugin provides a native WebView implementation that overlays on top of the Unity game surface, with customizable positioning, transparency options, and two-way communication between Unity C# code and JavaScript.

## Key Features

- **Cross-Platform Support**: Works on iOS, macOS (with Android and Windows planned)
- **Flexible Positioning**: Display WebView in full-screen mode or at specific positions and sizes
- **Background Transparency**: Option to make the WebView background transparent
- **Two-Way Communication**: Send and receive messages between Unity and JavaScript
- **Synchronous JavaScript Callbacks**: JavaScript functions can immediately return results back to C#
- **Offline Caching**: Cache web content for offline use
- **Customizable UI**: Appearance can be customized for each platform

## Architecture

The plugin is organized into two main layers:

1. **C# Interface Layer**: Unity scripts that provide an easy-to-use API for developers
2. **Platform-Specific Implementation**: Native code written in Objective-C (iOS/macOS), Java/Kotlin (Android), or C# (Windows)

## Installation

### Method 1: Import Unity Package

1. Download the latest `.unitypackage` file from the releases page
2. Open your Unity project
3. Go to Assets > Import Package > Custom Package
4. Select the downloaded `.unitypackage` file
5. Ensure all components are selected and click "Import"

### Method 2: Manual Integration

1. Clone the repository
2. Build the native libraries using the provided build scripts
3. Copy the generated plugins and C# scripts to your Unity project

## Usage

### Basic Usage

```csharp
using Balancy.WebView;

// Get singleton instance
BalancyWebView webView = BalancyWebView.Instance;

// Register event handlers
webView.OnMessage += HandleWebViewMessage;
webView.OnLoadCompleted += HandleLoadCompleted;

// Open a URL
webView.OpenWebView("https://example.com");

// Send a message to the WebView
webView.SendMessage("{\"action\":\"setUserData\",\"userId\":\"12345\"}");

// Close the WebView when done
webView.CloseWebView();

// Event handler implementations
private void HandleWebViewMessage(string message)
{
    Debug.Log($"Received from WebView: {message}");
    // Parse JSON and handle message
}

private void HandleLoadCompleted(bool success)
{
    if (success)
        Debug.Log("WebView loaded successfully");
    else
        Debug.LogError("WebView failed to load");
}
```

### Advanced Configuration

```csharp
// Configure the WebView before opening
BalancyWebView webView = BalancyWebView.Instance;

// Enable transparent background
webView.SetTransparentBackground(true);

// Enable offline caching
webView.SetOfflineCacheEnabled(true);

// Set custom position and size (x, y, width, height as screen percentages)
// Values range from 0.0 to 1.0 representing percentage of screen dimensions
webView.SetViewportRect(0.1f, 0.1f, 0.8f, 0.6f);  // 10% from left/top, 80% width, 60% height

// Open WebView with custom configuration
webView.OpenWebView("https://example.com");
```

### JavaScript Communication

To communicate from your web page to Unity:

```javascript
// Check if the BalancyWebView bridge is available
if (window.BalancyWebView) {
    // Send a message to Unity (asynchronous)
    window.BalancyWebView.postMessage(JSON.stringify({
        action: "userAction",
        data: {
            id: "submit_form",
            timestamp: Date.now()
        }
    }));
    
    // Send a message and get immediate response (synchronous)
    const response = window.BalancyWebView.callUnity(JSON.stringify({
        action: "getData",
        key: "userProfile"
    }));
    
    // Parse the response
    const responseData = JSON.parse(response);
    console.log("Received from Unity:", responseData);
}
```

To listen for messages from Unity in your web page:

```javascript
// Listen for messages from Unity
document.addEventListener('BalancyWebViewMessage', function(event) {
    const message = event.detail;
    console.log("Message from Unity:", message);
    
    try {
        const data = JSON.parse(message);
        // Process the data
        handleUnityMessage(data);
    } catch (e) {
        console.error("Error parsing message from Unity:", e);
    }
});

function handleUnityMessage(data) {
    switch(data.action) {
        case "initialize":
            initializeApp(data.config);
            break;
        case "updateData":
            updateAppData(data.data);
            break;
        // Handle other actions
    }
}
```

## API Reference

### C# API

#### BalancyWebView Class

Main interface for interacting with the WebView. Implemented as a singleton.

| Method | Description |
|--------|-------------|
| `OpenWebView(string url)` | Opens a WebView with the specified URL |
| `CloseWebView()` | Closes the currently open WebView |
| `SendMessage(string message)` | Sends a message to the WebView JavaScript |
| `CallJavaScript(string functionName, string[] args)` | Calls a JavaScript function with the given arguments |
| `SetTransparentBackground(bool transparent)` | Enables or disables background transparency |
| `SetOfflineCacheEnabled(bool enabled)` | Enables or disables offline caching of web content |
| `SetViewportRect(float x, float y, float width, float height)` | Sets the position and size of the WebView (values from 0.0 to 1.0) |
| `SetFullScreen(bool fullScreen)` | Toggles between full-screen and windowed mode |
| `InjectCSS(string cssCode)` | Injects custom CSS into the WebView |
| `InjectJavaScript(string jsCode)` | Injects custom JavaScript into the WebView |

#### Events

| Event | Description |
|-------|-------------|
| `OnMessage` | Triggered when a message is received from the WebView |
| `OnLoadStarted` | Triggered when the WebView begins loading a page |
| `OnLoadCompleted` | Triggered when the WebView finishes loading a page |
| `OnLoadError` | Triggered when an error occurs while loading a page |
| `OnClosed` | Triggered when the WebView is closed |
| `OnJavaScriptResult` | Triggered when a result is returned from a JavaScript function call |
| `OnCacheCompleted` | Triggered when offline caching is completed |

### JavaScript API

The following JavaScript API is automatically injected into every web page loaded in the WebView:

| Method | Description |
|--------|-------------|
| `window.BalancyWebView.postMessage(message)` | Sends a message to Unity (asynchronous) |
| `window.BalancyWebView.callUnity(message)` | Sends a message to Unity and returns the result (synchronous) |

#### Events

| Event | Description |
|-------|-------------|
| `BalancyWebViewMessage` | Triggered when a message is received from Unity |

## Sample HTML Page

Below is a sample HTML page that demonstrates the WebView communication capabilities:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Balancy WebView Demo</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 20px;
            background-color: rgba(245, 245, 245, 0.8);
        }
        
        #message-log {
            border: 1px solid #ccc;
            padding: 10px;
            height: 300px;
            overflow-y: auto;
            margin-bottom: 20px;
            background-color: white;
        }
        
        .message {
            margin-bottom: 5px;
            padding: 5px;
            border-radius: 4px;
        }
        
        .from-unity {
            background-color: #e3f2fd;
            border-left: 4px solid #2196F3;
        }
        
        .to-unity {
            background-color: #f1f8e9;
            border-left: 4px solid #8bc34a;
        }
        
        .error {
            background-color: #ffebee;
            border-left: 4px solid #f44336;
        }
        
        .status {
            background-color: #fff8e1;
            border-left: 4px solid #ffc107;
        }
        
        input, button {
            padding: 8px;
            margin-right: 10px;
        }
        
        input {
            width: 60%;
        }
        
        button {
            cursor: pointer;
            background-color: #4CAF50;
            border: none;
            color: white;
            border-radius: 4px;
        }
        
        button:hover {
            background-color: #45a049;
        }
    </style>
</head>
<body>
    <h1>Balancy WebView Demo</h1>
    
    <h2>Message Log</h2>
    <div id="message-log"></div>
    
    <h2>Send Message to Unity</h2>
    <div>
        <input type="text" id="message-input" placeholder="Enter message or JSON...">
        <button id="send-button">Send</button>
        <button id="send-and-get-response-button">Send and Get Response</button>
    </div>
    
    <script>
        // DOM elements
        const messageLog = document.getElementById('message-log');
        const messageInput = document.getElementById('message-input');
        const sendButton = document.getElementById('send-button');
        const sendAndGetResponseButton = document.getElementById('send-and-get-response-button');
        
        // Log a message to the UI
        function logMessage(message, type) {
            const messageElement = document.createElement('div');
            messageElement.classList.add('message', type);
            messageElement.textContent = message;
            messageLog.appendChild(messageElement);
            messageLog.scrollTop = messageLog.scrollHeight;
        }
        
        // Log a status message
        function logStatus(status) {
            logMessage(`Status: ${status}`, 'status');
        }
        
        // Initialize
        function initialize() {
            logStatus('WebView initialized');
            
            // Check if running in Balancy WebView
            if (window.BalancyWebView) {
                logStatus('BalancyWebView bridge detected');
            } else {
                logStatus('BalancyWebView bridge not detected - running in browser mode');
            }
        }
        
        // Initialize when page loads
        window.addEventListener('load', initialize);
        
        // Note: The BalancyWebView bridge functionality is injected by the native plugin
        // and is not included directly in this HTML file
    </script>
</body>
</html>
```

## Platform-Specific Implementation Details

### iOS Implementation

The iOS implementation uses `WKWebView` from WebKit to provide a modern WebView experience.

Key features:
- JavaScript bridge through `WKUserContentController`
- JavaScript injection using `WKUserScript`
- Transparent background using proper configuration of `WKWebView` and its container view
- Two-way message communication using `postMessage` and custom URL schemes
- Offline content caching using `NSURLCache` and custom request handling

### macOS Implementation

The macOS implementation also uses `WKWebView` but with macOS-specific adaptations.

Key differences from iOS:
- Window management using `NSWindow` instead of view controllers
- Different positioning and layout constraints
- Mouse and keyboard handling specific to macOS

### Future Platforms (Android, Windows)

Android implementation will use the Android WebView component with JavaScript interfaces for communication.

Windows implementation will use the Microsoft Edge WebView2 component for modern web rendering.

## Troubleshooting

### Common Issues

1. **WebView not appearing**
   - Check if the URL is valid and accessible
   - Verify that platform-specific initialization is complete
   - Check Unity Player Settings for correct platform configuration

2. **Communication not working**
   - Ensure JavaScript bridge is properly injected
   - Check if messages are properly formatted (valid JSON)
   - Verify event handlers are registered before opening the WebView

3. **Transparent background issues**
   - Some web content may not work well with transparency
   - Ensure HTML has appropriate CSS for transparency
   - Some platforms have limitations with transparent WebViews

4. **Performance issues**
   - Heavy web content can affect game performance
   - Consider using simpler web pages or native UI for performance-critical interfaces
   - Minimize communication frequency for better performance

### Debugging Tools

1. **Debug Logging**
   - Enable verbose logging: `BalancyWebView.Instance.SetDebugLogging(true);`
   - Check Unity console for detailed logs

2. **Test HTML Page**
   - Use the provided test HTML page to verify communication is working
   - Check browser console for JavaScript errors

3. **Platform-specific debugging**
   - iOS: Use Xcode debugging tools
   - macOS: Use Safari Web Inspector
   - Android: Use Chrome remote debugging
   - Windows: Use Edge DevTools

## Build Process

### Building from Source

1. Clone the repository
2. Set up the build environment:
   - For iOS/macOS: Xcode and command-line tools
   - For Android: Android SDK/JDK
   - For Windows: Visual Studio

3. Import the plugin source into your Unity project
4. Use Unity's build system to compile the plugin for your target platform

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Credits and Acknowledgements

Developed by Balancy Team.

## Version History

- 1.0.0: Initial release with iOS and macOS support
- (Future) 1.1.0: Added Android support
- (Future) 1.2.0: Added Windows support