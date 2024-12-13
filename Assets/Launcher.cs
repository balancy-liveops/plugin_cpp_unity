using Balancy;
using Balancy.Core;
using Balancy.Models;
using UnityEngine;

public class Launcher : MonoBehaviour
{
    private double t1;
    
    private void Awake()
    {
        Balancy.Callbacks.ClearAll();
        Balancy.Callbacks.InitExamplesWithLogs();
        
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
            CustomId = "My_Custom_Id",
            OnProgressUpdateCallback = (fileName, progress) =>
            {
                Debug.Log($"Progress {(int)(progress*100)}% - {fileName}");
            }
        };
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
