using TMPro;
using UnityEngine;

namespace Balancy.Cheats
{
    public class UserPropertiesPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputLevel;

        private void OnEnable()
        {
            if (!Balancy.Main.IsReadyToUse)
                return;

            Refresh();

            inputLevel.onEndEdit.AddListener(OnLevelChanged);
        }

        private void OnDisable()
        {
            inputLevel.onEndEdit.RemoveListener(OnLevelChanged);
        }

        private void Refresh()
        {
            var profile = Profiles.System;
            inputLevel.text = profile.GeneralInfo.Level.ToString();
        }

        private void OnLevelChanged(string newValue)
        {
            if (!int.TryParse(newValue, out var newLevel))
            {
                Debug.LogWarning("Invalid level input. Please enter a valid integer.");
                return;
            }

            // Update the profile level
            var profile = Profiles.System;
            profile.GeneralInfo.Level = newLevel;

            // Optional: Log the change
            Debug.Log($"Level updated to: {newLevel}");
        }
    }
}