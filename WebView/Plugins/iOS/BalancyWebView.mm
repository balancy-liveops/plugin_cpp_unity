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

// Go up enough directory levels from the loaded file to cover the entire
// app data container so the WebView can reach all local files.
static const int kDirectoryLevelsUp = 6;
static BalancyWebViewController* _sharedController = nil;
static UIViewController* GetRootViewController(void);

static NSURL* GetPersistentDataRootURL(void) {
    NSArray<NSString*>* documentDirectories = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES);
    NSString* documentsDirectory = documentDirectories.firstObject;
    if (documentsDirectory == nil || documentsDirectory.length == 0) {
        return nil;
    }

    return [NSURL fileURLWithPath:documentsDirectory isDirectory:YES];
}

static NSURL* CreatePersistentShellURL(NSString* sourcePath) {
    if (sourcePath == nil || sourcePath.length == 0) {
        return nil;
    }

    NSFileManager* fileManager = [NSFileManager defaultManager];
    NSURL* persistentDataRootURL = GetPersistentDataRootURL();
    NSString* documentsDirectory = persistentDataRootURL.path;
    if (documentsDirectory == nil) {
        return nil;
    }

    NSString* persistentDirectory = [documentsDirectory stringByAppendingPathComponent:@"Balancy/PersistentWebView"];
    NSError* directoryError = nil;
    if (![fileManager createDirectoryAtPath:persistentDirectory withIntermediateDirectories:YES attributes:nil error:&directoryError]) {
        NSLog(@"[BalancyWebView] Failed to create persistent shell directory: %@", directoryError.localizedDescription);
        return nil;
    }

    NSString* targetPath = [persistentDirectory stringByAppendingPathComponent:@"balancy-shell.html"];
    NSURL* targetURL = [NSURL fileURLWithPath:targetPath];
    NSURL* sourceURL = [NSURL fileURLWithPath:sourcePath];

    NSError* removeError = nil;
    if ([fileManager fileExistsAtPath:targetPath] && ![fileManager removeItemAtURL:targetURL error:&removeError]) {
        NSLog(@"[BalancyWebView] Failed to replace persistent shell file: %@", removeError.localizedDescription);
        return nil;
    }

    NSError* copyError = nil;
    if (![fileManager copyItemAtURL:sourceURL toURL:targetURL error:&copyError]) {
        NSLog(@"[BalancyWebView] Failed to copy persistent shell file: %@", copyError.localizedDescription);
        return nil;
    }

    return targetURL;
}

static BalancyWebViewController* GetExistingWebViewController(void) {
    if (_sharedController != nil) {
        return _sharedController;
    }

    UIViewController* rootViewController = GetRootViewController();
    if (!rootViewController) {
        return nil;
    }

    for (UIViewController* childVC in rootViewController.childViewControllers) {
        if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
            _sharedController = (BalancyWebViewController*)childVC;
            return _sharedController;
        }
    }

    return nil;
}

static BalancyWebViewController* CreateOrGetWebViewController(void (*messageCallback)(const char*),
                                                              void (*loadCompletedCallback)(bool),
                                                              void (*cacheCompletedCallback)(bool)) {
    BalancyWebViewController* existingController = GetExistingWebViewController();
    if (existingController != nil) {
        return existingController;
    }

    UIViewController* rootViewController = GetRootViewController();
    if (!rootViewController) {
        return nil;
    }

    BalancyWebViewController* webViewController = [[BalancyWebViewController alloc] initWithMessageCallback:messageCallback
                                                                                       loadCompletedCallback:loadCompletedCallback
                                                                                      cacheCompletedCallback:cacheCompletedCallback];

    [rootViewController addChildViewController:webViewController];
    webViewController.view.frame = rootViewController.view.bounds;
    webViewController.view.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    [rootViewController.view addSubview:webViewController.view];
    [webViewController didMoveToParentViewController:rootViewController];

    _sharedController = webViewController;
    return webViewController;
}

@interface BalancyWebViewController ()

@property (nonatomic, strong, readwrite) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, strong) UIActivityIndicatorView *activityIndicator;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) CGRect viewportRect;
@property (nonatomic, assign) BOOL transparentBackground;
@property (nonatomic, strong) NSTimer *emergencyExitHideTimer;
@property (nonatomic, assign) BOOL emergencyExitEnabled;
@property (nonatomic, assign, readwrite) BOOL persistentMode;
@property (nonatomic, assign) BOOL suppressNextAnimation;

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
        
        // Animation defaults
        _showDelay = 0.1f; // 100ms default delay
        _animationDuration = 0.1f; // 100ms default animation duration

        // Emergency exit enabled by default
        _emergencyExitEnabled = YES;
        _persistentMode = NO;
        _suppressNextAnimation = NO;
        
        // Setup WebView configuration
        [self setupWebView];
    }
    return self;
}

