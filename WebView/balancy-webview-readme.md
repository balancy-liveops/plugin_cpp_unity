# Balancy WebView for Unity

A cross-platform WebView solution for Unity that enables seamless integration of web content into your games and applications.

## Features

- **Cross-Platform**: Works on iOS and macOS (with Android and Windows support planned)
- **Easy Integration**: Simple API for Unity developers
- **Customizable Positioning**: Display WebView in full-screen or custom viewport
- **Background Transparency**: Support for transparent backgrounds
- **Two-Way Communication**: Send and receive messages between Unity and web content
- **Synchronous JavaScript Calls**: Get immediate responses from JavaScript
- **Offline Caching**: Cache web content for offline use

## Getting Started

### Installation

#### Option 1: Unity Package Manager (UPM)

1. Open the Package Manager window in Unity
2. Click the "+" button and select "Add package from git URL..."
3. Enter: `https://github.com/balancy/webview.git`

#### Option 2: Manual Installation

1. Download the latest release from the [Releases](https://github.com/balancy/webview/releases) page
2. Import the `.unitypackage` file into your Unity project

### Quick Example

```csharp
using Balancy.WebView;
using UnityEngine;

public class WebViewExample : MonoBehaviour
{
    void Start()
    {
        // Get the WebView instance
        BalancyWebView webView = BalancyWebView.Instance;
        
        // Register for events
        webView.OnMessage += HandleWebViewMessage;
        webView.OnLoadCompleted += HandleLoadCompleted;
        
        // Open a URL
        webView.OpenWebView("https://example.com");
    }
    
    private void HandleWebViewMessage(string message)
    {
        Debug.Log($"Message from WebView: {message}");
    }
    
    private void HandleLoadCompleted(bool success)
    {
        Debug.Log($"WebView loaded: {success}");
        
        // Send a message to the WebView
        if (success)
        {
            BalancyWebView.Instance.SendMessage("{\"action\":\"init\",\"data\":{\"userId\":\"12345\"}}");
        }
    }
    
    void OnDestroy()
    {
        // Clean up
        BalancyWebView webView = BalancyWebView.Instance;
        webView.OnMessage -= HandleWebViewMessage;
        webView.OnLoadCompleted -= HandleLoadCompleted;
        
        if (webView.IsWebViewOpen())
        {
            webView.CloseWebView();
        }
    }
}
```

## JavaScript Communication

The WebView automatically injects a JavaScript bridge that allows two-way communication:

```javascript
// Send a message to Unity
if (window.BalancyWebView) {
    window.BalancyWebView.postMessage(JSON.stringify({
        action: "buttonClicked",
        data: {
            buttonId: "submit",
            timestamp: Date.now()
        }
    }));
}

// Listen for messages from Unity
document.addEventListener('BalancyWebViewMessage', function(event) {
    const message = event.detail;
    console.log("Message from Unity:", message);
});
```

## Documentation

For detailed documentation, please visit the [Wiki](https://github.com/balancy/webview/wiki) or check the `Documentation` folder in the package.

## Requirements

- Unity 2019.4 or newer
- iOS 11+ for iOS support
- macOS 10.13+ for macOS support

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

If you encounter any issues or have questions, please [open an issue](https://github.com/balancy/webview/issues) on GitHub.
