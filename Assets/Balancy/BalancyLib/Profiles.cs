using System;
using System.Collections.Generic;
using Balancy.Data;
using Balancy.Data.SmartObjects;
using Balancy.Models;

namespace Balancy
{
    public static class Profiles
    {
        private static readonly Dictionary<string, ParentBaseData> _cachedProfiles = new Dictionary<string, ParentBaseData>();

        public static UnnyProfile System => Get<UnnyProfile>();
        
        public static T Get<T>() where T : Data.ParentBaseData, new()
        {
            var classNameFull = JsonBasedObject.GetDataClassName<T>();
            
            var elements = classNameFull.Split(".");
            var className = elements[^1];
            
            if (_cachedProfiles.TryGetValue(className, out var profile))
                return profile as T;
            
            var ptr = LibraryMethods.Data.balancyGetProfile(className);
            profile = JsonBasedObject.CreateObject<T>(ptr, false);
            _cachedProfiles.Add(className, profile);
            return (T)profile;
        }
        
        internal static void ProfileReset(string profileName, IntPtr newPointer)
        {
            if (_cachedProfiles.TryGetValue(profileName, out var profile))
                profile.RefreshData(newPointer);
        }

        public static void Reset()
        {
            LibraryMethods.Data.balancyResetAllProfiles();
        }

        internal static void Init()
        {
            LibraryMethods.Data.balancySetProfileOnReset(ProfileReset);
            LibraryMethods.Data.balancySetBaseDataParamChanged(OnBaseDataParamChanged);
            LibraryMethods.Data.balancySetBaseDataDestroyed(OnBaseDataDestroyed);
        }

        internal static void CleanUp()
        {
            LibraryMethods.Data.balancySetProfileOnReset(null);
            LibraryMethods.Data.balancySetBaseDataParamChanged(null);
            LibraryMethods.Data.balancySetBaseDataDestroyed(null);
            foreach (var profile in _cachedProfiles)
                profile.Value.CleanUp(false);
            _cachedProfiles.Clear();
            
            AllBaseDataSubscriptions.Clear();
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

            public void AddParamSubscription(string paramName, Action callback)
            {
                if (!_activeSubscriptions.TryGetValue(paramName, out var subs))
                {
                    subs = new ParamSubscriptions();
                    _activeSubscriptions.Add(paramName, subs);
                }

                subs.OnUpdated += callback;
            }

            public void OnBaseDataParamChanged(string paramName)
            {
                if (_activeSubscriptions.TryGetValue(paramName, out var subs))
                    subs.Invoke();
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

        private static void OnBaseDataParamChanged(IntPtr baseData, string paramName)
        {
            if (AllBaseDataSubscriptions.TryGetValue(baseData, out var subs))
                subs.OnBaseDataParamChanged(paramName);
        }
        
        private static void OnBaseDataDestroyed(IntPtr baseData)
        {
            AllBaseDataSubscriptions.Remove(baseData);
        }
    }
}