// Simple WebSocket bridge for Unity WebGL
// These functions are called directly from C++ EM_JS code

mergeInto(LibraryManager.library, {
  typescript_websocket_connect_request: function(connectionId, urlPtr, authDataPtr) {
    var url = UTF8ToString(urlPtr);
    var authData = UTF8ToString(authDataPtr);
    
    console.log('[WebSocket Bridge] Connect request:', connectionId, url);
    console.log('[WebSocket Bridge] Auth data:', authData);
    
    // TODO: Implement WebSocket connection
    console.warn('[WebSocket Bridge] NOT IMPLEMENTED YET - WebSocket functionality disabled for now');
  },
  
  typescript_websocket_disconnect_request: function(connectionId) {
    console.log('[WebSocket Bridge] Disconnect request:', connectionId);
  },
  
  typescript_websocket_subscribe_event: function(connectionId, eventNamePtr) {
    var eventName = UTF8ToString(eventNamePtr);
    console.log('[WebSocket Bridge] Subscribe event:', connectionId, eventName);
  },
  
  typescript_websocket_send_message: function(connectionId, eventNamePtr, dataPtr) {
    var eventName = UTF8ToString(eventNamePtr);
    var data = UTF8ToString(dataPtr);
    console.log('[WebSocket Bridge] Send message:', connectionId, eventName);
  },
  
  typescript_websocket_send_ack: function(connectionId, ackId, responseDataPtr) {
    var responseData = UTF8ToString(responseDataPtr);
    console.log('[WebSocket Bridge] Send ack:', connectionId, ackId);
  }
});
