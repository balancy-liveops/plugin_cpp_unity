mergeInto(LibraryManager.library, {
  balancyXsollaInstallPopupOpener: function (requestPtr) {
    var requestId = UTF8ToString(requestPtr);
    var previous = window.__balancyOpenPaymentWindow;
    var scope = { requestId: requestId, previous: previous, opener: null, popup: null };
    scope.closeUnusedPopup = function () {
      var popup = scope.popup;
      scope.popup = null;
      if (!popup) return;
      try { if (!popup.closed && popup.location.href === 'about:blank') popup.close(); }
      catch (_) { /* A navigated provider window is never closed. */ }
    };
    var opener = function (id) {
      if (id !== requestId || Module.balancyXsollaPopup !== scope) return null;
      scope.closeUnusedPopup();
      var popup = window.open('about:blank', '_blank');
      if (popup) popup.opener = null;
      scope.popup = popup;
      return popup;
    };
    scope.opener = opener;
    Module.balancyXsollaPopup = scope;
    window.__balancyOpenPaymentWindow = opener;
  },
  balancyXsollaRemovePopupOpener: function (requestPtr) {
    var scope = Module.balancyXsollaPopup;
    if (!scope || scope.requestId !== UTF8ToString(requestPtr)) return;
    scope.closeUnusedPopup(); // Synchronous, before the C# renderer lease removes its iframe.
    if (window.__balancyOpenPaymentWindow === scope.opener) {
      if (scope.previous === undefined) delete window.__balancyOpenPaymentWindow;
      else window.__balancyOpenPaymentWindow = scope.previous;
    }
    delete Module.balancyXsollaPopup;
  },
  balancyXsollaCheckoutDispatched: function (requestPtr) {
    var scope = Module.balancyXsollaPopup;
    if (scope && scope.requestId === UTF8ToString(requestPtr)) scope.popup = null;
  },
  balancyXsollaFlushStorage__deps: ['$FS'],
  balancyXsollaFlushStorage: function (callbackId, callback) {
    var done = function (ok) {
      {{{ makeDynCall('viii', 'callback') }}}(callbackId, ok ? 1 : 0, 0);
    };
    try {
      FS.syncfs(false, function (error) { done(!error); });
    } catch (_) { done(false); }
  }
});
