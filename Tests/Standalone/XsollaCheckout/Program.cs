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
namespace Balancy.Models.SmartObjects { public class StoreItem { public IntPtr GetRawPointer() => IntPtr.Zero; } }
namespace Balancy {
 public class Actions { public class BalancyProductInfo {
  public enum PurchaseType { StoreItem, Offer, OfferGroup, ShopSlot }
  public string ProductId, StoreItemUnnyId, OfferInstanceId, OfferUnnyId; public PurchaseType Type;
  public BalancyProductInfo(Models.SmartObjects.StoreItem _) {} public void ReportThePurchase(Core.PaymentInfo info) => Program.Reports++;
 } }
 public static class Constants { public enum BalancyPlatform { Undefined, IosAppStore, AndroidGooglePlay } }
 public class StubConfig { public Constants.BalancyPlatform BalancyPlatform; }
 public static class Controller { public static bool IsReadyToUse; public static StubConfig Config; public static event Action<bool,bool> OnDataUpdated; }
 public static class RenderViewsManager { public static IDisposable PresentPaymentChoice(Action ready,Action<string> failed) => null; public static void SendMessageToView(string msg) {} }
 public static class CMS { public static T GetModelByUnnyId<T>(string id) where T:class => null; }
 public static class Profiles { public static StubProfile System = new(); }
 public class StubProfile { public StubProfile ShopsInfo => this; public StubProfile SmartInfo => this; public Models.SmartObjects.StoreItem FindShopSlot(string id) => null; public Models.SmartObjects.StoreItem FindOfferInfo(string id) => null; public Models.SmartObjects.StoreItem FindOfferGroupInfo(string id) => null; }
 public static class LibraryMethods { public static class API {
  public delegate void XsollaCallback(int id,bool success,string json);
  public static IntPtr balancyXsollaGetSessionIdentity() => IntPtr.Zero;
  public static IntPtr balancyXsollaPreparePurchase(int kind,IntPtr owner,IntPtr store) => IntPtr.Zero;
  public static void balancyXsollaRequest(string action,string json,int id,XsollaCallback callback) {}
  public static void balancyXsollaComplete(string orderId,int id,XsollaCallback callback) {}
 } }
}
namespace Balancy.Core { public class PaymentInfo { public string ProductId,OrderId,Currency,Receipt; public float Price,PriceUSD; } }
class Program {
 public static int Reports; static int passed, callbackCount, nativeCount, leaseDisposed, ack, complete, opens;
 const string ValidAck="{\"acknowledged\":true,\"grantId\":\"grant\"}";
 static List<string> presentationStates = new();
 static string requestId, session, unavailableChoice; static Action<string> presentationFailed; static bool success; static List<string> actions = new();
 class Lease:IDisposable { public void Dispose() => leaseDisposed++; }
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
 static void Reset() {
  XsollaPurchasing.Stop(); Controller.IsReadyToUse=false; requestId = null; unavailableChoice=null; session="player-a"; Reports=callbackCount=nativeCount=leaseDisposed=ack=complete=opens=0; success=false; actions.Clear(); presentationStates.Clear();
  XsollaPurchasing.SessionIdentity=()=>session;
  XsollaPurchasing.Prepare=_=>"{\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"sku\",\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"doc\"}}";
  XsollaPurchasing.Present=(ready,failed)=>{ presentationFailed=failed; ready(); return new Lease(); };
  XsollaPurchasing.Send=json=> { var msg=UnityEngine.JsonUtility.FromJson<XsollaPurchasing.PresentationMessage>(json); if(msg.type=="paymentChoiceUpdate")presentationStates.Add(msg.state); if(msg.type=="paymentChoiceShow") { requestId=msg.requestId; unavailableChoice=msg.xsollaUnavailable; XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceReady\",\"requestId\":\""+requestId+"\"}"); } };
  XsollaPurchasing.OpenCheckout=_=>opens++;
  XsollaPurchasing.Persist=()=>Task.CompletedTask;
  XsollaPurchasing.BrowserPopup=false;
  XsollaPurchasing.Request=(action,body)=> {
   using(var envelope=JsonDocument.Parse(body)) {
    var allowed=action=="checkout"?new[]{"requestId","productId","country","language","context"}:action=="get"?new[]{"orderId"}:action=="ack"?new[]{"orderId","grantId"}:action=="discard"?new[]{"requestId"}:new[]{"before"};
    foreach(var property in envelope.RootElement.EnumerateObject()) if(Array.IndexOf(allowed,property.Name)<0)throw new Exception("forbidden route field "+property.Name);
    if(action=="checkout" && envelope.RootElement.GetProperty("context").EnumerateObject().Count()!=2)throw new Exception("context optional fields were added");
   }
   actions.Add(action); if(action=="ack") { ack++; return Task.FromResult(ValidAck); }
   return Task.FromResult(action=="checkout" ? "{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"pending\",\"checkoutUrl\":\"https://sandbox-secure.xsolla.com/paystation4/?token=test\"}}" : "{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"}}");
  };
  XsollaPurchasing.Complete=id=>{ complete++; return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"},\"grant\":{\"grantId\":\"grant\"},\"applied\":true,\"requiresPersistence\":true}"); };
  API.Xsolla.Configure(new XsollaOptions {Enabled=true,Country="US"});
 }
 static void Start()=>XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),(ok,error)=>{callbackCount++;success=ok;},()=>nativeCount++);
 static void Choice(string choice,string id=null)=>XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceSelected\",\"requestId\":\""+(id??requestId)+"\",\"choice\":\""+choice+"\"}");
 static async Task Wait()=> await Task.Delay(20);
 static async Task Main() {
  Reset(); API.Xsolla.Configure(null); Check(!XsollaPurchasing.TryIntercept(new Actions.BalancyProductInfo(null),null,null),"disabled preserves native flow");
  foreach(var prepareFault in new[]{"checkpoint","auth","invalid-contract"}) {
   Reset(); XsollaPurchasing.Prepare=_=>prepareFault=="invalid-contract"?"{}":throw new InvalidOperationException("xsolla_"+prepareFault+"_unavailable");
   Start(); Check(Guid.TryParse(requestId,out _)&&!string.IsNullOrEmpty(unavailableChoice)&&callbackCount==0&&actions.Count==0,"prepare "+prepareFault+" keeps native selector available without checkout IO");
   Choice("xsolla"); XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\"}"); await Wait();
   Check(complete==0&&opens==0&&actions.Count==0&&callbackCount==0,"prepare "+prepareFault+" rejects synthetic alternative selection");
   Choice("native"); Check(nativeCount==1&&callbackCount==0&&actions.Count==0&&leaseDisposed==1,"prepare "+prepareFault+" preserves explicit native callback without automatic fallback");
  }
  Reset(); XsollaPurchasing.Prepare=_=>throw new Exception("unavailable");Start();Choice("cancel");Check(callbackCount==1&&!success&&nativeCount==0&&actions.Count==0,"native-only selector cancellation grants nothing");
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
  deferredCheckout.SetResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"}}");
  await Wait(); Check(success&&callbackCount==1&&complete==1&&ack==1&&leaseDisposed==1,"external renderer closure preserves paid tracking and callback");
  Reset(); Start(); var wait=new TaskCompletionSource<string>(); XsollaPurchasing.Request=(action,body)=>{ actions.Add(action); return wait.Task; }; Choice("xsolla"); Choice("xsolla"); Check(actions.Count==1,"duplicate Xsolla click creates once"); Choice("cancel"); wait.SetResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"}}"); await Wait(); Check(!success&&complete==0&&nativeCount==0,"dismiss pending never falls back or applies late response");
  Reset(); Start(); XsollaPurchasing.Request=(a,b)=>{session="player-b";return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"}}");}; Choice("xsolla"); await Wait(); Check(complete==0&&ack==0&&Reports==0,"session change prevents completion and callbacks");
  Reset(); Start(); int flushCount=0; XsollaPurchasing.Persist=()=> ++flushCount==1?Task.CompletedTask:Task.FromException(new Exception("disk failure")); Choice("xsolla"); await Wait(); Check(complete==1&&ack==0&&Reports==0&&!success,"storage failure never acknowledges"); Choice("cancel");
  Reset(); XsollaPurchasing.Request=(a,b)=>{ if(a=="pending")return Task.FromResult("{\"orders\":[{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\",\"context\":{\"type\":\"StoreItem\"}}]}"); ack++;return Task.FromResult(ValidAck);}; XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"},\"grant\":{\"grantId\":\"grant\"},\"applied\":false}");}; await API.Xsolla.RecoverPurchases(); Check(complete==1&&ack==1&&Reports==0,"recovery acknowledges durable duplicate without re-reporting reward");
  foreach(var url in new[]{"https://sandbox-secure.xsolla.com/paystation4/?token=fixture","https://secure.xsolla.com/paystation4/?token=fixture"}) { XsollaPurchasing.ValidateCheckoutUrl(url); Check(true,"accept official checkout URL "+url); }
  Reset(); Start(); var sandboxRequest=XsollaPurchasing.Request; XsollaPurchasing.Request=async(a,b)=>(await sandboxRequest(a,b)).Replace("sandbox-secure.xsolla.com","secure.xsolla.com"); Choice("xsolla"); await Wait(); Check(success&&callbackCount==1&&opens==1&&ack==1,"server production checkout host follows same verified flow"); Choice("cancel");
  foreach(var url in new[]{"http://sandbox-secure.xsolla.com/paystation4/?token=a","http://secure.xsolla.com/paystation4/?token=a","https://evil.com/paystation4/?token=a","https://secure.xsolla.com/paystation4/","https://x@sandbox-secure.xsolla.com/paystation4/?token=a","https://sandbox-secure.xsolla.com:8443/paystation4/?token=a","https://secure.xsolla.com:8443/paystation4/?token=a","https://secure.xsolla.com.evil.com/paystation4/?token=a","https://sandbox-secure.xsolla.com.evil.com/paystation4/?token=a","https://secure-xsolla.com/paystation4/?token=a","https://secure.xsolla.com/wrong-path/?token=a","https://secure.xsolla.com/paystation4/?token=a#fragment"}) { bool blocked=false;try{XsollaPurchasing.ValidateCheckoutUrl(url);}catch{blocked=true;} Check(blocked,"reject unsafe URL "+url); }
  Reset(); Start(); XsollaPurchasing.Request=(a,b)=>Task.FromResult("{\"order\":{\"id\":\"foreign-owned\",\"state\":\"paid\",\"requestId\":\"00000000-0000-4000-8000-000000000002\"}}"); Choice("xsolla"); await Wait(); Check(complete==0&&ack==0&&Reports==0&&!success,"foreign owned request cannot finish active purchase"); Choice("cancel");
  Reset(); Start(); int attempts=0; XsollaPurchasing.Persist=()=> ++attempts==2?Task.FromException(new Exception("temporary disk failure")):Task.CompletedTask;
  XsollaPurchasing.Complete=id=>{complete++;return Task.FromResult("{\"order\":{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"sku\",\"state\":\"paid\"},\"grant\":{\"grantId\":\"grant\"},\"applied\":"+(complete==1?"true":"false")+",\"requiresPersistence\":true}");};
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
  const string PendingPaid="{\"orders\":[{\"id\":\"order\",\"state\":\"paid\",\"context\":{\"type\":\"StoreItem\"}}]}";
  XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(PendingPaid):recoveryFixtureRequest(a,b);
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
  var liveAck=new TaskCompletionSource<string>(); var recoveryAck=new TaskCompletionSource<string>();int concurrentAckCalls=0;
  XsollaPurchasing.Complete=async id=>{var result=await racingComplete(id);return complete==1?result:result.Replace("\"applied\":true","\"applied\":false");};
  XsollaPurchasing.Request=(a,b)=>a=="pending"?Task.FromResult(PendingPaid):a=="ack"?(++concurrentAckCalls==1?liveAck.Task:recoveryAck.Task):racingRequest(a,b);
  Choice("xsolla");await Wait();var racingRecovery=API.Xsolla.RecoverPurchases();await Wait();
  Check(complete==2&&concurrentAckCalls==2&&Reports==0&&callbackCount==0,"concurrent recovery and live finalization wait for acknowledgment");
  recoveryAck.SetResult(ValidAck);await racingRecovery;liveAck.SetResult(ValidAck);await Wait();
  Check(Reports==1&&callbackCount==1&&success,"concurrent recovery and live finalization report same grant once");Choice("cancel");
  Reset(); int pendingReads=0; Controller.IsReadyToUse=true; XsollaPurchasing.Request=(a,b)=>{pendingReads++;return Task.FromResult("{\"orders\":[]}");}; XsollaPurchasing.Resume(); await Wait(); Check(pendingReads==1,"application resume scans recoverable orders");
  Reset(); XsollaPurchasing.Request=(a,b)=>Task.FromResult("{\"orders\":[],\"nextBefore\":\"unchanged\"}"); bool cursorBlocked=false;try{await API.Xsolla.RecoverPurchases();}catch(InvalidOperationException){cursorBlocked=true;} Check(cursorBlocked,"repeated recovery cursor cannot loop forever");
  Reset(); XsollaPurchasing.BrowserPopup=true; Start(); Choice("xsolla"); await Wait(); Check(opens==0&&complete==0&&!actions.Contains("checkout"),"blocked browser popup never creates an order");
  XsollaPurchasing.ReceivePresentationMessage("{\"type\":\"paymentChoiceRetry\",\"requestId\":\""+requestId+"\",\"popupOpened\":true}"); await Wait(); Check(success&&complete==1&&opens==0,"browser retry navigates reserved popup without native OpenURL");
  XsollaPurchasing.Stop(); Console.WriteLine(passed+" adapter scenarios passed");
 }
}

#endif
