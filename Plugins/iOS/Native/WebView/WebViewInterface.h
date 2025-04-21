// WebViewInterface.h
#pragma once

#include <string>
#include <functional>

namespace Balancy {

/**
 * Interface for WebView functionality across platforms
 */
class WebViewInterface {
public:
    // Callback type for receiving messages from WebView
    using MessageCallback = std::function<void(const std::string&)>;
    
    // Callback type for handling load completion events
    using LoadCompletedCallback = std::function<void(bool success)>;
    
    // Callback type for cache completion notification
    using CacheCompletedCallback = std::function<void(bool success)>;
    
    virtual ~WebViewInterface() = default;
    
    /**
     * Opens a WebView with the specified URL
     * @param url The URL to load (can be remote or local)
     * @param width The width of the WebView (0 for full width)
     * @param height The height of the WebView (0 for full height)
     * @return true if WebView was opened successfully
     */
    virtual bool openWebView(const std::string& url, int width = 0, int height = 0) = 0;
    
    /**
     * Closes the currently open WebView
     */
    virtual void closeWebView() = 0;
    
    /**
     * Sends a message to the WebView
     * @param message The message to send (typically JSON)
     * @return true if message was sent successfully
     */
    virtual bool sendMessage(const std::string& message) = 0;
    
    /**
     * Sets the callback for receiving messages from WebView
     * @param callback The function to call when a message is received
     */
    virtual void setMessageCallback(MessageCallback callback) = 0;
    
    /**
     * Sets the callback for load completion notification
     * @param callback The function to call when page load completes
     */
    virtual void setLoadCompletedCallback(LoadCompletedCallback callback) = 0;
    
    /**
     * Sets the callback for cache completion notification
     * @param callback The function to call when caching completes
     */
    virtual void setCacheCompletedCallback(CacheCompletedCallback callback) = 0;
    
    /**
     * Enables or disables offline caching
     * @param enable True to enable caching, false to disable
     */
    virtual void setOfflineCacheEnabled(bool enable) = 0;
};

/**
 * Factory class to create platform-specific WebView implementations
 */
class WebViewFactory {
public:
    /**
     * Creates a WebView implementation for the current platform
     * @return A pointer to the created WebView implementation
     */
    static WebViewInterface* createWebView();
};

}