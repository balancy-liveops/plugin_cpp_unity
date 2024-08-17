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
            var className = JsonBasedObject.GetDataClassName<T>();

            if (_cachedProfiles.TryGetValue(className, out var profile))
                return profile as T;
            
            var ptr = LibraryMethods.Data.balancyGetProfile(className);
            profile = JsonBasedObject.CreateObject<T>(ptr);
            _cachedProfiles.Add(className, profile);
            return (T)profile;
        }

        internal static void RefreshAll()
        {
            _cachedProfiles.Clear();
        }
    }
}