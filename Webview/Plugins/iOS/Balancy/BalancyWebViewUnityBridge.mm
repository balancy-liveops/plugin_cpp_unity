#import "BalancyWebViewUnityBridge.h"
#import "BalancyWebView.h"

// Forward declaration of Unity's function
// The actual implementation is provided by Unity's runtime
// Using __attribute__((weak_import)) to avoid duplicate symbol errors
extern "C" {
    void UnitySendMessage(const char* obj, const char* method, const char* msg) __attribute__((weak_import));
}

// Forward declaration of Unity's view controller accessor
extern "C" UIViewController* UnityGetGLViewController() __attribute__((weak_import));

// Static reference to shared controller
static BalancyWebViewController* sharedController = nil;

// Callback implementations
void MessageCallback(void* context, const char* message) {
    UnitySendMessage("BalancyWebView", "OnMessageReceived", message);
}

void LoadCompletedCallback(void* context, bool success) {
    const char* successStr = success ? "true" : "false";
    UnitySendMessage("BalancyWebView", "OnLoadFinished", successStr);
}

void CacheCompletedCallback(void* context, bool success) {
    const char* successStr = success ? "true" : "false";
    UnitySendMessage("BalancyWebView", "OnCacheFinished", successStr);
}

@implementation BalancyWebViewController (Unity)

+ (void)setSharedController:(BalancyWebViewController*)controller {
    sharedController = controller;
}

+ (BalancyWebViewController*)sharedController {
    return sharedController;
}

@end

extern "C" {

bool _balancyWebViewOpen(const char* url, int width, int height, bool transparent) {
    @autoreleasepool {
        NSString* nsUrl = [NSString stringWithUTF8String:url];
        
        // Get the Unity root view controller
        UIViewController* rootViewController = UnityGetGLViewController();
        if (!rootViewController) {
            NSLog(@"Failed to get Unity view controller");
            return false;
        }
        
        // Create and configure the WebView controller with callbacks
        BalancyWebViewController* webViewController = [[BalancyWebViewController alloc] 
            initWithContext:NULL
            messageCallback:MessageCallback
            loadCompletedCallback:LoadCompletedCallback
            cacheCompletedCallback:CacheCompletedCallback];
        
        // Set transparency if requested
        if (transparent) {
            webViewController.view.backgroundColor = [UIColor clearColor];
            webViewController.view.opaque = NO;
            
            // Find the WKWebView inside the controller's view hierarchy and make it transparent
            // This needs to be called after the view is loaded
            dispatch_async(dispatch_get_main_queue(), ^{
                for (UIView* subview in webViewController.view.subviews) {
                    if ([subview isKindOfClass:[WKWebView class]]) {
                        WKWebView* webView = (WKWebView*)subview;
                        webView.backgroundColor = [UIColor clearColor];
                        webView.opaque = NO;
                    }
                }
            });
        }
        
        // Add as child view controller
        [rootViewController addChildViewController:webViewController];
        [rootViewController.view addSubview:webViewController.view];
        [webViewController didMoveToParentViewController:rootViewController];
        
        // Set the frame
        if (width > 0 && height > 0) {
            // Calculate center position
            CGFloat centerX = rootViewController.view.bounds.size.width / 2;
            CGFloat centerY = rootViewController.view.bounds.size.height / 2;
            
            // Set frame
            webViewController.view.frame = CGRectMake(centerX - width/2, centerY - height/2, width, height);
        } else {
            // Use full screen
            webViewController.view.frame = rootViewController.view.bounds;
        }
        
        // Store a reference to the controller
        [BalancyWebViewController setSharedController:webViewController];
        
        // Load the URL
        return [webViewController loadURL:nsUrl];
    }
}

void _balancyWebViewClose() {
    @autoreleasepool {
        BalancyWebViewController* controller = [BalancyWebViewController sharedController];
        if (controller) {
            [controller willMoveToParentViewController:nil];
            [controller.view removeFromSuperview];
            [controller removeFromParentViewController];
            [BalancyWebViewController setSharedController:nil];
        }
    }
}

bool _balancyWebViewSendMessage(const char* message) {
    @autoreleasepool {
        BalancyWebViewController* controller = [BalancyWebViewController sharedController];
        if (!controller) {
            return false;
        }
        
        NSString* nsMessage = [NSString stringWithUTF8String:message];
        return [controller sendMessage:nsMessage];
    }
}

void _balancyWebViewSetOfflineCacheEnabled(bool enabled) {
    @autoreleasepool {
        BalancyWebViewController* controller = [BalancyWebViewController sharedController];
        if (controller) {
            [controller setOfflineCacheEnabled:enabled];
        }
    }
}

}
