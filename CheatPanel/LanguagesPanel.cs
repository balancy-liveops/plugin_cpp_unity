using Balancy.Example;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class LanguagesPanel : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private RectTransform pagesContent;
        
        private void OnEnable()
        {
            Refresh();
        }
        
        private void Refresh()
        {
            pagesContent.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var allLocalizations = Balancy.API.Localization.GetAllLocalizationCodes();
            var currentLocalization = Balancy.API.Localization.GetCurrentLocalizationCode();

            foreach (var code in allLocalizations)
            {
                var newItem = Instantiate(prefab, pagesContent);
                newItem.SetActive(true);
                var btnWithText = newItem.GetComponent<ButtonWithText>();
                var localCode = code;
                btnWithText.Init(code, () =>
                {
                    Balancy.API.Localization.ChangeLocalization(localCode);
                    Refresh();
                });
                
                btnWithText.SetTextColor(code == currentLocalization ? Color.green : Color.black);
            }
        }
    }
}