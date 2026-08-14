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
                profile.RefreshData(newPointer);
        }

        private static Action _userResetCallback;

        public static void Reset(Action onComplete)
        {
            _userResetCallback = onComplete;
            Balancy.Callbacks.OnProfileResetStart?.Invoke();
            LibraryMethods.Data.balancyResetAllProfilesWithCallback(_resetProfilesCallback);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Data.ResetProfilesCallback))]
        private static void OnResetComplete()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                UnityMainThreadDispatcher.EnqueueFromAnyThread(OnResetComplete);
                return;
            }

            Balancy.Callbacks.OnProfileResetFinish?.Invoke();
            var cb = _userResetCallback;
            _userResetCallback = null;
            cb?.Invoke();
        }

        public static void ForceSaveSmartObjects()
        {
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
                
                AllBaseDataSubscriptions?.Clear();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Balancy] Error during cleanup: {e.Message}");
            }
        }

        class BaseDataSubscriptions
        {
            class ParamSubscriptions
            {
                public event Action OnUpdated;

                public void Invoke()
                {
                    OnUpdated?.Invoke();
                }
            }

            private readonly Dictionary<string, ParamSubscriptions> _activeSubscriptions = new Dictionary<string, ParamSubscriptions>();
            private event Action<string> _onAnyParamChanged;

            public void AddParamSubscription(string paramName, Action callback)
            {
                if (!_activeSubscriptions.TryGetValue(paramName, out var subs))
                {
                    subs = new ParamSubscriptions();
                    _activeSubscriptions.Add(paramName, subs);
                }

                subs.OnUpdated += callback;
            }

            public void AddWildcardSubscription(Action<string> callback)
            {
                _onAnyParamChanged += callback;
            }

            public void RemoveWildcardSubscription(Action<string> callback)
            {
                _onAnyParamChanged -= callback;
            }

            public void OnBaseDataParamChanged(string paramName)
            {
                if (_activeSubscriptions.TryGetValue(paramName, out var subs))
                    subs.Invoke();

                _onAnyParamChanged?.Invoke(paramName);
            }

            public void RemoveDataSubscription(string paramName, Action callback)
            {
                if (_activeSubscriptions.TryGetValue(paramName, out var subs))
                    subs.OnUpdated -= callback;
            }
        }

        private static readonly Dictionary<IntPtr, BaseDataSubscriptions> AllBaseDataSubscriptions = new Dictionary<IntPtr, BaseDataSubscriptions>();

        internal static void AddDataSubscription(IntPtr ptr, string paramName, Action callback)
        {
            if (!AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
            {
                subs = new BaseDataSubscriptions();
                AllBaseDataSubscriptions.Add(ptr, subs);
            }

            subs.AddParamSubscription(paramName, callback);
        }

        internal static void RemoveDataSubscription(IntPtr ptr, string paramName, Action callback)
        {
            if (AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
                subs.RemoveDataSubscription(paramName, callback);
        }

        internal static void AddWildcardSubscription(IntPtr ptr, Action<string> callback)
        {
            if (!AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
            {
                subs = new BaseDataSubscriptions();
                AllBaseDataSubscriptions.Add(ptr, subs);
            }

            subs.AddWildcardSubscription(callback);
        }

        internal static void RemoveWildcardSubscription(IntPtr ptr, Action<string> callback)
        {
            if (AllBaseDataSubscriptions.TryGetValue(ptr, out var subs))
                subs.RemoveWildcardSubscription(callback);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Data.ParamChangedCallback))]
        private static void OnBaseDataParamChanged(IntPtr baseData, string paramName)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                UnityMainThreadDispatcher.EnqueueFromAnyThread(() => OnBaseDataParamChanged(baseData, paramName));
                return;
            }

            if (AllBaseDataSubscriptions.TryGetValue(baseData, out var subs))
                subs.OnBaseDataParamChanged(paramName);
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Data.DataDestroyedCallback))]
        private static void OnBaseDataDestroyed(IntPtr baseData)
        {
            // The native object at this pointer is being destroyed RIGHT NOW
            // (this callback fires from ~BaseData, possibly on a worker thread).
            // Null out _pointer on every live C# wrapper referencing it
            // immediately and thread-safely, so a wrapper captured in a
            // background timer/coroutine delegate stops dereferencing freed
            // memory without waiting for the next main-thread frame.
            JsonBasedObject.InvalidateByPointer(baseData);

            // Subscription bookkeeping touches main-thread-only structures, so
            // marshal just that part to the main thread.
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                UnityMainThreadDispatcher.EnqueueFromAnyThread(() => AllBaseDataSubscriptions.Remove(baseData));
                return;
            }

            AllBaseDataSubscriptions.Remove(baseData);
        }
    }
}
