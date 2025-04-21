// BalancyWebViewUnityBridge.mm
// Unity native plugin wrapper for Balancy WebView

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>
#import "BalancyWebView.h"

// Unity C interface for iOS
extern "C" {
    // Open WebView with URL
    bool _BalancyWebViewOpen(const char* url, int width, int height) {
        @autoreleasepool {
            if (!url) return false;
            
            NSString* urlString = [NSString stringWithUTF8String:url];
            return [BalancyWebViewUnityBridge openWebView:urlString width:width height:height];
        }
    }
    
    // Close WebView
    void _BalancyWebViewClose() {
        @autoreleasepool {
            [BalancyWebViewUnityBridge closeWebView];
        }
    }
    
    // Send message to WebView
    bool _BalancyWebViewSendMessage(const char* message) {
        @autoreleasepool {
            if (!message) return false;
            
            NSString* messageString = [NSString stringWithUTF8String:message];
            return [BalancyWebViewUnityBridge sendMessage:messageString];
        }
    }
    
    // Set offline cache enabled
    void _BalancyWebViewSetOfflineCacheEnabled(bool enabled) {
        @autoreleasepool {
            [BalancyWebViewUnityBridge setOfflineCacheEnabled:enabled];
        }
    }
}

// Function to send a message to Unity
void UnitySendMessage(const char* gameObjectName, const char* methodName, const char* message) {
    // This function is defined in Unity's native plugin interface
    typedef void (*UnitySendMessageFuncType)(const char*, const char*, const char*);
    
    // Try to find the function in the unity binary - this is a simplification
    // In a real implementation, you would need to get a pointer to this function from Unity
    static UnitySendMessageFuncType unitySendMessageFunc = NULL;
    
    // Call the function if available
    if (unitySendMessageFunc != NULL) {
        unitySendMessageFunc(gameObjectName, methodName, message);
    } else {
        NSLog(@"UnitySendMessage function not available");
    }
}
