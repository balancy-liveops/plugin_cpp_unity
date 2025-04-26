/**
 * Balancy WebView JavaScript Bridge
 * This script provides bidirectional communication between the WebView and native code
 */
(function() {
    // Check if we're running in a WebView
    const isWebView = (window.webkit && window.webkit.messageHandlers) || 
                      (window.BalancyWebView);
    
    // Initialize bridge
    const BalancyBridge = {
        // Flag to indicate if bridge is initialized
        initialized: false,
        
        // Event listeners
        listeners: {},
        
        // Initialize the bridge
        init: function() {
            if (this.initialized) return;
            
            // Set up message receiver
            window.addEventListener('balancyMessage', function(e) {
                try {
                    const data = JSON.parse(e.detail);
                    BalancyBridge.dispatchEvent(data.action, data);
                } catch (err) {
                    console.error('Error parsing message:', err);
                }
            });
            
            // Inject bridge object for native code to call
            window.BalancyWebView = {
                postMessage: function(message) {
                    BalancyBridge.sendToNative(message);
                }
            };
            
            // Send ready event
            this.sendToNative({
                action: 'ready',
                timestamp: Date.now()
            });
            
            this.initialized = true;
        },
        
        // Send message to native code
        sendToNative: function(message) {
            const messageStr = typeof message === 'string' 
                ? message 
                : JSON.stringify(message);
                
            // iOS WebKit message handler
            if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.balancyBridge) {
                window.webkit.messageHandlers.balancyBridge.postMessage(messageStr);
            }
            // Android interface
            else if (window.BalancyAndroidBridge) {
                window.BalancyAndroidBridge.postMessage(messageStr);
            }
            // Unity WebView
            else if (window.unityWebView) {
                window.unityWebView.postMessage(messageStr);
            }
            // Console fallback (for debugging)
            else {
                console.log('[BalancyBridge] Message:', messageStr);
            }
        },
        
        // Add event listener
        addEventListener: function(event, callback) {
            if (!this.listeners[event]) {
                this.listeners[event] = [];
            }
            this.listeners[event].push(callback);
        },
        
        // Remove event listener
        removeEventListener: function(event, callback) {
            if (!this.listeners[event]) return;
            
            const index = this.listeners[event].indexOf(callback);
            if (index !== -1) {
                this.listeners[event].splice(index, 1);
            }
        },
        
        // Dispatch event to listeners
        dispatchEvent: function(event, data) {
            if (!this.listeners[event]) return;
            
            this.listeners[event].forEach(function(callback) {
                callback(data);
            });
        }
    };
    
    // Initialize the bridge
    BalancyBridge.init();
    
    // Expose bridge to global scope
    window.BalancyBridge = BalancyBridge;
})();