// BalancyWebView.mm
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>
#import "BalancyWebView.h"

// JavaScript interface code that will be injected into each webpage
NSString* const kBalancyJSInterface = @"window.BalancyWebView = {"
                                    "  postMessage: function(message) {"
                                    "    window.webkit.messageHandlers.balancyMessageHandler.postMessage(message);"
                                    "  }"
                                    "};";

// WebView delegate implementation
@implementation BalancyWebViewDelegate {
    BOOL _isInitialLoad;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _isInitialLoad = YES;
    }
    return self;
}

#pragma mark - WKNavigationDelegate

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    // Inject our JavaScript interface
    [webView evaluateJavaScript:kBalancyJSInterface completionHandler:^(id _Nullable result, NSError * _Nullable error) {
        if (error) {
            NSLog(@"Failed to inject BalancyWebView JS interface: %@", error);
        }
    }];
    
    // Notify load completion
    if (self.messageDelegate) {
        [self.messageDelegate webViewDidFinishLoad:YES];
    }
    
    if (self.loadCompletedCallback) {
        self.loadCompletedCallback(self.context, true);
    }
    
    // Check resources for caching if this is the initial load
    if (_isInitialLoad) {
        _isInitialLoad = NO;
        
        // In a real implementation, you would check the cache status
        // For now, we'll just simulate a successful cache completion
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            if (self.messageDelegate) {
                [self.messageDelegate webViewDidFinishCaching:YES];
            }
            
            if (self.cacheCompletedCallback) {
                self.cacheCompletedCallback(self.context, true);
            }
        });
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSLog(@"WebView navigation failed: %@", error);
    
    if (self.messageDelegate) {
        [self.messageDelegate webViewDidFinishLoad:NO];
    }
    
    if (self.loadCompletedCallback) {
        self.loadCompletedCallback(self.context, false);
    }
}

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    if ([message.name isEqualToString:@"balancyMessageHandler"]) {
        // Convert the message to a string
        NSString *messageString = nil;
        
        if ([message.body isKindOfClass:[NSString class]]) {
            messageString = (NSString *)message.body;
        } else {
            // Try to convert to JSON
            NSError *error = nil;
            NSData *jsonData = [NSJSONSerialization dataWithJSONObject:message.body options:0 error:&error];
            
            if (!error && jsonData) {
                messageString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
            } else {
                messageString = [NSString stringWithFormat:@"%@", message.body];
            }
        }
        
        // Forward the message to the delegate
        if (self.messageDelegate) {
            [self.messageDelegate webViewDidReceiveMessage:messageString];
        }
        
        // Call the C++ callback if set
        if (self.messageCallback && messageString) {
            self.messageCallback(self.context, [messageString UTF8String]);
        }
    }
}

@end

// WebView controller implementation
@implementation BalancyWebViewController {
    void* _context;
    MessageCallbackFunc _messageCallback;
    LoadCompletedCallbackFunc _loadCompletedCallback;
    CacheCompletedCallbackFunc _cacheCompletedCallback;
}

#pragma mark - BalancyWebViewMessageDelegate

- (void)webViewDidReceiveMessage:(NSString*)message {
    NSLog(@"WebView received message: %@", message);
    
    // Forward to Unity message handler if set
    if (self.unityMessageHandler) {
        self.unityMessageHandler(message);
    }
}

- (void)webViewDidFinishLoad:(BOOL)success {
    NSLog(@"WebView did finish load: %@", success ? @"YES" : @"NO");
}

- (void)webViewDidFinishCaching:(BOOL)success {
    NSLog(@"WebView did finish caching: %@", success ? @"YES" : @"NO");
}

- (instancetype)initWithContext:(void*)context 
                messageCallback:(MessageCallbackFunc)messageCallback
         loadCompletedCallback:(LoadCompletedCallbackFunc)loadCompletedCallback
         cacheCompletedCallback:(CacheCompletedCallbackFunc)cacheCompletedCallback {
    
    self = [super init];
    if (self) {
        _context = context;
        _messageCallback = messageCallback;
        _loadCompletedCallback = loadCompletedCallback;
        _cacheCompletedCallback = cacheCompletedCallback;
        _offlineCacheEnabled = NO;
        
        [self setupWebView];
    }
    return self;
}

- (void)setupWebView {
    // Create WKWebView configuration
    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    WKUserContentController *userContentController = [[WKUserContentController alloc] init];
    
    // Set up JavaScript message handler
    self.webViewDelegate = [[BalancyWebViewDelegate alloc] init];
    self.webViewDelegate.context = _context;
    self.webViewDelegate.messageCallback = _messageCallback;
    self.webViewDelegate.loadCompletedCallback = _loadCompletedCallback;
    self.webViewDelegate.cacheCompletedCallback = _cacheCompletedCallback;
    self.webViewDelegate.messageDelegate = self;
    
    [userContentController addScriptMessageHandler:self.webViewDelegate name:@"balancyMessageHandler"];
    configuration.userContentController = userContentController;
    
    // Configure caching
    if (self.offlineCacheEnabled) {
        NSURLCache *urlCache = [NSURLCache sharedURLCache];
        // Configure with appropriate cache size
        [NSURLCache setSharedURLCache:[[NSURLCache alloc] initWithMemoryCapacity:10 * 1024 * 1024  // 10MB memory
                                                                    diskCapacity:50 * 1024 * 1024   // 50MB disk
                                                                    directoryURL:nil]];
    }
    
    // Allow offline usage of cached resources
    configuration.websiteDataStore = [WKWebsiteDataStore defaultDataStore];
    
    // Create the WKWebView
    self.webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:configuration];
    self.webView.translatesAutoresizingMaskIntoConstraints = NO;
    self.webView.navigationDelegate = self.webViewDelegate;
}

