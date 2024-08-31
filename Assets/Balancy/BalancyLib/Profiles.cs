using System;
using System.Collections.Generic;
using Balancy.Data;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using UnityEngine;

namespace Balancy
{
    public static class Profiles
    {
        private static readonly Dictionary<string, ParentBaseData> _cachedProfiles = new Dictionary<string, ParentBaseData>();

        public static UnnyProfile System => Get<UnnyProfile>();
        
        public static T Get<T>() where T : Data.ParentBaseData, new()
        {
            var className = JsonBasedObject.GetDataClassName<T>();

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

        internal static void CleanUp()
        {
            Debug.LogError("CLEAN UP");
            foreach (var profile in _cachedProfiles)
                profile.Value.CleanUp(false);
            _cachedProfiles.Clear();
            Debug.LogError("CLEAN UP...");
        }
    }
}