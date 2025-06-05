//
//  BalancyWebviewMac.mm
//  Native macOS WebView implementation for Unity
//

#import "BalancyWebviewMac.h"
#import <Metal/Metal.h>
#import <QuartzCore/QuartzCore.h>

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

// Forward declarations
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

// Embedded WebView controller for rendering to texture
@interface BalancyEmbeddedWebViewController : NSViewController <WKNavigationDelegate, WKScriptMessageHandler>
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) void* texturePtr;
@property (nonatomic, assign) int textureWidth;
@property (nonatomic, assign) int textureHeight;
@property (nonatomic, strong) NSTimer *renderTimer;
@property (nonatomic, assign) unsigned char* pixelBuffer;
@property (nonatomic, assign) BOOL pixelDataReady;
@property (nonatomic, strong) NSWindow *offscreenWindow;

- (instancetype)initWithTexture:(void*)texturePtr width:(int)width height:(int)height;
- (BOOL)loadURL:(NSString *)url;
- (void)close;
- (BOOL)sendMessage:(NSString *)message;
- (BOOL)injectJSCode:(NSString *)code;
- (void)updateTexture:(void*)texturePtr width:(int)width height:(int)height;
- (void)handleMouseEvent:(int)x y:(int)y isClick:(BOOL)isClick;
- (void)renderToTexture;
@end

// Implementation of embedded WebView controller
@implementation BalancyEmbeddedWebViewController

