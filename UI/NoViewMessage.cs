using UnityEngine;
using UnityEngine.UI;

namespace Balancy.UI
{
    public class NoViewMessage : MonoBehaviour
    {
        [SerializeField] private Button _button;
        
        private void Start()
        {
            _button.onClick.AddListener(CloseWindow);
        }

        private void CloseWindow()
        {
            Destroy(gameObject);
        }
    }
}