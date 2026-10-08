const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.resolve(__dirname, '../../Plugins/WebGL/BalancyXsollaStorage.jslib'), 'utf8')
 .replace(/\{\{\{ makeDynCall\('viii', 'callback'\) \}\}\}/g, 'invokeCallback');
let library; let callbacks=[]; let opens=0; let fail=false;
const previous=()=>null;
const context={LibraryManager:{library:{}},mergeInto:(_,value)=>library=value,Module:{},UTF8ToString:x=>x,
 window:{__balancyOpenPaymentWindow:previous,open:()=>{opens++;return {opener:{}};}},
 FS:{syncfs:(populate,done)=>{assert.equal(populate,false);done(fail?new Error('storage fault'):null);}},
 invokeCallback:(id,ok,json)=>callbacks.push({id,ok,json})};
vm.runInNewContext(source, context);
library.balancyXsollaInstallPopupOpener('a');
assert.equal(context.window.__balancyOpenPaymentWindow('wrong'),null);assert.equal(opens,0);
assert.equal(context.window.__balancyOpenPaymentWindow('a').opener,null);assert.equal(opens,1);
library.balancyXsollaRemovePopupOpener('wrong');assert.notEqual(context.window.__balancyOpenPaymentWindow,previous);
library.balancyXsollaRemovePopupOpener('a');assert.equal(context.window.__balancyOpenPaymentWindow,previous);
library.balancyXsollaFlushStorage(7,1);assert.deepEqual(callbacks.pop(),{id:7,ok:1,json:0});
fail=true;library.balancyXsollaFlushStorage(8,1);assert.deepEqual(callbacks.pop(),{id:8,ok:0,json:0});
context.FS.syncfs=()=>{throw Error('sync storage fault');};
library.balancyXsollaFlushStorage(9,1);assert.deepEqual(callbacks.pop(),{id:9,ok:0,json:0});
console.log('PASS Unity WebGL gesture popup ownership and storage success/asynchronous/synchronous fault barriers');
