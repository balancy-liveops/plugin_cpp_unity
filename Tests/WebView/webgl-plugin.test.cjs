const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
function harness() {
  const scripts = [], messages = [];
  const context = { window: {}, console: { log() {}, warn() {}, error() {} }, Promise,
    document: { createElement: () => ({ remove() {} }), head: { appendChild: script => scripts.push(script) } },
    UTF8ToString: value => value, SendMessage: (...args) => messages.push(args),
    LibraryManager: { library: {} }, autoAddDeps() {}, mergeInto: Object.assign };
  vm.createContext(context);
  vm.runInContext(fs.readFileSync(path.join(root, 'Plugins/WebGL/BalancyWebView.jslib'), 'utf8'), context);
  context.BalancyWebViewState = context.BalancyWebViewPlugin.$BalancyWebViewState;
  return { context, plugin: context.BalancyWebViewPlugin, scripts, messages };
}
const tick = () => new Promise(resolve => setImmediate(resolve));
test('concurrent WebGL requests share loading and inject bridge only inside the WebView', async () => {
  const { context, scripts } = harness();
  const received = [];
  context.BalancyWebViewState.getWebView(view => received.push(view));
  context.BalancyWebViewState.getWebView(view => received.push(view));
  await tick(); assert.equal(scripts.length, 1); assert.equal(received.length, 0);
  scripts[0].onload(); await tick(); assert.equal(scripts.length, 2);
  const view = {}; context.window.balancyWebView = view;
  scripts[1].onload(); await tick();
  assert.deepEqual(received, [view, view]); assert.equal(scripts.length, 2);
  assert.ok(scripts.every(script => !script.src.includes('bridge')));
});
test('WebGL loader failure can retry; persistent controls reach the adapter once', async () => {
  const { context, plugin, scripts, messages } = harness();
  plugin._balancyPrepareWebView('failed-shell'); await tick();
  scripts[0].onerror(); await tick();
  assert.equal(JSON.parse(messages[0][2]).shellId, 'failed-shell');
  const calls = [];
  context.window.balancyWebView = { prepareWebView: id => calls.push(id), show: () => calls.push('show'),
    hide: () => calls.push('hide'), closeWebView: notify => calls.push(['close', notify]) };
  plugin._balancyPrepareWebView('next-shell'); await tick();
  plugin._balancyShowWebView(); plugin._balancyHideWebView(); plugin._balancyCloseWebView();
  assert.deepEqual(calls, ['next-shell', 'show', 'hide', ['close', false]]);
});
test('obsolete WebGL asset cannot overwrite active plugin exports', () => {
  const { context } = harness();
  const active = { ...context.LibraryManager.library };
  vm.runInContext(fs.readFileSync(path.join(root, 'WebView/Plugins/WebGL/BalancyWebViewPlugin.jslib'), 'utf8'), context);
  assert.deepEqual(context.LibraryManager.library, active);
});
