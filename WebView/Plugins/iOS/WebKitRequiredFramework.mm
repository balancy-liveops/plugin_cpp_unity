//
//  WebKitRequiredFramework.mm
//  This file ensures that the WebKit framework is linked when building for iOS
//

#import <WebKit/WebKit.h>

// This class is not used, it's only here to ensure WebKit is linked
@interface WebKitFrameworkLinker : NSObject
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, strong) WKWebViewConfiguration *configuration;
@property (nonatomic, strong) WKUserContentController *userContentController;
@property (nonatomic, strong) WKUserScript *userScript;
@end

@implementation WebKitFrameworkLinker
@end
