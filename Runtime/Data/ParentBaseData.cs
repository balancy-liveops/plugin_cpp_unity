namespace Balancy.Data
{
    public class ParentBaseData : BaseData
    {
        public int GetLastCloudSyncTime()
        {
            return Balancy.LibraryMethods.Data.balancyProfile_GetLastCloudSyncTime(GetRawPointer());
        }
    }
}
