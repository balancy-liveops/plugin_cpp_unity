using System;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.Cheats
{
    public class Tabs : MonoBehaviour
    {
        [Serializable]
        class TabInfo
        {
            public Button button;
            public GameObject section;
        }

        [SerializeField] private TabInfo[] allTabs;
        
        public RectTransform viewTrm;
        public GameObject defaultSection;

        private void Awake()
        {
            foreach (var tab in allTabs)
            {
                var tabInfo = tab;
                tabInfo.button.onClick.AddListener(() =>
                {
                    ActivateSection(tabInfo.section);
                });
            }

            ActivateSection(defaultSection);
        }

        private void ActivateSection(GameObject section)
        {
            for (int i = 0; i < viewTrm.childCount; i++)
            {
                var trm = viewTrm.GetChild(i);
                var go = trm.gameObject;
                go.SetActive(go == section);
            }
        }
    }
}
