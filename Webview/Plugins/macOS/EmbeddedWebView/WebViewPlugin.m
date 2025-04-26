// WebViewPlugin.m
#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#import "WebViewPlugin.h"

@interface WebViewDelegate : NSObject <WKNavigationDelegate, WKScriptMessageHandler>
@property (nonatomic, assign) void (*messageCallback)(const char* message);
@end

@implementation WebViewDelegate

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    // Inject JavaScript interface
    NSString *jsInterface = @"window.BalancyWebView = {"
                           "  postMessage: function(message) {"
                           "    window.webkit.messageHandlers.balancyMessageHandler.postMessage(message);"
                           "  }"
                           "};";
    
    [webView evaluateJavaScript:jsInterface completionHandler:nil];
}

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    if ([message.name isEqualToString:@"balancyMessageHandler"]) {
        if (self.messageCallback) {
            NSString *messageStr = [NSString stringWithFormat:@"%@", message.body];
            self.messageCallback([messageStr UTF8String]);
        }
    }
}

@end

// WebView container
@interface OffscreenWebView : NSObject
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WebViewDelegate *delegate;
@property (nonatomic, strong) NSImage *textureImage;
@property (nonatomic, assign) int width;
@property (nonatomic, assign) int height;
@property (nonatomic, assign) BOOL needsUpdate;
@end

@implementation OffscreenWebView

- (instancetype)initWithWidth:(int)width height:(int)height {
    self = [super init];
    if (self) {
        _width = width;
        _height = height;
        _needsUpdate = YES;
        
        // Create a configuration
        WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
        WKUserContentController *userContentController = [[WKUserContentController alloc] init];
        config.userContentController = userContentController;
        
        // Set up delegate
        _delegate = [[WebViewDelegate alloc] init];
        [userContentController addScriptMessageHandler:_delegate name:@"balancyMessageHandler"];
        
        // Create the web view
        NSRect frame = NSMakeRect(0, 0, width, height);
        _webView = [[WKWebView alloc] initWithFrame:frame configuration:config];
        _webView.navigationDelegate = _delegate;
        
        // Create a transparent window to hold the WebView (offscreen)
        NSWindow *offscreenWindow = [[NSWindow alloc] initWithContentRect:frame
                                                           styleMask:NSWindowStyleMaskBorderless
                                                             backing:NSBackingStoreBuffered
                                                               defer:YES];
        [offscreenWindow setContentView:_webView];
        [offscreenWindow setAlphaValue:0.0];
        [offscreenWindow orderBack:nil];
        
        // Initialize texture image
        _textureImage = [[NSImage alloc] initWithSize:NSMakeSize(width, height)];
    }
    return self;
}

- (void)captureWebViewContent {
    _needsUpdate = NO;
    
    // Force WebView to render
    [_webView layoutIfNeeded];
    
    // Capture WebView content
    NSRect bounds = NSMakeRect(0, 0, _width, _height);
    NSBitmapImageRep *imageRep = [_webView bitmapImageRepForCachingDisplayInRect:bounds];
    [_webView cacheDisplayInRect:bounds toBitmapImageRep:imageRep];
    
    // Create NSImage from the bitmap
    _textureImage = [[NSImage alloc] initWithSize:bounds.size];
    [_textureImage addRepresentation:imageRep];
}

- (void)getTextureData:(unsigned char*)buffer {
    if (_needsUpdate) {
        [self captureWebViewContent];
    }
    
    // Convert NSImage to raw RGBA data
    NSBitmapImageRep *imageRep = [NSBitmapImageRep imageRepWithData:[_textureImage TIFFRepresentation]];
    
    // Get pixel data
    int bytesPerPixel = 4; // RGBA
    unsigned char *pixels = [imageRep bitmapData];
    int bytesPerRow = [imageRep bytesPerRow];
    
    // Copy pixel data to buffer (flip vertically for Unity)
    for (int y = 0; y < _height; y++) {
        int srcRow = _height - 1 - y;
        for (int x = 0; x < _width; x++) {
            int srcIndex = srcRow * bytesPerRow + x * bytesPerPixel;
            int dstIndex = (y * _width + x) * bytesPerPixel;
            
            // Copy RGBA data
            buffer[dstIndex] = pixels[srcIndex];         // R
            buffer[dstIndex + 1] = pixels[srcIndex + 1]; // G
            buffer[dstIndex + 2] = pixels[srcIndex + 2]; // B
            buffer[dstIndex + 3] = pixels[srcIndex + 3]; // A
        }
    }
}

