#if BALANCY_STANDALONE_XSOLLA_TESTS
// Standalone boundary tests run the shipped adapter; native/WebView boundaries are fixtures.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Balancy;
namespace AOT { public class MonoPInvokeCallbackAttribute : Attribute { public MonoPInvokeCallbackAttribute(Type t) {} } }
namespace UnityEngine {
 public enum RuntimePlatform { IPhonePlayer, Android, Other }
 public static class Application { public static RuntimePlatform platform; public static void OpenURL(string s) {} }
 public static class Debug { public static void LogException(Exception e) { } public static void LogWarning(string s) { } }
 public static class JsonUtility {
  static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
  public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
  public static string ToJson(object obj) => JsonSerializer.Serialize(obj, Options);
 }
}
namespace Balancy.Models.SmartObjects { public class Item { public StubLocalizedName Name; } public class StubLocalizedName { public string Value; } public class StoreItem { public IntPtr GetRawPointer() => IntPtr.Zero; } }
namespace Balancy {
 public class Actions {
 public static class Purchasing {
  public class HardProductInfo { public string LocalizedPriceString, IsoCurrencyCode; public float LocalizedPrice; }
  public static Action<string,Action<HardProductInfo>> GetInfo = (sku,callback)=>callback(new HardProductInfo());
  internal static Action<string,Action<HardProductInfo>> GetHardPurchaseInfoCallback()=>GetInfo;
 }
 public class BalancyProductInfo {
  public enum PurchaseType { StoreItem, Offer, OfferGroup, ShopSlot }
  public string ProductId, StoreItemUnnyId, OfferInstanceId, OfferUnnyId; public PurchaseType Type;
  public BalancyProductInfo(Models.SmartObjects.StoreItem _) {} public void ReportThePurchase(Core.PaymentInfo info) { Program.Reports++; Program.LastReportedProduct=this; Program.LastPayment=info; }
 } }
 public static class Constants { public enum BalancyPlatform { Undefined, IosAppStore, AndroidGooglePlay } }
 public class StubConfig { public Constants.BalancyPlatform BalancyPlatform; }
 public static class Controller { public static bool IsReadyToUse; public static StubConfig Config; public static event Action<bool,bool> OnDataUpdated; public static void NotifyProfileChanged()=>OnDataUpdated?.Invoke(false,true); public static void NotifyCmsChanged()=>OnDataUpdated?.Invoke(true,false); }
 public static class RenderViewsManager { public static IDisposable PresentPaymentChoice(Action ready,Action<string> failed) => null; public static void SendMessageToView(string msg) {} public static bool SendPaymentMessageToView(string msg) => false; }
 public static class CMS { public static T GetModelByUnnyId<T>(string id) where T:class => null; }
 public static class Profiles { public static StubProfile System = new(); }
 public class StubProfile { public StubProfile ShopsInfo => this; public StubProfile SmartInfo => this; public Models.SmartObjects.StoreItem FindShopSlot(string id) => null; public Models.SmartObjects.StoreItem FindOfferInfo(string id) => null; public Models.SmartObjects.StoreItem FindOfferGroupInfo(string id) => null; }
 public static class LibraryMethods { public static class API {
  public delegate void XsollaCallback(int id,bool success,string json);
  public static IntPtr balancyXsollaGetSessionIdentity() => IntPtr.Zero;
  public static IntPtr balancyXsollaPreparePurchase(int kind,IntPtr owner,IntPtr store) => IntPtr.Zero;
  public static IntPtr balancyXsollaPreparePurchaseWithCapability(int kind,IntPtr owner,IntPtr store,bool allowed) => IntPtr.Zero;
  public static void balancyXsollaRequest(string action,string json,int id,XsollaCallback callback) {}
  public static void balancyXsollaComplete(string orderId,int id,XsollaCallback callback) {}
 } }
}
namespace Balancy.Core { public class PaymentInfo { public string ProductId,OrderId,Currency,Receipt; public float Price,PriceUSD; } }
class Program {
 public static int Reports; public static Actions.BalancyProductInfo LastReportedProduct; public static Balancy.Core.PaymentInfo LastPayment; static int passed, callbackCount, nativeCount, leaseDisposed, ack, complete, opens, presentations;
 const string ValidAck="{\"acknowledged\":true,\"grantId\":\"grant\"}";
 static List<string> presentationStates = new();
 static XsollaPurchasing.PaymentPresentation shownPresentation;
 static string requestId, session, unavailableChoice, lastError; static Action<string> presentationFailed; static bool success; static List<string> actions = new();
 class Lease:IDisposable { readonly Action closed; public Lease(Action closed=null){this.closed=closed;} public void Dispose(){leaseDisposed++;closed?.Invoke();} }
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
 static void Reset() {
  XsollaPurchasing.Stop(); LastReportedProduct=null; LastPayment=null; Controller.IsReadyToUse=false; requestId = null; unavailableChoice=null; lastError=null; session="player-a"; Reports=callbackCount=nativeCount=leaseDisposed=ack=complete=opens=presentations=0; shownPresentation=null; success=false; actions.Clear(); presentationStates.Clear();
  Actions.Purchasing.GetInfo=(sku,callback)=>callback(new Actions.Purchasing.HardProductInfo());
  XsollaPurchasing.SessionIdentity=()=>session;
  XsollaPurchasing.Prepare=_=>"{\"eligible\":true,\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"baseProductId\":\"sku\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"sku\",\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"doc\"}}";
  XsollaPurchasing.Present=(ready,failed)=>{ presentations++; presentationFailed=failed; ready(); return new Lease(); };
  XsollaPurchasing.Send=json=> { var msg=UnityEngine.JsonUtility.FromJson<XsollaPurchasing.PresentationMessage>(json); if(msg.type=="paymentChoiceUpdate")presentationStates.Add(msg.state); if(msg.type=="paymentChoiceShow") { requestId=msg.requestId; unavailableChoice=msg.xsollaUnavailable; shownPresentation=msg.presentation; XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceReady\",\"requestId\":\""+requestId+"\"}"); } };
  XsollaPurchasing.SendPayment=json=>{XsollaPurchasing.Send(json);return true;};
  XsollaPurchasing.OpenCheckout=_=>opens++;
  XsollaPurchasing.Persist=()=>Task.CompletedTask;
  XsollaPurchasing.UtcNow=()=>DateTimeOffset.UtcNow;
  XsollaPurchasing.RemovePopup=_=>{};XsollaPurchasing.CheckoutDispatched=_=>{};
  XsollaPurchasing.BrowserPopup=false; XsollaPurchasing.BonusName=id=>id;
  XsollaPurchasing.Request=(action,body)=> {
   using(var envelope=JsonDocument.Parse(body)) {
    var allowed=(action=="checkout"||action=="quote")?new[]{"requestId","country","language"}:action=="get"?new[]{"orderId"}:action=="ack"?new[]{"orderId","grantId"}:action=="discard"?new[]{"requestId"}:new[]{"before"};
    foreach(var property in envelope.RootElement.EnumerateObject()) if(Array.IndexOf(allowed,property.Name)<0)throw new Exception("forbidden route field "+property.Name);
    if(action=="checkout" && envelope.RootElement.EnumerateObject().Count()!=3)throw new Exception("checkout did not use core-owned reservation");
    if(action=="quote" && (envelope.RootElement.EnumerateObject().Count()!=3 || envelope.RootElement.GetProperty("country").GetString()!="US" || envelope.RootElement.GetProperty("language").GetString()!="en"))throw new Exception("quote did not freeze configured country and language");
   }
   actions.Add(action);
   if(action=="quote")return Task.FromResult("{\"quote\":{\"quoteId\":\"00000000-0000-4000-8000-000000000099\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"productId\":\"sku\",\"requestedProductId\":\"sku\",\"priceUsd\":\"4.99\",\"currency\":\"USD\",\"expiresAt\":\"2099-01-01T00:00:00Z\",\"bonus\":[]}}");
   if(action=="ack") { ack++; return Task.FromResult(ValidAck); }
   return Task.FromResult(action=="checkout" ? "{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"pending\",\"checkoutUrl\":\"https://sandbox-secure.xsolla.com/paystation4/?token=test\"}}" : "{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"}}");
  };
  XsollaPurchasing.Complete=id=>{ complete++; return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"},\"grant\":{\"grantId\":\"grant\"},\"applied\":true,\"requiresPersistence\":true}"); };
  API.Xsolla.Configure(new XsollaOptions {Enabled=true,Country="US",CanOfferAlternativePayment=()=>true});
 }
 static void Start(string sku=null)=>XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null){ProductId=sku},(ok,error)=>{callbackCount++;success=ok;lastError=error;},()=>nativeCount++);
 static void Choice(string choice,string id=null)=>XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceSelected\",\"requestId\":\""+(id??requestId)+"\",\"choice\":\""+choice+"\"}");
 static async Task Wait()=> await Task.Delay(20);
 static XsollaPurchasing.Order FixtureOrder(string id,string state="paid",bool reconciled=false,bool acknowledged=false) => new() {
  id=id,state=state,requestId="request-"+id,productId="sku-"+id,requestedProductId="sku-"+id,
  reconciliationRequired=reconciled,acknowledged=acknowledged,context=new XsollaPurchasing.Context {type="StoreItem",storeItemUnnyId="doc-"+id}
 };
 static string CompletionFixture(XsollaPurchasing.Order order,bool applied=true)=>UnityEngine.JsonUtility.ToJson(new XsollaPurchasing.Result {
  order=order,grant=new XsollaPurchasing.Grant {grantId="grant-"+order.id,orderId=order.id,baseProductId=order.productId},applied=applied,requiresPersistence=true
 });
 static string PendingFixture(params XsollaPurchasing.Order[] orders)=>UnityEngine.JsonUtility.ToJson(new XsollaPurchasing.Result {orders=orders});
 static string OrderFixture(XsollaPurchasing.Order order)=>UnityEngine.JsonUtility.ToJson(new XsollaPurchasing.Result {order=order});
 static async Task RecoverAllowErrors() {try{await API.Xsolla.RecoverPurchases();}catch(InvalidOperationException){} }
 static XsollaPurchasing.Prepared DefaultPrepared()=>new() {eligible=true,productId="sku",baseProductId="sku",requestId="00000000-0000-4000-8000-000000000001",ruleId="rule",configVersion="v1",context=new XsollaPurchasing.Context{type="StoreItem",storeItemUnnyId="doc"}};
 static XsollaPurchasing.PriceQuote DefaultPriceQuote()=>new() {quoteId="00000000-0000-4000-8000-000000000099",requestId="00000000-0000-4000-8000-000000000001",productId="sku",requestedProductId="sku",ruleId="rule",configVersion="v1",priceUsd="4.99",currency="USD",expiresAt="2099-01-01T00:00:00Z",bonus=Array.Empty<XsollaPurchasing.BonusEntry>()};
 static string PriceQuoteFixture(XsollaPurchasing.PriceQuote quote)=>UnityEngine.JsonUtility.ToJson(new XsollaPurchasing.Result{quote=quote});
 public static async Task<string> RunSecondInstanceScenario() {
  Reset();bool accepted=false;
  XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(accepted?PendingFixture():PendingFixture(FixtureOrder("replica"))):Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-replica\"}");
  XsollaPurchasing.Complete=id=>Task.FromResult(CompletionFixture(FixtureOrder(id),false));
  await API.Xsolla.RecoverPurchases();accepted=true;await API.Xsolla.RecoverPurchases();
  if(Reports!=1||LastPayment?.OrderId!="replica")throw new Exception("second SDK instance report contract");
  XsollaPurchasing.Stop();return Reports+"|"+LastPayment.OrderId;
 }
 static async Task Main() {
  Reset(); API.Xsolla.Configure(null); Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null),"disabled preserves native flow");
  foreach(var policy in new[]{"missing","false","throwing"}) {
   Reset();int preparationCalls=0; XsollaPurchasing.Prepare=_=>{preparationCalls++;throw new Exception("must not prepare");};
   API.Xsolla.Configure(new XsollaOptions {Enabled=true,Country="US",CanOfferAlternativePayment=policy=="missing"?null:()=>policy=="throwing"?throw new Exception("capability failed"):false});
   Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null)&&preparationCalls==0&&actions.Count==0&&presentations==0,"host "+policy+" never authorizes by country alone or starts alternate IO");
  }
  foreach(var gate in new[]{"no_rule","unsupported_provider","product_excluded","include_empty","below_minimum","unknown_bonus"}) {
   Reset();XsollaPurchasing.Prepare=_=>"{\"eligible\":false,\"reason\":\""+gate+"\"}";
   Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null)&&actions.Count==0&&presentations==0,"shared core "+gate+" routes directly to standard provider without popup");
  }
  Reset();bool activeRule=false;int currentRuleReads=0;var eligiblePrepare=XsollaPurchasing.Prepare;
  XsollaPurchasing.Prepare=p=>{currentRuleReads++;return activeRule?eligiblePrepare(p):"{\"eligible\":false,\"reason\":\"no_rule\"}";};
  Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null),"initial absent rule needs no activation event");
  activeRule=true;Start();Check(presentations==1&&shownPresentation.price.amount=="4.99","initially active current rule prepares authoritative price before showing selector");Choice("cancel");
  activeRule=false;Controller.NotifyCmsChanged();Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null)&&currentRuleReads==3&&presentations==1,"CMS reload and removal of last current document never keep old rule");
  session="player-b";Controller.NotifyProfileChanged();Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null)&&currentRuleReads==4,"new profile reevaluates shared current rule instead of old scope state");
  Reset();var presentationRequest=XsollaPurchasing.Request;var quoted=DefaultPriceQuote();quoted.bonus=new[]{new XsollaPurchasing.BonusEntry{itemUnnyId="booster",count=1}};
  XsollaPurchasing.BonusName=id=>"Localized booster";XsollaPurchasing.Request=(a,b)=>a=="quote"?Task.FromResult(PriceQuoteFixture(quoted)):presentationRequest(a,b);
  Start();Check(shownPresentation.price.amount=="4.99"&&shownPresentation.price.currency=="USD"&&shownPresentation.bonus.Single().count==1&&shownPresentation.bonus[0].name=="Localized booster"&&Reports==0,"UI displays verified quote price and localized quoted count without applying rewards");Choice("cancel");
  foreach(var invalidQuote in new[]{"request","requested","base","rule","version","price","currency","bonus-count"}) {
   Reset();var validRequest=XsollaPurchasing.Request;var bad=DefaultPriceQuote();
   if(invalidQuote=="request")bad.requestId="wrong";if(invalidQuote=="requested")bad.requestedProductId="wrong";if(invalidQuote=="base")bad.productId="wrong";if(invalidQuote=="rule")bad.ruleId="wrong";if(invalidQuote=="version")bad.configVersion="wrong";if(invalidQuote=="price")bad.priceUsd="NaN";if(invalidQuote=="currency")bad.currency="EUR";if(invalidQuote=="bonus-count")bad.bonus=new[]{new XsollaPurchasing.BonusEntry{itemUnnyId="booster",count=0}};
   XsollaPurchasing.Request=(a,b)=>a=="quote"?Task.FromResult(PriceQuoteFixture(bad)):validRequest(a,b);Start();
   Check(!string.IsNullOrEmpty(unavailableChoice)&&shownPresentation==null&&Reports==0,"invalid quote "+invalidQuote+" offers native-only selector without promising bonus");Choice("xsolla");Check(complete==0&&!actions.Contains("checkout"),"invalid quote "+invalidQuote+" cannot submit alternative checkout");Choice("native");Check(nativeCount==1&&callbackCount==0,"invalid quote "+invalidQuote+" preserves explicit normal provider callback");
  }
  Reset();var quoteWait=new TaskCompletionSource<string>();var delayedQuoteRequest=XsollaPurchasing.Request;
  XsollaPurchasing.Request=(a,b)=>a=="quote"?quoteWait.Task:delayedQuoteRequest(a,b);Start();
  XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceReady\",\"requestId\":\"00000000-0000-4000-8000-000000000001\"}");Choice("xsolla","00000000-0000-4000-8000-000000000001");
  Check(presentations==0&&!actions.Contains("checkout")&&Reports==0,"pending quote cannot show bonus or accept pre-presentation selection");session="player-b";quoteWait.SetResult(PriceQuoteFixture(DefaultPriceQuote()));await Wait();Check(presentations==0&&callbackCount==1&&!success&&Reports==0,"late quote from replaced profile never opens selector or submits checkout");
  foreach(var expirationPoint in new[]{"chooser","after-flush"}) {
   Reset();var fixedNow=DateTimeOffset.Parse("2098-12-31T23:59:59Z");XsollaPurchasing.UtcNow=()=>fixedNow;
   bool popupClosedBeforeLease=false,popupClosedWhenDisposed=false;XsollaPurchasing.RemovePopup=_=>{popupClosedBeforeLease=true;};
   XsollaPurchasing.Present=(ready,failed)=>{presentations++;presentationFailed=failed;ready();return new Lease(()=>{popupClosedWhenDisposed=popupClosedBeforeLease;failed("host closed during cleanup");});};
   int flush=0;XsollaPurchasing.Persist=()=>{flush++;return Task.CompletedTask;};
   Start();var flushesBeforeExpiry=flush;
   if(expirationPoint=="chooser")fixedNow=DateTimeOffset.Parse("2099-01-01T00:00:00Z");
   else XsollaPurchasing.Persist=()=>{flush++;fixedNow=DateTimeOffset.Parse("2099-01-01T00:00:00Z");return Task.CompletedTask;};
   Choice("xsolla");await Wait();
   Check(!actions.Contains("checkout")&&opens==0&&complete==0&&Reports==0&&nativeCount==0,"expired quote "+expirationPoint+" never submits or silently buys native");
   Check(actions.Count(a=>a=="discard")==1&&flush>flushesBeforeExpiry&&leaseDisposed==1&&popupClosedWhenDisposed&&callbackCount==1&&!success&&lastError=="Offer expired; start the purchase again","expired quote "+expirationPoint+" discards once, flushes, releases and reports clear failure");
  }
  Reset();XsollaPurchasing.BrowserPopup=true;int popupDispatches=0;XsollaPurchasing.CheckoutDispatched=_=>popupDispatches++;
  Start();XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceSelected\",\"requestId\":\""+requestId+"\",\"choice\":\"xsolla\",\"popupOpened\":true}");await Wait();
  Check(success&&popupDispatches==1,"verified checkout URL relinquishes WebGL blank-popup ownership before completion closes presentation");Choice("cancel");
  Reset();XsollaPurchasing.BrowserPopup=true;int failedDispatches=0;bool failedPopupClosed=false;var realSend=XsollaPurchasing.Send;
  XsollaPurchasing.CheckoutDispatched=_=>failedDispatches++;XsollaPurchasing.RemovePopup=_=>failedPopupClosed=true;
  XsollaPurchasing.Send=json=>{var message=UnityEngine.JsonUtility.FromJson<XsollaPurchasing.PresentationMessage>(json);if(message.checkoutUrl!=null)throw new Exception("iframe transport failed");realSend(json);};
  Start();XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceSelected\",\"requestId\":\""+requestId+"\",\"choice\":\"xsolla\",\"popupOpened\":true}");await Wait();
  Check(failedDispatches==0&&complete==0&&!success,"failed checkout URL transport retains blank popup ownership");Choice("cancel");
  Check(failedPopupClosed&&leaseDisposed==1,"cleanup synchronously closes unused popup after failed checkout URL transport");
  Reset();XsollaPurchasing.BrowserPopup=true;int falseDispatches=0;bool falsePopupClosed=false;
  XsollaPurchasing.CheckoutDispatched=_=>falseDispatches++;XsollaPurchasing.RemovePopup=_=>falsePopupClosed=true;
  XsollaPurchasing.SendPayment=_=>false;
  Start();XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceSelected\",\"requestId\":\""+requestId+"\",\"choice\":\"xsolla\",\"popupOpened\":true}");await Wait();
  Check(falseDispatches==0&&complete==0&&!success,"false checkout URL delivery retains blank popup ownership");Choice("cancel");
  Check(falsePopupClosed&&leaseDisposed==1,"false delivery cleanup closes unused popup synchronously");
  foreach(var prepareFault in new[]{"checkpoint","auth","invalid-contract"}) {
   Reset(); XsollaPurchasing.Prepare=_=>prepareFault=="invalid-contract"?"{}":throw new InvalidOperationException("xsolla_"+prepareFault+"_unavailable");
   Start(); Check(Guid.TryParse(requestId,out _)&&!string.IsNullOrEmpty(unavailableChoice)&&callbackCount==0&&actions.Count==0,"prepare "+prepareFault+" keeps native selector available without checkout IO");
   Choice("xsolla"); XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\"}"); await Wait();
   Check(complete==0&&opens==0&&actions.Count==0&&callbackCount==0,"prepare "+prepareFault+" rejects synthetic alternative selection");
   Choice("native"); Check(nativeCount==1&&callbackCount==0&&actions.Count==0&&leaseDisposed==1,"prepare "+prepareFault+" preserves explicit native callback without automatic fallback");
  }
  Reset(); XsollaPurchasing.Prepare=_=>throw new Exception("unavailable");Start();Choice("cancel");Check(callbackCount==1&&!success&&nativeCount==0&&actions.Count==0,"native-only selector cancellation grants nothing");
  foreach(var missingExport in new[]{"prepare-and-identity","identity-only"}) {
   Reset(); int identityCalls=0;
   if(missingExport=="prepare-and-identity")XsollaPurchasing.Prepare=_=>throw new EntryPointNotFoundException("balancyXsollaPreparePurchase");
   XsollaPurchasing.SessionIdentity=()=>{identityCalls++;throw new EntryPointNotFoundException("balancyXsollaGetSessionIdentity");};
   Start(); int capturedCalls=identityCalls;
   Check(Guid.TryParse(requestId,out _)&&!string.IsNullOrEmpty(unavailableChoice)&&callbackCount==0&&actions.Count==0,"missing "+missingExport+" still presents native-only selector");
   Choice("xsolla");Choice("native");
   Check(nativeCount==1&&callbackCount==0&&identityCalls==capturedCalls&&actions.Count==0,"missing "+missingExport+" native selection never calls missing identity export");
   Start();capturedCalls=identityCalls;Choice("cancel");
   Check(nativeCount==1&&callbackCount==1&&!success&&identityCalls==capturedCalls&&actions.Count==0,"missing "+missingExport+" cancellation never calls missing identity export");
   Start();string staleId=requestId;Controller.NotifyProfileChanged();Choice("native",staleId);
   Check(nativeCount==1&&callbackCount==2&&identityCalls==capturedCalls&&actions.Count==0,"missing "+missingExport+" profile lifecycle rejects stale native choice");
   Start();string freshId=requestId;XsollaPurchasing.Stop();Choice("native",freshId);
   Check(nativeCount==1&&callbackCount==3&&identityCalls==capturedCalls&&actions.Count==0,"missing "+missingExport+" SDK stop rejects stale native choice");
  }
  Reset(); Start(); Choice("native"); Check(nativeCount==1&&callbackCount==0&&leaseDisposed==1,"native choice preserves registered developer callback"); Choice("native"); Check(nativeCount==1,"duplicate native choice ignored");
  Reset(); Start(); Choice("xsolla","unrelated"); Check(opens==0,"foreign presentation message ignored"); Choice("cancel"); Check(callbackCount==1&&!success&&complete==0&&leaseDisposed==1,"cancel before payment grants nothing");
  Reset(); Start(); Choice("xsolla"); await Wait(); Check(success&&callbackCount==1&&opens==1&&complete==1&&ack==1&&Reports==1,"paid flow grants reports persists and acknowledges");
  Check(leaseDisposed==0,"success stays visible until Done");
  Choice("xsolla"); XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\"}");
  Check(complete==1&&callbackCount==1,"success cannot retry or charge again");
  Choice("cancel"); Check(leaseDisposed==1&&callbackCount==1&&success,"Done releases presentation without second callback");
  Reset(); Start(); var deferredCheckout=new TaskCompletionSource<string>(); var originalRequest=XsollaPurchasing.Request;
  XsollaPurchasing.Request=(a,b)=> a=="checkout"?deferredCheckout.Task:originalRequest(a,b);
  Choice("xsolla"); presentationFailed("renderer closed");
  deferredCheckout.SetResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"}}");
  await Wait(); Check(success&&callbackCount==1&&complete==1&&ack==1&&leaseDisposed==1,"external renderer closure preserves paid tracking and callback");
  Reset(); Start(); var wait=new TaskCompletionSource<string>(); XsollaPurchasing.Request=(action,body)=>{ actions.Add(action); return wait.Task; }; Choice("xsolla"); Choice("xsolla"); Check(actions.Count(a=>a=="checkout")==1,"duplicate Xsolla click creates once"); Choice("cancel"); wait.SetResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"}}"); await Wait(); Check(!success&&complete==0&&nativeCount==0,"dismiss pending never falls back or applies late response");
  Reset(); Start(); XsollaPurchasing.Request=(a,b)=>{session="player-b";return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"}}");}; Choice("xsolla"); await Wait(); Check(complete==0&&ack==0&&Reports==0,"session change prevents completion and callbacks");
  Reset(); Start(); int flushCount=0; XsollaPurchasing.Persist=()=> ++flushCount==1?Task.CompletedTask:Task.FromException(new Exception("disk failure")); Choice("xsolla"); await Wait(); Check(complete==1&&ack==0&&Reports==0&&!success,"storage failure never acknowledges"); Choice("cancel");
  Reset(); XsollaPurchasing.Request=(a,b)=>{ if(a=="pending")return Task.FromResult("{\"orders\":[{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\",\"context\":{\"type\":\"StoreItem\"}}]}"); ack++;return Task.FromResult(ValidAck);}; XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"},\"grant\":{\"grantId\":\"grant\"},\"applied\":false}");}; await API.Xsolla.RecoverPurchases(); Check(complete==1&&ack==1&&Reports==1,"authenticated unacknowledged duplicate reports after verified retry and ack");
  foreach(var url in new[]{"https://sandbox-secure.xsolla.com/paystation4/?token=fixture","https://secure.xsolla.com/paystation4/?token=fixture"}) { XsollaPurchasing.ValidateCheckoutUrl(url); Check(true,"accept official checkout URL "+url); }
  Reset(); Start(); var sandboxRequest=XsollaPurchasing.Request; XsollaPurchasing.Request=async(a,b)=>(await sandboxRequest(a,b)).Replace("sandbox-secure.xsolla.com","secure.xsolla.com"); Choice("xsolla"); await Wait(); Check(success&&callbackCount==1&&opens==1&&ack==1,"server production checkout host follows same verified flow"); Choice("cancel");
  foreach(var url in new[]{"http://sandbox-secure.xsolla.com/paystation4/?token=a","http://secure.xsolla.com/paystation4/?token=a","https://evil.com/paystation4/?token=a","https://secure.xsolla.com/paystation4/","https://x@sandbox-secure.xsolla.com/paystation4/?token=a","https://sandbox-secure.xsolla.com:8443/paystation4/?token=a","https://secure.xsolla.com:8443/paystation4/?token=a","https://secure.xsolla.com.evil.com/paystation4/?token=a","https://sandbox-secure.xsolla.com.evil.com/paystation4/?token=a","https://secure-xsolla.com/paystation4/?token=a","https://secure.xsolla.com/wrong-path/?token=a","https://secure.xsolla.com/paystation4/?token=a#fragment"}) { bool blocked=false;try{XsollaPurchasing.ValidateCheckoutUrl(url);}catch{blocked=true;} Check(blocked,"reject unsafe URL "+url); }
  Reset(); Start(); XsollaPurchasing.Request=(a,b)=>Task.FromResult("{\"order\":{\"id\":\"foreign-owned\",\"state\":\"paid\",\"requestId\":\"00000000-0000-4000-8000-000000000002\"}}"); Choice("xsolla"); await Wait(); Check(complete==0&&ack==0&&Reports==0&&!success,"foreign owned request cannot finish active purchase"); Choice("cancel");
  Reset(); Start(); int attempts=0; XsollaPurchasing.Persist=()=> ++attempts==2?Task.FromException(new Exception("temporary disk failure")):Task.CompletedTask;
  XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\"},\"grant\":{\"grantId\":\"grant\"},\"applied\":"+(complete==1?"true":"false")+",\"requiresPersistence\":true}");};
  Choice("xsolla"); await Wait(); Check(Reports==0&&ack==0,"live storage fault keeps callback unreported");
  XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\"}"); await Wait(); Check(success&&Reports==1&&complete==2&&ack==1,"live retry reports once after durable duplicate completion");
  foreach(var ackFault in new[]{"network","409-refund","malformed","empty","false","string-true","numeric-true","wrong-grant"}) {
   Reset(); Start(); var underlyingRequest=XsollaPurchasing.Request; var underlyingComplete=XsollaPurchasing.Complete; int grantApplications=0;
   XsollaPurchasing.Complete=async id=>{var result=await underlyingComplete(id);if(complete==1)grantApplications++;return complete==1?result:result.Replace("\"applied\":true","\"applied\":false");};
   XsollaPurchasing.Request=(a,b)=> {
    if(a!="ack")return underlyingRequest(a,b);
    ack++;
    return ackFault=="network"?Task.FromException<string>(new Exception("network unreachable")):
      ackFault=="409-refund"?Task.FromException<string>(new InvalidOperationException("409 xsolla_reconciliation_required")):
      Task.FromResult(ackFault=="malformed"?"not-json":ackFault=="empty"?"{}":ackFault=="string-true"?"{\"acknowledged\":\"true\",\"grantId\":\"grant\"}":ackFault=="numeric-true"?"{\"acknowledged\":1,\"grantId\":\"grant\"}":ackFault=="false"?"{\"acknowledged\":false,\"grantId\":\"grant\"}":"{\"acknowledged\":true,\"grantId\":\"other-grant\"}");
   };
   Choice("xsolla"); await Wait();
   Check(complete==1&&ack==1&&grantApplications==1&&Reports==0&&callbackCount==0&&!success&&presentationStates.Last()=="error"&&!presentationStates.Contains("success"),"ack "+ackFault+" cannot report definitive success or roll back applied ledger");
   XsollaPurchasing.Request=underlyingRequest;
   XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\"}"); await Wait();
   Check(complete==2&&ack==2&&grantApplications==1&&Reports==1&&callbackCount==1&&success&&opens==1&&presentationStates.Last()=="success"&&actions.Count(a=>a=="checkout")==1,"ack "+ackFault+" retry acknowledges same paid grant and reports once");
   Choice("cancel"); Check(callbackCount==1&&Reports==1,"ack "+ackFault+" Done cannot repeat callbacks");
  }
  Reset(); Start(); var pendingAck=new TaskCompletionSource<string>(); var ackRequest=XsollaPurchasing.Request;
  XsollaPurchasing.Request=(a,b)=>a=="ack"?pendingAck.Task:ackRequest(a,b);
  Choice("xsolla"); await Wait(); Check(complete==1&&Reports==0&&callbackCount==0&&!presentationStates.Contains("success"),"deferred ack cannot expose success before server acceptance");
  session="player-b"; pendingAck.SetResult(ValidAck); await Wait();
  Check(Reports==0&&!success&&!presentationStates.Contains("success"),"session change during acknowledgment cannot grant callbacks to another player");
  Reset(); XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult("{\"orders\":[{\"id\":\"order\",\"state\":\"paid\",\"context\":{\"type\":\"StoreItem\"}}]}"):Task.FromException<string>(new Exception("ack unavailable"));
  bool recoveryAckRejected=false;try{await API.Xsolla.RecoverPurchases();}catch(Exception){recoveryAckRejected=true;}
  Check(recoveryAckRejected&&complete==1&&Reports==0,"recovery propagates ack failure without reporting a newly applied grant");
  Reset(); var recoveryFixtureRequest=XsollaPurchasing.Request; var recoveryFixtureComplete=XsollaPurchasing.Complete;
  const string PendingPaid="{\"orders\":[{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"productId\":\"sku\",\"state\":\"paid\",\"context\":{\"type\":\"StoreItem\"}}]}";
  bool recoveryAcceptedAck=false;
  XsollaPurchasing.Request=(a,b)=>{
   if(a=="pending")return Task.FromResult(recoveryAcceptedAck?"{\"orders\":[]}":PendingPaid);
   var result=recoveryFixtureRequest(a,b);if(a=="ack")recoveryAcceptedAck=true;return result;
  };
  XsollaPurchasing.Complete=async id=>{var result=await recoveryFixtureComplete(id);return complete==1?result:result.Replace("\"applied\":true","\"applied\":false");};
  var withPendingRequest=XsollaPurchasing.Request;
  XsollaPurchasing.Request=(a,b)=>a=="ack"?Task.FromException<string>(new Exception("ack unavailable")):withPendingRequest(a,b);
  try{await API.Xsolla.RecoverPurchases();}catch(Exception){}
  XsollaPurchasing.Request=withPendingRequest; await API.Xsolla.RecoverPurchases();
  Check(complete==2&&Reports==1&&ack==1,"recovery retry reports deferred newly applied grant after validated ack");
  await API.Xsolla.RecoverPurchases(); Check(Reports==1,"repeated recovery cannot re-report acknowledged deferred grant");
  Reset(); var oldPending=new TaskCompletionSource<string>(); int scopedPendingReads=0;
  XsollaPurchasing.Request=(a,b)=>++scopedPendingReads==1?oldPending.Task:Task.FromResult("{\"orders\":[]}");
  var oldRecovery=API.Xsolla.RecoverPurchases(); session="player-b";
  await API.Xsolla.RecoverPurchases(); Check(scopedPendingReads==2,"old recovery task cannot suppress new session recovery");
  oldPending.SetResult("{\"orders\":[]}"); bool oldSessionStopped=false;try{await oldRecovery;}catch(OperationCanceledException){oldSessionStopped=true;}
  Check(oldSessionStopped&&Reports==0,"stale recovery result is rejected after session change");
  Reset(); Start(); var racingRequest=XsollaPurchasing.Request; var racingComplete=XsollaPurchasing.Complete;
  var liveAck=new TaskCompletionSource<string>(); var recoveryAck=new TaskCompletionSource<string>();int concurrentAckCalls=0;bool concurrentAcceptedAck=false;
  XsollaPurchasing.Complete=async id=>{var result=await racingComplete(id);return complete==1?result:result.Replace("\"applied\":true","\"applied\":false");};
  XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(concurrentAcceptedAck?"{\"orders\":[]}":PendingPaid):a=="ack"?(++concurrentAckCalls==1?liveAck.Task:recoveryAck.Task):racingRequest(a,b);
  Choice("xsolla");await Wait();var racingRecovery=API.Xsolla.RecoverPurchases();await Wait();
  Check(complete==2&&concurrentAckCalls==2&&Reports==0&&callbackCount==0,"concurrent recovery and live finalization wait for acknowledgment");
  concurrentAcceptedAck=true;recoveryAck.SetResult(ValidAck);await racingRecovery;liveAck.SetResult(ValidAck);await Wait();
  Check(Reports==1&&callbackCount==1&&success,"concurrent recovery and live finalization report same grant once");Choice("cancel");
  Reset();Start();var lostAckRequest=XsollaPurchasing.Request;var lostAckComplete=XsollaPurchasing.Complete;
  bool serverAcceptedAck=false;int deferredReads=0;
  XsollaPurchasing.Request=(a,b)=>{
   if(a=="pending")return Task.FromResult("{\"orders\":[]}");
   if(a=="get"&&serverAcceptedAck)deferredReads++;
   if(a=="ack"&&!serverAcceptedAck){serverAcceptedAck=true;ack++;return Task.FromException<string>(new Exception("ACK accepted but response lost"));}
   return lostAckRequest(a,b);
  };
  XsollaPurchasing.Complete=async id=>{var result=await lostAckComplete(id);return complete==1?result:result.Replace("\"applied\":true","\"applied\":false");};
  Choice("xsolla");await Wait();Choice("cancel");
  Check(serverAcceptedAck&&Reports==0&&callbackCount==1&&!success,"accepted ACK with lost response leaves retired init callback pending rather than success");
  await API.Xsolla.RecoverPurchases();
  Check(deferredReads==1&&complete==2&&ack==2&&Reports==1&&callbackCount==1&&!success,"empty server pending still recovers accepted ACK through same-scope order readback without reviving init callback");
  await API.Xsolla.RecoverPurchases();Check(deferredReads==1&&complete==2&&Reports==1,"resolved accepted ACK is not read back or reported again");
  Reset();Start();var scopeLostRequest=XsollaPurchasing.Request;int oldScopeReads=0;
  XsollaPurchasing.Request=(a,b)=>a=="ack"?Task.FromException<string>(new Exception("lost ACK")):a=="pending"?Task.FromResult("{\"orders\":[]}"):scopeLostRequest(a,b);
  Choice("xsolla");await Wait();Choice("cancel");session="player-b";
  var beforeScopeRead=XsollaPurchasing.Request;XsollaPurchasing.Request=(a,b)=>{if(a=="get")oldScopeReads++;return beforeScopeRead(a,b);};
  await API.Xsolla.RecoverPurchases();Check(oldScopeReads==0&&Reports==0&&callbackCount==1,"new scope never reads back or reports previous scope deferred ACK");
  foreach(var terminal in new[]{"canceled","reconciliation"}) {
   Reset(); bool initial=true;int healthyAck=0,terminalGet=0;
   XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult(CompletionFixture(FixtureOrder(id),initial));};
   XsollaPurchasing.Request=(a,b)=>{
    string id=a=="pending"?null:JsonDocument.Parse(b).RootElement.GetProperty("orderId").GetString();
    if(a=="pending")return Task.FromResult(PendingFixture(FixtureOrder(initial?"terminal":"healthy")));
    if(a=="ack"){if(id=="terminal")return Task.FromException<string>(new Exception("lost ack"));healthyAck++;return Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-healthy\"}");}
    terminalGet++;return Task.FromResult(OrderFixture(FixtureOrder(id,terminal=="canceled"?"canceled":"paid",terminal=="reconciliation")));
   };
   await RecoverAllowErrors();initial=false;await RecoverAllowErrors();
   Check(healthyAck==1&&Reports==1&&terminalGet==1,"deferred "+terminal+" cannot starve independent paid pending");
  }
  Reset();int isolatedHealthyAck=0;
  XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(PendingFixture(FixtureOrder("broken"),FixtureOrder("healthy"))):
   Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-healthy\"}");
  XsollaPurchasing.Complete=id=>{if(id=="broken")return Task.FromException<string>(new Exception("checkpoint unavailable"));isolatedHealthyAck++;return Task.FromResult(CompletionFixture(FixtureOrder(id)));};
  await RecoverAllowErrors();Check(isolatedHealthyAck==1&&Reports==1,"one pending completion failure cannot block later independent paid order");
  Reset(); bool seed=true;var readIds=new List<string>();int afterLimitAck=0;
  var many=Enumerable.Range(0,70).Select(i=>FixtureOrder("terminal-"+i)).ToArray();
  XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult(CompletionFixture(FixtureOrder(id)));};
  XsollaPurchasing.Request=(a,b)=>{
   if(a=="pending")return Task.FromResult(PendingFixture(seed?many:new[]{FixtureOrder("healthy")}));
   string id=JsonDocument.Parse(b).RootElement.GetProperty("orderId").GetString();
   if(a=="ack"){if(seed)return Task.FromException<string>(new Exception("lost ack"));afterLimitAck++;return Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-healthy\"}");}
   readIds.Add(id);return Task.FromResult(OrderFixture(FixtureOrder(id,"canceled")));
  };
  await RecoverAllowErrors();Check(complete==70,"more than 64 deferred finalizations never block another verified completion");
  seed=false;await RecoverAllowErrors();Check(afterLimitAck==1&&Reports==1&&readIds.Count==64,"healthy pending succeeds before bounded 64 terminal readbacks");
  XsollaPurchasing.Request=(a,b)=>{if(a=="pending")return Task.FromResult(PendingFixture());string id=JsonDocument.Parse(b).RootElement.GetProperty("orderId").GetString();readIds.Add(id);return Task.FromResult(OrderFixture(FixtureOrder(id,"canceled")));};
  await RecoverAllowErrors();Check(readIds.Count==128&&readIds.Contains("terminal-69"),"deferred readback rotates fairly and retains entries beyond first batch");
  foreach(var mismatch in new[]{"request","requested-product","canonical-product","completion-request","completion-product"}) {
   Reset(); bool initial=true;int readbackComplete=0;
   XsollaPurchasing.Request=(a,b)=>{
    if(a=="pending")return Task.FromResult(initial?PendingFixture(FixtureOrder("known")):PendingFixture());
    if(a=="ack")return Task.FromException<string>(new Exception("lost ack"));
    var order=FixtureOrder("known");if(mismatch=="request")order.requestId="other";if(mismatch=="requested-product")order.requestedProductId="other";if(mismatch=="canonical-product")order.productId="other";
    return Task.FromResult(OrderFixture(order));
   };
   XsollaPurchasing.Complete=id=>{complete++;if(!initial)readbackComplete++;var order=FixtureOrder(id);if(!initial&&mismatch=="completion-request")order.requestId="other";if(!initial&&mismatch=="completion-product")order.productId="other";return Task.FromResult(CompletionFixture(order,initial));};
   await RecoverAllowErrors();initial=false;await RecoverAllowErrors();
   Check(Reports==0&&readbackComplete==(mismatch.StartsWith("completion-")?1:0),"lost ACK fallback rejects "+mismatch+" identity mismatch without definitive notification");
  }
  Reset();int checkpointAttempts=0;bool checkpointAccepted=false;
  XsollaPurchasing.Request=(a,b)=>{if(a=="pending")return Task.FromResult(checkpointAccepted?PendingFixture():PendingFixture(FixtureOrder("checkpoint")));ack++;checkpointAccepted=true;return Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-checkpoint\"}");};
  XsollaPurchasing.Complete=id=>++checkpointAttempts==1?Task.FromException<string>(new Exception("checkpoint failed after grant")):Task.FromResult(CompletionFixture(FixtureOrder(id),false));
  await RecoverAllowErrors();Check(Reports==0&&ack==0,"failed core checkpoint payload cannot authorize callback or ack");
  await API.Xsolla.RecoverPurchases();Check(Reports==1&&ack==1&&callbackCount==0,"verified unacknowledged applied-false retry repairs checkpoint callback gap");
  await API.Xsolla.RecoverPurchases();Check(Reports==1,"checkpoint recovery global notification remains at most once in same scope");
  Reset();XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(PendingFixture(FixtureOrder("history"))):Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-history\"}");
  XsollaPurchasing.Complete=id=>Task.FromResult(CompletionFixture(FixtureOrder(id,acknowledged:true),false));
  await API.Xsolla.RecoverPurchases();Check(Reports==0,"acknowledged historical completion never creates a new global report despite pending race");
  Reset(); var originalProduct=new Actions.BalancyProductInfo(null){ProductId="override-sku",StoreItemUnnyId="original-store"};
  var overrideOrder=FixtureOrder("override");overrideOrder.requestId="00000000-0000-4000-8000-000000000001";overrideOrder.productId="canonical-sku";overrideOrder.requestedProductId="override-sku";overrideOrder.context.storeItemUnnyId="original-store";
  bool overrideAckFail=true;
  XsollaPurchasing.Prepare=_=>UnityEngine.JsonUtility.ToJson(new XsollaPurchasing.Prepared {eligible=true,ruleId="rule",configVersion="v1",baseProductId="canonical-sku",requestId=overrideOrder.requestId,productId="override-sku",context=overrideOrder.context});
  XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult(CompletionFixture(overrideOrder,complete==1));};
  XsollaPurchasing.Request=(a,b)=>a=="quote"?Task.FromResult("{\"quote\":{\"quoteId\":\"00000000-0000-4000-8000-000000000099\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"productId\":\"canonical-sku\",\"requestedProductId\":\"override-sku\",\"priceUsd\":\"4.99\",\"currency\":\"USD\",\"expiresAt\":\"2099-01-01T00:00:00Z\",\"bonus\":[]}}"):a=="ack"?(overrideAckFail?Task.FromException<string>(new Exception("ack failed")):Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-override\"}")):a=="pending"?Task.FromResult(PendingFixture(overrideOrder)):Task.FromResult(OrderFixture(overrideOrder));
  XsollaPurchasing.TryIntercept(originalProduct,(ok,e)=>{callbackCount++;success=ok;},()=>nativeCount++);Choice("xsolla");await Wait();Choice("cancel");
  Check(complete==1&&Reports==0,"live override descriptor remains pending after verified apply and failed ack");
  overrideAckFail=false;await API.Xsolla.RecoverPurchases();
  Check(Reports==1&&ReferenceEquals(LastReportedProduct,originalProduct)&&LastReportedProduct.ProductId=="override-sku"&&LastReportedProduct.StoreItemUnnyId=="original-store"&&LastPayment.ProductId=="canonical-sku","recovery preserves original descriptor identity requested SKU and StoreItem while payment uses verified canonical SKU");
  Reset();bool listedSeed=true;var listedGets=new List<string>();
  XsollaPurchasing.Complete=id=>Task.FromResult(CompletionFixture(FixtureOrder(id)));
  XsollaPurchasing.Request=(a,b)=>{
   if(a=="pending")return Task.FromResult(listedSeed?PendingFixture(FixtureOrder("listed"),FixtureOrder("hidden")):PendingFixture(FixtureOrder("listed","canceled")));
   if(a=="ack")return Task.FromException<string>(new Exception("lost ack"));
   var id=JsonDocument.Parse(b).RootElement.GetProperty("orderId").GetString();listedGets.Add(id);return Task.FromResult(OrderFixture(FixtureOrder(id,"canceled")));
  };
  await RecoverAllowErrors();listedSeed=false;await RecoverAllowErrors();
  Check(listedGets.SequenceEqual(new[]{"hidden"}),"valid nonpaid pending IDs are visited and do not consume deferred GET budget");
  foreach(var listedTerminal in new[]{"canceled","reconciliation"}) {
   Reset();bool preciseSeed=true;int preciseGets=0,preciseHealthyAck=0;
   XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult(CompletionFixture(FixtureOrder(id)));};
   XsollaPurchasing.Request=(a,b)=>{
    if(a=="pending")return Task.FromResult(preciseSeed?PendingFixture(FixtureOrder("known")):PendingFixture(FixtureOrder("known",listedTerminal=="canceled"?"canceled":"paid",listedTerminal=="reconciliation"),FixtureOrder("healthy")));
    if(a=="get"){preciseGets++;return Task.FromException<string>(new Exception("unexpected GET"));}
    if(preciseSeed)return Task.FromException<string>(new Exception("seed ACK unavailable"));
    preciseHealthyAck++;return Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-healthy\"}");
   };
   await RecoverAllowErrors();preciseSeed=false;InvalidOperationException preciseError=null;
   try{await API.Xsolla.RecoverPurchases();}catch(InvalidOperationException error){preciseError=error;}
   var causes=(preciseError?.InnerException as AggregateException)?.Flatten().InnerExceptions;
   Check(preciseError?.Message=="xsolla_recovery_incomplete"&&causes?.Count==1&&causes[0].Message=="xsolla_reconciliation_required","single known listed "+listedTerminal+" reports exact reconciliation aggregate without other failures");
   var debt=(System.Collections.IDictionary)typeof(XsollaPurchasing).GetField("deferredOrders",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
   Check(preciseGets==0&&debt.Count==1&&debt.Contains("known"),"single known listed "+listedTerminal+" keeps debt and spends zero GETs");
   Check(preciseHealthyAck==1&&Reports==1&&LastPayment.OrderId=="healthy"&&complete==2,"healthy following listed "+listedTerminal+" still completes and reports independently");
  }
  Reset();bool replicaSeed=true,replicaAccepted=false;int replicaCompletions=0;
  XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(replicaSeed?PendingFixture(FixtureOrder("replica")):PendingFixture()):a=="ack"?(replicaAccepted?Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-replica\"}"):Task.FromException<string>(new Exception("lost ack"))):Task.FromResult(OrderFixture(FixtureOrder("replica",acknowledged:replicaAccepted)));
  XsollaPurchasing.Complete=id=>Task.FromResult(CompletionFixture(FixtureOrder(id,acknowledged:replicaAccepted),++replicaCompletions==1));
  await RecoverAllowErrors();Check(Reports==0,"first instance retains verified descriptor until ack accepted");
  var secondInstance=new System.Runtime.Loader.AssemblyLoadContext("xsolla-second-instance",true);
  try {
   var secondAssembly=secondInstance.LoadFromAssemblyPath(typeof(Program).Assembly.Location);
   var secondReport=await (Task<string>)secondAssembly.GetType("Program").GetMethod("RunSecondInstanceScenario").Invoke(null,null);
   Check(secondReport=="1|replica"&&Reports==0,"independent SDK instance may report verified replicated ledger once with stable order ID");
  } finally {secondInstance.Unload();}
  replicaSeed=false;replicaAccepted=true;await API.Xsolla.RecoverPurchases();await API.Xsolla.RecoverPurchases();
  Check(Reports==1&&LastPayment.OrderId=="replica","first instance reports same stable order once; cross-instance consumer must dedupe OrderId");
  Reset(); int pendingReads=0; Controller.IsReadyToUse=true; XsollaPurchasing.Request=(a,b)=>{pendingReads++;return Task.FromResult("{\"orders\":[]}");}; XsollaPurchasing.Resume(); await Wait(); Check(pendingReads==1,"application resume scans recoverable orders");
  Reset(); XsollaPurchasing.Request=(a,b)=>Task.FromResult("{\"orders\":[],\"nextBefore\":\"unchanged\"}"); bool cursorBlocked=false;try{await API.Xsolla.RecoverPurchases();}catch(InvalidOperationException){cursorBlocked=true;} Check(cursorBlocked,"repeated recovery cursor cannot loop forever");
  Reset(); XsollaPurchasing.BrowserPopup=true; Start(); Choice("xsolla"); await Wait(); Check(opens==0&&complete==0&&!actions.Contains("checkout"),"blocked browser popup never creates an order");
  XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\",\"popupOpened\":true}"); await Wait(); Check(success&&complete==1&&opens==0,"browser retry navigates reserved popup without native OpenURL");
  Reset(); Actions.Purchasing.GetInfo=(sku,callback)=>callback(new Actions.Purchasing.HardProductInfo { LocalizedPrice=4.99f, IsoCurrencyCode="USD", LocalizedPriceString="$4.99" });
  Start("sku"); await Wait(); Check(shownPresentation?.nativePrice?.amount=="4.99"&&shownPresentation.nativePrice.currency=="USD"&&shownPresentation.nativePrice.display=="$4.99","native metadata travels with the authoritative quote"); Choice("cancel");
  Reset(); Actions.Purchasing.GetInfo=(sku,callback)=>throw new Exception("store unavailable"); Start("sku"); await Wait(); Check(shownPresentation?.price!=null&&shownPresentation.nativePrice==null&&unavailableChoice==null,"native price lookup failure does not disable quoted checkout"); Choice("cancel");
  Reset(); Actions.Purchasing.GetInfo=(sku,callback)=>callback(new Actions.Purchasing.HardProductInfo { LocalizedPrice=float.NaN, IsoCurrencyCode="USD" });
  Check(await XsollaPurchasing.NativePresentationPrice("sku")==null,"invalid native metadata cannot promise a price");
  Reset(); Action<Actions.Purchasing.HardProductInfo> latePrice=null; Actions.Purchasing.GetInfo=(sku,callback)=>latePrice=callback;
  var missingPrice=XsollaPurchasing.NativePresentationPrice("sku"); Check(await missingPrice==null,"unanswered native metadata hook is bounded");
  latePrice(new Actions.Purchasing.HardProductInfo { LocalizedPrice=9.99f, IsoCurrencyCode="USD" }); Check(await missingPrice==null,"late native metadata cannot replace an already completed price lookup");
  XsollaPurchasing.Stop(); Console.WriteLine(passed+" adapter scenarios passed");
 }
}

#endif
