//
//  BalancyWebView.mm
//  BalancyWebView iOS Implementation
//

#import "BalancyWebView.h"

// C function pointers for callbacks
extern "C" {
    void (*_messageCallback)(const char*) = NULL;
    void (*_loadCompletedCallback)(bool) = NULL;
    void (*_cacheCompletedCallback)(bool) = NULL;
}

@interface BalancyWebViewController ()

@property (nonatomic, strong, readwrite) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, strong) UIActivityIndicatorView *activityIndicator;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) CGRect viewportRect;
@property (nonatomic, assign) BOOL transparentBackground;

@end

@implementation BalancyWebViewController

#pragma mark - Initialization

- (instancetype)initWithMessageCallback:(void (*)(const char*))messageCallback
                  loadCompletedCallback:(void (*)(bool))loadCompletedCallback
                  cacheCompletedCallback:(void (*)(bool))cacheCompletedCallback {
    
    self = [super init];
    if (self) {
        // Store callbacks
        _messageCallback = messageCallback;
        _loadCompletedCallback = loadCompletedCallback;
        _cacheCompletedCallback = cacheCompletedCallback;
        
        // Default property values
        _offlineCacheEnabled = NO;
        _debugLogging = NO;
        _transparentBackground = YES; // Set transparent background by default
        _viewportRect = CGRectMake(0.0f, 0.0f, 1.0f, 1.0f);
        
        // Setup WebView configuration
        [self setupWebView];
    }
    return self;
}

- (void)setupWebView {
    // Create a WKWebViewConfiguration object
    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    
    // Disable various WebView features to make it feel more like a game UI
    // Note: Some of these preferences are only available in newer iOS versions,
    // so we're using availability checks
    if (@available(iOS 13.0, *)) {
        // In iOS 13+, we can use these properties to disable some interactive features
        // Uncomment if your deployment target is iOS 14+
        // configuration.preferences.textInteractionEnabled = NO;
    }
    
    // Disable all native context menus
    // Note: Additional configuration options available in newer iOS versions
    if (@available(iOS 14.0, *)) {
        // Make sure JavaScript is enabled
        configuration.defaultWebpagePreferences.allowsContentJavaScript = YES;
    }
    
    // Create user content controller and add script message handler
    _userContentController = [[WKUserContentController alloc] init];
    [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
    configuration.userContentController = _userContentController;
    
    // Add transparency CSS that will be injected at document start
    NSString *transparencyCSS = @"\
    (function() {\
        document.addEventListener('DOMContentLoaded', function() {\
            document.body.style.backgroundColor = 'transparent';\
            document.documentElement.style.backgroundColor = 'transparent';\
            var style = document.createElement('style');\
            style.type = 'text/css';\
            style.innerHTML = 'body, html { background-color: transparent !important; }';\
            document.head.appendChild(style);\
        });\
    })();\
    ";
    
    // Add CSS to disable user interaction features to make it feel like a game UI
    NSString *gameUICSS = @"\
    (function() {\
        document.addEventListener('DOMContentLoaded', function() {\
            var style = document.createElement('style');\
            style.type = 'text/css';\
            style.innerHTML = `\
                /* Disable text selection */\
                * {\
                    -webkit-user-select: none !important;\
                    -moz-user-select: none !important;\
                    -ms-user-select: none !important;\
                    user-select: none !important;\
                    -webkit-touch-callout: none !important;\
                }\
                \
                /* Disable default touch behaviors */\
                * {\
                    -webkit-tap-highlight-color: transparent !important;\
                }\
                \
                /* Hide scrollbars but allow programmatic scrolling if needed */\
                ::-webkit-scrollbar {\
                    width: 0px !important;\
                    height: 0px !important;\
                    background: transparent !important;\
                }\
                \
                /* Custom cursor to match game feel */\
                * {\
                    cursor: default !important;\
                }\
                \
                /* Make buttons feel more like game UI */\
                button, input[type=button], input[type=submit] {\
                    -webkit-appearance: none !important;\
                }\
                \
                /* Remove focus outlines */\
                *:focus {\
                    outline: none !important;\
                }\
            `;\
            document.head.appendChild(style);\
            \
            // Disable context menu (right click)\
            document.addEventListener('contextmenu', function(e) {\
                e.preventDefault();\
                return false;\
            }, false);\
            \
            // Disable user scaling (pinch to zoom)\
            document.addEventListener('gesturestart', function(e) {\
                e.preventDefault();\
            }, false);\
        });\
    })();\
    ";
    
    WKUserScript *transparencyScript = [[WKUserScript alloc] initWithSource:transparencyCSS
                                                               injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                            forMainFrameOnly:YES];
    [_userContentController addUserScript:transparencyScript];
    
    WKUserScript *gameUIScript = [[WKUserScript alloc] initWithSource:gameUICSS
                                                         injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                      forMainFrameOnly:YES];
    [_userContentController addUserScript:gameUIScript];
    
    // Create the WKWebView with the configuration
    _webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:configuration];
    _webView.navigationDelegate = self;
    _webView.UIDelegate = self;
    
    // Make WebView background transparent by default
    _webView.backgroundColor = [UIColor clearColor];
    _webView.opaque = NO;
    
    // Disable scrolling bounce effect to make it feel more like a game UI
    _webView.scrollView.bounces = NO;
    _webView.scrollView.alwaysBounceVertical = NO;
    _webView.scrollView.alwaysBounceHorizontal = NO;
    
    // Optional: Disable scrolling entirely if your content doesn't need to scroll
    // Comment this out if you need scrolling in your web content
    _webView.scrollView.scrollEnabled = NO;
    
    // Adjust content inset behavior if available
    if (@available(iOS 11.0, *)) {
        _webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
    }
    
    // Create activity indicator
    _activityIndicator = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleLarge];
    _activityIndicator.hidesWhenStopped = YES;
    
    // Add the activity indicator to the view
    [_activityIndicator startAnimating];
}

