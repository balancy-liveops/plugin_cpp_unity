using UnityEngine;

namespace Balancy
{
    [CreateAssetMenu(fileName = "BalancyConfiguration", menuName = "Balancy/Configuration", order = 1)]
    public class BalancyConfiguration : ScriptableObject
    {
        public static BalancyConfiguration Instance => Resources.Load<BalancyConfiguration>("BalancyConfiguration");

        [SerializeField] private int timeout = 10;
        [SerializeField] private int retries = 3;
        [SerializeField] private bool useCustomCDN = false;
        [SerializeField] private string url = "https://example.com";
        

        public int Timeout => timeout;
        public int Retries => retries;
        public bool UseCustomCDN => useCustomCDN;
        public string Url => url;
    }
}