//
//  BalancyWebViewMac.mm
//  BalancyWebView macOS Implementation
//

#import "BalancyWebViewMac.h"

// C function pointers for callbacks
static void (*_messageCallback)(const char*) = NULL;
static void (*_loadCompletedCallback)(bool) = NULL;
static void (*_cacheCompletedCallback)(bool) = NULL;

// JavaScript to inject into the WebView
static NSString *const kBalancyWebViewBridgeScript = @"(function() {\
    if (window.BalancyWebView) { return; }\
    \
    const BalancyWebView = {\
        postMessage: function(message) {\
            if (typeof message !== 'string') {\
                message = JSON.stringify(message);\
            }\
            \
            try {\
                if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.BalancyWebView) {\
                    window.webkit.messageHandlers.BalancyWebView.postMessage(message);\
                    return true;\
                } else {\
                    console.warn('BalancyWebView native bridge not found');\
                    return false;\
                }\
            } catch (e) {\
                console.error('Error sending message to Unity:', e);\
                return false;\
            }\
        },\
        \
        callUnity: function(message) {\
            if (typeof message !== 'string') {\
                message = JSON.stringify(message);\
            }\
            \
            try {\
                if (window._BalancyWebViewSynchronousInterface) {\
                    return window._BalancyWebViewSynchronousInterface.callUnity(message);\
                } else {\
                    console.warn('BalancyWebView synchronous interface not found');\
                    return JSON.stringify({error: 'Bridge not available'});\
                }\
            } catch (e) {\
                console.error('Error calling Unity synchronously:', e);\
                return JSON.stringify({error: e.message});\
            }\
        },\
        \
        _receiveMessageFromUnity: function(message) {\
            const event = new CustomEvent('BalancyWebViewMessage', {\
                detail: message,\
                bubbles: true,\
                cancelable: true\
            });\
            document.dispatchEvent(event);\
            \
            if (typeof window.onBalancyWebViewMessage === 'function') {\
                window.onBalancyWebViewMessage(message);\
            }\
        }\
    };\
    \
    window.BalancyWebView = BalancyWebView;\
    \
    document.addEventListener('DOMContentLoaded', function() {\
        setTimeout(function() {\
            BalancyWebView.postMessage(JSON.stringify({\
                action: 'ready',\
                timestamp: Date.now()\
            }));\
        }, 100);\
    });\
    \
    console.log('Balancy WebView Bridge initialized');\
})();";

@interface BalancyWebViewController ()

@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, strong) NSProgressIndicator *progressIndicator;
@property (nonatomic, strong) NSWindow *window;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) NSRect viewportRect;
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
        _transparentBackground = NO;
        _viewportRect = NSMakeRect(0.0f, 0.0f, 1.0f, 1.0f);
        
        // Setup WebView configuration
        [self setupWebView];
    }
    return self;
}

