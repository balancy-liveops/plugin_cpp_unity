namespace Balancy
{
    /// <summary>
    /// Reason a view failed to open. Passed to the optional onFailed callback of
    /// <see cref="RenderViewsManager.OpenLocalView"/> / <see cref="RenderViewsManager.OpenView"/>
    /// and <c>UnnyObject.OpenView</c> so the game can fall back to another option.
    /// </summary>
    public enum ViewOpenError
    {
        /// <summary>The view content could not be resolved (missing/empty path or URL, or the object is not a view).</summary>
        ViewNotFound = 0,

        /// <summary>Another view is already open; close it before opening a new one.</summary>
        AlreadyOpened = 1,

        /// <summary>A local HTML file was expected (persistent WebView) but does not exist on disk.</summary>
        FileNotFound = 2,

        /// <summary>The WebView failed to open or the local content could not be read.</summary>
        LoadFailed = 3,
    }
}
