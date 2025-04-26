// WebViewPlugin.h
#pragma once

#ifdef __cplusplus
extern "C" {
#endif

// Create an offscreen WebView and return its pointer
void* _CreateOffscreenWebView(const char* initialUrl, int width, int height);

// Close and cleanup WebView
void _CloseWebView(void* webViewPtr);

// Load a URL in the WebView
bool _LoadURL(void* webViewPtr, const char* url);

// Execute JavaScript in the WebView
bool _ExecuteJavaScript(void* webViewPtr, const char* script);

// Get the current texture data (RGBA bytes)
void _GetTextureData(void* webViewPtr, unsigned char* buffer);

// Handle input events (mouse/keyboard)
void _SendMouseEvent(void* webViewPtr, int eventType, float x, float y);
void _SendKeyEvent(void* webViewPtr, int keyCode, bool isKeyDown);

// Set a JavaScript message handler callback
void _SetJSMessageCallback(void* webViewPtr, void (*callback)(const char* message));

// Trigger a layout/redraw and check if the texture changed
bool _UpdateTexture(void* webViewPtr);

// Check if content has changed since last update
bool _HasContentChanged(void* webViewPtr);

#ifdef __cplusplus
}
#endif