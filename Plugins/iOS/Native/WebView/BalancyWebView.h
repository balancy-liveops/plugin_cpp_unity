// BalancyWebView.h
#pragma once
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

// C callback function types
typedef void (*MessageCallbackFunc)(void* context, const char* message);
typedef void (*LoadCompletedCallbackFunc)(void* context, bool success);
typedef void (*CacheCompletedCallbackFunc)(void* context, bool success);

// Protocol for WebView delegate
@protocol BalancyWebViewMessageDelegate <NSObject>
- (void)webViewDidReceiveMessage:(NSString*)message;
- (void)webViewDidFinishLoad:(BOOL)success;
- (void)webViewDidFinishCaching:(BOOL)success;
@end

// WebView delegate implementation
@interface BalancyWebViewDelegate : NSObject <WKNavigationDelegate, WKScriptMessageHandler>

@property (nonatomic, weak) id<BalancyWebViewMessageDelegate> messageDelegate;
@property (nonatomic, assign) void* context;
@property (nonatomic, assign) MessageCallbackFunc messageCallback;
@property (nonatomic, assign) LoadCompletedCallbackFunc loadCompletedCallback;
@property (nonatomic, assign) CacheCompletedCallbackFunc cacheCompletedCallback;

@end

// WebView controller
@interface BalancyWebViewController : UIViewController <BalancyWebViewMessageDelegate>

@property (nonatomic, strong) WKWebView* webView;
@property (nonatomic, strong) BalancyWebViewDelegate* webViewDelegate;
@property (nonatomic, assign) BOOL offlineCacheEnabled;
@property (nonatomic, copy) void (^unityMessageHandler)(NSString* message);

- (instancetype)initWithContext:(void*)context 
                messageCallback:(MessageCallbackFunc)messageCallback
         loadCompletedCallback:(LoadCompletedCallbackFunc)loadCompletedCallback
         cacheCompletedCallback:(CacheCompletedCallbackFunc)cacheCompletedCallback;

- (BOOL)loadURL:(NSString*)urlString;
- (BOOL)sendMessage:(NSString*)message;
- (void)setOfflineCacheEnabled:(BOOL)enabled;
- (void)close;
- (void)setUnityMessageHandler:(void (^)(NSString* message))handler;

@end

// Unity bridge
@interface BalancyWebViewUnityBridge : NSObject

+ (bool)openWebView:(NSString*)url width:(int)width height:(int)height;
+ (void)closeWebView;
+ (bool)sendMessage:(NSString*)message;
+ (void)setOfflineCacheEnabled:(bool)enabled;

@end