- (instancetype)initWithTexture:(void*)texturePtr width:(int)width height:(int)height {
    self = [super init];
    if (self) {
        _debugLogging = YES;
        _texturePtr = texturePtr;
        _textureWidth = width;
        _textureHeight = height;
        _pixelDataReady = NO;
        
        // Allocate pixel buffer
        size_t bufferSize = width * height * 4; // RGBA
        _pixelBuffer = (unsigned char*)malloc(bufferSize);
        memset(_pixelBuffer, 0, bufferSize); // Initialize to transparent (all zeros)
        
        // Create off-screen window to host the WebView
        NSRect windowFrame = NSMakeRect(-10000, -10000, width, height); // Position off-screen
        _offscreenWindow = [[NSWindow alloc] initWithContentRect:windowFrame
                                                      styleMask:NSWindowStyleMaskBorderless
                                                        backing:NSBackingStoreBuffered
                                                          defer:NO];
        [_offscreenWindow setLevel:kCGMinimumWindowLevel]; // Ensure it's not visible
        [_offscreenWindow setAlphaValue:0.0]; // Make it invisible
        [_offscreenWindow orderBack:nil]; // Put it in the back
        
        // Create container view with layer backing and transparency
        NSView *containerView = [[NSView alloc] initWithFrame:NSMakeRect(0, 0, width, height)];
        containerView.wantsLayer = YES; // Enable layer backing
        containerView.layer.backgroundColor = [[NSColor clearColor] CGColor]; // Transparent background
        containerView.layer.opaque = NO; // Enable transparency
        self.view = containerView;
        
        // Set the container as the window's content view
        [_offscreenWindow setContentView:containerView];
        
        // Configure WebView with better settings for off-screen rendering
        WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
        _userContentController = [[WKUserContentController alloc] init];
        [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
        configuration.userContentController = _userContentController;
        
        // Enable developer extras for debugging
        if (@available(macOS 10.11, *)) {
            [configuration.preferences setValue:@YES forKey:@"developerExtrasEnabled"];
        }
        
        // Additional WebView preferences for better rendering
        // mediaTypesRequiringUserActionForPlayback is available on macOS 10.12+
        if (@available(macOS 10.12, *)) {
            configuration.mediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypeNone;
        }
        
        // Create WebView with the specified size
        _webView = [[WKWebView alloc] initWithFrame:NSMakeRect(0, 0, width, height) configuration:configuration];
        _webView.navigationDelegate = self;
        
        // Enable layer backing for WebView with transparency support
        _webView.wantsLayer = YES;
        _webView.layer.backgroundColor = [[NSColor clearColor] CGColor]; // Use clear background
        _webView.layer.contentsScale = 1.0;
        _webView.layer.opaque = NO; // Enable transparency
        
        // Ensure WebView doesn't draw its own background
        [_webView setValue:@NO forKey:@"drawsBackground"];
        
        // Make WebView container transparent too
        containerView.layer.backgroundColor = [[NSColor clearColor] CGColor];
        containerView.layer.opaque = NO;
        
        // Set proper autoresizing
        _webView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        
        [containerView addSubview:_webView];
        
        // Start render timer (30 FPS)
        _renderTimer = [NSTimer scheduledTimerWithTimeInterval:1.0/30.0
                                                        target:self
                                                      selector:@selector(renderToTexture)
                                                      userInfo:nil
                                                       repeats:YES];
        
        if (_debugLogging) {
            LogToUnity("Embedded WebView controller initialized with off-screen window");
        }
    }
    return self;
}

- (BOOL)loadURL:(NSString *)url {
    if ([url hasPrefix:@"file://"]) {
        NSString *cleanUrl = url;
        NSString *filePath = [cleanUrl stringByReplacingOccurrencesOfString:@"file://" withString:@""];
        
        NSURL *fileURL = [NSURL fileURLWithPath:filePath];
        NSString *htmlPath = [fileURL path];
        NSString *parentDir = [htmlPath stringByDeletingLastPathComponent];
        NSString *filesDir = [parentDir stringByDeletingLastPathComponent];
        
        NSURL *broadReadAccessURL = [NSURL fileURLWithPath:filesDir];
        
        if (_debugLogging) {
            NSString *logMsg = [NSString stringWithFormat:@"Embedded File URL: %@", fileURL];
            LogToUnity([logMsg UTF8String]);
        }
        
        [_webView loadFileURL:fileURL allowingReadAccessToURL:broadReadAccessURL];
        return YES;
    }
    
    NSURL *nsUrl = [NSURL URLWithString:url];
    if (!nsUrl) {
        LogToUnity("Invalid URL for embedded WebView");
        return NO;
    }
    
    [_webView loadRequest:[NSURLRequest requestWithURL:nsUrl]];
    return YES;
}

- (void)close {
    if (_renderTimer) {
        [_renderTimer invalidate];
        _renderTimer = nil;
    }
    
    // Free pixel buffer
    if (_pixelBuffer) {
        free(_pixelBuffer);
        _pixelBuffer = nil;
    }
    
    [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
    [_webView stopLoading];
    [_webView removeFromSuperview];
    _webView = nil;
    
    // Close off-screen window
    if (_offscreenWindow) {
        [_offscreenWindow close];
        _offscreenWindow = nil;
    }
    
    if (_debugLogging) {
        LogToUnity("Embedded WebView closed");
    }
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

- (void)updateTexture:(void*)texturePtr width:(int)width height:(int)height {
    _texturePtr = texturePtr;
    _textureWidth = width;
    _textureHeight = height;
    
    // Reallocate pixel buffer if size changed
    size_t newBufferSize = width * height * 4; // RGBA
    if (_pixelBuffer) {
        free(_pixelBuffer);
    }
    _pixelBuffer = (unsigned char*)malloc(newBufferSize);
    memset(_pixelBuffer, 0, newBufferSize); // Initialize to transparent
    _pixelDataReady = NO;
    
    // Update WebView frame and window
    _webView.frame = NSMakeRect(0, 0, width, height);
    self.view.frame = NSMakeRect(0, 0, width, height);
    
    if (_offscreenWindow) {
        NSRect windowFrame = NSMakeRect(-10000, -10000, width, height);
        [_offscreenWindow setFrame:windowFrame display:NO];
    }
    
    if (_debugLogging) {
        NSString *logMsg = [NSString stringWithFormat:@"Updated embedded texture: %dx%d", width, height];
        LogToUnity([logMsg UTF8String]);
    }
}

- (void)handleMouseEvent:(int)x y:(int)y isClick:(BOOL)isClick {
    if (!_webView) return;
    
    // Create mouse event and send to WebView
    NSPoint point = NSMakePoint(x, _textureHeight - y); // Flip Y coordinate
    
    if (isClick) {
        // Simulate mouse down and up events
        NSEvent *mouseDown = [NSEvent mouseEventWithType:NSEventTypeLeftMouseDown
                                                 location:point
                                            modifierFlags:0
                                                timestamp:[[NSProcessInfo processInfo] systemUptime]
                                             windowNumber:0
                                                  context:nil
                                              eventNumber:0
                                               clickCount:1
                                                 pressure:1.0];
        
        NSEvent *mouseUp = [NSEvent mouseEventWithType:NSEventTypeLeftMouseUp
                                               location:point
                                          modifierFlags:0
                                              timestamp:[[NSProcessInfo processInfo] systemUptime]
                                           windowNumber:0
                                                context:nil
                                            eventNumber:0
                                             clickCount:1
                                               pressure:1.0];
        
        [_webView mouseDown:mouseDown];
        [_webView mouseUp:mouseUp];
        
        if (_debugLogging) {
            NSString *logMsg = [NSString stringWithFormat:@"Mouse click at (%d, %d)", x, y];
            LogToUnity([logMsg UTF8String]);
        }
    }
}

- (void)renderToTexture {
    if (!_webView || !_pixelBuffer) {
        return;
    }
    
    @try {
        // Use WKWebView's snapshot API for better rendering
        if (@available(macOS 10.13, *)) {
            WKSnapshotConfiguration *config = [[WKSnapshotConfiguration alloc] init];
            config.rect = CGRectMake(0, 0, _textureWidth, _textureHeight);
            
            [_webView takeSnapshotWithConfiguration:config completionHandler:^(NSImage * _Nullable snapshotImage, NSError * _Nullable error) {
                if (error || !snapshotImage) {
                    if (_debugLogging && error) {
                        NSString *logMsg = [NSString stringWithFormat:@"Snapshot error: %@", error.localizedDescription];
                        LogToUnity([logMsg UTF8String]);
                    }
                    return;
                }
                
                // Convert NSImage to pixel data
                CGImageRef cgImage = [snapshotImage CGImageForProposedRect:nil context:nil hints:nil];
                if (cgImage) {
                    CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
                    size_t bytesPerRow = _textureWidth * 4; // RGBA
                    
                    CGContextRef context = CGBitmapContextCreate(
                        _pixelBuffer,
                        _textureWidth,
                        _textureHeight,
                        8, // bits per component
                        bytesPerRow,
                        colorSpace,
                        kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big
                    );
                    
                    if (context) {
                        // Fill with transparent background
                        CGContextClearRect(context, CGRectMake(0, 0, _textureWidth, _textureHeight));
                        
                        // Draw the image
                        CGContextDrawImage(context, CGRectMake(0, 0, _textureWidth, _textureHeight), cgImage);
                        
                        _pixelDataReady = YES;
                        
                        CGContextRelease(context);
                    }
                    
                    CGColorSpaceRelease(colorSpace);
                }
            }];
            return;
        }
        
        // Fallback for older macOS versions - use layer rendering with proper view hierarchy
        if (!_webView.superview || !_webView.layer) {
            return;
        }
        
        // Create bitmap context
        CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
        size_t bytesPerRow = _textureWidth * 4; // RGBA
        
        CGContextRef context = CGBitmapContextCreate(
            _pixelBuffer,
            _textureWidth,
            _textureHeight,
            8, // bits per component
            bytesPerRow,
            colorSpace,
            kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big
        );
        
        if (context) {
            // Fill with transparent background
            CGContextClearRect(context, CGRectMake(0, 0, _textureWidth, _textureHeight));
            
            // Save context state
            CGContextSaveGState(context);
            
            // Scale the layer to fit our texture dimensions
            CGFloat scaleX = (CGFloat)_textureWidth / _webView.frame.size.width;
            CGFloat scaleY = (CGFloat)_textureHeight / _webView.frame.size.height;
            CGContextScaleCTM(context, scaleX, scaleY);
            
            // Render the WebView layer
            [_webView.layer renderInContext:context];
            
            // Restore context state
            CGContextRestoreGState(context);
            
            _pixelDataReady = YES;
            
            CGContextRelease(context);
        }
        
        CGColorSpaceRelease(colorSpace);
        
    } @catch (NSException *exception) {
        if (_debugLogging) {
            NSString *logMsg = [NSString stringWithFormat:@"Render exception: %@", exception.reason];
            LogToUnity([logMsg UTF8String]);
        }
    }
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
    if (_debugLogging) {
        LogToUnity("Embedded WebView navigation finished successfully");
    }
    
    // Inject minimal CSS to ensure proper rendering without forcing backgrounds
    NSString *jsCode = @"\
        var meta = document.querySelector('meta[name=viewport]'); \
        if (!meta) { \
            meta = document.createElement('meta'); \
            meta.name = 'viewport'; \
            document.head.appendChild(meta); \
        } \
        meta.content = 'width=device-width, initial-scale=1.0'; \
        \
        // Remove any forced overflow hidden that might hide content \
        document.body.style.overflow = 'auto'; \
        document.documentElement.style.overflow = 'auto';\
    ";
    
    [_webView evaluateJavaScript:jsCode completionHandler:nil];
    
    // Ensure the response handler is initialized
    NSString *initScript = @"if (window.BalancyWebView && typeof window.BalancyWebView.initResponseHandler === 'function') { window.BalancyWebView.initResponseHandler(); }";
    [_webView evaluateJavaScript:initScript completionHandler:nil];
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(true);
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSString *errorMsg = [NSString stringWithFormat:@"Embedded navigation failed with error: %@", error.localizedDescription];
    LogToUnity([errorMsg UTF8String]);
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSString *errorMsg = [NSString stringWithFormat:@"Embedded provisional navigation failed with error: %@", error.localizedDescription];
    LogToUnity([errorMsg UTF8String]);
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
    if (_debugLogging) {
        LogToUnity("Embedded WebView started loading");
    }
}

@end

// Global WebView controller instances
static BalancyWebViewController* _sharedController = nil;
static BalancyEmbeddedWebViewController* _embeddedController = nil;

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

// Embedding functionality implementation
bool _balancyOpenWebViewEmbedded(const char* url, void* texturePtr, int width, int height) {
    @autoreleasepool {
        LogToUnity("_balancyOpenWebViewEmbedded called");
        
        if (_embeddedController != nil) {
            LogToUnity("Closing existing embedded WebView");
            [_embeddedController close];
            _embeddedController = nil;
        }
        
        LogToUnity("Creating new embedded WebView controller");
        _embeddedController = [[BalancyEmbeddedWebViewController alloc] initWithTexture:texturePtr width:width height:height];
        
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        NSString *logMsg = [NSString stringWithFormat:@"Attempting to load URL in embedded mode: %@", nsUrl];
        LogToUnity([logMsg UTF8String]);
        
        BOOL result = [_embeddedController loadURL:nsUrl];
        
        NSString *resultMsg = [NSString stringWithFormat:@"_balancyOpenWebViewEmbedded result: %@", result ? @"SUCCESS" : @"FAILED"];
        LogToUnity([resultMsg UTF8String]);
        
        return result;
    }
}

void _balancyCloseWebViewEmbedded() {
    @autoreleasepool {
        if (_embeddedController != nil) {
            LogToUnity("Closing embedded WebView");
            [_embeddedController close];
            _embeddedController = nil;
        }
    }
}

void _balancyUpdateEmbeddedTexture(void* texturePtr, int width, int height) {
    @autoreleasepool {
        if (_embeddedController != nil) {
            [_embeddedController updateTexture:texturePtr width:width height:height];
        }
    }
}

void _balancySendMouseEvent(int x, int y, bool isClick) {
    @autoreleasepool {
        if (_embeddedController != nil) {
            [_embeddedController handleMouseEvent:x y:y isClick:isClick];
        }
    }
}

bool _balancyGetEmbeddedPixelData(unsigned char* buffer, int bufferSize) {
    @autoreleasepool {
        if (_embeddedController == nil) {
            LogToUnity("_balancyGetEmbeddedPixelData: _embeddedController is nil");
            return false;
        }
        
        if (!_embeddedController.pixelDataReady) {
            LogToUnity("_balancyGetEmbeddedPixelData: pixel data not ready");
            return false;
        }
        
        if (!_embeddedController.pixelBuffer) {
            LogToUnity("_balancyGetEmbeddedPixelData: pixel buffer is null");
            return false;
        }
        
        size_t expectedSize = _embeddedController.textureWidth * _embeddedController.textureHeight * 4;
        if (bufferSize < expectedSize) {
            NSString *logMsg = [NSString stringWithFormat:@"_balancyGetEmbeddedPixelData: buffer too small. Expected: %zu, got: %d", expectedSize, bufferSize];
            LogToUnity([logMsg UTF8String]);
            return false;
        }
        
        memcpy(buffer, _embeddedController.pixelBuffer, expectedSize);
        NSString *logMsg = [NSString stringWithFormat:@"_balancyGetEmbeddedPixelData: copied %zu bytes successfully", expectedSize];
        LogToUnity([logMsg UTF8String]);
        return true;
    }
}

}
