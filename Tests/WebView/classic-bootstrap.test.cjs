const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
const BRIDGE = 'file:///android_asset/Balancy/balancy-webview-bridge.js', SCRIPTS = 'file:///data/balancy/scripts_combined.js';
const BUNDLE = 'window.bundleRuns = (window.bundleRuns || 0) + 1;';
// The loader Unity injects after every page load; RuntimeBootstrapTests keeps this fixture identical to RuntimeBootstrap.
const LOADER = fs.readFileSync(path.join(root, 'Tests/WebView/native/classic-bootstrap.js.txt'), 'utf8')
  .replace('__BRIDGE_URL__', BRIDGE).replace('__SCRIPTS_URL__', SCRIPTS);
const FIXED_GUARD = "if (window.balancy && typeof window.balancy._installScripts === 'function') { start(); return; }";
// The page-finish shim exactly as BalancyWebViewPlugin.injectBalancyBridge() concatenates it.
function androidShim() {
  const java = fs.readFileSync(path.join(root, 'WebView/WVAndroidLib.androidlib/AndroidProject/app/src/main/java/com/balancy/webview/BalancyWebViewPlugin.java'), 'utf8');
  const method = /void injectBalancyBridge\(\) \{([\s\S]*?)webView\.evaluateJavascript\(bridge, null\);/.exec(java);
  assert.ok(method, 'injectBalancyBridge() changed shape; update this extractor');
  return [...method[1].matchAll(/"((?:[^"\\]|\\.)*)"/g)].map(m => JSON.parse('"' + m[1].replace(/\\'/g, "'") + '"')).join('');
}
// A page whose window is the global, so the shim's bare `balancy` and the loader's `window.balancy` are one object.
function page(...steps) {
  const scripts = [], requests = [], errors = [], hostErrors = [], installs = [];
  const context = vm.createContext({ console: { log() {}, warn() {}, error: error => errors.push(String(error)) }, Promise, JSON,
    BalancyWebView: { sendMessageToUnity() {} },
    document: { createElement: () => ({}), head: { appendChild: script => scripts.push(script) } },
    XMLHttpRequest: function () { requests.push(this); this.open = (method, url) => { this.url = url; }; this.send = () => {}; } });
  context.window = vm.runInContext('globalThis', context);
  // Loading balancy-webview-bridge.js assigns window.balancy unconditionally, replacing any shim.
  const loadBridge = () => { context.window.balancy = { _installScripts: (code, version) => { installs.push(version); vm.runInContext(code, context); },
    initResponseHandler() { context.window.handlerStarted = true; }, _postHostError: error => hostErrors.push(String(error)) }; };
  for (const step of steps) step === 'bridge' ? loadBridge() : vm.runInContext(step, context);
  return { context, scripts, requests, errors, hostErrors, installs, loadBridge };
}
function finishLoading(p) {
  if (p.scripts.length) { p.loadBridge(); p.scripts[0].onload(); }
  assert.equal(p.requests.length, 1); assert.equal(p.requests[0].url, SCRIPTS);
  Object.assign(p.requests[0], { status: 0, responseText: BUNDLE }).onload();
}
test('the Android page-finish shim leaves a window.balancy that cannot install scripts', () => {
  const p = page(androidShim());
  assert.equal(typeof p.context.window.balancy, 'object');
  assert.equal(typeof p.context.window.balancy._installScripts, 'undefined');
});
test('Android classic page: the loader replaces the page-finish shim with the bridge and installs the scripts', () => {
  const p = page(androidShim(), LOADER);
  assert.deepEqual(p.scripts.map(script => script.src), [BRIDGE]);
  finishLoading(p);
  assert.deepEqual(p.installs, ['golden-version']);
  assert.equal(p.context.window.bundleRuns, 1);
  assert.equal(p.context.window.handlerStarted, true);
  assert.deepEqual([...p.errors, ...p.hostErrors], []);
});
test('persistent shell: the loader reuses the bridge the shell loaded before page finish', () => {
  const p = page('bridge', androidShim(), LOADER);
  assert.equal(p.scripts.length, 0);
  finishLoading(p);
  assert.deepEqual(p.installs, ['golden-version']);
  assert.equal(p.context.window.bundleRuns, 1);
});
test('page without a shim (iOS): the loader loads the bridge', () => {
  const p = page(LOADER);
  assert.deepEqual(p.scripts.map(script => script.src), [BRIDGE]);
  finishLoading(p);
  assert.equal(p.context.window.bundleRuns, 1);
});
test('a loader that trusts any window.balancy fails on the Android shim (v1.9.1-v1.9.6), so the cases above catch it', () => {
  assert.ok(LOADER.includes(FIXED_GUARD), 'guard text changed; update FIXED_GUARD');
  const p = page(androidShim(), LOADER.replace(FIXED_GUARD, 'if (window.balancy) { start(); return; }'));
  assert.equal(p.scripts.length, 0);
  Object.assign(p.requests[0], { status: 0, responseText: BUNDLE }).onload();
  assert.match(p.errors.join('\n'), /_installScripts is not a function/);
  assert.equal(p.context.window.bundleRuns, undefined);
});