- (void)setupWebView {
    // Create a WKWebViewConfiguration object
    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    
    // === REDUCE WEBKIT PROCESS ERRORS ===
    // Configure process pool to reduce termination errors
    configuration.processPool = [[WKProcessPool alloc] init];
    
    // Suppress media playback (reduces GPU process usage)
    configuration.allowsInlineMediaPlayback = YES;
    configuration.mediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypeAll;
    
    // Configure preferences to reduce process spawning
    configuration.preferences.javaScriptEnabled = YES;
    configuration.preferences.javaScriptCanOpenWindowsAutomatically = NO;
    
    // Note: iOS doesn't allow universal file access via configuration
    // We'll handle cross-directory access via loadFileURL:allowingReadAccessToURL: instead
    
    // === AGGRESSIVE MAGNIFYING GLASS PREVENTION ===
    // Try to disable text interaction and magnification at the configuration level
    
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
    
    // Configure for local file access
    // WKWebView automatically allows file:// URLs to access other file:// URLs
    // in the same directory, which is what we need for local HTML content
    
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
                    -webkit-text-size-adjust: none !important;\
                    touch-action: manipulation !important;\
                }\
                \
                /* Completely disable interaction on images */\
                img {\
                    pointer-events: none !important;\
                    -webkit-user-drag: none !important;\
                    user-drag: none !important;\
                    -webkit-touch-callout: none !important;\
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
            // === AGGRESSIVE EVENT BLOCKING FOR MAGNIFYING GLASS ===\
            \
            // Block all gesture-related events\
            ['gesturestart', 'gesturechange', 'gestureend'].forEach(function(eventType) {\
                document.addEventListener(eventType, function(e) {\
                    e.preventDefault();\
                    e.stopImmediatePropagation();\
                    return false;\
                }, { passive: false, capture: true });\
                \
                window.addEventListener(eventType, function(e) {\
                    e.preventDefault();\
                    e.stopImmediatePropagation();\
                    return false;\
                }, { passive: false, capture: true });\
            });\
            \
            // Minimal touch event blocking - only for images and double-tap prevention\
            var lastTouchEnd = 0;\
            \
            document.addEventListener('touchstart', function(e) {\
                // Only block touch on images to prevent magnification\
                if (e.target.tagName === 'IMG') {\
                    e.preventDefault();\
                    e.stopImmediatePropagation();\
                    return false;\
                }\
            }, { passive: false, capture: true });\
            \
            document.addEventListener('touchend', function(e) {\
                var now = Date.now();\
                \
                // Only prevent fast double taps that could trigger zoom\
                if (now - lastTouchEnd <= 200) {\
                    e.preventDefault();\
                    e.stopImmediatePropagation();\
                }\
                \
                lastTouchEnd = now;\
            }, { passive: false, capture: true });\
            \
            // Disable context menu (right click)\
            document.addEventListener('contextmenu', function(e) {\
                e.preventDefault();\
                e.stopImmediatePropagation();\
                return false;\
            }, { passive: false, capture: true });\
            \
            // Disable user scaling (pinch to zoom)\
            document.addEventListener('wheel', function(e) {\
                if (e.ctrlKey) {\
                    e.preventDefault();\
                    e.stopImmediatePropagation();\
                    return false;\
                }\
            }, { passive: false, capture: true });\
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
    
    // Start with webview completely transparent for animation
    _webView.alpha = 0.0;
    
    // === GAME UI MODE NATIVE SETTINGS ===
    // These settings make the WebView feel more like a native game UI
    
    // Disable all native bounce effects
    _webView.scrollView.bounces = NO;
    _webView.scrollView.alwaysBounceVertical = NO;
    _webView.scrollView.alwaysBounceHorizontal = NO;
    _webView.scrollView.bouncesZoom = NO;
    
    // Disable zoom functionality completely
    _webView.scrollView.minimumZoomScale = 1.0;
    _webView.scrollView.maximumZoomScale = 1.0;
    
    // Hide scroll indicators
    _webView.scrollView.showsVerticalScrollIndicator = NO;
    _webView.scrollView.showsHorizontalScrollIndicator = NO;
    
    // Disable native navigation gestures
    _webView.allowsBackForwardNavigationGestures = NO;
    
    // Disable link preview on force touch
    _webView.allowsLinkPreview = NO;
    
    // Disable scrolling by default (can be re-enabled if needed)
    _webView.scrollView.scrollEnabled = NO;
    
    // === SELECTIVE DISABLE OF PROBLEMATIC GESTURES ===
    // Only disable gestures that cause magnifying glass, keep others for animations
    
    // Find and disable only double-tap gesture recognizers that cause magnifying glass
    NSArray *allGestureRecognizers = [_webView.gestureRecognizers copy];
    for (UIGestureRecognizer *recognizer in allGestureRecognizers) {
        if ([recognizer isKindOfClass:[UITapGestureRecognizer class]]) {
            UITapGestureRecognizer *tapRecognizer = (UITapGestureRecognizer *)recognizer;
            if (tapRecognizer.numberOfTapsRequired == 2) {
                recognizer.enabled = NO; // Disable double tap to prevent magnifying glass
            }
        }
        if ([recognizer isKindOfClass:[UILongPressGestureRecognizer class]]) {
            recognizer.enabled = NO; // Disable long press that can trigger text selection
        }
    }
    
    // Do the same for scroll view gesture recognizers
    NSArray *scrollGestureRecognizers = [_webView.scrollView.gestureRecognizers copy];
    for (UIGestureRecognizer *recognizer in scrollGestureRecognizers) {
        if ([recognizer isKindOfClass:[UITapGestureRecognizer class]]) {
            UITapGestureRecognizer *tapRecognizer = (UITapGestureRecognizer *)recognizer;
            if (tapRecognizer.numberOfTapsRequired == 2) {
                recognizer.enabled = NO; // Disable double tap to prevent zoom
            }
        }
        if ([recognizer isKindOfClass:[UILongPressGestureRecognizer class]]) {
            recognizer.enabled = NO; // Disable long press
        }
    }
    
    // Add a blocking double-tap gesture with higher priority
    UITapGestureRecognizer *doubleTap = [[UITapGestureRecognizer alloc] initWithTarget:self action:@selector(handleDoubleTap:)];
    doubleTap.numberOfTapsRequired = 2;
    doubleTap.numberOfTouchesRequired = 1;
    [_webView addGestureRecognizer:doubleTap];
    
    // Adjust content inset behavior if available
    if (@available(iOS 11.0, *)) {
        _webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
    }
    
    // Create activity indicator with iOS version compatibility
    if (@available(iOS 13.0, *)) {
        _activityIndicator = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleLarge];
    } else {
        // For iOS 12 and earlier, use the deprecated but compatible style
        _activityIndicator = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleWhiteLarge];
    }
    _activityIndicator.hidesWhenStopped = YES;
    
    // Add the activity indicator to the view
    [_activityIndicator startAnimating];
}

