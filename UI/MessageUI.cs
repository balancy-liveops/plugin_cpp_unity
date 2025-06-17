using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.UI
{
    public class MessageUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _header;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private Button _button;
        private Action _callback;

        public void ShowMessage(string header, string message, string buttonText, Action callback)
        {
            _header.SetText(header);
            _message.SetText(message);
            _buttonText.SetText(buttonText);
            _callback = callback;
            Show();
        }

        private void Awake()
        {
            _button.onClick.AddListener(OnButtonClicked);
            Hide();
        }

        private void OnButtonClicked()
        {
            _callback?.Invoke();
            Hide();
        }

        private void Show()
        {
            gameObject.SetActive(true);
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}