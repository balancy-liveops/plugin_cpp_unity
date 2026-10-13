const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');

// Optional DOM dependency supplied by the TypeScript workspace or a separate jsdom installation.
// This executes the shipped bridge, not the bridge mock in classic-bootstrap.test.cjs.
test('the generated loader preserves legacy messaging alongside real bridge request settlement', async t => {
  if (!process.env.BALANCY_JSDOM_PATH) return t.skip('Set BALANCY_JSDOM_PATH to the jsdom module path');
  const { JSDOM, VirtualConsole } = require(process.env.BALANCY_JSDOM_PATH);
  const dom = new JSDOM('<html data-balancy-shell><head></head><body></body></html>', {
    pretendToBeVisual: true, runScripts: 'outside-only', url: 'https://example.test/', virtualConsole: new VirtualConsole()
  });
  t.after(() => dom.window.close());
  const w = dom.window, outgoing = [], errors = [], legacyMessages = [];
  w.HTMLCanvasElement.prototype.getContext = () => ({ fillRect() {}, getImageData: () => ({ data: [] }) });
  w.console.error = error => errors.push(String(error));
  w.BalancyWebView = { sendMessageToUnity: message => outgoing.push(message) };
  const java = fs.readFileSync(path.join(root, 'WebView/WVAndroidLib.androidlib/AndroidProject/app/src/main/java/com/balancy/webview/BalancyWebViewPlugin.java'), 'utf8');
  const shim = /void injectBalancyBridge\(\) \{([\s\S]*?)webView\.evaluateJavascript\(bridge, null\);/.exec(java);
  assert.ok(shim);
  w.eval([...shim[1].matchAll(/"((?:[^"\\]|\\.)*)"/g)].map(m => JSON.parse('"' + m[1].replace(/\\'/g, "'") + '"')).join(''));
  const oldApi = w.balancy, owner = { instanceId: 'legacy-offer', value: 'Привет 🎁' };
  oldApi.owner = owner;
  oldApi.onMessage = message => legacyMessages.push(message);
  const requests = [];
  w.XMLHttpRequest = function() { requests.push(this); this.open = () => {}; this.send = () => {}; };
  const loader = fs.readFileSync(path.join(root, 'Tests/WebView/native/classic-bootstrap.js.txt'), 'utf8');
  w.eval(loader);
  const script = w.document.querySelector('script[src="__BRIDGE_URL__"]');
  assert.ok(script);
  w.eval(fs.readFileSync(path.join(root, 'WebView/Resources/balancy-webview-bridge.txt'), 'utf8'));
  script.onload();
  assert.equal(requests.length, 1);
  Object.assign(requests[0], { status: 0, responseText: 'window.installedOwner = balancy.owner;' }).onload();
  await w.balancy._ready;
  assert.equal(w.installedOwner, owner);
  assert.equal(w.balancy.owner, oldApi.owner);
  w.balancy.sendMessageToUnity('legacy-outbound');
  assert.ok(outgoing.includes('legacy-outbound'));

  // The real bridge must still resolve a correlated request, while the old onMessage sees that response once.
  const pending = w.balancy.sendCustomMessage('legacy-test', { value: 42 });
  const poll = setInterval(() => {
    for (let i = outgoing.length - 1; i >= 0; i--) {
      let message;
      try { message = JSON.parse(outgoing[i]); } catch (_) { continue; }
      const request = message.type === 'batch' ? message.requests.find(r => r.action === 1000) : message.action === 1000 ? message : null;
      if (!request) continue;
      clearInterval(poll);
      w.balancy._receiveMessageFromUnity(JSON.stringify({ type: 'response', id: request.id, result: 'modern-ok' }));
      return;
    }
  }, 5);
  t.after(() => clearInterval(poll));
  let timeout;
  const limit = new Promise((_, reject) => { timeout = setTimeout(() => reject(new Error('Modern request was not settled')), 1500); });
  t.after(() => clearTimeout(timeout));
  assert.equal(await Promise.race([pending, limit]), 'modern-ok');
  assert.equal(legacyMessages.length, 1);
  oldApi.onMessage = () => { throw new Error('legacy handler failed'); };
  w.BalancyWebView.sendMessageToUnity = message => {
    const envelope = JSON.parse(message), requests = envelope.type === 'batch' ? envelope.requests : [envelope];
    for (const request of requests) if (request.action === 1000)
      queueMicrotask(() => w.balancy._receiveMessageFromUnity(JSON.stringify({ type: 'response', id: request.id, result: 'still-modern-ok' })));
  };
  assert.equal(await Promise.race([w.balancy.sendCustomMessage('legacy-test', { value: 43 }), limit]), 'still-modern-ok');
  assert.match(errors.at(-1), /legacy handler failed/);
});