#pragma mark - Gesture Handlers

- (void)handleSingleTap:(UITapGestureRecognizer *)recognizer {
    // This method is now unused - we let native touch handling work
    // Only here for compatibility, does nothing
    if (_debugLogging) {
        CGPoint location = [recognizer locationInView:_webView];
        NSLog(@"[BalancyWebView] Custom single tap handler (unused): %.1f, %.1f", location.x, location.y);
    }
}

- (void)handleDoubleTap:(UITapGestureRecognizer *)recognizer {
    // Intercept and block double taps to prevent magnifying glass
    if (_debugLogging) {
        CGPoint location = [recognizer locationInView:_webView];
        NSLog(@"[BalancyWebView] Double tap blocked at location: %.1f, %.1f", location.x, location.y);
    }
    
    // Do nothing - this effectively blocks the double tap
    // You could optionally forward it as a single click if needed:
    // [self handleSingleTap:recognizer];
}

#pragma mark - Animation Methods

- (void)setShowDelay:(float)delaySeconds {
    _showDelay = delaySeconds;
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Show delay set to: %.3f seconds", delaySeconds);
    }
}

- (void)setAnimationDuration:(float)durationSeconds {
    _animationDuration = durationSeconds;
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Animation duration set to: %.3f seconds", durationSeconds);
    }
}

- (void)startShowAnimation {
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Starting show animation with delay: %.3f, duration: %.3f", _showDelay, _animationDuration);
    }

    self.view.hidden = NO;
    self.view.userInteractionEnabled = YES;
    _webView.alpha = 0.0;
    
    // Wait for the delay, then animate
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(_showDelay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [UIView animateWithDuration:self.animationDuration
                              delay:0.0
                            options:UIViewAnimationOptionCurveEaseOut
                         animations:^{
                             self.webView.alpha = 1.0;
                         }
                         completion:^(BOOL finished) {
                             if (self.debugLogging) {
                                 NSLog(@"[BalancyWebView] Show animation completed");
                             }
                         }];
    });
}

- (void)hideForPersistentMode {
    self.view.hidden = YES;
    self.view.userInteractionEnabled = NO;
    _webView.alpha = 0.0;

    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Persistent WebView hidden");
    }
}

- (void)preparePersistentShellLoad {
    _persistentMode = YES;
    _suppressNextAnimation = YES;
    [self hideForPersistentMode];

    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Preparing persistent shell load");
    }
}

#pragma mark - View Lifecycle

- (void)viewDidLoad {
    [super viewDidLoad];
    
    // Add the WebView to the view controller's view
    [self.view addSubview:_webView];
    
    // Add the activity indicator
    [self.view addSubview:_activityIndicator];
    _activityIndicator.center = self.view.center;
    
    // Setup emergency exit button after WebView is added
    [self setupEmergencyExitButton];
    
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

    // Update emergency exit button position
    [self updateEmergencyExitButtonPosition];
}

