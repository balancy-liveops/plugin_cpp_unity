using System;
using System.Runtime.InteropServices;

namespace Balancy
{
    public static class BalancyLoader
    {
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
#if UNITY_WEBGL && !UNITY_EDITOR
            balancyLoadAndInit(OnBalancyReady);
#else
            UnityEngine.Debug.Log("Balancy init skipped in editor");
            _readyCallback?.Invoke();
#endif
        }
    }
}