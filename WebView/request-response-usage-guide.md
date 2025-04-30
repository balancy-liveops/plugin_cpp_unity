# Balancy WebView Request-Response Usage Guide

This guide explains how to use the request-response functionality in the Balancy WebView system to communicate between JavaScript (in the WebView) and C# (in Unity).

## Overview

The request-response system allows JavaScript code to send requests to Unity and get responses back synchronously using Promises. This is useful for:

1. Getting data from the game to display in the WebView
2. Performing game actions triggered from the WebView
3. Processing data in Unity and returning results to the WebView
4. Validating user inputs from the WebView in Unity

## Using from JavaScript

### Basic Request

To send a simple request from JavaScript to Unity:

```javascript
// Send a request without parameters
window.BalancyWebView.sendRequest('getPlayerInfo')
  .then(playerInfo => {
    console.log('Player info:', playerInfo);
    // Use the player info in your UI
    document.getElementById('playerName').textContent = playerInfo.name;
    document.getElementById('playerLevel').textContent = playerInfo.level;
  })
  .catch(error => {
    console.error('Failed to get player info:', error);
    // Handle the error in your UI
    showErrorMessage('Failed to load player data');
  });
```

### Request with Parameters

To send a request with parameters:

```javascript
// Send a request with parameters
window.BalancyWebView.sendRequest('performAction', {
  actionType: 'purchase',
  itemId: 'sword_123',
  quantity: 1,
  currency: 'gems'
})
  .then(result => {
    if (result.success) {
      // Show success message
      showSuccessMessage(result.message);
      // Update UI with new data
      updateInventory(result.inventory);
    } else {
      // Show failure message
      showErrorMessage(result.message);
    }
  })
  .catch(error => {
    console.error('Action failed:', error);
    showErrorMessage('Action failed: ' + error.message);
  });
```

### Using with Async/Await

The `sendRequest` function returns a Promise, so you can use it with async/await for cleaner code:

```javascript
async function loadGameData() {
  try {
    // Show loading indicator
    showLoadingIndicator();
    
    // Make multiple requests sequentially
    const playerInfo = await window.BalancyWebView.sendRequest('getPlayerInfo');
    const gameState = await window.BalancyWebView.sendRequest('getGameState');
    const inventory = await window.BalancyWebView.sendRequest('getInventory');
    
    // Use all the data together
    updateUI(playerInfo, gameState, inventory);
    
    // Hide loading indicator
    hideLoadingIndicator();
  } catch (error) {
    console.error('Failed to load game data:', error);
    hideLoadingIndicator();
    showErrorMessage('Failed to load game data');
  }
}

// Call the async function
loadGameData();
```

### Error Handling

All errors in the request-response system are propagated to the JavaScript Promise's `catch` handler:

1. **Network errors**: If the request fails to reach Unity
2. **Timeout errors**: If Unity doesn't respond within the timeout period (default: 10 seconds)
3. **Processing errors**: If an error occurs while processing the request in Unity
4. **Missing handler errors**: If no handler is registered for the requested action

Always include error handling in your code to provide a good user experience.

## Implementing in Unity (C#)

### Registering Request Handlers

To handle requests from JavaScript, you need to register handler functions for specific actions:

```csharp
void Start()
{
    // Get the WebView instance
    var webView = BalancyWebView.Instance;
    
    // Register handlers for different request types
    webView.RegisterRequestHandler("getPlayerInfo", _ => {
        // Return player data from your game's state
        var player = GameManager.Instance.Player;
        return new {
            id = player.Id,
            name = player.Name,
            level = player.Level,
            xp = player.Experience,
            stats = new {
                strength = player.Strength,
                intelligence = player.Intelligence,
                dexterity = player.Dexterity
            }
        };
    });
    
    // Handler with parameters
    webView.RegisterRequestHandler("performAction", parameters => {
        string actionType = parameters["actionType"]?.ToString();
        string itemId = parameters["itemId"]?.ToString();
        int quantity = parameters["quantity"]?.Value<int>() ?? 1;
        string currency = parameters["currency"]?.ToString() ?? "coins";
        
        // Perform the action in your game
        bool success = false;
        string message = "Unknown error";
        
        try {
            // Example implementation
            switch (actionType)
            {
                case "purchase":
                    success = GameManager.Instance.PurchaseItem(itemId, quantity, currency);
                    message = success ? "Purchase successful" : "Purchase failed";
                    break;
                case "useItem":
                    success = GameManager.Instance.UseItem(itemId, quantity);
                    message = success ? "Item used" : "Failed to use item";
                    break;
                default:
                    message = "Unknown action type";
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            message = "Error: " + ex.Message;
        }
        
        // Return the result
        return new {
            success,
            message,
            inventory = GameManager.Instance.Player.GetSerializableInventory(),
            timestamp = DateTime.Now.ToString("o")
        };
    });
    
    // Open the WebView
    webView.OpenWebView("file:///path/to/your/page.html");
}
```

### Handler Function Requirements

Request handlers must:

