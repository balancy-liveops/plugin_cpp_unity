namespace Balancy.Models.SmartObjects
{
    public class ProfileFullPath : JsonBasedObject
    {
        public string Profile => GetStringParam("profile");
        public string Path => GetStringParam("path");
    }
}
