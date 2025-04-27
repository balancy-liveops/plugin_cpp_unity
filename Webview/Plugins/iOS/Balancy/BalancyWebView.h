#pragma once
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

// Forward declarations
@class BalancyWebViewController;

// Callback types
typedef void (*BalancyWebViewMessageCallback)(void* context, const char* message);
typedef void (*BalancyWebViewLoadCompletedCallback)(void* context, bool success);
typedef void (*BalancyWebViewCacheCompletedCallback)(void* context, bool success);

// Web View Controller
@interface BalancyWebViewController : UIViewController <WKNavigationDelegate, WKScriptMessageHandler>

// Initialize with callbacks
- (instancetype)initWithContext:(void*)context
                 messageCallback:(BalancyWebViewMessageCallback)messageCallback
           loadCompletedCallback:(BalancyWebViewLoadCompletedCallback)loadCompletedCallback
           cacheCompletedCallback:(BalancyWebViewCacheCompletedCallback)cacheCompletedCallback;

// WebView operations
- (bool)loadURL:(NSString*)url;
- (void)setTransparent:(bool)transparent;
- (bool)sendMessage:(NSString*)message;
- (void)setOfflineCacheEnabled:(bool)enabled;
- (bool)offlineCacheEnabled;
- (void)close;

@end