#pragma mark - View Lifecycle

- (void)viewDidLoad {
    [super viewDidLoad];
    
    // Add the WebView to the view controller's view
    [self.view addSubview:_webView];
    
    // Add the activity indicator
    [self.view addSubview:_activityIndicator];
    _activityIndicator.center = self.view.center;
    
    // Set up auto layout constraints
    _webView.translatesAutoresizingMaskIntoConstraints = NO;
    
    // Full screen by default
    [NSLayoutConstraint activateConstraints:@[
        [_webView.topAnchor constraintEqualToAnchor:self.view.topAnchor],
        [_webView.leftAnchor constraintEqualToAnchor:self.view.leftAnchor],
        [_webView.bottomAnchor constraintEqualToAnchor:self.view.bottomAnchor],
        [_webView.rightAnchor constraintEqualToAnchor:self.view.rightAnchor]
    ]];
    
    // Apply transparency if needed
    [self applyTransparencySettings];
}

- (void)viewDidLayoutSubviews {
    [super viewDidLayoutSubviews];
    
    // Update the activity indicator position
    _activityIndicator.center = self.view.center;
    
    // Apply viewport settings
    [self applyViewportSettings];
}

#pragma mark - Public Methods

- (BOOL)loadURL:(NSString *)urlString {
    NSURL *url = [NSURL URLWithString:urlString];
    if (!url) {
        if (_debugLogging) {
            NSLog(@"[BalancyWebView] Invalid URL: %@", urlString);
        }
        return NO;
    }
    
    NSURLRequest *request = [NSURLRequest requestWithURL:url];
    [_webView loadRequest:request];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Loading URL: %@", urlString);
    }
    
    return YES;
}

- (void)close {
    // Remove script message handler to avoid memory leaks
    [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
    
    // Stop all loading
    [_webView stopLoading];
    
    // Remove the WebView from its parent view
    [_webView removeFromSuperview];
    
    // Remove the view controller from its parent
    [self willMoveToParentViewController:nil];
    [self.view removeFromSuperview];
    [self removeFromParentViewController];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] WebView closed");
    }
}

- (BOOL)sendMessage:(NSString *)message {
    if (!_webView) {
        if (_debugLogging) {
            NSLog(@"[BalancyWebView] Cannot send message: WebView is not initialized");
        }
        return NO;
    }
    
    // Escape single quotes for JavaScript
    NSString *escapedMessage = [message stringByReplacingOccurrencesOfString:@"'" withString:@"\\'"];
    
    // JavaScript to send the message to the web page
    NSString *script = [NSString stringWithFormat:@"if (balancy) { balancy._receiveMessageFromUnity('%@'); }", escapedMessage];
    
    [_webView evaluateJavaScript:script completionHandler:^(id _Nullable result, NSError * _Nullable error) {
        if (error && self.debugLogging) {
            NSLog(@"[BalancyWebView] Error sending message: %@", error);
        }
    }];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Message sent: %@", message);
    }
    
    return YES;
}

