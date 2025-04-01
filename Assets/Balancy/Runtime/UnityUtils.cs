#if !BALANCY_SERVER
using System;
using UnityEngine;

namespace Balancy
{
    public static class UnityUtils
    {
        private const string DEVICE_UNIQUE_ID = "DEVICE_UNIQUE_ID";
        public static string _cachedDeviceId;
        public static Func<string> OnGetDeviceIdRequested;

        public static string GetEngineVersion()
        {
            return $"Unity {Application.unityVersion}";
        }

        public static string GetUniqId()
        {
            if (OnGetDeviceIdRequested != null)
                return OnGetDeviceIdRequested();
            
            if (_cachedDeviceId == null)
                _cachedDeviceId = GetUniqIdPrivate();
            return _cachedDeviceId;
        }

        private static string GetUniqIdPrivate()
        {
            if (PlayerPrefs.HasKey(DEVICE_UNIQUE_ID))
                return PlayerPrefs.GetString(DEVICE_UNIQUE_ID);

            var id = SystemInfo.deviceUniqueIdentifier;
            if (SystemInfo.unsupportedIdentifier == SystemInfo.deviceUniqueIdentifier || string.IsNullOrEmpty(id) || id.Length <= 10)
                id = Guid.NewGuid().ToString();
            
            PlayerPrefs.SetString(DEVICE_UNIQUE_ID, id);
            return id;
        }
    }
}
#endif