- (void)setupWebView {
    // Create a WKWebViewConfiguration object
    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    
    // Create user content controller and add script message handler
    _userContentController = [[WKUserContentController alloc] init];
    [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
    configuration.userContentController = _userContentController;
    
    // Add the bridge script
    WKUserScript *bridgeScript = [[WKUserScript alloc] initWithSource:kBalancyWebViewBridgeScript
                                                        injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                     forMainFrameOnly:YES];
    [_userContentController addUserScript:bridgeScript];
    
    // Create the WKWebView with the configuration
    NSRect frame = NSMakeRect(0, 0, 800, 600); // Default size
    _webView = [[WKWebView alloc] initWithFrame:frame configuration:configuration];
    _webView.navigationDelegate = self;
    
    // Create progress indicator
    _progressIndicator = [[NSProgressIndicator alloc] initWithFrame:NSMakeRect(0, 0, 32, 32)];
    [_progressIndicator setStyle:NSProgressIndicatorSpinningStyle];
    [_progressIndicator setBezeled:NO];
    [_progressIndicator setDisplayedWhenStopped:NO];
    [_progressIndicator setUsesThreadedAnimation:YES];
    
    // Create a window to host the WebView
    _window = [[NSWindow alloc] initWithContentRect:frame
                                          styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskResizable | NSWindowStyleMaskMiniaturizable
                                            backing:NSBackingStoreBuffered
                                              defer:NO];
    [_window setTitle:@"Balancy WebView"];
    [_window center];
    
    // Set the view controller's view
    self.view = [[NSView alloc] initWithFrame:frame];
}

#pragma mark - View Lifecycle

- (void)loadView {
    // Nothing to do here since we set the view in setupWebView
}

- (void)viewDidLoad {
    [super viewDidLoad];
    
    // Add the WebView to the view controller's view
    [self.view addSubview:_webView];
    
    // Add the progress indicator
    [self.view addSubview:_progressIndicator];
    [_progressIndicator setFrameOrigin:NSMakePoint((NSWidth(self.view.frame) - NSWidth(_progressIndicator.frame)) / 2,
                                                  (NSHeight(self.view.frame) - NSHeight(_progressIndicator.frame)) / 2)];
    
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

- (void)viewDidLayout {
    [super viewDidLayout];
    
    // Update the progress indicator position
    [_progressIndicator setFrameOrigin:NSMakePoint((NSWidth(self.view.frame) - NSWidth(_progressIndicator.frame)) / 2,
                                                  (NSHeight(self.view.frame) - NSHeight(_progressIndicator.frame)) / 2)];
    
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
    
    // Show the window
    [_window setContentViewController:self];
    [_window makeKeyAndOrderFront:nil];
    
    // Start the progress indicator
    [_progressIndicator startAnimation:nil];
    
    // Load the URL
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
    
    // Close the window
    [_window close];
    _window = nil;
    
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
    NSString *script = [NSString stringWithFormat:@"if (window.BalancyWebView) { window.BalancyWebView._receiveMessageFromUnity('%@'); }", escapedMessage];
    
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
    _viewportRect = NSMakeRect(x, y, width, height);
    
    // Apply the new viewport settings if the view is loaded
    if ([self isViewLoaded]) {
        [self applyViewportSettings];
    }
}

- (void)setTransparentBackground:(BOOL)transparent {
    _transparentBackground = transparent;
    
    // Apply the new transparency settings if the view is loaded
    if ([self isViewLoaded]) {
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

- (void)setDebugLogging:(BOOL)enabled {
    _debugLogging = enabled;
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Debug logging enabled");
    }
}

#pragma mark - Private Methods

- (void)applyViewportSettings {
    if (![self isViewLoaded] || !_webView || !_window) {
        return;
    }
    
    // Get the main screen frame (in screen coordinates)
    NSRect screenFrame = [[NSScreen mainScreen] frame];
    
    // Calculate actual pixel values from percentages
    CGFloat x = _viewportRect.origin.x * screenFrame.size.width;
    CGFloat y = _viewportRect.origin.y * screenFrame.size.height;
    CGFloat width = _viewportRect.size.width * screenFrame.size.width;
    CGFloat height = _viewportRect.size.height * screenFrame.size.height;
    
    // Convert y-coordinate (macOS uses bottom-left as origin, but we want top-left)
    y = screenFrame.size.height - (y + height);
    
    // Update window frame
    NSRect windowFrame = NSMakeRect(x, y, width, height);
    [_window setFrame:windowFrame display:YES animate:YES];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Viewport updated: x=%f, y=%f, width=%f, height=%f", x, y, width, height);
    }
}

- (void)applyTransparencySettings {
    if (![self isViewLoaded] || !_webView) {
        return;
    }
    
    if (_transparentBackground) {
        // Make the WebView background transparent
        [_webView setValue:@NO forKey:@"drawsBackground"];
        self.view.wantsLayer = YES;
        self.view.layer.backgroundColor = [NSColor clearColor].CGColor;
        [_window setBackgroundColor:[NSColor clearColor]];
        [_window setOpaque:NO];
        [_window setHasShadow:NO];
        
        // JavaScript to make the HTML document body transparent
        NSString *transparencyScript = @"document.body.style.backgroundColor = 'transparent';";
        [_webView evaluateJavaScript:transparencyScript completionHandler:nil];
    } else {
        // Reset to default opaque background
        [_webView setValue:@YES forKey:@"drawsBackground"];
        self.view.wantsLayer = YES;
        self.view.layer.backgroundColor = [NSColor whiteColor].CGColor;
        [_window setBackgroundColor:[NSColor whiteColor]];
        [_window setOpaque:YES];
        [_window setHasShadow:YES];
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
    // Show the progress indicator when loading starts
    [_progressIndicator startAnimation:nil];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Started loading page");
    }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    // Hide the progress indicator when loading completes
    [_progressIndicator stopAnimation:nil];
    
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
    // Hide the progress indicator
    [_progressIndicator stopAnimation:nil];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Failed to load page: %@", error);
    }
    
    // Notify Unity that loading failed
    if (_loadCompletedCallback != NULL) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Hide the progress indicator
    [_progressIndicator stopAnimation:nil];
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Failed to load page (provisional): %@", error);
    }
    
    // Notify Unity that loading failed
    if (_loadCompletedCallback != NULL) {
        _loadCompletedCallback(false);
    }
}

@end

#pragma mark - C Interface for Unity

// These functions are exported to be called from Unity's C# code

extern "C" {

// Open a WebView with the specified URL
bool _balancyOpenWebView(const char* url) {
    @autoreleasepool {
        // Create a WebView controller
        BalancyWebViewController* webViewController = [[BalancyWebViewController alloc] initWithMessageCallback:_messageCallback
                                                                                           loadCompletedCallback:_loadCompletedCallback
                                                                                          cacheCompletedCallback:_cacheCompletedCallback];
        
        // Load the URL
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        return [webViewController loadURL:nsUrl];
    }
}

// Close the WebView
void _balancyCloseWebView() {
    @autoreleasepool {
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
                [webViewController close];
                break;
            }
        }
    }
}

// Send a message to the WebView
bool _balancySendMessage(const char* message) {
    @autoreleasepool {
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
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
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
                
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
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
                [webViewController setViewportRect:x y:y width:width height:height];
                break;
            }
        }
    }
}

// Set transparent background for the WebView
void _balancySetTransparentBackground(bool transparent) {
    @autoreleasepool {
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
                [webViewController setTransparentBackground:transparent];
                break;
            }
        }
    }
}

// Enable or disable offline caching
void _balancySetOfflineCacheEnabled(bool enabled) {
    @autoreleasepool {
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
                [webViewController setOfflineCacheEnabled:enabled];
                break;
            }
        }
    }
}

// Enable or disable debug logging
void _balancySetDebugLogging(bool enabled) {
    @autoreleasepool {
        // Find the BalancyWebViewController in any open window
        for (NSWindow *window in [NSApp windows]) {
            if ([window.contentViewController isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)window.contentViewController;
                [webViewController setDebugLogging:enabled];
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

void _balancyRegisterCacheCompletedCallback(void (*callback)(bool)) {
    _cacheCompletedCallback = callback;
}

} // extern "C"