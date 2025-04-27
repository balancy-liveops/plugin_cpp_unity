/**
 * Balancy WebView Bridge
 * A unified communication layer between web content and native platforms
 */

(function() {
    // Store original event listeners for compatibility
    const originalEventListeners = {
        addEventListener: document.addEventListener,
        removeEventListener: document.removeEventListener
    };

    // Create the bridge object if it doesn't exist
    if (!window.BalancyWebView) {
        window.BalancyWebView = {
            /**
             * Send a message to the native platform
             * @param {string|object} message - Message to send (objects will be converted to JSON)
             * @returns {boolean} Success status
             */
            postMessage: function(message) {
                // Convert objects to JSON strings
                const messageStr = typeof message === 'string' 
                    ? message 
                    : JSON.stringify(message);
                
                // Try different methods of communication in order of preference
                
                // 1. Native iOS bridge
                try {
                    // iOS WebView implementation
                    if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.balancyBridge) {
                        window.webkit.messageHandlers.balancyBridge.postMessage(messageStr);
                        return true;
                    }
                } catch (e) {
                    console.warn('Failed to use iOS bridge:', e);
                }
                
                // 2. Unity Editor bridge via URL scheme
                try {
                    // Unity Editor implementation via URL
                    if (window.unityWebView) {
                        window.unityWebView.postMessage(messageStr);
                        return true;
                    }
                    
                    // Another attempt with URL scheme as fallback
                    const encodedMessage = encodeURIComponent(messageStr);
                    const iframe = document.createElement('iframe');
                    iframe.style.display = 'none';
                    iframe.src = 'unitymessage://balancy?message=' + encodedMessage;
                    document.body.appendChild(iframe);
                    setTimeout(function() {
                        document.body.removeChild(iframe);
                    }, 100);
                    return true;
                } catch (e) {
                    console.warn('Failed to use Unity bridge:', e);
                }
                
                // 3. Fallback to custom event
                try {
                    // Fallback for platforms without native bridges
                    const event = new CustomEvent('balancyNativeMessage', { 
                        detail: messageStr,
                        bubbles: true,
                        cancelable: true
                    });
                    document.dispatchEvent(event);
                    return true;
                } catch (e) {
                    console.error('All communication methods failed:', e);
                    return false;
                }
            },
            
            /**
             * Add a listener for messages from the native platform
             * @param {function} callback - Function to call when a message is received
             */
            addMessageListener: function(callback) {
                if (typeof callback !== 'function') {
                    console.error('Message listener must be a function');
                    return;
                }
                
                // Use standard event listener for all platforms
                originalEventListeners.addEventListener.call(document, 'balancyMessage', function(e) {
                    callback(e.detail);
                });
            },
            
            /**
             * Remove a message listener
             * @param {function} callback - The callback function to remove
             */
            removeMessageListener: function(callback) {
                originalEventListeners.removeEventListener.call(document, 'balancyMessage', callback);
            }
        };
        
        console.log('BalancyWebView bridge initialized');
    }
})();