- (NSString *)callJavaScript:(NSString *)function arguments:(NSArray<NSString *> *)arguments {
    if (!_webView) {
        if (_debugLogging) {
            NSLog(@"[BalancyWebView] Cannot call JavaScript: WebView is not initialized");
        }
        return @"{\"error\": \"WebView not initialized\"}";
    }
    
    // Build the JavaScript function call
    NSMutableString *script = [NSMutableString stringWithString:function];
    
    // If it's not an eval call, add parentheses and arguments
    if (![function isEqualToString:@"eval"]) {
        [script appendString:@"("];
        
        for (NSUInteger i = 0; i < arguments.count; i++) {
            // Properly escape and quote string arguments
            NSString *escapedArg = [arguments[i] stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
            [script appendFormat:@"\"%@\"", escapedArg];
            
            if (i < arguments.count - 1) {
                [script appendString:@", "];
            }
        }
        
        [script appendString:@")"];
    } else if (arguments.count > 0) {
        // For eval, just use the first argument as the script
        script = [NSMutableString stringWithString:arguments[0]];
    }
    
    // Create a semaphore to make this call synchronous
    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
    
    // Result variables
    __block NSString *resultString = @"{\"error\": \"JavaScript execution failed\"}";
    __block BOOL executionComplete = NO;
    
    // Execute JavaScript
    [_webView evaluateJavaScript:script completionHandler:^(id _Nullable result, NSError * _Nullable error) {
        if (error) {
            if (self.debugLogging) {
                NSLog(@"[BalancyWebView] JavaScript error: %@", error);
            }
            resultString = [NSString stringWithFormat:@"{\"error\": \"%@\"}", error.localizedDescription];
        } else {
            if (result == nil) {
                resultString = @"null";
            } else if ([result isKindOfClass:[NSString class]]) {
                resultString = (NSString *)result;
            } else if ([result isKindOfClass:[NSNumber class]]) {
                resultString = [result stringValue];
            } else {
                // Try to convert to JSON
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:result options:0 error:nil];
                if (jsonData) {
                    resultString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                } else {
                    resultString = [NSString stringWithFormat:@"\"%@\"", [result description]];
                }
            }
        }
        
        executionComplete = YES;
        dispatch_semaphore_signal(semaphore);
    }];
    
    // Wait for execution to complete with a timeout
    if (dispatch_semaphore_wait(semaphore, dispatch_time(DISPATCH_TIME_NOW, 5 * NSEC_PER_SEC)) != 0) {
        if (!executionComplete) {
            if (_debugLogging) {
                NSLog(@"[BalancyWebView] JavaScript execution timed out");
            }
            return @"{\"error\": \"JavaScript execution timed out\"}";
        }
    }
    
    return resultString;
}

- (void)setViewportRect:(CGFloat)x y:(CGFloat)y width:(CGFloat)width height:(CGFloat)height {
    // Store the viewport rect values (as percentages from 0.0 to 1.0)
    _viewportRect = CGRectMake(x, y, width, height);
    
    // Apply the new viewport settings if the view is loaded
    if (self.isViewLoaded) {
        [self applyViewportSettings];
    }
}

- (void)setTransparentBackground:(BOOL)transparent {
    _transparentBackground = transparent;
    
    // Apply the new transparency settings if the view is loaded
    if (self.isViewLoaded) {
        [self applyTransparencySettings];
    }
}

- (void)setOfflineCacheEnabled:(BOOL)enabled {
    _offlineCacheEnabled = enabled;
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Offline cache %@", enabled ? @"enabled" : @"disabled");
    }
    
    // Apply offline cache settings
    if (enabled) {
        // TODO: Implement offline caching logic
        // This would typically involve setting up a custom URL scheme handler
        // and implementing NSURLProtocol to intercept requests and cache responses
    }
}

