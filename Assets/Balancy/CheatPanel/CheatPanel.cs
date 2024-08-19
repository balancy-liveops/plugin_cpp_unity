using Balancy.Data;
using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects.Analytics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.Cheats
{
    public class CheatPanel : MonoBehaviour
    {
        private const float REFRESH_RATE = 1f;
        
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text generalInfo;
        [SerializeField] private TMP_Text abTestsInfo;
        [SerializeField] private TMP_Text segmentsInfo;
        [SerializeField] private Button resetButton;

        private float refreshTimeLeft = 0;

        private void Awake()
        {
            closeButton.onClick.AddListener(HideWindow);
            resetButton.onClick.AddListener(ResetProfiles);
        }

        private void ResetProfiles()
        {
            Balancy.Profiles.Reset();
            UpdateData();
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
            abTestsInfo.SetText(PrepareABTestsInfoText());
            segmentsInfo.SetText(PrepareSegmentsInfoText());
        }

        private void HideWindow()
        {
            gameObject.SetActive(false);
        }

        private string PrepareGeneralInfoText()
        {
            UnnyProfile profile = Profiles.System;
            var info = profile.GeneralInfo;
            return $"UserId:   {info.ProfileId}\n" +
                   $"DeviceId: {info.DeviceId}\n" +
                   $"CustomId: {info.CustomId}\n" +
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

        private string PrepareABTestsInfoText()
        {
            UnnyProfile profile = Profiles.System;
            var info = profile.TestsInfo;
            string result = string.Empty;
            for(int i = 0;i < info.Tests.Count;i++)
            {
                var test = info.Tests[i];
                result +=
                    $"{test.Test.UnnyId} - {test.Test.Name}, group = {test.Variant.Name} -- Finished = {test.Finished}\n";
            }

            if (info.AvoidedTests.Length > 0)
            {
                result += "\nAvoided Tests (I'll never join them):\n";
                for (int i = 0; i < info.AvoidedTests.Length; i++)
                {
                    var test = info.AvoidedTests[i];
                    var aTest = CMS.GetModelByUnnyId<ABTest>(test);
                    if (aTest != null)
                        result += $"{aTest.UnnyId} - {aTest.Name}\n";
                    else
                        result += $"{test}\n";
                }
            }

            return result;
        }
        
        private string PrepareSegmentsInfoText()
        {
            UnnyProfile profile = Profiles.System;
            var info = profile.SegmentsInfo;
            string result = string.Empty;
            for(int i = 0;i < info.Segments.Count;i++)
            {
                var segment = info.Segments[i];
                if (!segment.IsIn)
                    continue;
                
                result +=
                    $"{segment.Segment.UnnyId} - {segment.Segment.Name}, joined at {segment.LastIn}\n";
            }

            return result;
        }
    }
}
