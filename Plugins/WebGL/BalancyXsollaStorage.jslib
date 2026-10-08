mergeInto(LibraryManager.library, {
  balancyXsollaInstallPopupOpener: function (requestPtr) {
    var requestId = UTF8ToString(requestPtr);
    var previous = window.__balancyOpenPaymentWindow;
    var opener = function (id) {
      if (id !== requestId) return null;
      var popup = window.open('about:blank', '_blank');
      if (popup) popup.opener = null;
      return popup;
    };
    Module.balancyXsollaPopup = { requestId: requestId, previous: previous, opener: opener };
    window.__balancyOpenPaymentWindow = opener;
  },
  balancyXsollaRemovePopupOpener: function (requestPtr) {
    var scope = Module.balancyXsollaPopup;
    if (!scope || scope.requestId !== UTF8ToString(requestPtr)) return;
    if (window.__balancyOpenPaymentWindow === scope.opener) {
      if (scope.previous === undefined) delete window.__balancyOpenPaymentWindow;
      else window.__balancyOpenPaymentWindow = scope.previous;
    }
    delete Module.balancyXsollaPopup;
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
