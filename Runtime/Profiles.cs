using System;
using System.Collections.Generic;
using System.Threading;
using Balancy.Data;
using Balancy.Data.SmartObjects;
using Balancy.Models;

namespace Balancy
{
    public static class Profiles
    {
        private static readonly Dictionary<string, ParentBaseData> _cachedProfiles = new Dictionary<string, ParentBaseData>();
        private static readonly LibraryMethods.Data.ResetProfilesCallback _resetProfilesCallback = OnResetComplete;
        private static int _mainThreadId;

        public static UnnyProfile System => Get<UnnyProfile>();
        
        public static T Get<T>() where T : Data.ParentBaseData, new()
        {
            var classNameFull = JsonBasedObject.GetDataClassName<T>();
            
            var elements = classNameFull.Split(".");
            var className = elements[^1];
            
            if (_cachedProfiles.TryGetValue(className, out var profile))
                return profile as T;
            
            var ptr = LibraryMethods.Data.balancyGetProfile(className);
            if (ptr == IntPtr.Zero)
                return null;
            
            profile = JsonBasedObject.CreateObject<T>(ptr, false);
            _cachedProfiles.Add(className, profile);
            return (T)profile;
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.ModelRefreshedCallback))]
        internal static void ProfileReset(string profileName, IntPtr newPointer)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                UnityMainThreadDispatcher.EnqueueFromAnyThread(() => ProfileReset(profileName, newPointer));
                return;
            }

            if (_cachedProfiles.TryGetValue(profileName, out var profile))
            {
                try
                {
                    profile.RefreshData(newPointer);
                }
                catch (Exception exception)
                {
                    // This method is a native callback. Generated InitData() may
                    // execute game code, so never let it unwind through P/Invoke.
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        private static readonly List<Action> _userResetCallbacks = new List<Action>();
        private static readonly object _resetLock = new object();
        private static bool _resetInProgress;

        public static void Reset(Action onComplete)
        {
            if (!Controller.IsNativeInitialized)
            {
                UnityEngine.Debug.LogError("[Balancy] Profiles.Reset ignored: SDK is not initialized");
                return;
            }

            lock (_resetLock)
            {
                if (onComplete != null)
                    _userResetCallbacks.Add(onComplete);
                if (_resetInProgress)
                    return;
                _resetInProgress = true;
            }

            InvokeResetSubscribersSafely(Balancy.Callbacks.OnProfileResetStart);
            try
            {
                LibraryMethods.Data.balancyResetAllProfilesWithCallback(_resetProfilesCallback);
            }
            catch (Exception exception)
            {
                lock (_resetLock)
                {
                    _userResetCallbacks.Clear();
                    _resetInProgress = false;
                }
                UnityEngine.Debug.LogException(exception);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Data.ResetProfilesCallback))]
        private static void OnResetComplete()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                UnityMainThreadDispatcher.EnqueueFromAnyThread(OnResetComplete);
                return;
            }

            Action[] callbacks;
            lock (_resetLock)
            {
                callbacks = _userResetCallbacks.ToArray();
                _userResetCallbacks.Clear();
                _resetInProgress = false;
            }

            // Clear the state before invoking game code. A throwing subscriber
            // must not strand every future reset behind _resetInProgress=true.
            InvokeResetSubscribersSafely(Balancy.Callbacks.OnProfileResetFinish);

            foreach (var callback in callbacks)
            {
                try
                {
                    callback.Invoke();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        private static void InvokeResetSubscribersSafely(Callbacks.OnProfileResetDelegate subscribers)
        {
            if (subscribers == null)
                return;

            foreach (Callbacks.OnProfileResetDelegate subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber.Invoke();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        public static void ForceSaveSmartObjects()
        {
            if (Controller.IsNativeInitialized)
                LibraryMethods.Data.balancyForceSaveSmartObjects();
        }

        internal static void Init()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            LibraryMethods.Data.balancySetProfileOnReset(ProfileReset);
            LibraryMethods.Data.balancySetBaseDataParamChanged(OnBaseDataParamChanged);
            LibraryMethods.Data.balancySetBaseDataDestroyed(OnBaseDataDestroyed);
        }

        internal static void CleanUp()
        {
            try
            {
                LibraryMethods.Data.balancySetProfileOnReset(null);
                LibraryMethods.Data.balancySetBaseDataParamChanged(null);
                LibraryMethods.Data.balancySetBaseDataDestroyed(null);
                
                if (_cachedProfiles != null)
                {
                    foreach (var profile in _cachedProfiles)
                        profile.Value?.CleanUp(false);
                    _cachedProfiles.Clear();
                }
                
                lock (_subscriptionsLock)
                    AllBaseDataSubscriptions.Clear();
                lock (_resetLock)
                {
                    _userResetCallbacks.Clear();
                    _resetInProgress = false;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Balancy] Error during cleanup: {e.Message}");
            }
        }

        // Native destruction can arrive on a worker thread. Retire its subscription
        // entry immediately, before the allocator can reuse the native address.
        // Never invoke game callbacks while holding this lock.
        private static readonly object _subscriptionsLock = new object();
        private static readonly Dictionary<IntPtr, BaseDataSubscriptions> AllBaseDataSubscriptions = new Dictionary<IntPtr, BaseDataSubscriptions>();

        private sealed class BaseDataSubscriptions
        {
            private readonly Dictionary<string, Action> _activeSubscriptions = new Dictionary<string, Action>();
            private Action<string> _onAnyParamChanged;

            public void AddParamSubscription(string paramName, Action callback)
            {
                _activeSubscriptions.TryGetValue(paramName, out var subscribers);
                _activeSubscriptions[paramName] = subscribers + callback;
            }

            public void RemoveDataSubscription(string paramName, Action callback)
            {
                if (_activeSubscriptions.TryGetValue(paramName, out var subscribers))
                    _activeSubscriptions[paramName] = subscribers - callback;
            }

            public void AddWildcardSubscription(Action<string> callback) => _onAnyParamChanged += callback;
            public void RemoveWildcardSubscription(Action<string> callback) => _onAnyParamChanged -= callback;

            public Delegate[] GetParamCallbacks(string paramName)
            {
                return _activeSubscriptions.TryGetValue(paramName, out var subscribers)
                    ? subscribers?.GetInvocationList() : null;
            }

            public Delegate[] GetWildcardCallbacks() => _onAnyParamChanged?.GetInvocationList();
        }

        private static BaseDataSubscriptions GetOrCreateSubscriptions(IntPtr ptr)
        {
            // Caller holds _subscriptionsLock.
            if (!AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
            {
                subs = new BaseDataSubscriptions();
                AllBaseDataSubscriptions.Add(ptr, subs);
            }
            return subs;
        }

        internal static void AddDataSubscription(IntPtr ptr, string paramName, Action callback)
        {
            lock (_subscriptionsLock)
                GetOrCreateSubscriptions(ptr).AddParamSubscription(paramName, callback);
        }

        internal static void RemoveDataSubscription(IntPtr ptr, string paramName, Action callback)
        {
            lock (_subscriptionsLock)
            {
                if (AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
                    subs.RemoveDataSubscription(paramName, callback);
            }
        }

        internal static void AddWildcardSubscription(IntPtr ptr, Action<string> callback)
        {
            lock (_subscriptionsLock)
                GetOrCreateSubscriptions(ptr).AddWildcardSubscription(callback);
        }

        internal static void RemoveWildcardSubscription(IntPtr ptr, Action<string> callback)
        {
            lock (_subscriptionsLock)
            {
                if (AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
                    subs.RemoveWildcardSubscription(callback);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Data.ParamChangedCallback))]
        private static void OnBaseDataParamChanged(IntPtr baseData, string paramName)
        {
            BaseDataSubscriptions subs;
            lock (_subscriptionsLock)
            {
                if (!AllBaseDataSubscriptions.TryGetValue(baseData, out subs))
                    return;
            }

            // Capture the entry, not just its address: an old queued notification
            // must never be delivered to a replacement object at that address.
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                UnityMainThreadDispatcher.EnqueueFromAnyThread(() => DispatchParamChanged(baseData, paramName, subs));
                return;
            }
            DispatchParamChanged(baseData, paramName, subs);
        }

        private static bool IsCurrentSubscription(IntPtr baseData, BaseDataSubscriptions subs)
        {
            lock (_subscriptionsLock)
                return AllBaseDataSubscriptions.TryGetValue(baseData, out var current) && ReferenceEquals(current, subs);
        }

        private static void DispatchParamChanged(IntPtr baseData, string paramName, BaseDataSubscriptions subs)
        {
            Delegate[] callbacks;
            lock (_subscriptionsLock)
            {
                if (!AllBaseDataSubscriptions.TryGetValue(baseData, out var current) || !ReferenceEquals(current, subs))
                    return;
                callbacks = subs.GetParamCallbacks(paramName);
            }
            if (callbacks != null)
            {
                foreach (Action callback in callbacks)
                {
                    if (!IsCurrentSubscription(baseData, subs))
                        return;
                    try { callback.Invoke(); }
                    catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
                }
            }

            lock (_subscriptionsLock)
            {
                // A field callback may itself destroy or replace this object.
                if (!AllBaseDataSubscriptions.TryGetValue(baseData, out var current) || !ReferenceEquals(current, subs))
                    return;
                callbacks = subs.GetWildcardCallbacks();
            }
            if (callbacks != null)
            {
                foreach (Action<string> callback in callbacks)
                {
                    if (!IsCurrentSubscription(baseData, subs))
                        return;
                    try { callback.Invoke(paramName); }
                    catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Data.DataDestroyedCallback))]
        private static void OnBaseDataDestroyed(IntPtr baseData)
        {
            JsonBasedObject.InvalidateByPointer(baseData);
            lock (_subscriptionsLock)
                AllBaseDataSubscriptions.Remove(baseData);
        }
    }
}
