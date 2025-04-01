using System;
using System.Runtime.InteropServices;
using Balancy;
using Balancy.Core;
using Balancy.Data;
using Balancy.Dictionaries;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class ImportScript : MonoBehaviour
{
    [SerializeField] private Image TestImage;
    
    void Start()
    {
        Debug.LogError($"PATH = {Application.persistentDataPath}");
        // PrintSizeAndOffsets<Notifications.InitNotificationLocalReady>();
        // PrintSizeAndOffsets<Notifications.InitNotificationCloudSynced>();
        // PrintSizeAndOffsets<Notifications.InitNotificationAuthFailed>();
        // PrintSizeAndOffsets<Notifications.InitNotificationCloudProfileFailed>();
        InitFinal();
    }

    private double t1;

    private void InitFinal()
    {
        Balancy.Callbacks.ClearAll();
        Balancy.Callbacks.InitExamplesWithLogs();
        
        t1 = Time.realtimeSinceStartupAsDouble;
        var config = CreateAppConfig();
        Balancy.Main.Init(config);
        
        Balancy.Callbacks.OnDataUpdated += status => 
            Debug.Log("OnDataUpdated Cloud = " + status.IsCloudSynced + 
                      " ;CMS = " + status.IsCMSUpdated + 
                      " ;Profiles = " + status.IsProfileUpdated);
    }

    // private MyCustomTemplate TestItem(string unnyId)
    // {
    //     var allItems = Balancy.CMS.GetModels<Balancy.Models.SmartObjects.Item>(true);
    //     Debug.Log("AllItems count = " + allItems.Length);
    //     foreach (var item in allItems)
    //     {
    //         Debug.Log("=== New Item ===");
    //         Debug.Log($"unnyId = {item.UnnyId}");
    //         Debug.Log($"Name = {item.Name}");
    //         Debug.Log($"MaxStack = {item.MaxStack}");
    //     }
    //     
    //     
    //     var myTemplate = CMS.GetModelByUnnyId<MyCustomTemplate>(unnyId);
    //     Debug.Log($"myTemplate.unnyId = {myTemplate.UnnyId}");
    //     Debug.Log($"myTemplate.TestInt = {myTemplate.TestInt}");
    //     Debug.Log($"myTemplate.TestBool = {myTemplate.TestBool}");
    //     Debug.Log($"myTemplate.TestDuration = {myTemplate.TestDuration}");
    //     Debug.Log($"myTemplate.TestFloat = {myTemplate.TestFloat}");
    //     Debug.Log($"myTemplate.TestLong = {myTemplate.TestLong}");
    //     Debug.Log($"myTemplate.TestString = {myTemplate.TestString}");
    //     
    //     Debug.LogWarning($"TestIntArr Size = {myTemplate.TestIntArr.Length}");
    //     for (int i = 0;i<myTemplate.TestIntArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestIntArr[i]}");
    //     
    //     Debug.LogWarning($"TestFloatArr Size = {myTemplate.TestFloatArr.Length}");
    //     for (int i = 0;i<myTemplate.TestFloatArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestFloatArr[i]}");
    //     
    //     Debug.LogWarning($"TestBoolArr Size = {myTemplate.TestBoolArr.Length}");
    //     for (int i = 0;i<myTemplate.TestBoolArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestBoolArr[i]}");
    //     
    //     Debug.LogWarning($"TestLongArr Size = {myTemplate.TestLongArr.Length}");
    //     for (int i = 0;i<myTemplate.TestLongArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestLongArr[i]}");
    //     
    //     Debug.LogWarning($"TestDurationArr Size = {myTemplate.TestDurationArr.Length}");
    //     for (int i = 0;i<myTemplate.TestDurationArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestDurationArr[i]}");
    //     
    //     Debug.LogWarning($"TestStringArr Size = {myTemplate.TestStringArr.Length}");
    //     for (int i = 0;i<myTemplate.TestStringArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestStringArr[i]}");
    //     
    //     // return;
    //     Debug.Log($"myTemplate.TestDate = {myTemplate.TestDate?.Time}");
    //     Debug.Log($"myTemplate.testProduct = {myTemplate.TestProduct?.ProductId} : {myTemplate.TestProduct?.Price}");
    //     
    //     Debug.Log($"myTemplate.Sprite = {myTemplate.Sprite?.Id}");
    //     Debug.Log($"myTemplate.TestInj = {myTemplate.TestInj?.X} ; {myTemplate.TestInj?.Y} ; {myTemplate.TestInj?.Z}");
    //     
    //     Debug.Log($"myTemplate.Color = {myTemplate.Color?.Value}");
    //     Debug.Log($"myTemplate.Loc = {myTemplate.Loc?.Key}");
    //     
    //     Debug.LogWarning($"TestDateArr Size = {myTemplate.TestDateArr.Length}");
    //     for (int i = 0;i<myTemplate.TestDateArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestDateArr[i].Time}");
    //     
    //     Debug.LogWarning($"TestInjArr Size = {myTemplate.TestInjArr.Length}");
    //     for (int i = 0;i<myTemplate.TestInjArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.TestInjArr[i].X} : {myTemplate.TestInjArr[i].Y} : {myTemplate.TestInjArr[i].Z}");
    //     
    //     Debug.LogWarning($"SpriteArr Size = {myTemplate.SpriteArr.Length}");
    //     for (int i = 0;i<myTemplate.SpriteArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.SpriteArr[i].Id}");
    //     
    //     Debug.LogWarning($"ProductArr Size = {myTemplate.ProductArr.Length}");
    //     for (int i = 0;i<myTemplate.ProductArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.ProductArr[i].ProductId} ;price = {myTemplate.ProductArr[i].Price}");
    //     
    //     Debug.LogWarning($"LocArr Size = {myTemplate.LocArr.Length}");
    //     for (int i = 0;i<myTemplate.LocArr.Length;i++)
    //         Debug.Log($"{i}) {myTemplate.LocArr[i].Key}");
    //     
    //     // Debug.LogWarning($"Colors Size = {myTemplate.Colors.Length}");
    //     // for (int i = 0;i<myTemplate.Colors.Length;i++)
    //     //     Debug.Log($"{i}) {myTemplate.Colors[i].Value}");
    //     
    //     
    //     Debug.LogError($"LINK = {myTemplate.SelfLink?.UnnyId} - {myTemplate.SelfLink?.TestString}" );
    //     var links = myTemplate.SelfLinkArray;
    //     Debug.LogWarning($"SelfLinkArray Size = {links.Length}");
    //     for (int i = 0;i<links.Length;i++)
    //         Debug.Log($"{i}) {links[i].UnnyId} - {links[i].TestString}");
    //
    //     return myTemplate;
    // }
    //
    // private void TestItemEnum(string unnyId)
    // {
    //     var model = TestItem(unnyId) as MyCustomTemplate2;
    //     if (model == null)
    //         return;
    //
    //     Debug.LogWarning($"Enum1 = {model.Enum1}");
    //     Debug.LogWarning($"Enum2 = {model.Enum2}");
    // }
    //
    // private void TestItems()
    // {
    //     var tmpYes = CMS.GetModels<MyCustomTemplate>(true);
    //     var tmpNo = CMS.GetModels<MyCustomTemplate>(false);
    //
    //     if (tmpYes != null)
    //     {
    //         Debug.LogError($"tmpYes: {tmpYes.Length}");
    //         foreach (var m in tmpYes)
    //             Debug.LogWarning($">>> {m.UnnyId}");
    //     }
    //
    //     if (tmpNo != null)
    //     {
    //         Debug.LogError($"tmpNo: {tmpNo.Length}");
    //         foreach (var m in tmpNo)
    //             Debug.LogWarning($">>> {m.UnnyId}");
    //     }
    // }

    private AppConfig CreateAppConfig()
    {
        return new AppConfig
        {
            ApiGameId = "6f5d4614-36c0-11ef-9145-066676c39f77",
            PublicKey = "MzA5MGY0NWUwNGE5MTk5ZDU4MDAzNT",
            Environment = Constants.Environment.Development,
            CustomId = "My_Custom_Id",
            OnProgressUpdateCallback = (fileName, progress) =>
            {
                Debug.Log($"Launch progress {(int)(progress*100)}% - {fileName}");
            }
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

    // private void TestProfile()
    // {
    //     var _profile = Profiles.Get<Profile>();
    //     var systemProfile = Balancy.Profiles.System;
    //     
    //     Debug.LogWarning("Profile Int = " + _profile.GeneralInfo.TestInt);
    //
    //     _profile.GeneralInfo.TestInt = 33;
    //     _profile.AnotherInfo.Name = "OPA";
    //         
    //     Debug.LogWarning("Profile Int NOW = " + _profile.GeneralInfo.TestInt);
    //
    //     var list = _profile.GeneralInfo.TestList;
    //     Debug.LogError($"1> List size = {list.Count} vs {_profile.GeneralInfo.TestList.Count}");
    //     var newElement = _profile.GeneralInfo.TestList.Add();
    //     Debug.LogError($"2> List size = {list.Count} vs {_profile.GeneralInfo.TestList.Count}");
    //     newElement.Name = "My Name is";
    //     Debug.LogError($"3> List size = {list.Count} vs {_profile.GeneralInfo.TestList.Count}");
    //         
    //     Debug.LogError($"newElement = {newElement.Name} list = {list[0].Name} vs {_profile.GeneralInfo.TestList[0].Name}");
    // }

    void RenderButton(Rect rect, string id, Image image)
    {
        if (GUI.Button(rect, "Test: " + id))
        {
            DataObjectsManager.GetObject(id, sprite =>
            {
                image.sprite = sprite;
            });
        }
    }

    private void OnGUI()
    {
        // Rect rect = new Rect(100, 10, 300, 30);
        // RenderButton(rect, "855", TestImage);
        // rect.y += rect.height;
        // RenderButton(rect, "856", TestImage);
        // rect.y += rect.height;
        // RenderButton(rect, "1132", TestImage);
        //
        // rect.y += rect.height;
        // if (GUI.Button(rect, "Clean: "))
        //     DataObjectsManager.ClearFromMemory("1132");
        // rect.y += rect.height;
        // if (GUI.Button(rect, "Delete: "))
        //     DataObjectsManager.ClearFromDisk("1132");
        
        // var profile = Profiles.Get<Profile>();
        // if (profile == null)
        //     return;
        //
        // var list = profile.GeneralInfo.TestList;
        // Rect rect = new Rect(100, 10, 300, 50);
        // for (int i = 0; i < list.Count; i++)
        // {
        //     var iRect = rect;
        //     if (GUI.Button(rect, list[i].Name))
        //         list[i].Name = Random.Range(0, 10000).ToString();
        //
        //     iRect.x += iRect.width;
        //     if (GUI.Button(iRect, "Delete"))
        //         list.RemoveAt(i);
        //     
        //     rect.y += rect.height;
        // }
        //
        // if (GUI.Button(rect, "Add Element"))
        //     list.Add().Name = "New element";
        //
        // rect.y += rect.height;
        // if (GUI.Button(rect, "Clear"))
        //     list.Clear();
       
        // Rect rect = new Rect(100, 100, 300, 50);
        // if (GUI.Button(rect, "Test"))
        //     Profiles.Get<Profile>().GeneralInfo.TestInt = UnityEngine.Random.Range(0, 10);
        //
        // rect.y += rect.height;
        // if (GUI.Button(rect, "Ad Watched"))
        //     Balancy.LiveOps.Ads.TrackRevenue(Ads.AdType.Rewarded, 0.31, "test");
    }
    
    private void OnApplicationQuit()
    {
        Main.Stop();
    }
}
