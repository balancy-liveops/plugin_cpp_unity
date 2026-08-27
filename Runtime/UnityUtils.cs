#if !BALANCY_SERVER
using System;
using UnityEngine;

namespace Balancy
{
    public static class UnityUtils
    {
        private const string DEVICE_UNIQUE_ID = "DEVICE_UNIQUE_ID";
        public static string _cachedDeviceId;

        public static string GetEngineVersion()
        {
            return $"Unity {Application.unityVersion}";
        }

        public static string GetUniqId()
        {
            if (_cachedDeviceId == null)
                _cachedDeviceId = GetUniqIdPrivate();
            return _cachedDeviceId;
        }

        private static string GetUniqIdPrivate()
        {
            try
            {
                if (PlayerPrefs.HasKey(DEVICE_UNIQUE_ID))
                {
                    var storedId = PlayerPrefs.GetString(DEVICE_UNIQUE_ID);
                    if (IsValidDeviceId(storedId))
                        return storedId;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Balancy] Failed to read the device ID from PlayerPrefs. " +
                                 $"A session-only ID will be used: {exception.Message}");
            }

            var id = SystemInfo.deviceUniqueIdentifier;
            if (!IsValidDeviceId(id))
                id = Guid.NewGuid().ToString();

            try
            {
                PlayerPrefs.SetString(DEVICE_UNIQUE_ID, id);

                // WebGL stores PlayerPrefs in IndexedDB. Persist the generated ID
                // immediately instead of relying on application shutdown, which a
                // browser tab is not guaranteed to report to Unity.
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Balancy] Failed to persist the device ID in PlayerPrefs. " +
                                 $"It may change after the application restarts: {exception.Message}");
            }

            return id;
        }

        private static bool IsValidDeviceId(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   id.Length > 10 &&
                   !string.Equals(id, SystemInfo.unsupportedIdentifier, StringComparison.Ordinal);
        }
    }
}
#endif
