using UnityEngine;

namespace Balancy
{
    public static class Profiles
    {
        private static Data.Profile _profile;
        
        public static Data.Profile Profile => _profile;

        public static void UpdateProfiles()
        {
            var p = LibraryMethods.Data.balancyGetProfile("Profile");
            Debug.LogError(" p = " + p);
            _profile = new Data.Profile();
            _profile.SetData(p);
            _profile.InitData();
            
            Debug.LogWarning("Profile Int = " + _profile.GeneralInfo.TestInt);

            _profile.GeneralInfo.TestInt = 63;
            
            Debug.LogWarning("Profile Int NOW = " + _profile.GeneralInfo.TestInt);

            var list = _profile.GeneralInfo.TestList;
            Debug.LogError($"1> List size = {list.Count} vs {_profile.GeneralInfo.TestList.Count}");
            var newElement = _profile.GeneralInfo.TestList.Add();
            Debug.LogError($"2> List size = {list.Count} vs {_profile.GeneralInfo.TestList.Count}");
            newElement.Name = "My Name is";
            Debug.LogError($"3> List size = {list.Count} vs {_profile.GeneralInfo.TestList.Count}");
            
            Debug.LogError($"newElement = {newElement.Name} list = {list[0].Name} vs {_profile.GeneralInfo.TestList[0].Name}");
        }
    }
}