//
//  BalancyWebViewUnityBridge.mm
//  BalancyWebView iOS Implementation
//  Bridge between Unity and native iOS code
//

#import <Foundation/Foundation.h>
#import <WebKit/WebKit.h>
#import "BalancyWebView.h"

// External declaration of the callback variables from BalancyWebView.mm
extern "C" {
    extern void (*_messageCallback)(const char*);
    extern void (*_loadCompletedCallback)(bool);
    extern void (*_cacheCompletedCallback)(bool);
}

// Helper function to get the root view controller safely across all iOS versions
static UIViewController* GetRootViewControllerBridge() {
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

// Function to register callback for cache completion (needed by C# code)
extern "C" void _balancyRegisterCacheCompletedCallback(void (*callback)(bool)) {
    // Store the callback in the global static variable defined in BalancyWebView.mm
    _cacheCompletedCallback = callback;
}

// Additional C interface functions for Unity
extern "C" {

// Function to inject JavaScript code into the WebView
bool _balancyInjectJSCode(const char* code) {
    @autoreleasepool {
        
        if (code == NULL) {
            NSLog(@"[BalancyWebView] Cannot inject JS code: code is NULL");
            return false;
        }
        
        
        // Find the BalancyWebViewController using the helper function
        UIViewController* rootViewController = GetRootViewControllerBridge();
        if (!rootViewController) {
            NSLog(@"[BalancyWebView] Cannot inject JS code: Failed to get root view controller");
            return false;
        }
        
        
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                
                if (!webViewController.webView) {
                    NSLog(@"[BalancyWebView] ERROR: webView is nil!");
                    return false;
                }
                
                NSString* jsCode = [NSString stringWithUTF8String:code];
                
                [webViewController.webView evaluateJavaScript:jsCode completionHandler:^(id _Nullable result, NSError * _Nullable error) {
                    if (error) {
                        NSLog(@"Failed to inject %@", jsCode);
                        // Only log non-trivial errors (skip WKErrorCode 5 - unsupported return type)
                        if (error.code != 5) {
                            NSLog(@"[BalancyWebView] JS injection error (code %ld): %@", (long)error.code, error.localizedDescription);
                        } else {
                            NSLog(@"[BalancyWebView] JS injection completed (ignoring error code 5)");
                        }
                    } else {
                    }
                }];
                
                return true;
            }
        }
        
        NSLog(@"[BalancyWebView] Cannot inject JS code: WebView not found");
        return false;
    }
}

// Animation configuration functions
void _balancySetShowDelay(float delaySeconds) {
    @autoreleasepool {
        // Find the BalancyWebViewController using the helper function
        UIViewController* rootViewController = GetRootViewControllerBridge();
        if (!rootViewController) return;
        
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setShowDelay:delaySeconds];
                break;
            }
        }
    }
}

void _balancySetAnimationDuration(float durationSeconds) {
    @autoreleasepool {
        // Find the BalancyWebViewController using the helper function
        UIViewController* rootViewController = GetRootViewControllerBridge();
        if (!rootViewController) return;
        
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                [webViewController setAnimationDuration:durationSeconds];
                break;
            }
        }
    }
}

// Declare emergency exit function (implementation is in BalancyWebView.mm)
void _balancySetEmergencyExitEnabled(bool enabled);

} // extern "C"
