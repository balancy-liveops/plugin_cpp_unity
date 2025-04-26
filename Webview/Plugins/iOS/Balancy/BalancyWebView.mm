#import "BalancyWebView.h"

@interface BalancyWebViewController ()
@property (nonatomic, strong) WKWebView *webView;
@property (nonatomic, assign) void *context;
@property (nonatomic, assign) BalancyWebViewMessageCallback messageCallback;
@property (nonatomic, assign) BalancyWebViewLoadCompletedCallback loadCompletedCallback;
@property (nonatomic, assign) BalancyWebViewCacheCompletedCallback cacheCompletedCallback;
@property (nonatomic, assign) BOOL offlineCacheEnabled;
@end

@implementation BalancyWebViewController

- (instancetype)initWithContext:(void*)context
                messageCallback:(BalancyWebViewMessageCallback)messageCallback
          loadCompletedCallback:(BalancyWebViewLoadCompletedCallback)loadCompletedCallback
          cacheCompletedCallback:(BalancyWebViewCacheCompletedCallback)cacheCompletedCallback {
    self = [super init];
    if (self) {
        self.context = context;
        self.messageCallback = messageCallback;
        self.loadCompletedCallback = loadCompletedCallback;
        self.cacheCompletedCallback = cacheCompletedCallback;
        self.offlineCacheEnabled = NO;
    }
    return self;
}

- (void)viewDidLoad {
    [super viewDidLoad];
    
    // Configure WebView
    WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
    [config.userContentController addScriptMessageHandler:self name:@"balancyBridge"];
    
    // Add JavaScript bridge
    NSString *bridgePath = [[NSBundle mainBundle] pathForResource:@"balancy-bridge" ofType:@"js"];
    if (bridgePath) {
        NSString *bridgeCode = [NSString stringWithContentsOfFile:bridgePath encoding:NSUTF8StringEncoding error:nil];
        if (bridgeCode) {
            WKUserScript *script = [[WKUserScript alloc] initWithSource:bridgeCode
                                                          injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                       forMainFrameOnly:YES];
            [config.userContentController addUserScript:script];
        } else {
            NSLog(@"Failed to load JavaScript bridge code, using fallback");
            // Fallback: include the bridge code directly
            NSString *fallbackBridgeCode = @"(function(){const e=window.webkit&&window.webkit.messageHandlers||window.BalancyWebView;const a={initialized:false,listeners:{},init:function(){if(this.initialized)return;window.addEventListener('balancyMessage',function(e){try{const t=JSON.parse(e.detail);a.dispatchEvent(t.action,t)}catch(e){console.error('Error parsing message:',e)}});window.BalancyWebView={postMessage:function(e){a.sendToNative(e)}};this.sendToNative({action:'ready',timestamp:Date.now()});this.initialized=true},sendToNative:function(e){const t=typeof e==='string'?e:JSON.stringify(e);if(window.webkit&&window.webkit.messageHandlers&&window.webkit.messageHandlers.balancyBridge){window.webkit.messageHandlers.balancyBridge.postMessage(t)}else if(window.BalancyAndroidBridge){window.BalancyAndroidBridge.postMessage(t)}else if(window.unityWebView){window.unityWebView.postMessage(t)}else{console.log('[BalancyBridge] Message:',t)}},addEventListener:function(e,t){if(!this.listeners[e]){this.listeners[e]=[]}this.listeners[e].push(t)},removeEventListener:function(e,t){if(!this.listeners[e])return;const n=this.listeners[e].indexOf(t);if(n!==-1){this.listeners[e].splice(n,1)}},dispatchEvent:function(e,t){if(!this.listeners[e])return;this.listeners[e].forEach(function(e){e(t)})}};a.init();window.BalancyBridge=a})();";
            WKUserScript *fallbackScript = [[WKUserScript alloc] initWithSource:fallbackBridgeCode
                                                               injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                            forMainFrameOnly:YES];
            [config.userContentController addUserScript:fallbackScript];
        }
    } else {
        NSLog(@"JavaScript bridge not found, using fallback");
        // Fallback: include the bridge code directly
        NSString *fallbackBridgeCode = @"(function(){const e=window.webkit&&window.webkit.messageHandlers||window.BalancyWebView;const a={initialized:false,listeners:{},init:function(){if(this.initialized)return;window.addEventListener('balancyMessage',function(e){try{const t=JSON.parse(e.detail);a.dispatchEvent(t.action,t)}catch(e){console.error('Error parsing message:',e)}});window.BalancyWebView={postMessage:function(e){a.sendToNative(e)}};this.sendToNative({action:'ready',timestamp:Date.now()});this.initialized=true},sendToNative:function(e){const t=typeof e==='string'?e:JSON.stringify(e);if(window.webkit&&window.webkit.messageHandlers&&window.webkit.messageHandlers.balancyBridge){window.webkit.messageHandlers.balancyBridge.postMessage(t)}else if(window.BalancyAndroidBridge){window.BalancyAndroidBridge.postMessage(t)}else if(window.unityWebView){window.unityWebView.postMessage(t)}else{console.log('[BalancyBridge] Message:',t)}},addEventListener:function(e,t){if(!this.listeners[e]){this.listeners[e]=[]}this.listeners[e].push(t)},removeEventListener:function(e,t){if(!this.listeners[e])return;const n=this.listeners[e].indexOf(t);if(n!==-1){this.listeners[e].splice(n,1)}},dispatchEvent:function(e,t){if(!this.listeners[e])return;this.listeners[e].forEach(function(e){e(t)})}};a.init();window.BalancyBridge=a})();";
        WKUserScript *fallbackScript = [[WKUserScript alloc] initWithSource:fallbackBridgeCode
                                                           injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                        forMainFrameOnly:YES];
        [config.userContentController addUserScript:fallbackScript];
    }
    
    // Create WebView
    self.webView = [[WKWebView alloc] initWithFrame:self.view.bounds configuration:config];
    self.webView.navigationDelegate = self;
    self.webView.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    [self.view addSubview:self.webView];
}

- (bool)loadURL:(NSString*)url {
    if (!self.webView) {
        return NO;
    }
    
    NSURL *nsUrl = [NSURL URLWithString:url];
    NSURLRequest *request = [NSURLRequest requestWithURL:nsUrl];
    [self.webView loadRequest:request];
    return YES;
}

- (bool)sendMessage:(NSString*)message {
    if (!self.webView) {
        return NO;
    }
    
    NSString *jsCode = [NSString stringWithFormat:@"document.dispatchEvent(new CustomEvent('balancyMessage', { detail: '%@' }));", message];
    [self.webView evaluateJavaScript:jsCode completionHandler:nil];
    return YES;
}

- (void)setOfflineCacheEnabled:(bool)enabled {
    self.offlineCacheEnabled = enabled;
    
    // Implement cache configuration if needed
    if (self.cacheCompletedCallback) {
        self.cacheCompletedCallback(self.context, enabled);
    }
}

- (bool)offlineCacheEnabled {
    return self.offlineCacheEnabled;
}

- (void)close {
    // Clean up
    [self.webView stopLoading];
    self.webView = nil;
}

#pragma mark - WKNavigationDelegate

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    if (self.loadCompletedCallback) {
        self.loadCompletedCallback(self.context, YES);
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    if (self.loadCompletedCallback) {
        self.loadCompletedCallback(self.context, NO);
    }
}

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    if ([message.name isEqualToString:@"balancyBridge"] && self.messageCallback) {
        NSString *messageStr = [message.body description];
        self.messageCallback(self.context, [messageStr UTF8String]);
    }
}

@end