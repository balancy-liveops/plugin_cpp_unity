//
//  BalancyWebviewMac.mm
//  Native macOS WebView implementation for Unity
//

#import "BalancyWebviewMac.h"

// Function pointers for callbacks
typedef void (*MessageCallback)(const char* message);
typedef void (*LoadCompletedCallback)(bool success);
typedef void (*CacheCompletedCallback)(bool success);

// Global callback function pointers
static MessageCallback _messageCallback = NULL;
static LoadCompletedCallback _loadCompletedCallback = NULL;
static CacheCompletedCallback _cacheCompletedCallback = NULL;

// Unity logging function - sends logs to Unity console
// Note: UnitySendMessage is provided by Unity at runtime, not during library build
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg) __attribute__((weak));

void LogToUnity(const char* message) {
    // Send log message to Unity console if available
    if (UnitySendMessage != NULL) {
        UnitySendMessage("BalancyWebView", "LogFromNative", message);
    }
    // Always log to system console for debugging
    NSLog(@"[BalancyWebView] %s", message);
}

// Forward declaration
@interface BalancyWebViewController : NSWindowController <WKNavigationDelegate, WKScriptMessageHandler>
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) BOOL transparentBackground;
@property (nonatomic, assign) BOOL offlineCacheEnabled;

- (instancetype)init;
- (BOOL)loadURL:(NSString *)url;
- (void)close;
- (BOOL)sendMessage:(NSString *)message;
- (BOOL)injectJSCode:(NSString *)code;
- (NSString *)callJavaScript:(NSString *)function args:(NSArray<NSString *> *)args;
- (void)setViewportRect:(CGFloat)x y:(CGFloat)y width:(CGFloat)width height:(CGFloat)height;
- (void)setTransparentBackground:(BOOL)transparent;
- (void)setDebugLogging:(BOOL)enabled;
@end

// Global WebView controller instance
static BalancyWebViewController* _sharedController = nil;

@implementation BalancyWebViewController {
    NSRect _viewportRect;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _debugLogging = NO;
        _transparentBackground = NO;
        _offlineCacheEnabled = NO;
        _viewportRect = NSMakeRect(0, 0, 1, 1);
        
        // Create a window
        NSRect screenRect = [[NSScreen mainScreen] frame];
        NSRect windowRect = NSMakeRect(0, 0, screenRect.size.width * 0.8, screenRect.size.height * 0.8);
        NSWindow *window = [[NSWindow alloc] initWithContentRect:windowRect
                                                      styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskResizable 
                                                        backing:NSBackingStoreBuffered 
                                                          defer:NO];
        [window setTitle:@"Balancy WebView"];
        [window center];
        
        // Initialize with the window
        self = [self initWithWindow:window];
        
        // Configure WebView
        WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
        _userContentController = [[WKUserContentController alloc] init];
        [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
        configuration.userContentController = _userContentController;
        
        //debugging
        // Enable developer extras for debugging
        if (@available(macOS 10.11, *)) {
            [configuration.preferences setValue:@YES forKey:@"developerExtrasEnabled"];
        }
        
        // You might also want to enable these for better debugging
        [configuration.preferences setValue:@YES forKey:@"fullScreenEnabled"];
        [configuration.preferences setValue:@YES forKey:@"javaScriptCanAccessClipboard"];
        [configuration.preferences setValue:@YES forKey:@"shouldAllowUserInstalledFonts"];
        //debugging...
        
        // Create WebView
        _webView = [[WKWebView alloc] initWithFrame:[[window contentView] bounds] configuration:configuration];
        _webView.navigationDelegate = self;
        _webView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        [[window contentView] addSubview:_webView];
    }
    return self;
}

- (BOOL)loadURL:(NSString *)url {
    // Show window
    [[self window] makeKeyAndOrderFront:nil];
    
    if ([url hasPrefix:@"file://"]) {
        NSString *cleanUrl = url;
        NSString *filePath = [cleanUrl stringByReplacingOccurrencesOfString:@"file://" withString:@""];

        NSURL *fileURL = [NSURL fileURLWithPath:filePath];
        NSURL *readAccessURL = [fileURL URLByDeletingLastPathComponent];
        

        NSString *htmlPath = [fileURL path];
        NSString *parentDir = [htmlPath stringByDeletingLastPathComponent]; // Gets the immediate parent
        NSString *filesDir = [parentDir stringByDeletingLastPathComponent];  // Goes up one more level to "Files"
        
        NSURL *broadReadAccessURL = [NSURL fileURLWithPath:filesDir];
        
        if (_debugLogging) {
            NSString *logMsg = [NSString stringWithFormat:@"File URL: %@", fileURL];
            LogToUnity([logMsg UTF8String]);
            NSString *logMsg2 = [NSString stringWithFormat:@"Read access URL: %@", broadReadAccessURL];
            LogToUnity([logMsg2 UTF8String]);
        }
        
        [_webView loadFileURL:fileURL allowingReadAccessToURL:broadReadAccessURL];
        return YES;
    }
    
    // Load URL
    NSURL *nsUrl = [NSURL URLWithString:url];
    if (!nsUrl) {
        LogToUnity("Invalid URL");
        return NO;
    }
    
    [_webView loadRequest:[NSURLRequest requestWithURL:nsUrl]];
    return YES;
}

