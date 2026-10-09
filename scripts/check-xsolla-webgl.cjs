// Execute the shipped popup jslib with deterministic Window fixtures; no provider or browser IO.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const source=fs.readFileSync(path.join(__dirname,'../Plugins/WebGL/BalancyXsollaStorage.jslib'),'utf8');
const library={},popups=[],previous=()=>null;
const window={__balancyOpenPaymentWindow:previous,open:()=>{const popup={closed:false,location:{href:'about:blank'},close(){this.closed=true;}};popups.push(popup);return popup;}};
const Module={};vm.runInNewContext(source.slice(0,source.indexOf('  balancyXsollaFlushStorage__deps:'))+'});',{
 LibraryManager:{library},mergeInto:(target,methods)=>Object.assign(target,methods),UTF8ToString:x=>x,Module,window
});
let scenarios=0;const check=(condition)=>{assert(condition);scenarios++;};
library.balancyXsollaInstallPopupOpener('first');const old=window.__balancyOpenPaymentWindow;
check(old('wrong')===null&&popups.length===0);
const blank=old('first');library.balancyXsollaRemovePopupOpener('wrong');check(!blank.closed);
library.balancyXsollaRemovePopupOpener('first');check(blank.closed&&window.__balancyOpenPaymentWindow===previous&&!Module.balancyXsollaPopup);
check(old('first')===null); // A removed iframe's saved opener cannot reserve another window.
library.balancyXsollaInstallPopupOpener('next');const retryA=window.__balancyOpenPaymentWindow('next'),retryB=window.__balancyOpenPaymentWindow('next');
check(retryA.closed&&!retryB.closed);
library.balancyXsollaCheckoutDispatched('wrong');library.balancyXsollaRemovePopupOpener('next');check(retryB.closed);
library.balancyXsollaInstallPopupOpener('paid');const paid=window.__balancyOpenPaymentWindow('paid');library.balancyXsollaCheckoutDispatched('paid');library.balancyXsollaRemovePopupOpener('paid');check(!paid.closed);
library.balancyXsollaInstallPopupOpener('navigated');const navigated=window.__balancyOpenPaymentWindow('navigated');navigated.location.href='https://sandbox-secure.xsolla.com/paystation4/?token=fixture';library.balancyXsollaRemovePopupOpener('navigated');check(!navigated.closed);
library.balancyXsollaInstallPopupOpener('cross-origin');const cross=window.__balancyOpenPaymentWindow('cross-origin');Object.defineProperty(cross,'location',{get(){throw Error('cross-origin');}});library.balancyXsollaRemovePopupOpener('cross-origin');check(!cross.closed);
console.log(`PASS ${scenarios} WebGL popup ownership checks; synchronous blank cleanup, retry replacement, stale scope, dispatched/provider windows retained.`);