- (void)sendMouseEvent:(int)eventType x:(float)x y:(float)y {
    NSEvent *event = nil;
    CGPoint point = CGPointMake(x, y);
    
    switch (eventType) {
        case 0: // MouseDown
            event = [NSEvent mouseEventWithType:NSEventTypeLeftMouseDown
                                    location:point
                                modifierFlags:0
                                    timestamp:[NSDate timeIntervalSinceReferenceDate]
                                    windowNumber:0
                                    context:nil
                                    eventNumber:0
                                    clickCount:1
                                    pressure:1.0];
            break;
        case 1: // MouseUp
            event = [NSEvent mouseEventWithType:NSEventTypeLeftMouseUp
                                    location:point
                                modifierFlags:0
                                    timestamp:[NSDate timeIntervalSinceReferenceDate]
                                    windowNumber:0
                                    context:nil
                                    eventNumber:0
                                    clickCount:1
                                    pressure:0.0];
            break;
        case 2: // MouseMove
            event = [NSEvent mouseEventWithType:NSEventTypeMouseMoved
                                    location:point
                                modifierFlags:0
                                    timestamp:[NSDate timeIntervalSinceReferenceDate]
                                    windowNumber:0
                                    context:nil
                                    eventNumber:0
                                    clickCount:0
                                    pressure:0.0];
            break;
    }
    
    if (event) {
        [_webView mouseDown:event];
    }
}

- (void)markNeedsUpdate {
    _needsUpdate = YES;
}

@end

// Plugin implementation
void* _CreateOffscreenWebView(const char* initialUrl, int width, int height) {
    @autoreleasepool {
        OffscreenWebView* offscreenWebView = [[OffscreenWebView alloc] initWithWidth:width height:height];
        
        // Load the initial URL if provided
        if (initialUrl != NULL) {
            NSString* urlString = [NSString stringWithUTF8String:initialUrl];
            NSURL* url = [NSURL URLWithString:urlString];
            NSURLRequest* request = [NSURLRequest requestWithURL:url];
            [offscreenWebView.webView loadRequest:request];
        }
        
        return (void*)CFBridgingRetain(offscreenWebView);
    }
}

void _CloseWebView(void* webViewPtr) {
    @autoreleasepool {
        if (webViewPtr != NULL) {
            OffscreenWebView* offscreenWebView = (__bridge_transfer OffscreenWebView*)webViewPtr;
            offscreenWebView = nil;
        }
    }
}

bool _LoadURL(void* webViewPtr, const char* url) {
    @autoreleasepool {
        if (webViewPtr == NULL || url == NULL)
            return false;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        NSString* urlString = [NSString stringWithUTF8String:url];
        NSURL* nsUrl = [NSURL URLWithString:urlString];
        NSURLRequest* request = [NSURLRequest requestWithURL:nsUrl];
        [offscreenWebView.webView loadRequest:request];
        [offscreenWebView markNeedsUpdate];
        return true;
    }
}

bool _ExecuteJavaScript(void* webViewPtr, const char* script) {
    @autoreleasepool {
        if (webViewPtr == NULL || script == NULL)
            return false;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        NSString* jsString = [NSString stringWithUTF8String:script];
        [offscreenWebView.webView evaluateJavaScript:jsString completionHandler:^(id _Nullable result, NSError * _Nullable error) {
            if (error) {
                NSLog(@"Error executing JavaScript: %@", error);
            }
        }];
        [offscreenWebView markNeedsUpdate];
        return true;
    }
}

void _GetTextureData(void* webViewPtr, unsigned char* buffer) {
    @autoreleasepool {
        if (webViewPtr == NULL || buffer == NULL)
            return;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        [offscreenWebView getTextureData:buffer];
    }
}

void _SendMouseEvent(void* webViewPtr, int eventType, float x, float y) {
    @autoreleasepool {
        if (webViewPtr == NULL)
            return;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        [offscreenWebView sendMouseEvent:eventType x:x y:y];
        [offscreenWebView markNeedsUpdate];
    }
}

void _SetJSMessageCallback(void* webViewPtr, void (*callback)(const char* message)) {
    @autoreleasepool {
        if (webViewPtr == NULL)
            return;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        offscreenWebView.delegate.messageCallback = callback;
    }
}

bool _UpdateTexture(void* webViewPtr) {
    @autoreleasepool {
        if (webViewPtr == NULL)
            return false;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        bool needsUpdate = offscreenWebView.needsUpdate;
        if (needsUpdate) {
            [offscreenWebView captureWebViewContent];
        }
        return needsUpdate;
    }
}

void _SendKeyEvent(void* webViewPtr, int keyCode, bool isKeyDown) {
    // Implementation for key events - not needed for basic functionality
}

bool _HasContentChanged(void* webViewPtr) {
    @autoreleasepool {
        if (webViewPtr == NULL)
            return false;
        
        OffscreenWebView* offscreenWebView = (__bridge OffscreenWebView*)webViewPtr;
        return offscreenWebView.needsUpdate;
    }
}