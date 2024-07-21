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

    private void TestItem(string unnyId)
    {
        var myTemplate = DataManager.GetModelByUnnyId<MyCustomTemplate>(unnyId);
        Debug.Log($"myTemplate.unnyId = {myTemplate.UnnyId}");
        Debug.Log($"myTemplate.TestInt = {myTemplate.TestInt}");
        Debug.Log($"myTemplate.TestBool = {myTemplate.TestBool}");
        Debug.Log($"myTemplate.TestDuration = {myTemplate.TestDuration}");
        Debug.Log($"myTemplate.TestFloat = {myTemplate.TestFloat}");
        Debug.Log($"myTemplate.TestLong = {myTemplate.TestLong}");
        Debug.Log($"myTemplate.TestString = {myTemplate.TestString}");
        
        Debug.LogWarning($"TestIntArr Size = {myTemplate.TestIntArr.Length}");
        for (int i = 0;i<myTemplate.TestIntArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestIntArr[i]}");
        
        Debug.LogWarning($"TestFloatArr Size = {myTemplate.TestFloatArr.Length}");
        for (int i = 0;i<myTemplate.TestFloatArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestFloatArr[i]}");
        
        Debug.LogWarning($"TestBoolArr Size = {myTemplate.TestBoolArr.Length}");
        for (int i = 0;i<myTemplate.TestBoolArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestBoolArr[i]}");
        
        Debug.LogWarning($"TestLongArr Size = {myTemplate.TestLongArr.Length}");
        for (int i = 0;i<myTemplate.TestLongArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestLongArr[i]}");
        
        Debug.LogWarning($"TestDurationArr Size = {myTemplate.TestDurationArr.Length}");
        for (int i = 0;i<myTemplate.TestDurationArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestDurationArr[i]}");
        
        Debug.LogWarning($"TestStringArr Size = {myTemplate.TestStringArr.Length}");
        for (int i = 0;i<myTemplate.TestStringArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestStringArr[i]}");
        
        // return;
        Debug.Log($"myTemplate.TestDate = {myTemplate.TestDate?.Time}");
        Debug.Log($"myTemplate.testProduct = {myTemplate.TestProduct?.ProductId} : {myTemplate.TestProduct?.Price}");
        
        Debug.Log($"myTemplate.Sprite = {myTemplate.Sprite?.Id}");
        Debug.Log($"myTemplate.TestInj = {myTemplate.TestInj?.X} ; {myTemplate.TestInj?.Y} ; {myTemplate.TestInj?.Z}");
        
        Debug.Log($"myTemplate.Color = {myTemplate.Color?.Value}");
        Debug.Log($"myTemplate.Loc = {myTemplate.Loc?.Key}");
        
        Debug.LogWarning($"TestDateArr Size = {myTemplate.TestDateArr.Length}");
        for (int i = 0;i<myTemplate.TestDateArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestDateArr[i].Time}");
        
        Debug.LogWarning($"TestInjArr Size = {myTemplate.TestInjArr.Length}");
        for (int i = 0;i<myTemplate.TestInjArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.TestInjArr[i].X} : {myTemplate.TestInjArr[i].Y} : {myTemplate.TestInjArr[i].Z}");
        
        Debug.LogWarning($"SpriteArr Size = {myTemplate.SpriteArr.Length}");
        for (int i = 0;i<myTemplate.SpriteArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.SpriteArr[i].Id}");
        
        Debug.LogWarning($"ProductArr Size = {myTemplate.ProductArr.Length}");
        for (int i = 0;i<myTemplate.ProductArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.ProductArr[i].ProductId} ;price = {myTemplate.ProductArr[i].Price}");
        
        Debug.LogWarning($"LocArr Size = {myTemplate.LocArr.Length}");
        for (int i = 0;i<myTemplate.LocArr.Length;i++)
            Debug.Log($"{i}) {myTemplate.LocArr[i].Key}");
        
        Debug.LogWarning($"Colors Size = {myTemplate.Colors.Length}");
        for (int i = 0;i<myTemplate.Colors.Length;i++)
            Debug.Log($"{i}) {myTemplate.Colors[i].Value}");
    }

    private void TestItems()
    {
        var tmpYes = DataManager.GetModels<MyCustomTemplate>(true);
        var tmpNo = DataManager.GetModels<MyCustomTemplate>(false);

        if (tmpYes != null)
        {
            Debug.LogError($"tmpYes: {tmpYes.Length}");
            foreach (var m in tmpYes)
                Debug.LogWarning($">>> {m.UnnyId}");
        }

        if (tmpNo != null)
        {
            Debug.LogError($"tmpNo: {tmpNo.Length}");
            foreach (var m in tmpNo)
                Debug.LogWarning($">>> {m.UnnyId}");
        }
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
                // var model = DataManager.GetModelByUnnyId("814");
                // Debug.LogError($"Model = {model} !!");
                // var model2 = DataManager.GetModelByUnnyId<MyCustomTemplate>("814");
                // Debug.LogError($"Model2 = {model2} > {model2?.TestInt}!!");
                
                // var maxStack = DataManager.GetIntParam(model, "maxStack");
                // var maxStack2 = DataManager.GetIntParam(model, "maxStack2");
                // Debug.LogError($"Model = {model} ; maxStack = {maxStack} >>2>> {maxStack2}");

                // var model2 = DataManager.GetModelByUnnyId<BaseModel>("684");
                // TestItem();
                break;
            case Notifications.NotificationType.CloudSynched:
                var cloudSynched = Marshal.PtrToStructure<Notifications.InitNotificationCloudSynched>(notificationPtr);
                Debug.LogError($"**==> Cloud Synched. DICT = {cloudSynched.WereDictUpdated}, Profiles = {cloudSynched.WereProfilesUpdated}" + " Size = " + Marshal.SizeOf(typeof(Notifications.InitNotificationCloudSynched)));
                try
                {
                    // TestItem("814");
                    // TestItem("872");
                    TestItems();
                }
                catch (Exception e)
                {
                    Debug.LogError("OOPS " + e.Message);
                }

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