- (void)close {
    [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
    [_webView stopLoading];
    [[self window] close];
}

- (BOOL)sendMessage:(NSString *)message {
    if (!_webView) return NO;
    
    NSString *escapedMessage = [message stringByReplacingOccurrencesOfString:@"'" withString:@"\\'"];
    NSString *script = [NSString stringWithFormat:@"if (balancy) { balancy._receiveMessageFromUnity('%@'); }", escapedMessage];
    
    [_webView evaluateJavaScript:script completionHandler:nil];
    return YES;
}

- (BOOL)injectJSCode:(NSString *)code {
    if (!_webView) return NO;
    
    [_webView evaluateJavaScript:code completionHandler:nil];
    return YES;
}

- (NSString *)callJavaScript:(NSString *)function args:(NSArray<NSString *> *)args {
    if (!_webView) return @"{\"error\": \"WebView not initialized\"}";
    
    // Build script
    NSMutableString *script = [NSMutableString stringWithString:function];
    if (![function isEqualToString:@"eval"]) {
        [script appendString:@"("];
        for (NSUInteger i = 0; i < args.count; i++) {
            [script appendFormat:@"\"%@\"%@", 
                [args[i] stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""], 
                (i < args.count - 1) ? @", " : @""];
        }
        [script appendString:@")"];
    } else if (args.count > 0) {
        script = [NSMutableString stringWithString:args[0]];
    }
    
    // Execute synchronously with semaphore
    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
    __block NSString *result = @"{\"error\": \"timeout\"}";
    
    [_webView evaluateJavaScript:script completionHandler:^(id _Nullable jsResult, NSError * _Nullable error) {
        if (error) {
            result = [NSString stringWithFormat:@"{\"error\": \"%@\"}", error.localizedDescription];
        } else if (jsResult == nil) {
            result = @"null";
        } else if ([jsResult isKindOfClass:[NSString class]]) {
            result = (NSString *)jsResult;
        } else {
            result = [NSString stringWithFormat:@"%@", jsResult];
        }
        dispatch_semaphore_signal(semaphore);
    }];
    
    dispatch_semaphore_wait(semaphore, dispatch_time(DISPATCH_TIME_NOW, 3 * NSEC_PER_SEC));
    return result;
}

- (void)setViewportRect:(CGFloat)x y:(CGFloat)y width:(CGFloat)width height:(CGFloat)height {
    _viewportRect = NSMakeRect(x, y, width, height);
    
    if ([self window]) {
        NSRect screenRect = [[NSScreen mainScreen] frame];
        NSRect windowRect = NSMakeRect(
            screenRect.size.width * x,
            screenRect.size.height * (1 - y - height),
            screenRect.size.width * width,
            screenRect.size.height * height
        );
        [[self window] setFrame:windowRect display:YES];
    }
}

- (void)setTransparentBackground:(BOOL)transparent {
    _transparentBackground = transparent;
    
    if (_webView) {
        [_webView setValue:@(!transparent) forKey:@"drawsBackground"];
        
        if (transparent) {
            [[self window] setBackgroundColor:[NSColor clearColor]];
            [[self window] setOpaque:NO];
            
            NSString *jsCode = @"document.body.style.backgroundColor = 'transparent';";
            [_webView evaluateJavaScript:jsCode completionHandler:nil];
        } else {
            [[self window] setBackgroundColor:[NSColor windowBackgroundColor]];
            [[self window] setOpaque:YES];
        }
    }
}

- (void)setDebugLogging:(BOOL)enabled {
    _debugLogging = enabled;
}

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    if (![message.name isEqualToString:@"BalancyWebView"]) return;
    
    NSString *messageString;
    if ([message.body isKindOfClass:[NSString class]]) {
        messageString = (NSString *)message.body;
    } else {
        messageString = [NSString stringWithFormat:@"%@", message.body];
    }
    
    if (_messageCallback) {
        _messageCallback([messageString UTF8String]);
    }
}

