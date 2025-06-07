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
        // Find the BalancyWebViewController
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        for (UIViewController* childVC in rootViewController.childViewControllers) {
            if ([childVC isKindOfClass:[BalancyWebViewController class]]) {
                BalancyWebViewController* webViewController = (BalancyWebViewController*)childVC;
                
                NSString* jsCode = [NSString stringWithUTF8String:code];
                [webViewController.webView evaluateJavaScript:jsCode completionHandler:^(id _Nullable result, NSError * _Nullable error) {
                    if (error) {
                        NSLog(@"[BalancyWebView] JavaScript injection error: %@", error);
                    }
                }];
                
                return true;
            }
        }
        
        NSLog(@"[BalancyWebView] Cannot inject JS code: WebView not found");
        return false;
    }
}

} // extern "C"
