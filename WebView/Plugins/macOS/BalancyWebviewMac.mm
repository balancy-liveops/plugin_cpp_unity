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
@property (nonatomic, strong) NSButton *emergencyExitButton;  // Emergency Exit button

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
@end

// Embedded WebView controller for rendering to texture
@interface BalancyEmbeddedWebViewController : NSViewController <WKNavigationDelegate, WKScriptMessageHandler>
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, assign) BOOL debugLogging;
@property (nonatomic, assign) int textureWidth;
@property (nonatomic, assign) int textureHeight;
@property (nonatomic, strong) NSTimer *renderTimer;
@property (nonatomic, assign) unsigned char* pixelBuffer;
@property (nonatomic, assign) BOOL pixelDataReady;
@property (nonatomic, strong) NSWindow *offscreenWindow;
@property (nonatomic, assign) CGContextRef persistentContext;  // OPTIMIZATION #2: Reusable context
@property (nonatomic, assign) BOOL hasNewFrame;                // OPTIMIZATION #3: Smart sync flag

- (instancetype)initWithWidth:(int)width height:(int)height;
- (BOOL)loadURL:(NSString *)url;
- (void)close;
- (BOOL)sendMessage:(NSString *)message;
- (BOOL)injectJSCode:(NSString *)code;
- (void)updateTexture:(int)width height:(int)height;
- (void)handleMouseEvent:(int)x y:(int)y eventType:(NSString*)eventType;
- (void)renderToTexture;
@end

// Implementation of embedded WebView controller
@implementation BalancyEmbeddedWebViewController

- (instancetype)initWithWidth:(int)width height:(int)height {
    self = [super init];
    if (self) {
        _debugLogging = YES;
        _textureWidth = width;
        _textureHeight = height;
        _pixelDataReady = NO;
        _hasNewFrame = NO;  // OPTIMIZATION #3: Initialize sync flag
        
        // Allocate pixel buffer
        size_t bufferSize = width * height * 4; // RGBA
        _pixelBuffer = (unsigned char*)malloc(bufferSize);
        memset(_pixelBuffer, 0, bufferSize);
        
        // OPTIMIZATION #2: Create persistent CGContext once
        [self createPersistentContext];
        
        // ✅ ИСПРАВЛЕНИЕ 1: НЕВИДИМОЕ окно (убираем popup)
        NSRect windowFrame = NSMakeRect(-100, -100, width, height); // ← За пределами экрана
        _offscreenWindow = [[NSWindow alloc] initWithContentRect:windowFrame
                                                      styleMask:NSWindowStyleMaskBorderless  // ← Без границ
                                                        backing:NSBackingStoreBuffered
                                                          defer:NO];
        [_offscreenWindow setTitle:@"Balancy Embedded (Hidden)"];
        
        // ✅ УБИРАЕМ видимость - только минимально необходимое для работы браузера
        [_offscreenWindow setAlphaValue:0.01]; // Почти невидимое
//         [_offscreenWindow setLevel:NSNormalWindowLevel]; // Обычный уровень
//         [_offscreenWindow setCollectionBehavior:NSWindowCollectionBehaviorDefault];
        [_offscreenWindow setBackgroundColor:[NSColor clearColor]];
        [_offscreenWindow setOpaque:NO];
        [_offscreenWindow setHasShadow:NO];
        
        // Простая конфигурация WebView
        WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
        _userContentController = [[WKUserContentController alloc] init];
        [_userContentController addScriptMessageHandler:self name:@"BalancyWebView"];
        configuration.userContentController = _userContentController;
        
        // Только базовые настройки
        // Enable debugging and transparency-related settings
        [configuration.preferences setValue:@YES forKey:@"developerExtrasEnabled"];
        
        // Простой контейнер
        NSView *containerView = [[NSView alloc] initWithFrame:NSMakeRect(0, 0, width, height)];
        containerView.wantsLayer = YES;
        containerView.layer.backgroundColor = [[NSColor clearColor] CGColor];
        containerView.layer.opaque = NO;
        
        self.view = containerView;
        [_offscreenWindow setContentView:containerView];
        
        // Простой WebView
        _webView = [[WKWebView alloc] initWithFrame:NSMakeRect(0, 0, width, height) configuration:configuration];
        _webView.navigationDelegate = self;
        _webView.wantsLayer = YES;
        _webView.layer.backgroundColor = [[NSColor clearColor] CGColor];
        _webView.layer.opaque = NO;
        [_webView setValue:@NO forKey:@"drawsBackground"];
        
        [containerView addSubview:_webView];
        
        // ✅ ИСПРАВЛЕНИЕ 2: Снижаем частоту до 15 FPS
        _renderTimer = [NSTimer scheduledTimerWithTimeInterval:1.0/15.0  // ← 15 FPS вместо 60
                                                        target:self
                                                      selector:@selector(renderToTexture)
                                                      userInfo:nil
                                                       repeats:YES];
        
        // ✅ ИСПРАВЛЕНИЕ 3: Минимальное окно - только для работы браузера
        [_offscreenWindow makeKeyAndOrderFront:nil];
        
        // ✅ УБИРАЕМ все агрессивные методы принуждения видимости
        // Больше никаких orderFrontRegardless, makeKeyWindow и т.д.
        
        if (_debugLogging) {
            LogToUnity("OPTIMIZED embedded View initialized (NO popup window)");
        }
    }
    return self;
}