#pragma mark - WKNavigationDelegate

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    LogToUnity("WebView navigation finished successfully");
    
    if (_transparentBackground) {
        [self setTransparentBackground:YES];
    }
    
    // Ensure the response handler is initialized
    NSString *initScript = @"if (window.BalancyWebView && typeof window.BalancyWebView.initResponseHandler === 'function') { window.BalancyWebView.initResponseHandler(); }";
    [_webView evaluateJavaScript:initScript completionHandler:nil];
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(true);
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSString *errorMsg = [NSString stringWithFormat:@"Navigation failed with error: %@", error.localizedDescription];
    LogToUnity([errorMsg UTF8String]);
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSString *errorMsg = [NSString stringWithFormat:@"Provisional navigation failed with error: %@", error.localizedDescription];
    LogToUnity([errorMsg UTF8String]);
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
    if (_debugLogging) {
        LogToUnity("WebView started loading");
    }
}

@end

// C interface for Unity
extern "C" {

bool _balancyOpenWebView(const char* url) {
    @autoreleasepool {
        LogToUnity("_balancyOpenWebView called");
        
        if (_sharedController == nil) {
            LogToUnity("Creating new WebView controller");
            _sharedController = [[BalancyWebViewController alloc] init];
        }
        
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        NSString *logMsg = [NSString stringWithFormat:@"Attempting to load URL: %@", nsUrl];
        LogToUnity([logMsg UTF8String]);
        
        BOOL result = [_sharedController loadURL:nsUrl];
        
        NSString *resultMsg = [NSString stringWithFormat:@"_balancyOpenWebView result: %@", result ? @"SUCCESS" : @"FAILED"];
        LogToUnity([resultMsg UTF8String]);
        
        return result;
    }
}

void _balancyCloseWebView() {
    @autoreleasepool {
        if (_sharedController != nil) {
            [_sharedController close];
            _sharedController = nil;
        }
    }
}

bool _balancySendMessage(const char* message) {
    @autoreleasepool {
        if (_sharedController == nil) return false;
        
        NSString* nsMessage = [NSString stringWithUTF8String:message];
        return [_sharedController sendMessage:nsMessage];
    }
}

bool _balancyInjectJSCode(const char* message) {
    @autoreleasepool {
        if (_sharedController == nil) return false;
        
        NSString* nsMessage = [NSString stringWithUTF8String:message];
        return [_sharedController injectJSCode:nsMessage];
    }
}

const char* _balancyCallJavaScript(const char* function, const char** args, int argsCount) {
    @autoreleasepool {
        if (_sharedController == nil) {
            return strdup("{\"error\": \"WebView not found\"}");
        }
        
        NSString* nsFunction = [NSString stringWithUTF8String:function];
        NSMutableArray<NSString*>* nsArgs = [NSMutableArray arrayWithCapacity:argsCount];
        
        for (int i = 0; i < argsCount; i++) {
            [nsArgs addObject:[NSString stringWithUTF8String:args[i]]];
        }
        
        NSString* result = [_sharedController callJavaScript:nsFunction args:nsArgs];
        return strdup([result UTF8String]);
    }
}

void _balancySetViewportRect(float x, float y, float width, float height) {
    @autoreleasepool {
        if (_sharedController != nil) {
            [_sharedController setViewportRect:x y:y width:width height:height];
        }
    }
}

void _balancySetTransparentBackground(bool transparent) {
    @autoreleasepool {
        if (_sharedController != nil) {
            [_sharedController setTransparentBackground:transparent];
        }
    }
}

void _balancySetOfflineCacheEnabled(bool enabled) {
    @autoreleasepool {
        if (_sharedController != nil) {
            _sharedController.offlineCacheEnabled = enabled;
        }
    }
}

void _balancySetDebugLogging(bool enabled) {
    @autoreleasepool {
        if (_sharedController != nil) {
            [_sharedController setDebugLogging:enabled];
        }
    }
}

void _balancyRegisterMessageCallback(MessageCallback callback) {
    _messageCallback = callback;
}

void _balancyRegisterLoadCompletedCallback(LoadCompletedCallback callback) {
    _loadCompletedCallback = callback;
}

void _balancyRegisterCacheCompletedCallback(CacheCompletedCallback callback) {
    _cacheCompletedCallback = callback;
}

}
