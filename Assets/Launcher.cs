using Balancy;
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
    
    private void OnApplicationQuit()
    {
        Main.Stop();
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
}
