#if UNITY_EDITOR && !BALANCY_SERVER
using System;

namespace Balancy.Editor
{
    public class Balancy_Editor
    {
        public delegate void SynchAddressablesDelegate(
            Balancy_EditorAuth editorAuth,
            string gameId,
            string token,
            int branchId,
            string branchName,
            Action<string, float> onProgress,
            Action onStart,
            Action<string> onComplete
        );

        public static event SynchAddressablesDelegate SynchAddressablesEvent;

        public static void TriggerSynchAddressables(
            Balancy_EditorAuth editorAuth,
            string gameId,
            string token,
            int branchId,
            string branchName,
            Action<string, float> onProgress,
            Action onStart,
            Action<string> onComplete)
        {
            SynchAddressablesEvent?.Invoke(editorAuth, gameId, token, branchId, branchName, onProgress, onStart, onComplete);
        }
    }
}
#endif
