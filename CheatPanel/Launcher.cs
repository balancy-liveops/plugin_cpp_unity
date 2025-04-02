using UnityEngine;

namespace Balancy.Cheats
{
    public class Launcher : MonoBehaviour
    {
        [SerializeField] private string _apiGameId;
        [SerializeField] private string _publicKey;
        [SerializeField] private Constants.Environment _environment;
        
        private void Awake()
        {
            Balancy.Callbacks.ClearAll();
            Balancy.Callbacks.InitExamplesWithLogs();

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
                ApiGameId = _apiGameId,
                PublicKey = _publicKey,
                Environment = _environment,
                CustomId = "My_Custom_Id",
                OnProgressUpdateCallback = (fileName, progress) =>
                {
                    Debug.Log($"Progress {(int)(progress * 100)}% - {fileName}");
                }
            };
        }
    }
}
