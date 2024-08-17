using Balancy.Data.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy
{
    public class CheatPanel : MonoBehaviour
    {
        private const float REFRESH_RATE = 1f;
        
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text generalInfo;

        private float refreshTimeLeft = 0;

        private void Awake()
        {
            closeButton.onClick.AddListener(HideWindow);
        }

        private void Update()
        {
            if (!Balancy.Main.IsReadyToUse)
                return;
            
            refreshTimeLeft -= Time.deltaTime;
            if (refreshTimeLeft > 0)
                return;

            refreshTimeLeft = REFRESH_RATE;
            UpdateData();
        }

        private void UpdateData()
        {
            generalInfo.SetText(PrepareGeneralInfoText());
        }

        private void HideWindow()
        {
            gameObject.SetActive(false);
        }

        private string PrepareGeneralInfoText()
        {
            UnnyProfile profile = Profiles.System;
            var info = profile.GeneralInfo;
            return $"UserId:            {info.ProfileId}\n" +
                   $"DeviceId:          {info.DeviceId}\n" +
                   $"CustomId:          {info.CustomId}\n" +
                   $"AppVersion:        {info.AppVersion}\n" +
                   $"EngineVersion:     {info.EngineVersion}\n" +
                   $"Platform:          {info.PlatformId}\n" +

                   $"Country:           {info.Country}\n" +
                   $"SystemLanguage:    {info.SystemLanguage}\n" +
                   $"GameLocalization:  {info.GameLocalization}\n" +
                   
                   $"Session:           {info.Session}\n" +
                   $"IsNewUser:         {info.IsNewUser}\n" +
                   $"FirstLoginTime:    {info.FirstLoginTime}\n" +
                   $"PlayTime:          {info.PlayTime}\n" +
                   $"TimeSinceInstall:  {info.TimeSinceInstall}\n" +
                   $"TimeSincePurchase: {info.TimeSincePurchase}\n" +
                   
                   $"Level:             {info.Level}\n" +
                   $"TutorialStep:      {info.TutorialStep}\n" +
                   
                   $"TrafficSource:     {info.TrafficSource}\n" +
                   $"TrafficCampaign:   {info.TrafficCampaign}\n" +
                   
                   $"DeviceModel:       {info.DeviceModel}\n" +
                   $"DeviceName:        {info.DeviceName}\n" +
                   $"DeviceType:        {info.DeviceType}\n" +
                   $"OperatingSystem:   {info.OperatingSystem}\n" +
                   $"OperatingSystemFamily:   {info.OperatingSystemFamily}\n" +
                   $"SystemMemorySize:  {info.SystemMemorySize}\n";

        }
    }
}