#pragma mark - Public Methods

- (BOOL)loadURL:(NSString *)urlString {
    // Handle local file URLs
    if ([urlString hasPrefix:@"file://"]) {
        NSString *cleanUrl = urlString;
        NSString *filePath = [cleanUrl stringByReplacingOccurrencesOfString:@"file://" withString:@""];

        NSURL *fileURL = [NSURL fileURLWithPath:filePath];
        NSURL *broadReadAccessURL = GetPersistentDataRootURL();

        if (broadReadAccessURL == nil || ![filePath hasPrefix:broadReadAccessURL.path]) {
            // Fallback for any local file that is not under Documents.
            NSString *readAccessPath = filePath;
            for (int i = 0; i < kDirectoryLevelsUp; i++) {
                readAccessPath = [readAccessPath stringByDeletingLastPathComponent];
            }
            broadReadAccessURL = [NSURL fileURLWithPath:readAccessPath isDirectory:YES];
        }

        if (_debugLogging) {
            NSLog(@"[BalancyWebView] File URL: %@", fileURL);
            NSLog(@"[BalancyWebView] Read access URL: %@", broadReadAccessURL);
        }

        [_webView loadFileURL:fileURL allowingReadAccessToURL:broadReadAccessURL];
        
        if (_debugLogging) {
            NSLog(@"[BalancyWebView] Loading local file: %@", urlString);
        }
        
        return YES;
    }
    
    // Handle regular URLs
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
    _persistentMode = NO;
    _suppressNextAnimation = NO;

    // Remove emergency exit button and timer
    [_emergencyExitHideTimer invalidate];
    _emergencyExitHideTimer = nil;
    if (_emergencyExitButton) {
        [_emergencyExitButton removeFromSuperview];
        _emergencyExitButton = nil;
    }
    
    // Clean up WebView properly to avoid ExtensionKit termination errors
    if (_webView) {
        // Clear delegates first to prevent callbacks during cleanup
        _webView.navigationDelegate = nil;
        _webView.UIDelegate = nil;
        
        // Remove script message handler before stopping loading
        if (_userContentController) {
            [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
            _userContentController = nil;
        }
        
        // Stop all loading and navigation
        [_webView stopLoading];
        
        // Load about:blank to cleanly terminate web processes
        [_webView loadRequest:[NSURLRequest requestWithURL:[NSURL URLWithString:@"about:blank"]]];
        
        // Remove from view hierarchy
        [_webView removeFromSuperview];
        
        // Nullify the WebView reference immediately
        // This helps the system clean up WebKit processes
        _webView = nil;
        
        if (_debugLogging) {
            NSLog(@"[BalancyWebView] WebView cleanup initiated");
        }
    }
    
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
    escapedMessage = [escapedMessage stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
    
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
        // === COMPREHENSIVE GAME UI MODE SETTINGS ===
        
        // Disable all bounce effects
        _webView.scrollView.bounces = NO;
        _webView.scrollView.alwaysBounceVertical = NO;
        _webView.scrollView.alwaysBounceHorizontal = NO;
        _webView.scrollView.bouncesZoom = NO;
        
        // Disable zoom completely
        _webView.scrollView.minimumZoomScale = 1.0;
        _webView.scrollView.maximumZoomScale = 1.0;
        
        // Hide scroll indicators
        _webView.scrollView.showsVerticalScrollIndicator = NO;
        _webView.scrollView.showsHorizontalScrollIndicator = NO;
        
        // Disable native gestures
        _webView.allowsBackForwardNavigationGestures = NO;
        _webView.allowsLinkPreview = NO;
        
        // Disable scrolling
        _webView.scrollView.scrollEnabled = NO;
        
        // === SELECTIVE MAGNIFYING GLASS PREVENTION ===
        // Only disable double-tap and long-press gestures, keep other gestures for animations
        for (UIGestureRecognizer *recognizer in _webView.gestureRecognizers) {
            if ([recognizer isKindOfClass:[UITapGestureRecognizer class]]) {
                UITapGestureRecognizer *tapRecognizer = (UITapGestureRecognizer *)recognizer;
                if (tapRecognizer.numberOfTapsRequired == 2) {
                    recognizer.enabled = NO; // Disable double tap
                }
            }
            if ([recognizer isKindOfClass:[UILongPressGestureRecognizer class]]) {
                recognizer.enabled = NO; // Disable long press
            }
        }
        
        for (UIGestureRecognizer *recognizer in _webView.scrollView.gestureRecognizers) {
            if ([recognizer isKindOfClass:[UITapGestureRecognizer class]]) {
                UITapGestureRecognizer *tapRecognizer = (UITapGestureRecognizer *)recognizer;
                if (tapRecognizer.numberOfTapsRequired == 2) {
                    recognizer.enabled = NO; // Disable double tap zoom
                }
            }
            if ([recognizer isKindOfClass:[UILongPressGestureRecognizer class]]) {
                recognizer.enabled = NO; // Disable long press
            }
        }
        
        // Inject minimal UI customizations for game-like feel
        NSString *gameUIModeScript = @"\
        (function() {\
            // Basic game UI setup\
            document.documentElement.style.webkitUserSelect = 'none';\
            document.documentElement.style.userSelect = 'none';\
            document.documentElement.oncontextmenu = function() { return false; };\
            document.documentElement.style.webkitTouchCallout = 'none';\
            document.documentElement.style.webkitTextSizeAdjust = 'none';\
            document.documentElement.style.touchAction = 'manipulation';\
            \
            // Basic styling\
            var styleElement = document.createElement('style');\
            styleElement.textContent = '*:focus { outline: none !important; } img { pointer-events: none !important; -webkit-user-drag: none !important; }';\
            document.head.appendChild(styleElement);\
            \
            // Only prevent gesture events that cause magnifying glass\
            ['gesturestart', 'gesturechange', 'gestureend'].forEach(function(eventType) {\
                document.addEventListener(eventType, function(e) {\
                    e.preventDefault();\
                    e.stopPropagation();\
                    return false;\
                }, { passive: false, capture: true });\
            });\
            \
            // Minimal double-tap prevention only\
            var lastTouchEnd = 0;\
            document.addEventListener('touchend', function(e) {\
                var now = Date.now();\
                if (now - lastTouchEnd <= 200 && e.target.tagName !== 'BUTTON' && !e.target.onclick) {\
                    e.preventDefault();\
                }\
                lastTouchEnd = now;\
            }, { passive: false });\
            \
            return undefined;\
        })();\
        ";
        
        // FIX: Add proper completion handler to catch and ignore the "unsupported type" error
        [_webView evaluateJavaScript:gameUIModeScript completionHandler:^(id _Nullable result, NSError * _Nullable error) {
            if (error && self.debugLogging && error.code != 5) {
                NSLog(@"[BalancyWebView] Game UI mode script error (code %ld): %@", (long)error.code, error.localizedDescription);
            }
        }];
    } else {
        // Enable standard web browsing features
        _webView.scrollView.bounces = YES;
        _webView.scrollView.alwaysBounceVertical = YES;
        _webView.scrollView.alwaysBounceHorizontal = YES;
        _webView.scrollView.bouncesZoom = YES;
        _webView.scrollView.minimumZoomScale = 0.5;
        _webView.scrollView.maximumZoomScale = 3.0;
        _webView.scrollView.showsVerticalScrollIndicator = YES;
        _webView.scrollView.showsHorizontalScrollIndicator = YES;
        _webView.allowsBackForwardNavigationGestures = YES;
        _webView.allowsLinkPreview = YES;
        _webView.scrollView.scrollEnabled = YES;
        
        // Re-enable only the gestures we disabled (double-tap and long-press)
        for (UIGestureRecognizer *recognizer in _webView.gestureRecognizers) {
            if ([recognizer isKindOfClass:[UITapGestureRecognizer class]]) {
                UITapGestureRecognizer *tapRecognizer = (UITapGestureRecognizer *)recognizer;
                if (tapRecognizer.numberOfTapsRequired == 2) {
                    recognizer.enabled = YES; // Re-enable double tap
                }
            }
            if ([recognizer isKindOfClass:[UILongPressGestureRecognizer class]]) {
                recognizer.enabled = YES; // Re-enable long press
            }
        }
        
        for (UIGestureRecognizer *recognizer in _webView.scrollView.gestureRecognizers) {
            if ([recognizer isKindOfClass:[UITapGestureRecognizer class]]) {
                UITapGestureRecognizer *tapRecognizer = (UITapGestureRecognizer *)recognizer;
                if (tapRecognizer.numberOfTapsRequired == 2) {
                    recognizer.enabled = YES; // Re-enable double tap zoom
                }
            }
            if ([recognizer isKindOfClass:[UILongPressGestureRecognizer class]]) {
                recognizer.enabled = YES; // Re-enable long press
            }
        }
        
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
            \
            return undefined;\
        })();\
        ";
        
        // FIX: Add proper completion handler to catch and ignore the "unsupported type" error
        [_webView evaluateJavaScript:standardWebScript completionHandler:^(id _Nullable result, NSError * _Nullable error) {
            if (error && self.debugLogging && error.code != 5) {
                NSLog(@"[BalancyWebView] Standard web script error (code %ld): %@", (long)error.code, error.localizedDescription);
            }
        }];
    }
    
    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Game UI mode %@", enabled ? @"enabled" : @"disabled");
    }
}