- (void)injectTransparencyScript {
    if (!_webView) return;
    
    NSString *transparencyScript = @"\
        (function() { \
            console.log('🔍 Balancy: Injecting transparency script'); \
            const style = document.createElement('style'); \
            style.textContent = ` \
                html { background: transparent !important; background-color: transparent !important; } \
                body { background: transparent !important; background-color: transparent !important; } \
            `; \
            document.head.appendChild(style); \
            document.documentElement.style.backgroundColor = 'transparent'; \
            document.body.style.backgroundColor = 'transparent'; \
            console.log('✅ Balancy: Transparency script applied'); \
        })();";
    
    [_webView evaluateJavaScript:transparencyScript completionHandler:^(id result, NSError *error) {
        if (error) {
            NSString *logMsg = [NSString stringWithFormat:@"Transparency script error: %@", error.localizedDescription];
            LogToUnity([logMsg UTF8String]);
        } else {
            LogToUnity("✅ Transparency script injected successfully");
        }
    }];
}

// OPTIMIZATION #2: Create persistent CGContext method
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
    
    if (_debugLogging) {
        LogToUnity("✅ OPTIMIZATION #2: Persistent CGContext created");
    }
}

// 2. ADD THIS NEW METHOD to inject animation boost scripts after page loads:

- (void)injectAnimationBoostScript {
    if (!_webView) return;
    
    NSString *animationBoostScript = @"\
        (function() { \
            console.log('🎬 Balancy: Injecting animation boost script'); \
            \
            // Force all CSS animations to run \
            const style = document.createElement('style'); \
            style.textContent = ` \
                *, *::before, *::after { \
                    animation-play-state: running !important; \
                    -webkit-animation-play-state: running !important; \
                } \
                \
                /* Prevent any pausing of animations */ \
                .rotating-fx { \
                    animation-play-state: running !important; \
                    will-change: transform !important; \
                } \
            `; \
            document.head.appendChild(style); \
            \
            // Force reflow to apply styles \
            document.body.offsetHeight; \
            \
            // Keep page active with periodic micro-tasks \
            setInterval(() => { \
                document.body.style.transform = 'translateZ(0)'; \
                document.body.offsetHeight; \
                document.body.style.transform = ''; \
            }, 100); \
            \
            console.log('✅ Balancy: Animation boost script applied'); \
        })();";
    
    [_webView evaluateJavaScript:animationBoostScript completionHandler:^(id result, NSError *error) {
        if (error) {
            NSString *logMsg = [NSString stringWithFormat:@"Animation boost script error: %@", error.localizedDescription];
            LogToUnity([logMsg UTF8String]);
        } else {
            LogToUnity("Animation boost script injected successfully");
        }
    }];
}

- (BOOL)loadURL:(NSString *)url {
    if (_debugLogging) {
        NSString *logMsg = [NSString stringWithFormat:@"📥 BalancyEmbeddedWebViewController loadURL called with: %@", url];
        LogToUnity([logMsg UTF8String]);
    }
    
    if ([url hasPrefix:@"file://"]) {
        NSString *cleanUrl = url;
        NSString *filePath = [cleanUrl stringByReplacingOccurrencesOfString:@"file://" withString:@""];
        
        NSURL *fileURL = [NSURL fileURLWithPath:filePath];
        NSString *htmlPath = [fileURL path];
        NSString *parentDir = [htmlPath stringByDeletingLastPathComponent];
        NSString *filesDir = [parentDir stringByDeletingLastPathComponent];
        
        NSURL *broadReadAccessURL = [NSURL fileURLWithPath:filesDir];
        
//         if (_debugLogging) {
//             NSString *logMsg = [NSString stringWithFormat:@"📁 Embedded File URL: %@", fileURL];
//             LogToUnity([logMsg UTF8String]);
//             NSString *logMsg2 = [NSString stringWithFormat:@"📂 Read access URL: %@", broadReadAccessURL];
//             LogToUnity([logMsg2 UTF8String]);
//             
//             // Check if file exists
//             BOOL fileExists = [[NSFileManager defaultManager] fileExistsAtPath:filePath];
//             NSString *logMsg3 = [NSString stringWithFormat:@"📄 File exists: %@", fileExists ? @"YES" : @"NO"];
//             LogToUnity([logMsg3 UTF8String]);
//         }
        
        [_webView loadFileURL:fileURL allowingReadAccessToURL:broadReadAccessURL];
        return YES;
    }
    
    NSURL *nsUrl = [NSURL URLWithString:url];
    if (!nsUrl) {
        LogToUnity("❌ Invalid URL for embedded WebView");
        return NO;
    }
    
    if (_debugLogging) {
        NSString *logMsg = [NSString stringWithFormat:@"🌐 Loading web URL: %@", nsUrl];
        LogToUnity([logMsg UTF8String]);
    }
    
    [_webView loadRequest:[NSURLRequest requestWithURL:nsUrl]];
    return YES;
}

