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
        }
    }
}
