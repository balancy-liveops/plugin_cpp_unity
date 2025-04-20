using System;
using System.Runtime.InteropServices;

namespace Balancy
{
    public static class BalancyLoader
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private static Action _readyCallback;

        [AOT.MonoPInvokeCallback(typeof(Action))]
        public static void OnBalancyReady()
        {
            UnityEngine.Debug.Log("✅ Balancy is initialized and ready!");
            _readyCallback?.Invoke();
        }

        [DllImport("__Internal")]
        private static extern void balancyLoadAndInit(Action onReadyCallback);

        public static void Init(Action readyCallback)
        {
            _readyCallback = readyCallback;
            balancyLoadAndInit(OnBalancyReady);
        }
#else
        public static void Init(Action readyCallback)
        {
            UnityEngine.Debug.Log("Balancy init skipped in editor");
            readyCallback?.Invoke();
        }
#endif
    }
}