#pragma mark - Emergency Exit Methods

// Setup triple-tap detection via JavaScript injection.
// WKWebView on iOS completely swallows UIKit touch events, so native gesture recognizers
// and overlay views cannot reliably detect taps. Instead, we inject a JS touchstart listener
// that counts rapid taps and sends a message to native when 3 fast taps are detected.
- (void)setupEmergencyExitButton {
    if (!_webView) return;

    // Inject JS triple-tap detector as a user script (runs at document start on every page)
    NSString *tripleTapScript = @"\
    (function() {\
        var _beeLastTap = 0;\
        var _beeCount = 0;\
        document.addEventListener('touchstart', function(e) {\
            var now = Date.now();\
            if (now - _beeLastTap < 400) {\
                _beeCount++;\
            } else {\
                _beeCount = 1;\
            }\
            _beeLastTap = now;\
            if (_beeCount >= 3) {\
                _beeCount = 0;\
                window.webkit.messageHandlers.BalancyWebView.postMessage('__emergencyTripleTap__');\
            }\
        }, { passive: true, capture: true });\
    })();\
    ";

    WKUserScript *tripleTapUserScript = [[WKUserScript alloc] initWithSource:tripleTapScript
                                                                injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                             forMainFrameOnly:YES];
    [_userContentController addUserScript:tripleTapUserScript];

    if (_debugLogging) {
        NSLog(@"[BalancyWebView] JS triple-tap emergency exit script injected");
    }
}

