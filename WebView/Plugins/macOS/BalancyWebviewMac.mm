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

// Number of directory levels to go up from HTML file to reach common parent
static const int kDirectoryLevelsUp = 6;

// Unity logging function
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg) __attribute__((weak));

void LogToUnity(const char* message) {
    if (UnitySendMessage != NULL) {
        UnitySendMessage("BalancyWebView", "LogFromNative", message);
    }
    NSLog(@"[BalancyWebView] %s", message);
}

// Forward declarations
@interface BalancyWebViewController : NSWindowController <WKNavigationDelegate, WKScriptMessageHandler>
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) BOOL transparentBackground;
@property (nonatomic, assign) BOOL offlineCacheEnabled;
@property (nonatomic, assign) BOOL webInspectorEnabled;
@property (nonatomic, assign) BOOL gameUIMode;
@property (nonatomic, strong) NSButton *emergencyExitButton;
@property (nonatomic, strong) NSTimer *emergencyExitHideTimer;
@property (nonatomic, assign) float showDelay;
@property (nonatomic, assign) float animationDuration;

- (instancetype)init;
- (instancetype)initWithSize:(NSSize)size;
- (BOOL)loadURL:(NSString *)url;
- (void)close;
- (BOOL)sendMessage:(NSString *)message;
- (BOOL)injectJSCode:(NSString *)code;
- (NSString *)callJavaScript:(NSString *)function args:(NSArray<NSString *> *)args;
- (void)setViewportRect:(CGFloat)x y:(CGFloat)y width:(CGFloat)width height:(CGFloat)height;
- (void)setTransparentBackground:(BOOL)transparent;
- (void)setDebugLogging:(BOOL)enabled;
- (void)setWebInspectorEnabled:(BOOL)enabled;
@end

// Embedded WebView controller for rendering to texture
@interface BalancyEmbeddedWebViewController : NSViewController <WKNavigationDelegate, WKScriptMessageHandler>
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) BOOL webInspectorEnabled;
@property (nonatomic, assign) BOOL gameUIMode;
@property (nonatomic, assign) float showDelay;
@property (nonatomic, assign) float animationDuration;
@property (nonatomic, assign) int textureWidth;
@property (nonatomic, assign) int textureHeight;
@property (nonatomic, strong) NSTimer *renderTimer;
@property (nonatomic, assign) unsigned char* pixelBuffer;
@property (nonatomic, assign) BOOL pixelDataReady;
@property (nonatomic, strong) NSWindow *offscreenWindow;
@property (nonatomic, assign) CGContextRef persistentContext;
@property (nonatomic, assign) BOOL hasNewFrame;
@property (nonatomic, assign) NSTimeInterval lastClickTime;
@property (nonatomic, assign) int rapidClickCount;

- (instancetype)initWithWidth:(int)width height:(int)height;
- (BOOL)loadURL:(NSString *)url;
- (void)close;
- (BOOL)sendMessage:(NSString *)message;
- (BOOL)injectJSCode:(NSString *)code;
- (void)updateTexture:(int)width height:(int)height;
- (void)handleMouseEvent:(int)x y:(int)y eventType:(NSString*)eventType;
- (void)renderToTexture;
- (void)setWebInspectorEnabled:(BOOL)enabled;
@end

// Implementation of embedded WebView controller
@implementation BalancyEmbeddedWebViewController

