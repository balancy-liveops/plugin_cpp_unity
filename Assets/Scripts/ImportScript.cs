using System;
using System.Runtime.InteropServices;
using Balancy;
using Balancy.Core;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using UnityEngine;

public class ImportScript : MonoBehaviour
{
    void Start()
    {
        Debug.LogError($"PATH = {Application.persistentDataPath}");
        // PrintSizeAndOffsets<Notifications.InitNotificationLocalReady>();
        // PrintSizeAndOffsets<Notifications.InitNotificationCloudSynched>();
        // PrintSizeAndOffsets<Notifications.InitNotificationAuthFailed>();
        // PrintSizeAndOffsets<Notifications.InitNotificationCloudProfileFailed>();
        InitFinal();
    }

    private double t1;

    private void InitFinal()
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
            UpdateType = UpdateType.FullUpdate,
            UpdatePeriod = 600,
            // OnSaveFileInCache = SaveInCache,
            // OnSaveFileInResources = SaveInResources,
            OnStatusUpdate = OnStatusUpdate,
            AutoLogin = 1,
            CustomId = "My_Custom_Id"
        };
    }

    private void SaveInCache(string path, string data)
    {
        Debug.LogError($"OnSaveFileInCache at {path} : {data}");
    }
    
    private void SaveInResources(string path, string data)
    {
        Debug.LogError($"OnSaveFileInResources at {path} : {data}");
    }
    
    private void OnStatusUpdate(IntPtr notificationPtr)
    {
        var notification = Marshal.PtrToStructure<Notifications.StatusNotificationBase>(notificationPtr);
        switch (notification.Type)
        {
            case Notifications.NotificationType.LocalReady:
                var localReady = Marshal.PtrToStructure<Notifications.InitNotificationLocalReady>(notificationPtr);
                double t2 = Time.realtimeSinceStartupAsDouble;
                Debug.LogError("**==> Local loaded. Size = " + Marshal.SizeOf(typeof(Notifications.InitNotificationLocalReady)) + $" in {(t2-t1)*1000} ms");
                var model = DataManager.GetModelByUnnyId("684");
                Debug.LogError($"Model = {model} !!");
                var model2 = DataManager.GetModelByUnnyId<Item>("684");
                Debug.LogError($"Model2 = {model2} > {model2?.MaxStack}!!");
                
                // var maxStack = DataManager.GetIntParam(model, "maxStack");
                // var maxStack2 = DataManager.GetIntParam(model, "maxStack2");
                // Debug.LogError($"Model = {model} ; maxStack = {maxStack} >>2>> {maxStack2}");

                // var model2 = DataManager.GetModelByUnnyId<BaseModel>("684");
                break;
            case Notifications.NotificationType.CloudSynched:
                var cloudSynched = Marshal.PtrToStructure<Notifications.InitNotificationCloudSynched>(notificationPtr);
                Debug.LogError($"**==> Cloud Synched. DICT = {cloudSynched.WereDictUpdated}, Profiles = {cloudSynched.WereProfilesUpdated}" + " Size = " + Marshal.SizeOf(typeof(Notifications.InitNotificationCloudSynched)));
                break;
            case Notifications.NotificationType.AuthFailed:
                var authFailed = Marshal.PtrToStructure<Notifications.InitNotificationAuthFailed>(notificationPtr);
                // string authFailedMessage = Marshal.PtrToStringAnsi(authFailed.Message); // Manually marshal string
                // Debug.Log($"Auth failed. {authFailedMessage}");
                Debug.LogError($"**==> Auth failed. {authFailed.Message} " + " Size = " + Marshal.SizeOf(typeof(Notifications.InitNotificationAuthFailed)));
                break;
            case Notifications.NotificationType.CloudProfileFailed:
                var profileFailed = Marshal.PtrToStructure<Notifications.InitNotificationCloudProfileFailed>(notificationPtr);
                // string profileFailedMessage = Marshal.PtrToStringAnsi(profileFailed.Message); // Manually marshal string
                // Debug.Log($"Profile load failed. {profileFailedMessage}");
                Debug.LogError($"**==> Profile load failed. {profileFailed.Message}" + " Size = " + Marshal.SizeOf(typeof(Notifications.InitNotificationCloudProfileFailed)));
                break;
            default:
                Debug.LogError("**==> Unknown notification type. " + notification.Type);
                break;
        }
    }
    
    public static void PrintSizeAndOffsets<T>()
    {
        Debug.LogWarning($"Size of {typeof(T).Name}: {Marshal.SizeOf<T>()}");

        var fields = typeof(T).GetFields();
        foreach (var field in fields)
        {
            var offset = Marshal.OffsetOf<T>(field.Name);
            Debug.Log($"Field: {field.Name}, Offset: {offset}, Type: {field.FieldType}");
        }
    }
}
