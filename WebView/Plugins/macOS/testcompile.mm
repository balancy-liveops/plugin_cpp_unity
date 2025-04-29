//
// Simple test file to check compilation 
//

#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#import <Foundation/Foundation.h>

@interface TestWebViewController : NSObject <WKNavigationDelegate, WKScriptMessageHandler>

@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) NSWindow *window;

- (instancetype)init;
- (void)loadURL:(NSString *)url;
- (void)close;

@end

@implementation TestWebViewController

- (instancetype)init {
    self = [super init];
    if (self) {
        // Create window
        NSRect screenRect = [[NSScreen mainScreen] frame];
        NSRect windowRect = NSMakeRect(
            screenRect.size.width * 0.1,
            screenRect.size.height * 0.1,
            screenRect.size.width * 0.8,
            screenRect.size.height * 0.8
        );
        
        _window = [[NSWindow alloc] initWithContentRect:windowRect
                                              styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable
                                                backing:NSBackingStoreBuffered
                                                  defer:NO];
        [_window setTitle:@"Test WebView"];
        
        // Create WebView
        WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
        _webView = [[WKWebView alloc] initWithFrame:[[_window contentView] bounds] configuration:config];
        _webView.navigationDelegate = self;
        
        // Add to window
        [[_window contentView] addSubview:_webView];
    }
    return self;
}

- (void)loadURL:(NSString *)url {
    NSURL *nsUrl = [NSURL URLWithString:url];
    NSURLRequest *request = [NSURLRequest requestWithURL:nsUrl];
    [_webView loadRequest:request];
    [_window makeKeyAndOrderFront:nil];
}

- (void)close {
    [_window close];
}

#pragma mark - WKNavigationDelegate

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    NSLog(@"Page loaded");
}

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    NSLog(@"Message: %@", message.body);
}

@end

// Test entry point
int main(int argc, const char * argv[]) {
    @autoreleasepool {
        TestWebViewController *controller = [[TestWebViewController alloc] init];
        [controller loadURL:@"https://example.com"];
        
        // Run app indefinitely
        [[NSApplication sharedApplication] run];
    }
    return 0;
}