- (instancetype)initWithWidth:(int)width height:(int)height {
    self = [super init];
    if (self) {
        _debugLogging = NO;
        _webInspectorEnabled = NO;
        _gameUIMode = YES;
        _showDelay = 0.1f;
        _animationDuration = 0.1f;
        _textureWidth = width;
        _textureHeight = height;
        _pixelDataReady = NO;
        _hasNewFrame = NO;
        _lastClickTime = 0;
        _rapidClickCount = 0;
        
        // Allocate pixel buffer
        size_t bufferSize = width * height * 4; // RGBA
        _pixelBuffer = (unsigned char*)malloc(bufferSize);
        memset(_pixelBuffer, 0, bufferSize);
        
        [self createPersistentContext];
        
        // Hidden offscreen window
        NSRect windowFrame = NSMakeRect(-100, -100, width, height);
        _offscreenWindow = [[NSWindow alloc] initWithContentRect:windowFrame
                                                      styleMask:NSWindowStyleMaskBorderless
                                                        backing:NSBackingStoreBuffered
                                                          defer:NO];
        [_offscreenWindow setTitle:@"Balancy Embedded"];
        [_offscreenWindow setAlphaValue:0.01];
        [_offscreenWindow setBackgroundColor:[NSColor clearColor]];
        [_offscreenWindow setOpaque:NO];
        [_offscreenWindow setHasShadow:NO];
        
        // WebView configuration
        WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
        
        // Configure preferences for Web Inspector
        WKPreferences *preferences = [[WKPreferences alloc] init];
        
        // Enable developer extras (Web Inspector)
        if (@available(macOS 10.11, *)) {
            [preferences setValue:@YES forKey:@"developerExtrasEnabled"];
        }
        
        configuration.preferences = preferences;
        
        // Disable local file restrictions to allow cross-directory file access
        if (@available(macOS 10.11, *)) {
            [configuration.preferences setValue:@YES forKey:@"allowFileAccessFromFileURLs"];
            [configuration setValue:@YES forKey:@"allowUniversalAccessFromFileURLs"];
        }
        
        _userContentController = [[WKUserContentController alloc] init];
        [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
        configuration.userContentController = _userContentController;
        
        // Container view
        NSView *containerView = [[NSView alloc] initWithFrame:NSMakeRect(0, 0, width, height)];
        containerView.wantsLayer = YES;
        containerView.layer.backgroundColor = [[NSColor clearColor] CGColor];
        containerView.layer.opaque = NO;
        
        self.view = containerView;
        [_offscreenWindow setContentView:containerView];
        
        // WebView
        _webView = [[WKWebView alloc] initWithFrame:NSMakeRect(0, 0, width, height) configuration:configuration];
        _webView.navigationDelegate = self;
        _webView.wantsLayer = YES;
        _webView.layer.backgroundColor = [[NSColor clearColor] CGColor];
        _webView.layer.opaque = NO;
        [_webView setValue:@NO forKey:@"drawsBackground"];
        
        // Enable context menu for Web Inspector
        _webView.allowsBackForwardNavigationGestures = YES;
        if (@available(macOS 10.13, *)) {
            _webView.configuration.preferences.tabFocusesLinks = NO;
        }
        
        [containerView addSubview:_webView];
        
        // Render timer at 15 FPS
        _renderTimer = [NSTimer scheduledTimerWithTimeInterval:1.0/15.0
                                                        target:self
                                                      selector:@selector(renderToTexture)
                                                      userInfo:nil
                                                       repeats:YES];
        
        [_offscreenWindow makeKeyAndOrderFront:nil];
    }
    return self;
}

- (void)injectTransparencyScript {
    if (!_webView) return;
    
    NSString *transparencyScript = @"\
        (function() { \
            const style = document.createElement('style'); \
            style.textContent = ` \
                html { background: transparent !important; background-color: transparent !important; } \
                body { background: transparent !important; background-color: transparent !important; } \
            `; \
            document.head.appendChild(style); \
            document.documentElement.style.backgroundColor = 'transparent'; \
            document.body.style.backgroundColor = 'transparent'; \
        })();";
    
    [_webView evaluateJavaScript:transparencyScript completionHandler:nil];
}

- (void)createPersistentContext {
    if (_persistentContext) {
        CGContextRelease(_persistentContext);
    }
    
    CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
    size_t bytesPerRow = _textureWidth * 4;
    
    _persistentContext = CGBitmapContextCreate(
        _pixelBuffer,
        _textureWidth,
        _textureHeight,
        8,
        bytesPerRow,
        colorSpace,
        kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big
    );
    
    CGColorSpaceRelease(colorSpace);
}

- (BOOL)loadURL:(NSString *)url {
    if ([url hasPrefix:@"file://"]) {
        NSString *filePath = [url stringByReplacingOccurrencesOfString:@"file://" withString:@""];
        NSURL *fileURL = [NSURL fileURLWithPath:filePath];
        
        // Go up directory levels to reach common parent containing both Balancy and BalancyResources
        NSString *readAccessPath = filePath;
        for (int i = 0; i < kDirectoryLevelsUp; i++) {
            readAccessPath = [readAccessPath stringByDeletingLastPathComponent];
        }
        NSURL *broadReadAccessURL = [NSURL fileURLWithPath:readAccessPath isDirectory:YES];
        
        [_webView loadFileURL:fileURL allowingReadAccessToURL:broadReadAccessURL];
        return YES;
    }
    
    NSURL *nsUrl = [NSURL URLWithString:url];
    if (!nsUrl) {
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
    
    if (_webView) {
        // Stop all loading and clear navigation
        [_webView stopLoading];
        
        // Load empty page to clean up resources
        [_webView loadHTMLString:@"" baseURL:nil];
        
        // Clear navigation delegates
        [_webView setNavigationDelegate:nil];
        [_webView setUIDelegate:nil];
        
        // Remove from superview and nullify
        [_webView removeFromSuperview];
        _webView = nil;
    }
    
    if (_userContentController) {
        [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
        _userContentController = nil;
    }
    
    if (_persistentContext) {
        CGContextRelease(_persistentContext);
        _persistentContext = nil;
    }
    
    if (_pixelBuffer) {
        free(_pixelBuffer);
        _pixelBuffer = nil;
    }
    
    if (_offscreenWindow) {
        [_offscreenWindow close];
        _offscreenWindow = nil;
    }
}

- (BOOL)sendMessage:(NSString *)message {
    if (!_webView) return NO;
    
    NSString *escapedMessage = [message stringByReplacingOccurrencesOfString:@"'" withString:@"\\'"];
    escapedMessage = [escapedMessage stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
    NSString *script = [NSString stringWithFormat:@"if (balancy) { balancy._receiveMessageFromUnity('%@'); }", escapedMessage];
    
    [_webView evaluateJavaScript:script completionHandler:nil];
    return YES;
}

- (BOOL)injectJSCode:(NSString *)code {
    if (!_webView) return NO;
    
    [_webView evaluateJavaScript:code completionHandler:nil];
    return YES;
}

- (void)updateTexture:(int)width height:(int)height {
    _textureWidth = width;
    _textureHeight = height;
    
    size_t newBufferSize = width * height * 4;
    if (_pixelBuffer) {
        free(_pixelBuffer);
    }
    _pixelBuffer = (unsigned char*)malloc(newBufferSize);
    memset(_pixelBuffer, 0, newBufferSize);
    _pixelDataReady = NO;
    _hasNewFrame = NO;
    
    [self createPersistentContext];
    
    _webView.frame = NSMakeRect(0, 0, width, height);
    self.view.frame = NSMakeRect(0, 0, width, height);
    
    if (_offscreenWindow) {
        NSRect windowFrame = NSMakeRect(-100, -100, width, height);
        [_offscreenWindow setFrame:windowFrame display:NO];
    }
}

- (void)triggerEmergencyExit {
    if (_messageCallback) {
        _messageCallback("{\"action\":200, \"params\":{}}");
    }
}

- (void)handleMouseEvent:(int)x y:(int)y eventType:(NSString*)eventType {
    if (!_webView) return;

    NSPoint point = NSMakePoint(x, _textureHeight - y);

    // Track rapid clicks for triple-click emergency exit
    if ([eventType isEqualToString:@"down"]) {
        NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
        if (now - _lastClickTime < 0.4) {
            _rapidClickCount++;
        } else {
            _rapidClickCount = 1;
        }
        _lastClickTime = now;

        if (_rapidClickCount >= 3) {
            _rapidClickCount = 0;
            [self triggerEmergencyExit];
            return;
        }
    }

    if ([eventType isEqualToString:@"down"]) {
        NSEvent *mouseDown = [NSEvent mouseEventWithType:NSEventTypeLeftMouseDown
                                                 location:point
                                            modifierFlags:0
                                                timestamp:[[NSProcessInfo processInfo] systemUptime]
                                             windowNumber:[_webView window].windowNumber
                                                  context:nil
                                              eventNumber:0
                                               clickCount:1
                                                 pressure:1.0];
        [_webView mouseDown:mouseDown];
        
    } else if ([eventType isEqualToString:@"up"]) {
        NSEvent *mouseUp = [NSEvent mouseEventWithType:NSEventTypeLeftMouseUp
                                               location:point
                                          modifierFlags:0
                                              timestamp:[[NSProcessInfo processInfo] systemUptime]
                                           windowNumber:[_webView window].windowNumber
                                                context:nil
                                            eventNumber:0
                                             clickCount:1
                                               pressure:1.0];
        [_webView mouseUp:mouseUp];
        
    } else if ([eventType isEqualToString:@"move"]) {
        NSEvent *mouseDrag = [NSEvent mouseEventWithType:NSEventTypeLeftMouseDragged
                                                 location:point
                                            modifierFlags:0
                                                timestamp:[[NSProcessInfo processInfo] systemUptime]
                                             windowNumber:[_webView window].windowNumber
                                                  context:nil
                                              eventNumber:0
                                               clickCount:0
                                               pressure:1.0];
        [_webView mouseDragged:mouseDrag];
    }
}

- (void)handleScrollEvent:(int)x y:(int)y deltaX:(float)deltaX deltaY:(float)deltaY {
    if (!_webView) return;
    
    NSPoint point = NSMakePoint(x, _textureHeight - y);
    NSPoint screenPoint = [[_webView window] convertPointToScreen:point];
    
    CGEventRef scrollEvent = CGEventCreateScrollWheelEvent(
        NULL,
        kCGScrollEventUnitPixel,
        2,
        deltaY * 10.0f,
        deltaX * 10.0f
    );
    
    if (scrollEvent) {
        CGEventSetLocation(scrollEvent, CGPointMake(screenPoint.x, screenPoint.y));
        NSEvent *nsScrollEvent = [NSEvent eventWithCGEvent:scrollEvent];
        [_webView scrollWheel:nsScrollEvent];
        CFRelease(scrollEvent);
    }
}

- (void)renderToTexture {
    if (!_webView || !_pixelBuffer || !_persistentContext) {
        return;
    }
    
    @try {
        [_webView.layer setNeedsDisplay];
        [_webView.layer displayIfNeeded];
        
        if (@available(macOS 10.13, *)) {
            WKSnapshotConfiguration *config = [[WKSnapshotConfiguration alloc] init];
            config.rect = CGRectMake(0, 0, _textureWidth, _textureHeight);
            config.afterScreenUpdates = YES;
            
            [_webView takeSnapshotWithConfiguration:config completionHandler:^(NSImage * _Nullable snapshotImage, NSError * _Nullable error) {
                if (error || !snapshotImage) {
                    return;
                }
                
                CGImageRef cgImage = [snapshotImage CGImageForProposedRect:nil context:nil hints:nil];
                if (cgImage && self->_persistentContext) {
                    CGContextClearRect(self->_persistentContext, CGRectMake(0, 0, self->_textureWidth, self->_textureHeight));
                    CGContextSetBlendMode(self->_persistentContext, kCGBlendModeCopy);
                    CGContextDrawImage(self->_persistentContext, CGRectMake(0, 0, self->_textureWidth, self->_textureHeight), cgImage);
                    
                    self->_pixelDataReady = YES;
                    self->_hasNewFrame = YES;
                }
            }];
            return;
        }
        
        [self fallbackLayerRendering];
        
    } @catch (NSException *exception) {
        // Ignore errors
    }
}

- (void)fallbackLayerRendering {
    if (!_webView.superview || !_webView.layer || !_pixelBuffer || !_persistentContext) {
        return;
    }
    
    [_webView.layer setNeedsDisplayInRect:_webView.bounds];
    [_webView.layer displayIfNeeded];
    
    if (_persistentContext) {
        CGContextClearRect(_persistentContext, CGRectMake(0, 0, _textureWidth, _textureHeight));
        CGContextSaveGState(_persistentContext);
        
        CGFloat scaleX = (CGFloat)_textureWidth / _webView.frame.size.width;
        CGFloat scaleY = (CGFloat)_textureHeight / _webView.frame.size.height;
        CGContextScaleCTM(_persistentContext, scaleX, scaleY);
        CGContextSetInterpolationQuality(_persistentContext, kCGInterpolationHigh);
        CGContextSetBlendMode(_persistentContext, kCGBlendModeCopy);
        
        [_webView.layer renderInContext:_persistentContext];
        
        CGContextRestoreGState(_persistentContext);
        
        _pixelDataReady = YES;
        _hasNewFrame = YES;
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
    [self injectTransparencyScript];
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(true);
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    if (_loadCompletedCallback) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    if (_loadCompletedCallback) {
        _loadCompletedCallback(false);
    }
}

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
    // Navigation started
}

- (void)setWebInspectorEnabled:(BOOL)enabled {
    _webInspectorEnabled = enabled;
    
    if (_webView && _webView.configuration.preferences) {
        if (@available(macOS 10.11, *)) {
            [_webView.configuration.preferences setValue:@(enabled) forKey:@"developerExtrasEnabled"];
            
            // Also try these additional settings
            if (enabled) {
                [_webView.configuration.preferences setValue:@YES forKey:@"allowsInlineMediaPlayback"];
                [_webView.configuration setValue:@YES forKey:@"allowsAirPlayForMediaPlayback"];
                
                // Force enable context menu
                if ([_webView respondsToSelector:@selector(_setCustomUserAgent:)]) {
                    // This helps ensure the context menu works
                }
            }
        }
    }
}

- (void)setShowDelay:(float)delaySeconds {
    _showDelay = fmaxf(0.0f, delaySeconds);
}

- (void)setAnimationDuration:(float)durationSeconds {
    _animationDuration = fmaxf(0.0f, durationSeconds);
}

- (void)setGameUIMode:(BOOL)enabled {
    _gameUIMode = enabled;
    // Apply game UI mode settings if needed
}

@end

// Global WebView controller instances
static BalancyWebViewController* _sharedController = nil;
static BalancyEmbeddedWebViewController* _embeddedController = nil;

@implementation BalancyWebViewController {
    NSRect _viewportRect;
}

- (instancetype)init {
    return [self initWithSize:NSMakeSize(800, 600)];
}

- (instancetype)initWithSize:(NSSize)size {
    self = [super init];
    if (self) {
        _debugLogging = NO;
        _transparentBackground = NO;
        _offlineCacheEnabled = NO;
        _webInspectorEnabled = NO;
        _gameUIMode = YES;
        _showDelay = 0.1f;
        _animationDuration = 0.1f;
        _viewportRect = NSMakeRect(0, 0, 1, 1);
        
        NSRect windowRect = NSMakeRect(0, 0, size.width, size.height);
        NSWindow *window = [[NSWindow alloc] initWithContentRect:windowRect
                                                      styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskResizable 
                                                        backing:NSBackingStoreBuffered 
                                                          defer:NO];
        [window setTitle:@"Balancy WebView"];
        [window center];
        
        self = [self initWithWindow:window];
        
        WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
        
        // Configure preferences for Web Inspector
        WKPreferences *preferences = [[WKPreferences alloc] init];
        
        // Enable developer extras (Web Inspector)
        if (@available(macOS 10.11, *)) {
            [preferences setValue:@YES forKey:@"developerExtrasEnabled"];
        }
        
        configuration.preferences = preferences;
        
        // Disable local file restrictions to allow cross-directory file access
        if (@available(macOS 10.11, *)) {
            [configuration.preferences setValue:@YES forKey:@"allowFileAccessFromFileURLs"];
            [configuration setValue:@YES forKey:@"allowUniversalAccessFromFileURLs"];
        }
        
        _userContentController = [[WKUserContentController alloc] init];
        [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
        configuration.userContentController = _userContentController;
        
        _webView = [[WKWebView alloc] initWithFrame:[[window contentView] bounds] configuration:configuration];
        _webView.navigationDelegate = self;
        _webView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        
        // Enable context menu for Web Inspector
        _webView.allowsBackForwardNavigationGestures = YES;
        if (@available(macOS 10.13, *)) {
            _webView.configuration.preferences.tabFocusesLinks = NO;
        }
        
        [[window contentView] addSubview:_webView];

        // Triple-click gesture to reveal emergency exit button
        NSClickGestureRecognizer *tripleClick = [[NSClickGestureRecognizer alloc] initWithTarget:self action:@selector(handleTripleClick:)];
        tripleClick.numberOfClicksRequired = 3;
        tripleClick.delaysPrimaryMouseButtonEvents = NO;
        [_webView addGestureRecognizer:tripleClick];

        [[NSNotificationCenter defaultCenter] addObserver:self
                                                 selector:@selector(windowDidResize:)
                                                     name:NSWindowDidResizeNotification
                                                   object:[self window]];
    }
    return self;
}

- (BOOL)loadURL:(NSString *)url {
    // Set window to be initially hidden BEFORE showing it
    [[self window] setAlphaValue:0.0f];
    [[self window] makeKeyAndOrderFront:nil];
    
    if ([url hasPrefix:@"file://"]) {
        NSString *filePath = [url stringByReplacingOccurrencesOfString:@"file://" withString:@""];
        NSURL *fileURL = [NSURL fileURLWithPath:filePath];
        
        // Go up directory levels to reach common parent containing both Balancy and BalancyResources
        NSString *readAccessPath = filePath;
        for (int i = 0; i < kDirectoryLevelsUp; i++) {
            readAccessPath = [readAccessPath stringByDeletingLastPathComponent];
        }
        NSURL *broadReadAccessURL = [NSURL fileURLWithPath:readAccessPath isDirectory:YES];
        
        [_webView loadFileURL:fileURL allowingReadAccessToURL:broadReadAccessURL];
        return YES;
    }
    
    NSURL *nsUrl = [NSURL URLWithString:url];
    if (!nsUrl) {
        return NO;
    }
    
    [_webView loadRequest:[NSURLRequest requestWithURL:nsUrl]];
    return YES;
}

- (void)handleTripleClick:(NSClickGestureRecognizer *)recognizer {
    [self showEmergencyExitButton];
}

- (void)showEmergencyExitButton {
    // Cancel any pending hide timer
    [_emergencyExitHideTimer invalidate];
    _emergencyExitHideTimer = nil;

    if (!_emergencyExitButton) {
        _emergencyExitButton = [[NSButton alloc] initWithFrame:NSZeroRect];
        [_emergencyExitButton setTarget:self];
        [_emergencyExitButton setAction:@selector(emergencyExitButtonClicked:)];
        [_emergencyExitButton setBezelStyle:NSBezelStyleRounded];
        [_emergencyExitButton setTitle:@"Close View"];
        [_emergencyExitButton setFont:[NSFont boldSystemFontOfSize:16]];
        _emergencyExitButton.wantsLayer = YES;
        _emergencyExitButton.layer.zPosition = 1000;
        [[[self window] contentView] addSubview:_emergencyExitButton positioned:NSWindowAbove relativeTo:_webView];
    }

    // Size and center the button
    [_emergencyExitButton sizeToFit];
    NSRect btnFrame = _emergencyExitButton.frame;
    // Add padding
    btnFrame.size.width += 40;
    btnFrame.size.height += 16;
    NSRect contentFrame = [[[self window] contentView] bounds];
    btnFrame.origin.x = (contentFrame.size.width - btnFrame.size.width) / 2.0;
    btnFrame.origin.y = (contentFrame.size.height - btnFrame.size.height) / 2.0;
    [_emergencyExitButton setFrame:btnFrame];

    [_emergencyExitButton setHidden:NO];
    [_emergencyExitButton setAlphaValue:1.0];

    // Auto-hide after 3 seconds
    _emergencyExitHideTimer = [NSTimer scheduledTimerWithTimeInterval:3.0
                                                              target:self
                                                            selector:@selector(hideEmergencyExitButton)
                                                            userInfo:nil
                                                             repeats:NO];
}

- (void)hideEmergencyExitButton {
    _emergencyExitHideTimer = nil;
    if (_emergencyExitButton) {
        [NSAnimationContext runAnimationGroup:^(NSAnimationContext *context) {
            context.duration = 0.3;
            _emergencyExitButton.animator.alphaValue = 0.0;
        } completionHandler:^{
            [_emergencyExitButton setHidden:YES];
        }];
    }
}

- (void)emergencyExitButtonClicked:(NSButton *)sender {
    [_emergencyExitHideTimer invalidate];
    _emergencyExitHideTimer = nil;
    if (_messageCallback) {
        _messageCallback("{\"action\":200, \"params\":{}}");
    }
}

- (void)windowDidResize:(NSNotification *)notification {
    // Re-center the button if it's visible
    if (_emergencyExitButton && ![_emergencyExitButton isHidden]) {
        [_emergencyExitButton sizeToFit];
        NSRect btnFrame = _emergencyExitButton.frame;
        btnFrame.size.width += 40;
        btnFrame.size.height += 16;
        NSRect contentFrame = [[[self window] contentView] bounds];
        btnFrame.origin.x = (contentFrame.size.width - btnFrame.size.width) / 2.0;
        btnFrame.origin.y = (contentFrame.size.height - btnFrame.size.height) / 2.0;
        [_emergencyExitButton setFrame:btnFrame];
    }
}

- (void)close {
    [[NSNotificationCenter defaultCenter] removeObserver:self];
    [_emergencyExitHideTimer invalidate];
    _emergencyExitHideTimer = nil;
    if (_emergencyExitButton) {
        [_emergencyExitButton removeFromSuperview];
        _emergencyExitButton = nil;
    }
    
    [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
    [_webView stopLoading];
    [[self window] close];
}

- (BOOL)sendMessage:(NSString *)message {
    if (!_webView) return NO;
    
    NSString *escapedMessage = [message stringByReplacingOccurrencesOfString:@"'" withString:@"\\'"];
    escapedMessage = [escapedMessage stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
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

- (void)setGameUIMode:(BOOL)enabled {
    _gameUIMode = enabled;
    // Apply game UI mode settings if needed
}

- (void)setShowDelay:(float)delaySeconds {
    _showDelay = fmaxf(0.0f, delaySeconds);
    if (_debugLogging) {
        LogToUnity([[NSString stringWithFormat:@"Show delay set to: %.3f seconds", _showDelay] UTF8String]);
    }
}

- (void)setAnimationDuration:(float)durationSeconds {
    _animationDuration = fmaxf(0.0f, durationSeconds);
    if (_debugLogging) {
        LogToUnity([[NSString stringWithFormat:@"Animation duration set to: %.3f seconds", _animationDuration] UTF8String]);
    }
}

- (void)startShowAnimation {
    // Window is already hidden (alpha = 0.0f) from loadURL
    
    if (_debugLogging) {
        LogToUnity([[NSString stringWithFormat:@"Starting show animation with delay: %.3fs, duration: %.3fs", _showDelay, _animationDuration] UTF8String]);
    }
    
    // Delay before starting the animation
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(_showDelay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        // Animate the webview to fully visible
        [NSAnimationContext runAnimationGroup:^(NSAnimationContext *context) {
            context.duration = self->_animationDuration;
            context.timingFunction = [CAMediaTimingFunction functionWithName:kCAMediaTimingFunctionEaseInEaseOut];
            [[[self window] animator] setAlphaValue:1.0f];
        } completionHandler:^{
            if (self->_debugLogging) {
                LogToUnity("Show animation completed");
            }
        }];
    });
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
    if (_transparentBackground) {
        [self setTransparentBackground:YES];
    }
    
    NSString *initScript = @"if (window.BalancyWebView && typeof window.BalancyWebView.initResponseHandler === 'function') { window.BalancyWebView.initResponseHandler(); }";
    [_webView evaluateJavaScript:initScript completionHandler:nil];
    
    // Start the show animation instead of immediately showing
    [self startShowAnimation];
   
    if (_loadCompletedCallback) {
        _loadCompletedCallback(true);
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
   if (_loadCompletedCallback) {
       _loadCompletedCallback(false);
   }
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
   if (_loadCompletedCallback) {
       _loadCompletedCallback(false);
   }
}

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
   // Navigation started
}

- (void)setWebInspectorEnabled:(BOOL)enabled {
    _webInspectorEnabled = enabled;
    
    if (_webView && _webView.configuration.preferences) {
        if (@available(macOS 10.11, *)) {
            [_webView.configuration.preferences setValue:@(enabled) forKey:@"developerExtrasEnabled"];
            
            // Also try these additional settings
            if (enabled) {
                [_webView.configuration.preferences setValue:@YES forKey:@"allowsInlineMediaPlayback"];
                [_webView.configuration setValue:@YES forKey:@"allowsAirPlayForMediaPlayback"];
                
                // Force enable context menu
                if ([_webView respondsToSelector:@selector(_setCustomUserAgent:)]) {
                    // This helps ensure the context menu works
                }
            }
        }
    }
}

@end

// C interface for Unity
extern "C" {

bool _balancyOpenWebView(const char* url) {
   @autoreleasepool {
       if (_sharedController == nil) {
           _sharedController = [[BalancyWebViewController alloc] init];
       }
       
       NSString* nsUrl = [NSString stringWithUTF8String:url];
       return [_sharedController loadURL:nsUrl];
   }
}

bool _balancyOpenWebViewWithSize(const char* url, int width, int height) {
   @autoreleasepool {
       if (_sharedController == nil) {
           NSSize windowSize = NSMakeSize(width, height);
           _sharedController = [[BalancyWebViewController alloc] initWithSize:windowSize];
       }
       
       NSString* nsUrl = [NSString stringWithUTF8String:url];
       return [_sharedController loadURL:nsUrl];
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
       NSString* nsMessage = [NSString stringWithUTF8String:message];
       
       if (_sharedController != nil) {        
           return [_sharedController sendMessage:nsMessage];
       }
       
       if (_embeddedController != nil) {        
           return [_embeddedController sendMessage:nsMessage];
       }
       return false;
   }
}

bool _balancyInjectJSCode(const char* message) {
   @autoreleasepool {
       NSString* nsMessage = [NSString stringWithUTF8String:message];
       
       if (_sharedController != nil) {
           return [_sharedController injectJSCode:nsMessage];
       }
       
       if (_embeddedController != nil) {
           return [_embeddedController injectJSCode:nsMessage];
       }
       
       return false;
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

void _balancySetGameUIMode(bool enabled) {
   @autoreleasepool {
       if (_sharedController != nil) {
           [_sharedController setGameUIMode:enabled];
       }
       
       if (_embeddedController != nil) {
           [_embeddedController setGameUIMode:enabled];
       }
   }
}

void _balancySetShowDelay(float delaySeconds) {
   @autoreleasepool {
       if (_sharedController != nil) {
           [_sharedController setShowDelay:delaySeconds];
       }
       
       if (_embeddedController != nil) {
           [_embeddedController setShowDelay:delaySeconds];
       }
   }
}

void _balancySetAnimationDuration(float durationSeconds) {
   @autoreleasepool {
       if (_sharedController != nil) {
           [_sharedController setAnimationDuration:durationSeconds];
       }
       
       if (_embeddedController != nil) {
           [_embeddedController setAnimationDuration:durationSeconds];
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

bool _balancyOpenWebViewEmbedded(const char* url, int width, int height) {
   @autoreleasepool {
       if (_embeddedController != nil) {
           [_embeddedController close];
           _embeddedController = nil;
       }
       
       _embeddedController = [[BalancyEmbeddedWebViewController alloc] initWithWidth:width height:height];
       
       NSString* nsUrl = [NSString stringWithUTF8String:url];
       return [_embeddedController loadURL:nsUrl];
   }
}

void _balancyCloseWebViewEmbedded() {
   @autoreleasepool {
       if (_embeddedController != nil) {
           [_embeddedController close];
           _embeddedController = nil;
       }
   }
}

void _balancyUpdateEmbeddedTexture(int width, int height) {
   @autoreleasepool {
       if (_embeddedController != nil) {
           [_embeddedController updateTexture:width height:height];
       }
   }
}

bool _balancySendMouseEvent(int x, int y, const char* eventType) {
   @autoreleasepool {
       if (_embeddedController != nil) {
           NSString* nsEventType = [NSString stringWithUTF8String:eventType];
           [_embeddedController handleMouseEvent:x y:y eventType:nsEventType];
           return true;
       }
       return false;
   }
}

bool _balancySendScrollEvent(int x, int y, float deltaX, float deltaY) {
   @autoreleasepool {
       if (_embeddedController != nil) {
           [_embeddedController handleScrollEvent:x y:y deltaX:deltaX deltaY:deltaY];
           return true;
       }
       return false;
   }
}

bool _balancyGetEmbeddedPixelData(unsigned char* buffer, int bufferSize) {
   @autoreleasepool {
       if (_embeddedController == nil) {
           return false;
       }
       
       if (!_embeddedController.hasNewFrame) {
           return false;
       }
       
       if (!_embeddedController.pixelDataReady) {
           return false;
       }
       
       if (!_embeddedController.pixelBuffer) {
           return false;
       }
       
       size_t expectedSize = _embeddedController.textureWidth * _embeddedController.textureHeight * 4;
       if (bufferSize < expectedSize) {
           return false;
       }
       
       memcpy(buffer, _embeddedController.pixelBuffer, expectedSize);
       _embeddedController.hasNewFrame = NO;
       
       return true;
   }
}

void _balancySetEmergencyExitEnabled(bool enabled) {
   @autoreleasepool {
       if (_sharedController != nil && !enabled) {
           // Hide the button if currently shown
           [_sharedController.emergencyExitHideTimer invalidate];
           _sharedController.emergencyExitHideTimer = nil;
           if (_sharedController.emergencyExitButton) {
               [_sharedController.emergencyExitButton removeFromSuperview];
               _sharedController.emergencyExitButton = nil;
           }
       }
       // When enabled, the triple-click gesture recognizer (always active) handles showing the button
   }
}

void _balancySetWebInspectorEnabled(bool enabled) {
    @autoreleasepool {
        if (_sharedController != nil) {
            [_sharedController setWebInspectorEnabled:enabled];
        }
        
        if (_embeddedController != nil) {
            [_embeddedController setWebInspectorEnabled:enabled];
        }
    }
}

void _balancyShowWebInspector() {
    @autoreleasepool {
        if (_sharedController != nil && _sharedController.webView) {
            // First make sure developer extras are enabled
            [_sharedController setWebInspectorEnabled:YES];
            
            // Try multiple methods to show the Web Inspector
            if (@available(macOS 10.11, *)) {
                // Method 1: Direct private API call
                if ([_sharedController.webView respondsToSelector:@selector(_showInspector)]) {
                    [_sharedController.webView performSelector:@selector(_showInspector)];
                    return;
                }
                
                // Method 2: Alternative private API
                if ([_sharedController.webView respondsToSelector:@selector(_inspector)]) {
                    id inspector = [_sharedController.webView performSelector:@selector(_inspector)];
                    if (inspector && [inspector respondsToSelector:@selector(show)]) {
                        [inspector performSelector:@selector(show)];
                        return;
                    }
                }
                
                // Method 3: Context menu simulation
                NSEvent *rightClick = [NSEvent mouseEventWithType:NSEventTypeRightMouseDown
                                                         location:NSMakePoint(100, 100)
                                                    modifierFlags:0
                                                        timestamp:[[NSProcessInfo processInfo] systemUptime]
                                                     windowNumber:[_sharedController.webView window].windowNumber
                                                          context:nil
                                                      eventNumber:0
                                                       clickCount:1
                                                         pressure:1.0];
                [_sharedController.webView rightMouseDown:rightClick];
            }
        }
        
        // Also try for embedded controller
        if (_embeddedController != nil && _embeddedController.webView) {
            [_embeddedController setWebInspectorEnabled:YES];
            
            if (@available(macOS 10.11, *)) {
                if ([_embeddedController.webView respondsToSelector:@selector(_showInspector)]) {
                    [_embeddedController.webView performSelector:@selector(_showInspector)];
                }
            }
        }
    }
}

}