- (void)showEmergencyExitButton {
    if (!_emergencyExitEnabled) return;

    // Cancel any pending hide timer
    [_emergencyExitHideTimer invalidate];
    _emergencyExitHideTimer = nil;

    if (!_emergencyExitButton) {
        CGFloat btnSize = 32.0;
        _emergencyExitButton = [[UIView alloc] initWithFrame:CGRectMake(0, 0, btnSize, btnSize)];
        _emergencyExitButton.layer.cornerRadius = btnSize / 2.0;
        _emergencyExitButton.backgroundColor = [UIColor colorWithRed:0.9 green:0.1 blue:0.1 alpha:0.9];
        _emergencyExitButton.layer.zPosition = 1000;

        // Draw white X using CAShapeLayer
        CGFloat inset = 10.0;
        UIBezierPath *xPath = [UIBezierPath bezierPath];
        [xPath moveToPoint:CGPointMake(inset, inset)];
        [xPath addLineToPoint:CGPointMake(btnSize - inset, btnSize - inset)];
        [xPath moveToPoint:CGPointMake(btnSize - inset, inset)];
        [xPath addLineToPoint:CGPointMake(inset, btnSize - inset)];

        CAShapeLayer *xLayer = [CAShapeLayer layer];
        xLayer.path = xPath.CGPath;
        xLayer.strokeColor = [UIColor whiteColor].CGColor;
        xLayer.lineWidth = 2.5;
        xLayer.lineCap = kCALineCapRound;
        xLayer.fillColor = nil;
        [_emergencyExitButton.layer addSublayer:xLayer];

        // Tap handler on the button itself
        UITapGestureRecognizer *tap = [[UITapGestureRecognizer alloc] initWithTarget:self action:@selector(emergencyExitButtonTapped:)];
        [_emergencyExitButton addGestureRecognizer:tap];
        _emergencyExitButton.userInteractionEnabled = YES;

        [self.view addSubview:_emergencyExitButton];
    }

    // Position top-right corner with margin
    CGFloat margin = 12.0;
    CGFloat safeTop = 0;
    if (@available(iOS 11.0, *)) {
        safeTop = self.view.safeAreaInsets.top;
    }
    CGRect bounds = self.view.bounds;
    CGRect btnFrame = _emergencyExitButton.frame;
    btnFrame.origin.x = bounds.size.width - btnFrame.size.width - margin;
    btnFrame.origin.y = safeTop + margin;
    _emergencyExitButton.frame = btnFrame;

    [self.view bringSubviewToFront:_emergencyExitButton];
    _emergencyExitButton.hidden = NO;
    _emergencyExitButton.alpha = 1.0;

    // Auto-hide after 3 seconds
    _emergencyExitHideTimer = [NSTimer scheduledTimerWithTimeInterval:3.0
                                                              target:self
                                                            selector:@selector(hideEmergencyExitButton)
                                                            userInfo:nil
                                                             repeats:NO];

    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Emergency exit button shown at top-right");
    }
}

- (void)hideEmergencyExitButton {
    _emergencyExitHideTimer = nil;
    if (_emergencyExitButton) {
        [UIView animateWithDuration:0.3 animations:^{
            self->_emergencyExitButton.alpha = 0.0;
        } completion:^(BOOL finished) {
            self->_emergencyExitButton.hidden = YES;
        }];
    }
}

// Emergency exit button tap handler
- (void)emergencyExitButtonTapped:(id)sender {
    [_emergencyExitHideTimer invalidate];
    _emergencyExitHideTimer = nil;

    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Emergency exit button tapped in iOS mode");
    }

    // Send message to Unity
    if (_messageCallback != NULL) {
        _messageCallback("{\"action\":200, \"params\":{}}");
    }
}

