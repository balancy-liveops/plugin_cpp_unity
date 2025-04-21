// IOSWebViewImpl.mm
#include "IOSWebViewImpl.h"
#import "BalancyWebView.h"
#include <memory>

namespace Balancy {

// Private implementation class that holds all iOS-specific objects
class IOSWebViewImpl::Impl {
public:
    Impl();
    ~Impl();
    
    bool openWebView(const std::string& url, int width, int height);
    void closeWebView();
    bool sendMessage(const std::string& message);
    
    void setMessageCallback(WebViewInterface::MessageCallback callback) {
        m_messageCallback = std::move(callback);
    }
    
    void setLoadCompletedCallback(WebViewInterface::LoadCompletedCallback callback) {
        m_loadCompletedCallback = std::move(callback);
    }
    
    void setCacheCompletedCallback(WebViewInterface::CacheCompletedCallback callback) {
        m_cacheCompletedCallback = std::move(callback);
    }
    
    void setOfflineCacheEnabled(bool enable);
    
    // Called by Objective-C when a message is received
    void onMessageReceived(const std::string& message);
    
    // Called by Objective-C when a page load completes
    void onLoadCompleted(bool success);
    
    // Called by Objective-C when caching completes
    void onCacheCompleted(bool success);
    
private:
    BalancyWebViewController* m_viewController;
    WebViewInterface::MessageCallback m_messageCallback;
    WebViewInterface::LoadCompletedCallback m_loadCompletedCallback;
    WebViewInterface::CacheCompletedCallback m_cacheCompletedCallback;
};

// Forward declarations for Objective-C callbacks
extern "C" {
    void BalancyWebViewMessageCallback(void* context, const char* message);
    void BalancyWebViewLoadCompletedCallback(void* context, bool success);
    void BalancyWebViewCacheCompletedCallback(void* context, bool success);
}

// Create a replacement for std::make_unique (for C++11 compatibility)
template<typename T, typename... Args>
std::unique_ptr<T> make_unique_ptr(Args&&... args) {
    return std::unique_ptr<T>(new T(std::forward<Args>(args)...));
}

// Implementations
IOSWebViewImpl::IOSWebViewImpl() : m_impl(make_unique_ptr<Impl>()) {
}

IOSWebViewImpl::~IOSWebViewImpl() = default;

bool IOSWebViewImpl::openWebView(const std::string& url, int width, int height) {
    return m_impl->openWebView(url, width, height);
}

void IOSWebViewImpl::closeWebView() {
    m_impl->closeWebView();
}

bool IOSWebViewImpl::sendMessage(const std::string& message) {
    return m_impl->sendMessage(message);
}

void IOSWebViewImpl::setMessageCallback(MessageCallback callback) {
    m_impl->setMessageCallback(std::move(callback));
}

void IOSWebViewImpl::setLoadCompletedCallback(LoadCompletedCallback callback) {
    m_impl->setLoadCompletedCallback(std::move(callback));
}

void IOSWebViewImpl::setCacheCompletedCallback(CacheCompletedCallback callback) {
    m_impl->setCacheCompletedCallback(std::move(callback));
}

void IOSWebViewImpl::setOfflineCacheEnabled(bool enable) {
    m_impl->setOfflineCacheEnabled(enable);
}

// Implementation of the private Impl class
IOSWebViewImpl::Impl::Impl() : m_viewController(nil) {
}

IOSWebViewImpl::Impl::~Impl() {
    closeWebView();
}

bool IOSWebViewImpl::Impl::openWebView(const std::string& url, int width, int height) {
    @autoreleasepool {
        // Close any existing webview first
        closeWebView();
        
        // Create the web view controller
        m_viewController = [[BalancyWebViewController alloc] initWithContext:this
                                                            messageCallback:&BalancyWebViewMessageCallback
                                                            loadCompletedCallback:&BalancyWebViewLoadCompletedCallback
                                                            cacheCompletedCallback:&BalancyWebViewCacheCompletedCallback];
        
        // Set offline cache enabled if previously configured
        [m_viewController setOfflineCacheEnabled:m_viewController.offlineCacheEnabled];
        
        // Present the WebView
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        if (!rootViewController) {
            NSLog(@"Failed to get root view controller");
            return false;
        }
        
        // Add the webview controller as a child
        [rootViewController addChildViewController:m_viewController];
        [rootViewController.view addSubview:m_viewController.view];
        [m_viewController didMoveToParentViewController:rootViewController];
        
        // Configure the view size
        if (width > 0 && height > 0) {
            // Calculate center position
            CGFloat centerX = rootViewController.view.bounds.size.width / 2;
            CGFloat centerY = rootViewController.view.bounds.size.height / 2;
            
            // Set frame
            m_viewController.view.frame = CGRectMake(centerX - width/2, centerY - height/2, width, height);
        } else {
            // Use full screen
            m_viewController.view.frame = rootViewController.view.bounds;
        }
        
        // Load the URL
        NSString* nsUrl = [NSString stringWithUTF8String:url.c_str()];
        return [m_viewController loadURL:nsUrl];
    }
}

void IOSWebViewImpl::Impl::closeWebView() {
    @autoreleasepool {
        if (m_viewController) {
            [m_viewController close];
            m_viewController = nil;
        }
    }
}

bool IOSWebViewImpl::Impl::sendMessage(const std::string& message) {
    @autoreleasepool {
        if (!m_viewController) {
            return false;
        }
        
        NSString* nsMessage = [NSString stringWithUTF8String:message.c_str()];
        return [m_viewController sendMessage:nsMessage];
    }
}

void IOSWebViewImpl::Impl::setOfflineCacheEnabled(bool enable) {
    @autoreleasepool {
        if (m_viewController) {
            [m_viewController setOfflineCacheEnabled:enable];
        }
    }
}

void IOSWebViewImpl::Impl::onMessageReceived(const std::string& message) {
    if (m_messageCallback) {
        m_messageCallback(message);
    }
}

void IOSWebViewImpl::Impl::onLoadCompleted(bool success) {
    if (m_loadCompletedCallback) {
        m_loadCompletedCallback(success);
    }
}

void IOSWebViewImpl::Impl::onCacheCompleted(bool success) {
    if (m_cacheCompletedCallback) {
        m_cacheCompletedCallback(success);
    }
}

// C callback functions
void BalancyWebViewMessageCallback(void* context, const char* message) {
    auto* impl = static_cast<IOSWebViewImpl::Impl*>(context);
    if (impl && message) {
        impl->onMessageReceived(message);
    }
}

void BalancyWebViewLoadCompletedCallback(void* context, bool success) {
    auto* impl = static_cast<IOSWebViewImpl::Impl*>(context);
    if (impl) {
        impl->onLoadCompleted(success);
    }
}

void BalancyWebViewCacheCompletedCallback(void* context, bool success) {
    auto* impl = static_cast<IOSWebViewImpl::Impl*>(context);
    if (impl) {
        impl->onCacheCompleted(success);
    }
}

// Define the factory method for iOS
WebViewInterface* WebViewFactory::createWebView() {
    return new IOSWebViewImpl();
}

}