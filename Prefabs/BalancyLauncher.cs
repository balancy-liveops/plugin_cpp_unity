using System;
using System.Collections.Generic;
using UnityEngine;

namespace Balancy
{
    public class BalancyLauncher : MonoBehaviour
    {
        [SerializeField] private bool autoStart = true;
        
        [SerializeField] private string apiGameId;
        [SerializeField] private string apiPublicKey;

        [SerializeField] private Constants.Environment environment = Constants.Environment.Development;
        [SerializeField] private string branchName;
        
        public void SetGameId(string value) { apiGameId = value; }
        public void SetPublicKey(string value) { apiPublicKey = value; }
        public void SetBranchName(string value) { branchName = value; }
        
        private static BalancyLauncher _instance;

        private void Start()
        {
            if (autoStart)
                InitPrivate();
            
            DontDestroyOnLoad(gameObject);
        }

        public static void Init()
        {
            if (_instance)
                _instance.InitPrivate();
            else
                Debug.LogError("No BalancyLauncher instance found. Please add one to the scene.");
        }
        
        private void InitPrivate()
        {
            Balancy.Callbacks.InitExamplesWithLogs();
            Balancy.Main.Init(new AppConfig
            {
                ApiGameId = apiGameId,
                PublicKey = apiPublicKey,
                Environment = GetEnvironment(),
                BranchName = branchName,
                OnProgressUpdateCallback = ((fileName, progress) =>
                {
                    Debug.Log($"Balancy launch progress {(progress*100):2}% : {fileName}");
                }),
            });
        }
        
        private Constants.Environment GetEnvironment()
        {
            //TODO use define symbols here, like PRODUCTION or DEVELOPMENT if required
            return environment;
        }

        private void OnDestroy()
        {
            Balancy.Callbacks.ClearAll();
            Main.Stop();
        }

        private void OnGUI()
        {
            if (!Balancy.Main.IsReadyToUse)
                return;
        
            var rect = new Rect(200, 500, 200, 100);
            var newLevel = GUI.TextField(rect, Profiles.System.GeneralInfo.Level.ToString());
            if (int.TryParse(newLevel, out var intLevel) && intLevel != Profiles.System.GeneralInfo.Level)
            {
                Profiles.System.GeneralInfo.Level = intLevel;
                Debug.Log($"Set new level: {intLevel}");
            }
        
            rect.y += rect.height * 1.5f;

            if (GUI.Button(rect, "RESET"))
            {
                Profiles.Reset();
                // RenderViewsManager.OpenView("https://balancy.co");
            }
        }
    }
}