- (void)close {
    // Stop render timer first
    if (_renderTimer) {
        [_renderTimer invalidate];
        _renderTimer = nil;
    }
    
    // Clean up WebView
    if (_webView) {
        [_webView stopLoading];
        [_webView setNavigationDelegate:nil];
        [_webView removeFromSuperview];
        _webView = nil;
    }
    
    // Clean up user content controller
    if (_userContentController) {
        [_userContentController removeScriptMessageHandlerForName:@"BalancyWebView"];
        _userContentController = nil;
    }
    
    // OPTIMIZATION #2: Clean up persistent context
    if (_persistentContext) {
        CGContextRelease(_persistentContext);
        _persistentContext = nil;
        if (_debugLogging) {
            LogToUnity("✅ OPTIMIZATION #2: Persistent CGContext released");
        }
    }
    
    // Free pixel buffer
    if (_pixelBuffer) {
        free(_pixelBuffer);
        _pixelBuffer = nil;
    }
    
    // Close and clean up window
    if (_offscreenWindow) {
        [_offscreenWindow close];
        _offscreenWindow = nil;
    }
    
    if (_debugLogging) {
        LogToUnity("OPTIMIZED embedded WebView closed and cleaned up");
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

- (void)updateTexture:(int)width height:(int)height {
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
    _hasNewFrame = NO;  // OPTIMIZATION #3: Reset frame flag
    
    // OPTIMIZATION #2: Recreate persistent context for new size
    [self createPersistentContext];
    
    // Update WebView frame and window
    _webView.frame = NSMakeRect(0, 0, width, height);
    self.view.frame = NSMakeRect(0, 0, width, height);
    
    if (_offscreenWindow) {
        NSRect windowFrame = NSMakeRect(100, 100, width, height);
        [_offscreenWindow setFrame:windowFrame display:NO];
    }
    
//     if (_debugLogging) {
//         NSString *logMsg = [NSString stringWithFormat:@"OPTIMIZED: Updated embedded texture: %dx%d", width, height];
//         LogToUnity([logMsg UTF8String]);
//     }
}

// Check if point is in emergency exit zone (10% x 10% in top-right corner)
- (BOOL)isPointInEmergencyExitZone:(CGPoint)point {
    // Emergency exit zone is top-right corner, 10% x 10% of WebView size
    CGFloat exitZoneWidth = _textureWidth * 0.10;
    CGFloat exitZoneHeight = _textureHeight * 0.10;
    
    CGFloat exitZoneX = _textureWidth - exitZoneWidth;
    CGFloat exitZoneY = _textureHeight - exitZoneHeight;
    
    BOOL inZone = (point.x >= exitZoneX && point.x <= _textureWidth &&
                   point.y >= exitZoneY && point.y <= _textureHeight);
    
    if (inZone && _debugLogging) {
        NSString *logMsg = [NSString stringWithFormat:@"🚨 Point (%.1f, %.1f) is in emergency exit zone [%.1f-%.1f, %.1f-%.1f]", 
                           point.x, point.y, exitZoneX, (float)_textureWidth, exitZoneY, (float)_textureHeight];
        LogToUnity([logMsg UTF8String]);
    }
    
    return inZone;
}

// Handle emergency exit activation
- (void)triggerEmergencyExit {
    if (_debugLogging) {
        LogToUnity("🚨 Emergency exit triggered in embedded mode");
    }
    
    // Send message to Unity
    if (_messageCallback) {
        _messageCallback("{\"action\":200, \"params\":{}}");
    }
}

- (void)handleMouseEvent:(int)x y:(int)y eventType:(NSString*)eventType {
    if (!_webView) return;
    
    // Convert Unity coordinates to our coordinate system
    NSPoint point = NSMakePoint(x, _textureHeight - y); // Flip Y coordinate
    
    // Check if this is a mouse down event in the emergency exit zone
    if ([eventType isEqualToString:@"down"] && [self isPointInEmergencyExitZone:point]) {
        // Emergency exit triggered - don't pass event to WebView
        [self triggerEmergencyExit];
        return;  // Early return - don't process this event further
    }
    
    // Not in emergency exit zone or not a down event - handle normally
//     if (_debugLogging) {
//         NSString *logMsg = [NSString stringWithFormat:@"🖱️ Mouse %@ at (%d, %d) -> NSPoint(%.1f, %.1f)", eventType, x, y, point.x, point.y];
//         LogToUnity([logMsg UTF8String]);
//     }
    
    // Continue with original mouse event handling
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
    
//     if (_debugLogging) {
//         NSString *logMsg = [NSString stringWithFormat:@"📜 Scroll at (%d, %d), delta: (%.2f, %.2f)", x, y, deltaX, deltaY];
//         LogToUnity([logMsg UTF8String]);
//     }
    
    // Convert window point to screen point for CGEvent
    NSPoint screenPoint = [[_webView window] convertPointToScreen:point];
    
    // Create Core Graphics scroll event
    CGEventRef scrollEvent = CGEventCreateScrollWheelEvent(
        NULL,
        kCGScrollEventUnitPixel,
        2,  // Number of wheel axes (X and Y)
        deltaY * 10.0f,  // Y delta (amplified)
        deltaX * 10.0f   // X delta (amplified)
    );
    
    if (scrollEvent) {
        // Set the location
        CGEventSetLocation(scrollEvent, CGPointMake(screenPoint.x, screenPoint.y));
        
        // Convert to NSEvent and send to WebView
        NSEvent *nsScrollEvent = [NSEvent eventWithCGEvent:scrollEvent];
        [_webView scrollWheel:nsScrollEvent];
        
        // Clean up
        CFRelease(scrollEvent);
    }
}

// OPTIMIZATION #2 & #3: Optimized renderToTexture with persistent context and smart sync
- (void)renderToTexture {
    if (!_webView || !_pixelBuffer || !_persistentContext) {
        return;
    }
    
    @try {
        // Простое принуждение к обновлению
        [_webView.layer setNeedsDisplay];
        [_webView.layer displayIfNeeded];
        
        // Используем snapshot API (основной метод)
        if (@available(macOS 10.13, *)) {
            WKSnapshotConfiguration *config = [[WKSnapshotConfiguration alloc] init];
            config.rect = CGRectMake(0, 0, _textureWidth, _textureHeight);
            config.afterScreenUpdates = YES;
            
            [_webView takeSnapshotWithConfiguration:config completionHandler:^(NSImage * _Nullable snapshotImage, NSError * _Nullable error) {
                if (error || !snapshotImage) {
                    return; // Просто игнорируем ошибки
                }
                
                // Конвертируем в pixel buffer
                CGImageRef cgImage = [snapshotImage CGImageForProposedRect:nil context:nil hints:nil];
                if (cgImage) {
                
//                     NSString *pavelLog = [NSString stringWithFormat:@">>>Snapshot done!!"];
//                     LogToUnity([pavelLog UTF8String]);
                    
                    // OPTIMIZATION #2: Use persistent context instead of creating new one
                    if (self->_persistentContext) {
                        CGContextClearRect(self->_persistentContext, CGRectMake(0, 0, self->_textureWidth, self->_textureHeight));
                        CGContextSetBlendMode(self->_persistentContext, kCGBlendModeCopy);
                        CGContextDrawImage(self->_persistentContext, CGRectMake(0, 0, self->_textureWidth, self->_textureHeight), cgImage);
                        
                        self->_pixelDataReady = YES;
                        self->_hasNewFrame = YES;  // OPTIMIZATION #3: Mark new frame available
                    }
                }
            }];
            return;
        }
        
        // Fallback для старых версий macOS
        [self fallbackLayerRendering];
        
    } @catch (NSException *exception) {
        // Игнорируем ошибки
    }
}

// OPTIMIZATION #2 & #3: Optimized fallback layer rendering
- (void)fallbackLayerRendering {
    if (!_webView.superview || !_webView.layer || !_pixelBuffer || !_persistentContext) {
        return;
    }
    
    // Force any pending layer updates
    [_webView.layer setNeedsDisplayInRect:_webView.bounds];
    [_webView.layer displayIfNeeded];
    
    NSString *pavelLog = [NSString stringWithFormat:@">>>Fallback Snapshot done!!"];
    LogToUnity([pavelLog UTF8String]);
    
    // OPTIMIZATION #2: Use persistent context instead of creating new one
    if (_persistentContext) {
        // Fill with transparent background
        CGContextClearRect(_persistentContext, CGRectMake(0, 0, _textureWidth, _textureHeight));
        
        // Save context state
        CGContextSaveGState(_persistentContext);
        
        // Better scaling and rendering quality
        CGFloat scaleX = (CGFloat)_textureWidth / _webView.frame.size.width;
        CGFloat scaleY = (CGFloat)_textureHeight / _webView.frame.size.height;
        CGContextScaleCTM(_persistentContext, scaleX, scaleY);
        CGContextSetInterpolationQuality(_persistentContext, kCGInterpolationHigh);
        CGContextSetBlendMode(_persistentContext, kCGBlendModeCopy);
        
        // Render the WebView layer with all sublayers
        [_webView.layer renderInContext:_persistentContext];
        
        // Restore context state
        CGContextRestoreGState(_persistentContext);
        
        _pixelDataReady = YES;
        _hasNewFrame = YES;  // OPTIMIZATION #3: Mark new frame available
        
        // DEBUG: Log successful fallback rendering occasionally
//         if (_debugLogging && (rand() % 200 == 0)) { // Log every ~3 seconds at 60fps
//             LogToUnity("OPTIMIZED: Fallback layer rendering completed successfully");
//         }    
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

// ✅ ИСПРАВЛЕНИЕ 4: Упрощенный didFinishNavigation (без агрессивных скриптов)
- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    if (_debugLogging) {
        LogToUnity("✅ Simple embedded View navigation finished");
    }
    
    [self injectTransparencyScript];
    
    // Простая проверка контента
    [_webView evaluateJavaScript:@"document.body ? document.body.innerHTML.length : -1" completionHandler:^(id result, NSError *error) {
        if (error) {
            NSString *logMsg = [NSString stringWithFormat:@"❌ JavaScript evaluation error: %@", error.localizedDescription];
            LogToUnity([logMsg UTF8String]);
        } else {
            NSString *logMsg = [NSString stringWithFormat:@"📊 Content length: %@ characters", result];
            LogToUnity([logMsg UTF8String]);
        }
    }];
    
    // ✅ УБИРАЕМ все сложные скрипты инъекции
    // Пусть браузер работает как хочет
    
    if (_loadCompletedCallback) {
        _loadCompletedCallback(true);
    }
}

- (void)injectSuperOptimizationPrevention {
    if (!_webView) return;
    
    NSString *superOptimizationScript = @"\
        (function() { \
            console.log('🚀🚀 SUPER OPTIMIZATION PREVENTION ACTIVATED'); \
            \
            // Override ALL possible visibility and focus detection \
            Object.defineProperty(document, 'hidden', { value: false, writable: false, configurable: false }); \
            Object.defineProperty(document, 'visibilityState', { value: 'visible', writable: false, configurable: false }); \
            Object.defineProperty(document, 'webkitHidden', { value: false, writable: false, configurable: false }); \
            Object.defineProperty(document, 'webkitVisibilityState', { value: 'visible', writable: false, configurable: false }); \
            \
            // Override page focus detection \
            document.hasFocus = function() { return true; }; \
            \
            // Prevent any visibility change events \
            const originalAddEventListener = document.addEventListener; \
            document.addEventListener = function(type, listener, options) { \
                if (type.includes('visibility') || type.includes('focus') || type.includes('blur')) { \
                    console.log('🚫 Blocked event listener for:', type); \
                    return; \
                } \
                return originalAddEventListener.call(this, type, listener, options); \
            }; \
            \
            // Super-aggressive RAF enhancement \
            const originalRAF = window.requestAnimationFrame; \
            let frameId = 1; \
            const callbacks = new Map(); \
            let isRunning = false; \
            \
            function forceAnimationLoop() { \
                if (isRunning) return; \
                isRunning = true; \
                console.log('🎬 Force animation loop started'); \
                \
                function loop() { \
                    const now = performance.now(); \
                    callbacks.forEach((callback, id) => { \
                        try { \
                            callback(now); \
                            callbacks.delete(id); \
                        } catch(e) { \
                            console.error('RAF error:', e); \
                            callbacks.delete(id); \
                        } \
                    }); \
                    \
                    // Always continue the loop \
                    setTimeout(loop, 16); \
                } \
                \
                loop(); \
            } \
            \
            window.requestAnimationFrame = function(callback) { \
                const id = frameId++; \
                callbacks.set(id, callback); \
                \
                if (!isRunning) { \
                    forceAnimationLoop(); \
                } \
                \
                return id; \
            }; \
            \
            window.cancelAnimationFrame = function(id) { \
                callbacks.delete(id); \
            }; \
            \
            // Force all CSS animations to run \
            const style = document.createElement('style'); \
            style.textContent = ` \
                *, *::before, *::after { \
                    animation-play-state: running !important; \
                    -webkit-animation-play-state: running !important; \
                } \
                body, html { \
                    display: block !important; \
                    visibility: visible !important; \
                } \
            `; \
            document.head.appendChild(style); \
            \
            // Keep page active with constant micro-tasks \
            setInterval(() => { \
                document.body.style.transform = 'translateZ(0)'; \
                document.body.offsetHeight; \
                document.body.style.transform = ''; \
            }, 50); \
            \
            console.log('✅✅ SUPER OPTIMIZATION PREVENTION COMPLETE'); \
        })();";
    
    [_webView evaluateJavaScript:superOptimizationScript completionHandler:^(id result, NSError *error) {
        if (error) {
            NSString *logMsg = [NSString stringWithFormat:@"Super optimization script error: %@", error.localizedDescription];
            LogToUnity([logMsg UTF8String]);
        } else {
            LogToUnity("✅ Super optimization prevention injected successfully");
        }
    }];
}

// 5. ADD METHOD to force window visibility when needed:

- (void)ensureWindowVisibility {
    if (_offscreenWindow) {
        dispatch_async(dispatch_get_main_queue(), ^{
            [self->_offscreenWindow orderFrontRegardless];
            [self->_offscreenWindow makeKeyWindow];
            
            // Force WebView to refresh
            [self->_webView setNeedsDisplay:YES];
            [self->_webView.layer setNeedsDisplay];
        });
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
        LogToUnity("💻 Embedded View started loading");
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
    return [self initWithSize:NSMakeSize(800, 600)]; // Default size
}

- (instancetype)initWithSize:(NSSize)size {
    self = [super init];
    if (self) {
        _debugLogging = NO;
        _transparentBackground = NO;
        _offlineCacheEnabled = NO;
        _viewportRect = NSMakeRect(0, 0, 1, 1);
        
        // Create a window with the specified size
        NSRect windowRect = NSMakeRect(0, 0, size.width, size.height);
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
        
        // ✅ ИСПРАВЛЕНИЕ: Упрощенные настройки для инспектора
        // Основная настройка для inspector
        [configuration.preferences setValue:@YES forKey:@"developerExtrasEnabled"];
        
        // ✅ ДОБАВЛЯЕМ: Настройки для контекстного меню и инспектора
        // Убираем устаревшие API - они не обязательны для inspector
        
        // Только базовые настройки для совместимости
        if (@available(macOS 10.13, *)) {
            [configuration.preferences setValue:@YES forKey:@"javaScriptCanAccessClipboard"];
        }
        
        // Create WebView
        _webView = [[WKWebView alloc] initWithFrame:[[window contentView] bounds] configuration:configuration];
        _webView.navigationDelegate = self;
        _webView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        
        // ✅ ДОБАВЬТЕ принудительное включение инспектора:
        if (@available(macOS 10.11, *)) {
            [_webView setValue:@YES forKey:@"drawsBackground"];
            // Это должно гарантировать доступность инспектора
            [_webView.configuration.preferences setValue:@YES forKey:@"developerExtrasEnabled"];
            
            // ✅ КЛЮЧЕВОЕ: Включаем контекстное меню для inspector
            // Проверяем что WebView не блокирует правый клик
            @try {
                [_webView setValue:@YES forKey:@"allowsLinkPreview"];
            } @catch (NSException *exception) {
                // Игнорируем если ключ не поддерживается
            }
            
            // ✅ НОВОЕ: Принудительно включаем Web Inspector через приватные API
            @try {
                // Пытаемся получить доступ к Web Inspector
                id inspector = [_webView performSelector:@selector(_inspector)];
                if (inspector) {
                    LogToUnity("🔍 Web Inspector object found - enabling...");
                    [inspector performSelector:@selector(show)];
                }
            } @catch (NSException *exception) {
                LogToUnity("ℹ️ Private inspector API not available, using standard approach");
            }
            
            // ✅ Альтернативный способ через WebKit приватные методы
            @try {
                if ([_webView respondsToSelector:@selector(_setDeveloperExtrasEnabled:)]) {
                    [_webView performSelector:@selector(_setDeveloperExtrasEnabled:) withObject:@YES];
                    LogToUnity("🔧 Used private _setDeveloperExtrasEnabled method");
                }
            } @catch (NSException *exception) {
                LogToUnity("ℹ️ Private _setDeveloperExtrasEnabled not available");
            }
        }
        
        [[window contentView] addSubview:_webView];
        
        // Setup emergency exit button after WebView is created
        [self setupEmergencyExitButton];
        
        // Observe window resize to update button position
        [[NSNotificationCenter defaultCenter] addObserver:self
                                                 selector:@selector(windowDidResize:)
                                                     name:NSWindowDidResizeNotification
                                                   object:[self window]];
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

// Emergency exit button creation and setup
- (void)setupEmergencyExitButton {
    if (!_webView) return;
    
    // Calculate button size (10% of WebView size)
    CGRect webViewFrame = _webView.frame;
    CGFloat buttonWidth = webViewFrame.size.width * 0.10;
    CGFloat buttonHeight = webViewFrame.size.height * 0.10;
    
    // Position in top-right corner
    CGFloat buttonX = webViewFrame.origin.x + webViewFrame.size.width - buttonWidth;
    CGFloat buttonY = webViewFrame.origin.y + webViewFrame.size.height - buttonHeight;
    
    NSRect buttonFrame = NSMakeRect(buttonX, buttonY, buttonWidth, buttonHeight);
    
    // Remove existing button if any
    if (_emergencyExitButton) {
        [_emergencyExitButton removeFromSuperview];
    }
    
    // Create invisible button
    _emergencyExitButton = [[NSButton alloc] initWithFrame:buttonFrame];
    [_emergencyExitButton setTarget:self];
    [_emergencyExitButton setAction:@selector(emergencyExitButtonClicked:)];
    [_emergencyExitButton setButtonType:NSButtonTypeMomentaryChange];
    [_emergencyExitButton setBordered:NO];  // No border
    [_emergencyExitButton setTransparent:YES];  // Transparent
    [_emergencyExitButton setTitle:@""];  // No title
    [_emergencyExitButton setAlphaValue:0.0];  // Completely invisible
    
    // Add to the window's content view (above WebView)
    [[[self window] contentView] addSubview:_emergencyExitButton positioned:NSWindowAbove relativeTo:_webView];
    
    if (_debugLogging) {
        NSString *logMsg = [NSString stringWithFormat:@"Emergency exit button created at (%.1f, %.1f) size (%.1f x %.1f)", 
                           buttonX, buttonY, buttonWidth, buttonHeight];
        LogToUnity([logMsg UTF8String]);
    }
}

// Emergency exit button click handler
- (void)emergencyExitButtonClicked:(NSButton *)sender {
    if (_debugLogging) {
        LogToUnity("🚨 Emergency exit button clicked in popup mode");
    }
    
    // Send message to Unity
    if (_messageCallback) {
        _messageCallback("{\"action\":200, \"params\":{}}");
    }
}

// Update emergency exit button position when window resizes
- (void)updateEmergencyExitButtonPosition {
    if (!_emergencyExitButton || !_webView) return;
    
    CGRect webViewFrame = _webView.frame;
    CGFloat buttonWidth = webViewFrame.size.width * 0.10;
    CGFloat buttonHeight = webViewFrame.size.height * 0.10;
    
    CGFloat buttonX = webViewFrame.origin.x + webViewFrame.size.width - buttonWidth;
    CGFloat buttonY = webViewFrame.origin.y + webViewFrame.size.height - buttonHeight;
    
    NSRect buttonFrame = NSMakeRect(buttonX, buttonY, buttonWidth, buttonHeight);
    [_emergencyExitButton setFrame:buttonFrame];
}

// Handle window resize notifications
- (void)windowDidResize:(NSNotification *)notification {
    [self updateEmergencyExitButtonPosition];
}

- (void)close {
    // Remove observer and clean up button
    [[NSNotificationCenter defaultCenter] removeObserver:self];
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
    
    // ✅ ДОБАВЬТЕ: Проверка доступности inspector
    BOOL developerExtrasEnabled = [[_webView.configuration.preferences valueForKey:@"developerExtrasEnabled"] boolValue];
    NSString *inspectorMsg = [NSString stringWithFormat:@"🔍 Developer extras (inspector) enabled: %@", 
                             developerExtrasEnabled ? @"YES" : @"NO"];
    LogToUnity([inspectorMsg UTF8String]);
    
    // Принудительное включение если выключено
    if (!developerExtrasEnabled) {
        LogToUnity("🔧 Force enabling developer extras");
        [_webView.configuration.preferences setValue:@YES forKey:@"developerExtrasEnabled"];
    }
    
    // ✅ НОВОЕ: Попытка программно открыть inspector после загрузки
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        @try {
            // Пытаемся открыть inspector программно
            id inspector = [self->_webView performSelector:@selector(_inspector)];
            if (inspector && [inspector respondsToSelector:@selector(show)]) {
                [inspector performSelector:@selector(show)];
                LogToUnity("🎉 Inspector opened programmatically!");
            } else {
                LogToUnity("ℹ️ Inspector object not accessible, try manual methods");
            }
        } @catch (NSException *exception) {
            LogToUnity("ℹ️ Could not open inspector programmatically");
        }
    });
    
    // ✅ Проверяем что контекстное меню доступно
    LogToUnity("🖱️ TIP: Right-click on webpage should show 'Inspect Element' option");
    LogToUnity("⌨️ TIP: Or press Cmd+Option+I to open inspector");
    LogToUnity("🔬 TIP: If nothing works, inspector may open automatically in 1 second...");
    
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
            LogToUnity("Creating new WebView controller with default size");
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

bool _balancyOpenWebViewWithSize(const char* url, int width, int height) {
    @autoreleasepool {
        NSString *logMsg = [NSString stringWithFormat:@"_balancyOpenWebViewWithSize called with size: %dx%d", width, height];
        LogToUnity([logMsg UTF8String]);
        
        if (_sharedController == nil) {
            LogToUnity("Creating new WebView controller with custom size");
            NSSize windowSize = NSMakeSize(width, height);
            _sharedController = [[BalancyWebViewController alloc] initWithSize:windowSize];
        }
        
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        NSString *logMsg2 = [NSString stringWithFormat:@"Attempting to load URL: %@", nsUrl];
        LogToUnity([logMsg2 UTF8String]);
        
        BOOL result = [_sharedController loadURL:nsUrl];
        
        NSString *resultMsg = [NSString stringWithFormat:@"_balancyOpenWebViewWithSize result: %@", result ? @"SUCCESS" : @"FAILED"];
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
        
        // Сначала пробуем popup контроллер
        if (_sharedController != nil) {
            return [_sharedController injectJSCode:nsMessage];
        }
        
        // Если popup не открыт, пробуем embedded контроллер
        if (_embeddedController != nil) {
            return [_embeddedController injectJSCode:nsMessage];
        }
        
        // Ни один контроллер не доступен
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

void _balancyRegisterMessageCallback(MessageCallback callback) {
    _messageCallback = callback;
}

void _balancyRegisterLoadCompletedCallback(LoadCompletedCallback callback) {
    _loadCompletedCallback = callback;
}

void _balancyRegisterCacheCompletedCallback(CacheCompletedCallback callback) {
    _cacheCompletedCallback = callback;
}

// OPTIMIZATION #2 & #3: Optimized embedded functionality with smart sync
bool _balancyOpenWebViewEmbedded(const char* url, int width, int height) {
    @autoreleasepool {
        LogToUnity("_balancyOpenWebViewEmbedded called (OPTIMIZED)");
        
        if (_embeddedController != nil) {
            LogToUnity("Closing existing embedded WebView");
            [_embeddedController close];
            _embeddedController = nil;
        }
        
        NSString *logMsg = [NSString stringWithFormat:@"OPTIMIZED embedded WebView controller initialized with size: %dx%d", width, height];
        LogToUnity([logMsg UTF8String]);
        
        _embeddedController = [[BalancyEmbeddedWebViewController alloc] initWithWidth:width height:height];
        
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        NSString *logMsg2 = [NSString stringWithFormat:@"Attempting to load URL in embedded mode: %@", nsUrl];
        LogToUnity([logMsg2 UTF8String]);
        
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

// OPTIMIZATION #3: Smart sync - only copy data when new frame is available
bool _balancyGetEmbeddedPixelData(unsigned char* buffer, int bufferSize) {
    @autoreleasepool {
        if (_embeddedController == nil) {
            LogToUnity("_balancyGetEmbeddedPixelData: _embeddedController is nil");
            return false;
        }
        
        // OPTIMIZATION #3: Check if new frame is available
        if (!_embeddedController.hasNewFrame) {
            // No new frame - avoid unnecessary copying
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
        
        // OPTIMIZATION #3: Mark frame as consumed
        _embeddedController.hasNewFrame = NO;
        
//         NSString *logMsg = [NSString stringWithFormat:@"*balancyGetEmbeddedPixelData: copied %zu bytes successfully", expectedSize];
//         LogToUnity([logMsg UTF8String]);
        return true;
    }
}

// Optional C function to enable/disable emergency exit
void _balancySetEmergencyExitEnabled(bool enabled) {
    @autoreleasepool {
        // For popup mode
        if (_sharedController != nil) {
            if (enabled) {
                [_sharedController setupEmergencyExitButton];
            } else if (_sharedController.emergencyExitButton) {
                [_sharedController.emergencyExitButton removeFromSuperview];
                _sharedController.emergencyExitButton = nil;
            }
        }
        
        // For embedded mode - the emergency exit is always enabled via mouse event interception
        // Could add a flag here if you want to disable it programmatically
        
        NSString *logMsg = [NSString stringWithFormat:@"Emergency exit %@", enabled ? @"enabled" : @"disabled"];
        LogToUnity([logMsg UTF8String]);
    }
}

}
