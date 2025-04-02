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
        
        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }
        
        private void Awake()
        {
            button.onClick.AddListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            OnClick?.Invoke();
        }

        public void SetTextColor(Color color)
        {
            title.color = color;
        }
    }
}