// Update emergency exit button position when view layout changes
- (void)updateEmergencyExitButtonPosition {
    if (!_emergencyExitButton || _emergencyExitButton.hidden || !self.isViewLoaded) return;

    CGFloat margin = 12.0;
    CGFloat safeTop = 0;
    if (@available(iOS 11.0, *)) {
        safeTop = self.view.safeAreaInsets.top;
    }
    CGRect bounds = self.view.bounds;
    CGRect btnFrame = _emergencyExitButton.frame;
    btnFrame.origin.x = bounds.size.width - btnFrame.size.width - margin;
    btnFrame.origin.y = safeTop + margin;
    _emergencyExitButton.frame = btnFrame;
}

// Enable or disable emergency exit
- (void)setEmergencyExitEnabled:(BOOL)enabled {
    _emergencyExitEnabled = enabled;

    if (!enabled) {
        [_emergencyExitHideTimer invalidate];
        _emergencyExitHideTimer = nil;
        if (_emergencyExitButton) {
            [_emergencyExitButton removeFromSuperview];
            _emergencyExitButton = nil;
        }
    }
    // When enabled, the triple-tap gesture (always active on webView) handles showing the button

    if (_debugLogging) {
        NSLog(@"[BalancyWebView] Emergency exit %@", enabled ? @"enabled" : @"disabled");
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
    // WebView is always full screen via Auto Layout constraints - nothing to do here
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
        // IMPORTANT: Return undefined explicitly to avoid WKErrorJavaScriptResultTypeIsUnsupported
        NSString *transparencyScript = @"\
        (function() {\
            document.body.style.backgroundColor = 'transparent';\
            document.documentElement.style.backgroundColor = 'transparent';\
            var style = document.createElement('style');\
            style.type = 'text/css';\
            style.innerHTML = 'body { background-color: transparent !important; }';\
            document.head.appendChild(style);\
            return undefined;\
        })();\
        ";
        
        // FIX: Add proper completion handler to catch and ignore the "unsupported type" error
        [_webView evaluateJavaScript:transparencyScript completionHandler:^(id _Nullable result, NSError * _Nullable error) {
            if (error) {
                // WKErrorJavaScriptResultTypeIsUnsupported = 5
                // Only log unexpected errors, not the common "unsupported type" error
                if (self.debugLogging && error.code != 5) {
                    NSLog(@"[BalancyWebView] Transparency script error (code %ld): %@", (long)error.code, error.localizedDescription);
                }
            }
        }];
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

    // Check for emergency triple-tap signal from JS
    if ([message.body isKindOfClass:[NSString class]] && [message.body isEqualToString:@"__emergencyTripleTap__"]) {
        if (_debugLogging) {
            NSLog(@"[BalancyWebView] Triple-tap detected via JS — showing emergency exit button");
        }
        [self showEmergencyExitButton];
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

    NSString *initScript = @"if (window.BalancyWebView && typeof window.BalancyWebView.initResponseHandler === 'function') { window.BalancyWebView.initResponseHandler(); }";
    [_webView evaluateJavaScript:initScript completionHandler:nil];

    if (_suppressNextAnimation) {
        _suppressNextAnimation = NO;

        if (_debugLogging) {
            NSLog(@"[BalancyWebView] Persistent shell loaded, staying hidden");
        }

        if (_loadCompletedCallback != NULL) {
            _loadCompletedCallback(true);
        }
        return;
    }

    // Start the show animation instead of immediately showing
    [self startShowAnimation];
    
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

// Helper function to get the root view controller safely across all iOS versions
static UIViewController* GetRootViewController() {
    UIViewController* rootViewController = nil;
    
    // iOS 13+ uses scenes
    if (@available(iOS 13.0, *)) {
        // Try to get from the first connected scene
        NSSet<UIScene *> *connectedScenes = [UIApplication sharedApplication].connectedScenes;
        for (UIScene *scene in connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene *windowScene = (UIWindowScene *)scene;
                for (UIWindow *window in windowScene.windows) {
                    if (window.isKeyWindow) {
                        rootViewController = window.rootViewController;
                        if (rootViewController) {
                            return rootViewController;
                        }
                    }
                }
            }
        }
        
        // Fallback: try the first window in the first scene
        for (UIScene *scene in connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene *windowScene = (UIWindowScene *)scene;
                UIWindow *firstWindow = windowScene.windows.firstObject;
                if (firstWindow && firstWindow.rootViewController) {
                    return firstWindow.rootViewController;
                }
            }
        }
    }
    
    // iOS 12 and earlier - use keyWindow directly
    rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
    
    // Final fallback - try all windows
    if (!rootViewController) {
        for (UIWindow *window in [UIApplication sharedApplication].windows) {
            if (window.rootViewController) {
                rootViewController = window.rootViewController;
                break;
            }
        }
    }
    
    return rootViewController;
}

// These functions are exported to be called from Unity's C# code

extern "C" {

// Open a WebView with the specified URL
bool _balancyOpenWebView(const char* url) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = CreateOrGetWebViewController(_messageCallback,
                                                                                   _loadCompletedCallback,
                                                                                   _cacheCompletedCallback);
        if (!webViewController) {
            NSLog(@"[BalancyWebView] Failed to get root view controller");
            return false;
        }

        webViewController.persistentMode = NO;
        webViewController.view.hidden = NO;
        webViewController.view.userInteractionEnabled = YES;

        // Load the URL
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        return [webViewController loadURL:nsUrl];
    }
}