- (void)viewDidLoad {
    [super viewDidLoad];
    
    // Add the web view to the view hierarchy
    [self.view addSubview:self.webView];
    
    // Set up constraints for full screen
    NSArray *constraints = @[
        [self.webView.topAnchor constraintEqualToAnchor:self.view.topAnchor],
        [self.webView.leadingAnchor constraintEqualToAnchor:self.view.leadingAnchor],
        [self.webView.trailingAnchor constraintEqualToAnchor:self.view.trailingAnchor],
        [self.webView.bottomAnchor constraintEqualToAnchor:self.view.bottomAnchor]
    ];
    
    [NSLayoutConstraint activateConstraints:constraints];
}

- (BOOL)loadURL:(NSString*)urlString {
    NSURL *url = [NSURL URLWithString:urlString];
    
    // Check if this is a local file URL and adjust if needed
    if ([urlString hasPrefix:@"file://"]) {
        // Handle local file URLs
        NSString *localPath = [urlString stringByReplacingOccurrencesOfString:@"file://" withString:@""];
        url = [NSURL fileURLWithPath:localPath];
    } else if (![urlString hasPrefix:@"http://"] && ![urlString hasPrefix:@"https://"]) {
        // Try to interpret as a local file path
        url = [NSURL fileURLWithPath:urlString];
    }
    
    if (!url) {
        NSLog(@"Invalid URL: %@", urlString);
        return NO;
    }
    
    NSURLRequest *request = [NSURLRequest requestWithURL:url];
    [self.webView loadRequest:request];
    return YES;
}

- (BOOL)sendMessage:(NSString*)message {
    if (!self.webView) {
        return NO;
    }
    
    // Escape single quotes in the message
    NSString *escapedMessage = [message stringByReplacingOccurrencesOfString:@"'" withString:@"\\'"];
    
    // Create JavaScript to dispatch the message
    NSString *jsCode = [NSString stringWithFormat:@"if (typeof window.onBalancyMessage === 'function') { "
                        "window.onBalancyMessage('%@'); "
                        "} else { "
                        "document.dispatchEvent(new CustomEvent('balancyMessage', { detail: '%@' })); "
                        "}", escapedMessage, escapedMessage];
    
    [self.webView evaluateJavaScript:jsCode completionHandler:^(id _Nullable result, NSError * _Nullable error) {
        if (error) {
            NSLog(@"Error sending message to WebView: %@", error);
        }
    }];
    
    return YES;
}

- (void)setOfflineCacheEnabled:(BOOL)enabled {
    _offlineCacheEnabled = enabled;
}

- (void)close {
    // Remove the script message handler before deallocating
    [self.webView.configuration.userContentController removeScriptMessageHandlerForName:@"balancyMessageHandler"];
    
    // If presented modally, dismiss
    if (self.presentingViewController) {
        [self dismissViewControllerAnimated:YES completion:nil];
    } else {
        // Otherwise remove from parent view controller
        [self.view removeFromSuperview];
        [self removeFromParentViewController];
    }
}

- (void)setUnityMessageHandler:(void (^)(NSString *))handler {
    self.unityMessageHandler = handler;
}

@end

// Unity bridge implementation
@implementation BalancyWebViewUnityBridge

static BalancyWebViewController* _viewController = nil;

+ (bool)openWebView:(NSString*)url width:(int)width height:(int)height {
    // Close existing WebView if any
    [self closeWebView];
    
    // Create new WebView controller
    _viewController = [[BalancyWebViewController alloc] initWithContext:NULL
                                                      messageCallback:NULL
                                                   loadCompletedCallback:NULL
                                                   cacheCompletedCallback:NULL];
    
    // Present the WebView
    UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
    if (!rootViewController) {
        return false;
    }
    
    [rootViewController addChildViewController:_viewController];
    [rootViewController.view addSubview:_viewController.view];
    [_viewController didMoveToParentViewController:rootViewController];
    
    // Configure the view size
    if (width > 0 && height > 0) {
        CGFloat centerX = rootViewController.view.bounds.size.width / 2;
        CGFloat centerY = rootViewController.view.bounds.size.height / 2;
        _viewController.view.frame = CGRectMake(centerX - width/2, centerY - height/2, width, height);
    } else {
        _viewController.view.frame = rootViewController.view.bounds;
    }
    
    // Load the URL
    return [_viewController loadURL:url];
}

+ (void)closeWebView {
    if (_viewController) {
        [_viewController close];
        _viewController = nil;
    }
}

+ (bool)sendMessage:(NSString*)message {
    if (!_viewController) {
        return false;
    }
    
    return [_viewController sendMessage:message];
}

+ (void)setOfflineCacheEnabled:(bool)enabled {
    if (_viewController) {
        [_viewController setOfflineCacheEnabled:enabled];
    }
}

@end