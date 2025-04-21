// IOSWebViewImpl.h
#pragma once

#include "WebViewInterface.h"
#include <memory>

namespace Balancy {

/**
 * iOS-specific implementation of WebView interface
 */
class IOSWebViewImpl : public WebViewInterface {
public:
    IOSWebViewImpl();
    ~IOSWebViewImpl() override;
    
    // WebViewInterface implementation
    bool openWebView(const std::string& url, int width = 0, int height = 0) override;
    void closeWebView() override;
    bool sendMessage(const std::string& message) override;
    void setMessageCallback(MessageCallback callback) override;
    void setLoadCompletedCallback(LoadCompletedCallback callback) override;
    void setCacheCompletedCallback(CacheCompletedCallback callback) override;
    void setOfflineCacheEnabled(bool enable) override;
    
    // Private implementation to hide iOS-specific details
    class Impl;
    std::unique_ptr<Impl> m_impl;
};

}