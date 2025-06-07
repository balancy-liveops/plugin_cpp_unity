//
//  BalancyWebView.h
//  BalancyWebView iOS Implementation
//

#import <Foundation/Foundation.h>
#import <WebKit/WebKit.h>
#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

@interface BalancyWebViewController : UIViewController <WKNavigationDelegate, WKScriptMessageHandler, WKUIDelegate>

// Properties
@property (nonatomic, strong, readonly) WKWebView *webView;
@property (nonatomic, assign) BOOL offlineCacheEnabled;
@property (nonatomic, assign) BOOL gameUIMode; // New property for game UI mode

// Initialize with context and callbacks
- (instancetype)initWithMessageCallback:(void (*)(const char*))messageCallback
                  loadCompletedCallback:(void (*)(bool))loadCompletedCallback
                  cacheCompletedCallback:(void (*)(bool))cacheCompletedCallback;

// Core functionality
- (BOOL)loadURL:(NSString *)urlString;
- (void)close;
- (BOOL)sendMessage:(NSString *)message;
- (NSString *)callJavaScript:(NSString *)function arguments:(NSArray<NSString *> *)arguments;

// Configuration
- (void)setViewportRect:(CGFloat)x y:(CGFloat)y width:(CGFloat)width height:(CGFloat)height;
- (void)setTransparentBackground:(BOOL)transparent;
- (void)setOfflineCacheEnabled:(BOOL)enabled;
- (void)setDebugLogging:(BOOL)enabled;
- (void)setGameUIMode:(BOOL)enabled; // New method to toggle game UI mode

@end

NS_ASSUME_NONNULL_END