- (void)setGameUIMode:(BOOL)enabled {
    _gameUIMode = enabled;
    
    // Apply game UI settings
    if (enabled) {
        // Disable scrolling bounce effect
        _webView.scrollView.bounces = NO;
        _webView.scrollView.alwaysBounceVertical = NO;
        _webView.scrollView.alwaysBounceHorizontal = NO;
        
        // Disable scrolling entirely unless content needs it
        _webView.scrollView.scrollEnabled = NO;
        
        // Inject UI customizations for game-like feel
        NSString *gameUIModeScript = @"\
        (function() {\
            // Disable text selection\
            document.documentElement.style.webkitUserSelect = 'none';\
            document.documentElement.style.userSelect = 'none';\
            \
            // Disable context menu\
            document.documentElement.oncontextmenu = function() { return false; };\
            \
            // Disable text selection on tap\
            document.documentElement.style.webkitTouchCallout = 'none';\
            \
            // Remove any focus outlines\
            var styleElement = document.createElement('style');\
            styleElement.textContent = '*:focus { outline: none !important; }';\
            document.head.appendChild(styleElement);\
            \
            // Prevent default touch behavior\
            document.addEventListener('touchstart', function(e) {\
                // Allow clicking on links and buttons, but prevent other behaviors\
                if (e.target.tagName !== 'A' && e.target.tagName !== 'BUTTON' && \
                    e.target.tagName !== 'INPUT') {\
                    e.preventDefault();\
                }\
            }, { passive: false });\
        })();\
        ";
        
        [_webView evaluateJavaScript:gameUIModeScript completionHandler:nil];
    } else {
        // Enable standard web browsing features
        _webView.scrollView.bounces = YES;
        _webView.scrollView.scrollEnabled = YES;
        
        // Re-enable standard web behaviors
        NSString *standardWebScript = @"\
        (function() {\
            // Enable text selection\
            document.documentElement.style.webkitUserSelect = 'auto';\
            document.documentElement.style.userSelect = 'auto';\
            \
            // Enable context menu\
            document.documentElement.oncontextmenu = null;\
            \
            // Enable text selection on tap\
            document.documentElement.style.webkitTouchCallout = 'default';\
            \
            // Remove our custom style overrides\
            var styleElements = document.head.querySelectorAll('style');\
            for (var i = 0; i < styleElements.length; i++) {\
                if (styleElements[i].textContent.indexOf('outline: none !important') !== -1) {\
                    styleElements[i].remove();\
                }\
            }\
        })();\
        ";
        
        [_webView evaluateJavaScript:standardWebScript completionHandler:nil];
    }
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Game UI mode %@", enabled ? @"enabled" : @"disabled");
    }
}

- (void)setDebugLogging:(BOOL)enabled {
    _debugLogging = enabled;
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Debug logging enabled");
    }
}

#pragma mark - Private Methods

- (void)applyViewportSettings {
    if (!self.isViewLoaded || !_webView) {
        return;
    }
    
    // Calculate actual pixel values from percentages
    CGFloat screenWidth = self.view.bounds.size.width;
    CGFloat screenHeight = self.view.bounds.size.height;
    
    CGFloat x = _viewportRect.origin.x * screenWidth;
    CGFloat y = _viewportRect.origin.y * screenHeight;
    CGFloat width = _viewportRect.size.width * screenWidth;
    CGFloat height = _viewportRect.size.height * screenHeight;
    
    // Update WebView frame
    _webView.frame = CGRectMake(x, y, width, height);
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Viewport updated: x=%f, y=%f, width=%f, height=%f", x, y, width, height);
    }
}

- (void)applyTransparencySettings {
    if (!self.isViewLoaded || !_webView) {
        return;
    }
    
    if (_transparentBackground) {
        // Make the WebView background transparent
        _webView.backgroundColor = [UIColor clearColor];
        _webView.opaque = NO;
        self.view.backgroundColor = [UIColor clearColor];
        
        // JavaScript to make the HTML document body transparent
        NSString *transparencyScript = @"\
        (function() {\
            document.body.style.backgroundColor = 'transparent';\
            document.documentElement.style.backgroundColor = 'transparent';\
            var style = document.createElement('style');\
            style.type = 'text/css';\
            style.innerHTML = 'body { background-color: transparent !important; }';\
            document.head.appendChild(style);\
        })();\
        ";
        
        [_webView evaluateJavaScript:transparencyScript completionHandler:nil];
    } else {
        // Reset to default opaque background
        _webView.backgroundColor = [UIColor whiteColor];
        _webView.opaque = YES;
        self.view.backgroundColor = [UIColor whiteColor];
    }
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Transparency %@", _transparentBackground ? @"enabled" : @"disabled");
    }
}

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    // Make sure the message is from our handler
    if (![message.name isEqualToString:@"BalancyWebView"]) {
        return;
    }
    
    // Extract the message body (should be a string)
    NSString *messageString = nil;
    
    if ([message.body isKindOfClass:[NSString class]]) {
        messageString = (NSString *)message.body;
    } else {
        // Try to convert to JSON string
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:message.body options:0 error:nil];
        if (jsonData) {
            messageString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        } else {
            messageString = [NSString stringWithFormat:@"%@", message.body];
        }
    }
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Received message from WebView: %@", messageString);
    }
    
    // Forward the message to Unity via the callback
    if (_messageCallback != NULL) {
        const char *cString = [messageString UTF8String];
        _messageCallback(cString);
    }
}

