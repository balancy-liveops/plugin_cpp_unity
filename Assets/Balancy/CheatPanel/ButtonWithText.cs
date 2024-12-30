using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.CheatPanel
{
    public class ButtonWithText : MonoBehaviour
    {
        [SerializeField] protected TMP_Text title;
        [SerializeField] protected Button button;

        private Action OnClick;

        public void Init(string text, Action callback)
        {
            title.text = text;
            OnClick = callback;
        }
        
        private void Awake()
        {
            button.onClick.AddListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            OnClick?.Invoke();
        }
    }
}