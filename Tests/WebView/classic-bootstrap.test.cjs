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
  const scripts = [], requests = [], errors = [], hostErrors = [], installs = [], received = [], sent = [];
  const context = vm.createContext({ console: { log() {}, warn() {}, error: error => errors.push(String(error)) }, Promise, JSON,
    BalancyWebView: { sendMessageToUnity: message => sent.push(message) },
    document: { createElement: () => ({}), head: { appendChild: script => scripts.push(script) } },
    XMLHttpRequest: function () { requests.push(this); this.open = (method, url) => { this.url = url; }; this.send = () => {}; } });
  context.window = vm.runInContext('globalThis', context);
  // Loading balancy-webview-bridge.js assigns window.balancy unconditionally, replacing any shim.
  const loadBridge = () => { context.window.balancy = { _installScripts: (code, version) => { installs.push(version); vm.runInContext(code, context); },
    initResponseHandler() { context.window.handlerStarted = true; }, _postHostError: error => hostErrors.push(String(error)),
    _receiveMessageFromUnity: message => received.push(message) }; };
  for (const step of steps) step === 'bridge' ? loadBridge() : vm.runInContext(step, context);
  return { context, scripts, requests, errors, hostErrors, installs, received, sent, loadBridge };
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
test('a bridge download error reaches Unity through the Android shim transport', () => {
  const p = page(androidShim(), LOADER);
  p.scripts[0].onerror();
  assert.equal(p.sent.length, 1);
  const message = JSON.parse(p.sent[0]);
  assert.equal(message.type, 'shellError');
  assert.equal(message.shellId, 'golden-shell');
  assert.match(message.error, /Failed to load Balancy bridge/);
  assert.equal(p.requests.length, 0);
  assert.equal(p.errors.length, 1);
});
test('a throwing bridge error reporter falls back to the native transport once', () => {
  const p = page('bridge', 'balancy._postHostError = () => { throw new Error("reporter failed"); };', LOADER);
  p.requests[0].onerror();
  assert.equal(p.sent.length, 1);
  assert.equal(JSON.parse(p.sent[0]).type, 'shellError');
  assert.match(JSON.parse(p.sent[0]).error, /Failed to load scripts file/);
});
test('a bridge download failure can use a custom legacy transport without the Android native object', () => {
  const p = page('window.rawErrors = []; window.BalancyWebView = undefined; window.balancy = { sendMessageToUnity(message) { rawErrors.push(message); } };', LOADER);
  p.scripts[0].onerror();
  assert.equal(p.context.window.rawErrors.length, 1);
  assert.equal(JSON.parse(p.context.window.rawErrors[0]).type, 'shellError');
});
test('a broken raw transport does not throw from the bridge error handler', () => {
  const p = page(androidShim(), 'BalancyWebView.sendMessageToUnity = () => { throw new Error("transport failed"); };', LOADER);
  assert.doesNotThrow(() => p.scripts[0].onerror());
  assert.equal(p.errors.length, 1);
  assert.match(p.errors[0], /Failed to load Balancy bridge/);
});
test('page without a shim (iOS): the loader loads the bridge', () => {
  const p = page(LOADER);
  assert.deepEqual(p.scripts.map(script => script.src), [BRIDGE]);
  finishLoading(p);
  assert.equal(p.context.window.bundleRuns, 1);
});
test('classic upgrade preserves owner and the legacy outbound transport', () => {
  const p = page(androidShim(), 'balancy.owner = { value: "Привет \\\\ \\\" 🎁" }; window.oldApi = balancy;', LOADER);
  const owner = p.context.window.oldApi.owner;
  finishLoading(p);
  assert.notEqual(p.context.window.balancy, p.context.window.oldApi);
  assert.equal(p.context.window.balancy.owner, owner);
  p.context.window.balancy.sendMessageToUnity('legacy request');
  p.context.window.oldApi.sendMessageToUnity('cached legacy request');
  assert.deepEqual(p.sent, ['legacy request', 'cached legacy request']);
  p.context.window.balancy.owner = { value: 'updated' };
  assert.equal(p.context.window.oldApi.owner.value, 'updated');
});
test('classic upgrade delivers each inbound message to the real bridge and the existing legacy handler', () => {
  const p = page(androidShim(), 'window.legacyMessages = []; balancy.onMessage = function(message) { legacyMessages.push(message); window.callbackOwner = this.owner; }; balancy.owner = { value: 42 };', LOADER);
  finishLoading(p);
  p.context.window.balancy._receiveMessageFromUnity('response');
  assert.deepEqual(p.received, ['response']);
  assert.deepEqual([...p.context.window.legacyMessages], ['response']);
  assert.equal(p.context.window.callbackOwner.value, 42);
});
test('late handler replacement and removal through either API reference are preserved', () => {
  const p = page(androidShim(), 'window.oldApi = balancy; window.legacyMessages = [];', LOADER);
  finishLoading(p);
  vm.runInContext('oldApi.onMessage = message => legacyMessages.push("old:" + message);', p.context);
  p.context.window.balancy._receiveMessageFromUnity('one');
  vm.runInContext('balancy.onMessage = message => legacyMessages.push("new:" + message);', p.context);
  p.context.window.balancy._receiveMessageFromUnity('two');
  p.context.window.balancy.onMessage = null;
  p.context.window.balancy._receiveMessageFromUnity('three');
  assert.deepEqual([...p.context.window.legacyMessages], ['old:one', 'new:two']);
  assert.equal(p.context.window.oldApi.onMessage, null);
  assert.deepEqual(p.received, ['one', 'two', 'three']);
});
test('a throwing legacy handler cannot prevent modern response delivery or later messages', () => {
  const p = page(androidShim(), 'balancy.onMessage = () => { throw new Error("legacy callback failed"); };', LOADER);
  finishLoading(p);
  p.context.window.balancy._receiveMessageFromUnity('one');
  p.context.window.balancy._receiveMessageFromUnity('two');
  assert.deepEqual(p.received, ['one', 'two']);
  assert.equal(p.errors.length, 2);
  assert.match(p.errors[0], /legacy callback failed/);
  assert.deepEqual(p.hostErrors, []);
});
test('a custom legacy receiver keeps its original state and does not replace the modern receiver', () => {
  const p = page('window.balancy = { count: 0, _receiveMessageFromUnity(message) { this.count++; this.lastMessage = message; } }; window.oldApi = balancy;', androidShim(), LOADER);
  finishLoading(p);
  p.context.window.balancy._receiveMessageFromUnity('response');
  assert.equal(p.context.window.oldApi.count, 1);
  assert.equal(p.context.window.oldApi.lastMessage, 'response');
  assert.deepEqual(p.received, ['response']);
});
test('reinjecting the loader does not wrap the legacy receiver twice', () => {
  const p = page(androidShim(), 'window.legacyMessages = []; balancy.onMessage = message => legacyMessages.push(message);', LOADER);
  finishLoading(p);
  const receive = p.context.window.balancy._receiveMessageFromUnity;
  vm.runInContext(LOADER, p.context);
  assert.equal(p.scripts.length, 1);
  assert.equal(p.context.window.balancy._receiveMessageFromUnity, receive);
  p.context.window.balancy._receiveMessageFromUnity('response');
  assert.deepEqual(p.received, ['response']);
  assert.deepEqual([...p.context.window.legacyMessages], ['response']);
});
test('a persistent bridge retains its API and receiver without classic compatibility wrapping', () => {
  const p = page('bridge', androidShim());
  const api = p.context.window.balancy, receive = api._receiveMessageFromUnity;
  vm.runInContext(LOADER, p.context);
  finishLoading(p);
  assert.equal(p.context.window.balancy, api);
  assert.equal(api._receiveMessageFromUnity, receive);
});
test('a loader that trusts any window.balancy fails on the Android shim (v1.9.1-v1.9.6), so the cases above catch it', () => {
  assert.ok(LOADER.includes(FIXED_GUARD), 'guard text changed; update FIXED_GUARD');
  const p = page(androidShim(), LOADER.replace(FIXED_GUARD, 'if (window.balancy) { start(); return; }'));
  assert.equal(p.scripts.length, 0);
  Object.assign(p.requests[0], { status: 0, responseText: BUNDLE }).onload();
  assert.match(p.errors.join('\n'), /_installScripts is not a function/);
  assert.equal(p.context.window.bundleRuns, undefined);
});