1. Accept a single `JObject` parameter, which contains the request parameters from JavaScript
2. Return a serializable object (classes, anonymous objects, primitives, collections, etc.)
3. Handle exceptions internally to avoid crashing the application

The return value will be automatically serialized to JSON and sent back to JavaScript.

### Complex Data Processing

For complex data processing, you can use the full power of C#:

```csharp
webView.RegisterRequestHandler("processData", parameters => {
    try
    {
        // Extract data array from parameters
        JArray dataArray = parameters["data"] as JArray;
        if (dataArray == null || dataArray.Count == 0)
        {
            return new { success = false, error = "No data provided" };
        }
        
        // Extract action type
        string analysisType = parameters["analysisType"]?.ToString() ?? "summary";
        
        // Process the data based on analysis type
        switch (analysisType)
        {
            case "summary":
                // Calculate summary statistics
                double sum = 0;
                double min = double.MaxValue;
                double max = double.MinValue;
                
                foreach (var item in dataArray)
                {
                    if (item.Type == JTokenType.Integer || item.Type == JTokenType.Float)
                    {
                        double value = item.Value<double>();
                        sum += value;
                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                    }
                }
                
                double mean = dataArray.Count > 0 ? sum / dataArray.Count : 0;
                
                return new {
                    success = true,
                    results = new {
                        count = dataArray.Count,
                        sum,
                        mean,
                        min,
                        max
                    }
                };
                
            case "histogram":
                // Create a histogram
                // ... implementation details ...
                return new {
                    success = true,
                    results = histogramData
                };
                
            default:
                return new { success = false, error = "Unknown analysis type" };
        }
    }
    catch (Exception ex)
    {
        Debug.LogException(ex);
        return new { success = false, error = ex.Message };
    }
});
```

## Tips and Best Practices

### JavaScript

1. **Use timeouts wisely**: The default timeout is 10 seconds. For complex operations, consider increasing it.
2. **Handle errors gracefully**: Always provide user feedback when errors occur.
3. **Batch related requests**: If you need to make multiple related requests, consider creating a single request that returns all the needed data.
4. **Keep request parameters simple**: Use plain objects with primitive values when possible.

### Unity (C#)

1. **Keep handlers focused**: Each handler should do one thing well.
2. **Validate input parameters**: Always check parameters for null and invalid values.
3. **Handle exceptions**: Catch exceptions in your handlers to avoid crashing the application.
4. **Return meaningful error messages**: When an error occurs, provide a useful error message for the JavaScript side.
5. **Use anonymous objects for responses**: They are easy to create and automatically serialize to JSON.
6. **Consider thread safety**: If your handlers access shared resources, ensure they are thread-safe.

## Common Patterns

### Authentication

```javascript
// JavaScript
async function login(username, password) {
  try {
    const result = await window.BalancyWebView.sendRequest('login', {
      username,
      password
    });
    
    if (result.success) {
      // Store auth token and redirect to main page
      localStorage.setItem('authToken', result.token);
      window.location.href = 'main.html';
    } else {
      showLoginError(result.message);
    }
  } catch (error) {
    showLoginError('Login failed: ' + error.message);
  }
}
```

```csharp
// C#
webView.RegisterRequestHandler("login", parameters => {
    string username = parameters["username"]?.ToString();
    string password = parameters["password"]?.ToString();
    
    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
    {
        return new { success = false, message = "Username and password are required" };
    }
    
    // Authenticate with your backend
    bool authSuccess = AuthManager.Instance.Authenticate(username, password);
    
    if (authSuccess)
    {
        string token = AuthManager.Instance.GenerateToken();
        return new { success = true, token, expiresIn = 3600 };
    }
    else
    {
        return new { success = false, message = "Invalid username or password" };
    }
});
```

### Data Pagination

```javascript
// JavaScript
async function loadItems(page, pageSize) {
  try {
    const result = await window.BalancyWebView.sendRequest('getItems', {
      page,
      pageSize
    });
    
    displayItems(result.items);
    updatePagination(result.totalItems, result.totalPages, result.currentPage);
  } catch (error) {
    showError('Failed to load items: ' + error.message);
  }
}
```

```csharp
// C#
webView.RegisterRequestHandler("getItems", parameters => {
    int page = parameters["page"]?.Value<int>() ?? 1;
    int pageSize = parameters["pageSize"]?.Value<int>() ?? 10;
    
    // Get paginated items from your data source
    var allItems = ItemManager.Instance.GetAllItems();
    int totalItems = allItems.Count;
    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
    
    // Clamp page to valid range
    page = Math.Max(1, Math.Min(page, totalPages));
    
    // Get items for the current page
    var items = allItems
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(item => new {
            id = item.Id,
            name = item.Name,
            description = item.Description,
            price = item.Price,
            imageUrl = item.ImageUrl
        })
        .ToList();
    
    return new {
        items,
        totalItems,
        totalPages,
        currentPage = page
    };
});
```

## Conclusion

The request-response system provides a powerful way to communicate between your web UI and Unity game logic. By leveraging Promises in JavaScript and strong typing in C#, you can create robust, maintainable code that handles complex interactions between the two environments.
