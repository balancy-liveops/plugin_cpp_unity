using Balancy;
using Balancy.Core;
using Balancy.Models;
using UnityEngine;

public class Launcher : MonoBehaviour
{
    private double t1;
    
    private void Start()
    {
        t1 = Time.realtimeSinceStartupAsDouble;
        var config = CreateAppConfig();
        Balancy.Main.Init(config);
    }
    
    private AppConfig CreateAppConfig()
    {
        return new AppConfig
        {
            ApiGameId = "6f5d4614-36c0-11ef-9145-066676c39f77",
            PublicKey = "MzA5MGY0NWUwNGE5MTk5ZDU4MDAzNT",
            Environment = Constants.Environment.Development,
            OnStatusUpdate = OnStatusUpdate,
            CustomId = "My_Custom_Id",
            OnProgressUpdateCallback = (fileName, progress) =>
            {
                Debug.Log($"Progress {(int)(progress*100)}% - {fileName}");
            }
        };
    }
    
    private void OnStatusUpdate(Notifications.NotificationBase notification)
    {
        switch (notification)
        {
            case Notifications.InitNotificationDataIsReady dataIsReady:
                Debug.LogError($"**==> Data is Ready; Cloud =" + dataIsReady.IsCloudSynched + $" ;DICT = {dataIsReady.IsCMSUpdated}, Profiles = {dataIsReady.IsProfileUpdated}");
                if (dataIsReady.IsCMSUpdated)
                    TestData();
                break;
            case Notifications.InitNotificationAuthFailed authFailed:
                Debug.LogError($"**==> Auth failed. {authFailed.Message} ");
                break;
            case Notifications.InitNotificationCloudProfileFailed profileFailed:
                Debug.LogError($"**==> Profile load failed. {profileFailed.Message}");
                break;
            default:
                Debug.LogError("**==> Unknown notification type. " + notification.Type);
                break;
        }
    }

    private void TestData()
    {
        var oneDocument = CMS.GetModelByUnnyId<MyCustomTemplate>("872");
        
        var myCustomTemplatesOnly = CMS.GetModels<MyCustomTemplate>(false);
        Debug.LogWarning($"myCustomTemplatesOnly = {myCustomTemplatesOnly.Length}");
        
        var myCustomTemplatesWithChildren = CMS.GetModels<MyCustomTemplate>(true);
        Debug.LogWarning($"myCustomTemplatesWithChildren = {myCustomTemplatesWithChildren.Length}");

        foreach (var template in myCustomTemplatesWithChildren)
            Debug.Log($"Template = {template.TestString} of {template.UnnyId}");
    }
}