// Open a WebView with the specified URL and custom size
bool _balancyOpenWebViewWithSize(const char* url, int width, int height) {
    @autoreleasepool {
        (void)width;
        (void)height;

        BalancyWebViewController* webViewController = CreateOrGetWebViewController(_messageCallback,
                                                                                   _loadCompletedCallback,
                                                                                   _cacheCompletedCallback);
        if (!webViewController) {
            NSLog(@"[BalancyWebView] Failed to get root view controller");
            return false;
        }

        webViewController.persistentMode = NO;
        webViewController.view.hidden = NO;
        webViewController.view.userInteractionEnabled = YES;

        // Load the URL
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        return [webViewController loadURL:nsUrl];
    }
}

// Close the WebView
void _balancyCloseWebView() {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController close];
            if (_sharedController == webViewController) {
                _sharedController = nil;
            }
        }
    }
}

bool _balancyPrepareWebView(const char* shellUrl) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = CreateOrGetWebViewController(_messageCallback,
                                                                                   _loadCompletedCallback,
                                                                                   _cacheCompletedCallback);
        if (!webViewController) {
            NSLog(@"[BalancyWebView] Failed to create persistent WebView controller");
            return false;
        }

        [webViewController preparePersistentShellLoad];

        NSString* sourcePath = [[NSString stringWithUTF8String:shellUrl] stringByReplacingOccurrencesOfString:@"file://" withString:@""];
        NSURL* persistentShellURL = CreatePersistentShellURL(sourcePath);
        NSString* nsUrl = persistentShellURL != nil ? persistentShellURL.absoluteString : [NSString stringWithUTF8String:shellUrl];
        return [webViewController loadURL:nsUrl];
    }
}

void _balancyShowWebView() {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController startShowAnimation];
        }
    }
}

void _balancyHideWebView() {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController hideForPersistentMode];
        }
    }
}

// Send a message to the WebView
bool _balancySendMessage(const char* message) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            NSString* nsMessage = [NSString stringWithUTF8String:message];
            return [webViewController sendMessage:nsMessage];
        }
        return false;
    }
}

// Call a JavaScript function in the WebView
const char* _balancyCallJavaScript(const char* function, const char** args, int argsCount) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            NSString* nsFunction = [NSString stringWithUTF8String:function];
            NSMutableArray<NSString*>* nsArgs = [NSMutableArray arrayWithCapacity:argsCount];

            for (int i = 0; i < argsCount; i++) {
                NSString* arg = [NSString stringWithUTF8String:args[i]];
                [nsArgs addObject:arg];
            }

            NSString* result = [webViewController callJavaScript:nsFunction arguments:nsArgs];

            const char* cResult = [result UTF8String];
            char* resultCopy = (char*)malloc(strlen(cResult) + 1);
            strcpy(resultCopy, cResult);
            return resultCopy;
        }

        return strdup("{\"error\": \"WebView not found\"}");
    }
}

// Set the viewport rectangle for the WebView
void _balancySetViewportRect(float x, float y, float width, float height) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController setViewportRect:x y:y width:width height:height];
        }
    }
}

// Set transparent background for the WebView
void _balancySetTransparentBackground(bool transparent) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController setTransparentBackground:transparent];
        }
    }
}

// Enable or disable offline caching
void _balancySetOfflineCacheEnabled(bool enabled) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController setOfflineCacheEnabled:enabled];
        }
    }
}

// Enable or disable debug logging
void _balancySetDebugLogging(bool enabled) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController setDebugLogging:enabled];
        }
    }
}

// Enable or disable game UI mode
void _balancySetGameUIMode(bool enabled) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController setGameUIMode:enabled];
        }
    }
}

// Enable or disable emergency exit
void _balancySetEmergencyExitEnabled(bool enabled) {
    @autoreleasepool {
        BalancyWebViewController* webViewController = GetExistingWebViewController();
        if (webViewController != nil) {
            [webViewController setEmergencyExitEnabled:enabled];
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

// Note: The following functions are implemented in BalancyWebViewUnityBridge.mm:
// - _balancyRegisterCacheCompletedCallback
// - _balancyInjectJSCode
// - _balancySetShowDelay
// - _balancySetAnimationDuration
// - _balancySetEmergencyExitEnabled

} // extern "C"
