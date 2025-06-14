//
//  BalancyWebviewMac.h
//  Native macOS WebView implementation for Unity
//

#import <Foundation/Foundation.h>
#import <WebKit/WebKit.h>
#import <Cocoa/Cocoa.h>

#ifdef __cplusplus
extern "C" {
#endif

// Core functionality
bool _balancyOpenWebView(const char* url);
void _balancyCloseWebView();
bool _balancySendMessage(const char* message);
bool _balancyInjectJSCode(const char* message);
const char* _balancyCallJavaScript(const char* function, const char** args, int argsCount);

// Configuration
void _balancySetViewportRect(float x, float y, float width, float height);
void _balancySetTransparentBackground(bool transparent);
void _balancySetOfflineCacheEnabled(bool enabled);
void _balancySetDebugLogging(bool enabled);

// Callback registration
typedef void (*MessageCallback)(const char* message);
typedef void (*LoadCompletedCallback)(bool success);
typedef void (*CacheCompletedCallback)(bool success);

void _balancyRegisterMessageCallback(MessageCallback callback);
void _balancyRegisterLoadCompletedCallback(LoadCompletedCallback callback);
void _balancyRegisterCacheCompletedCallback(CacheCompletedCallback callback);

// Embedding functionality (Unity Editor only) - OPTIMIZED VERSION
bool _balancyOpenWebViewEmbedded(const char* url, int width, int height);
void _balancyCloseWebViewEmbedded();
void _balancyUpdateEmbeddedTexture(int width, int height);
bool _balancySendMouseEvent(int x, int y, const char* eventType);
bool _balancySendScrollEvent(int x, int y, float deltaX, float deltaY);
bool _balancyGetEmbeddedPixelData(unsigned char* buffer, int bufferSize);

// Emergency Exit functionality
void _balancySetEmergencyExitEnabled(bool enabled);

#ifdef __cplusplus
}
#endif