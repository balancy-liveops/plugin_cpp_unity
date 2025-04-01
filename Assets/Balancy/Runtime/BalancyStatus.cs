using Balancy.Models;

namespace Balancy
{
    public class BalancyStatus : JsonBasedObject
    {
        public int Deploy => GetIntParam("deploy");
        public long LastCloudConfigsUpdate => GetLongParam("dicts_cloud_update");
        public string BranchName => GetStringParam("branch_name");
        
        public long ServerTime => GetLongParam("server_time");
        public long GameTime => GetLongParam("admin_time");
        public long ServerTimeMs => GetLongParam("server_time_ms");
        public long GameTimeMs => GetLongParam("admin_time_ms");
    }
}