#pragma mark - WKNavigationDelegate

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
    // Show the activity indicator when loading starts
    [_activityIndicator startAnimating];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Started loading page");
    }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    // Hide the activity indicator when loading completes
    [_activityIndicator stopAnimating];
    
    // Apply transparency settings again after page load
    if (_transparentBackground) {
        [self applyTransparencySettings];
    }
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Page loaded successfully");
    }
    
    // Notify Unity that loading is complete
    if (_loadCompletedCallback != NULL) {
        _loadCompletedCallback(true);
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Hide the activity indicator
    [_activityIndicator stopAnimating];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Failed to load page: %@", error);
    }
    
    // Notify Unity that loading failed
    if (_loadCompletedCallback != NULL) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Hide the activity indicator
    [_activityIndicator stopAnimating];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Failed to load page (provisional): %@", error);
    }
    
    // Notify Unity that loading failed
    if (_loadCompletedCallback != NULL) {
        _loadCompletedCallback(false);
    }
}

#pragma mark - WKUIDelegate

// Implement UIDelegate methods as needed for handling alerts, confirms, prompts, etc.

@end

#pragma mark - C Interface for Unity

// These functions are exported to be called from Unity's C# code

extern "C" {

// Open a WebView with the specified URL
bool _balancyOpenWebView(const char* url) {
    @autoreleasepool {
        // Get the top-most view controller
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        if (!rootViewController) {
            NSLog(@"[BalancyWebView] Failed to get root view controller");
            return false;
        }
        
        // Create a WebView controller
        BalancyWebViewController* webViewController = [[BalancyWebViewController alloc] initWithMessageCallback:_messageCallback
                                                                                           loadCompletedCallback:_loadCompletedCallback
                                                                                          cacheCompletedCallback:_cacheCompletedCallback];
        
        // Add as a child view controller
        [rootViewController addChildViewController:webViewController];
        [rootViewController.view addSubview:webViewController.view];
        webViewController.view.frame = rootViewController.view.bounds;
        [webViewController didMoveToParentViewController:rootViewController];
        
        // Load the URL
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        return [webViewController loadURL:nsUrl];
    }
}

// Close the WebView
void _balancyCloseWebView() {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController close];
                break;
            }
        }
    }
}

// Send a message to the WebView
bool _balancySendMessage(const char* message) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                NSString* nsMessage = [NSString stringWithUTF8String:message];
                return [webViewController sendMessage:nsMessage];
            }
        }
        return false;
    }
}

// Call a JavaScript function in the WebView
const char* _balancyCallJavaScript(const char* function, const char** args, int argsCount) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                
                // Convert C strings to NSString array
                NSString* nsFunction = [NSString stringWithUTF8String:function];
                NSMutableArray<NSString*>* nsArgs = [NSMutableArray arrayWithCapacity:argsCount];
                
                for (int i = 0; i < argsCount; i++) {
                    NSString* arg = [NSString stringWithUTF8String:args[i]];
                    [nsArgs addObject:arg];
                }
                
                // Call JavaScript
                NSString* result = [webViewController callJavaScript:nsFunction arguments:nsArgs];
                
                // Return the result as a C string
                const char* cResult = [result UTF8String];
                char* resultCopy = (char*)malloc(strlen(cResult) + 1);
                strcpy(resultCopy, cResult);
                return resultCopy;
            }
        }
        
        return strdup("{\"error\": \"WebView not found\"}");
    }
}

// Set the viewport rectangle for the WebView
void _balancySetViewportRect(float x, float y, float width, float height) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setViewportRect:x y:y width:width height:height];
                break;
            }
        }
    }
}

// Set transparent background for the WebView
void _balancySetTransparentBackground(bool transparent) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setTransparentBackground:transparent];
                break;
            }
        }
    }
}

// Enable or disable offline caching
void _balancySetOfflineCacheEnabled(bool enabled) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setOfflineCacheEnabled:enabled];
                break;
            }
        }
    }
}

// Enable or disable debug logging
void _balancySetDebugLogging(bool enabled) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setDebugLogging:enabled];
                break;
            }
        }
    }
}

// Enable or disable game UI mode
void _balancySetGameUIMode(bool enabled) {
    @autoreleasepool {
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setGameUIMode:enabled];
                break;
            }
        }
    }
}

// Register callbacks from Unity
void _balancyRegisterMessageCallback(void (*callback)(const char*)) {
    _messageCallback = callback;
}

void _balancyRegisterLoadCompletedCallback(void (*callback)(bool)) {
    _loadCompletedCallback = callback;
}

// Note: _balancyRegisterCacheCompletedCallback is implemented in BalancyWebViewUnityBridge.mm

} // extern "C"
