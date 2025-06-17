using System;
using UnityEngine;

namespace Balancy.UI
{
    public class MainUI : MonoBehaviour
    {
        [SerializeField] private MessageUI messageUI;
        
        private static MainUI _instance = null;

        private void Awake()
        {
            _instance = this;
        }

        public static void ShowMessage(string header, string message, string buttonText, Action callback)
        {
            _instance?.ShowMessagePrivate(header, message, buttonText, callback);
        }
        

        private void ShowMessagePrivate(string header, string message, string buttonText, Action callback)
        {
            messageUI.ShowMessage(header, message, buttonText, callback);
        }
    }
}