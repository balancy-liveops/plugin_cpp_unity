using Balancy.Models;
namespace Balancy
{
    public static class GeneratedMain
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        private static void PrepareCms()
        {
            CMS.OnTypeRequested = OnTypeRequested;
        }
        
        private static BaseModel OnTypeRequested(string templateName)
        {
            switch (templateName)
            {
                
				case "MyGameEvent": return new Balancy.Models.MyGameEvent();
				case "MyGameOffer": return new Balancy.Models.MyGameOffer();
                default: return null;
            }
        }
    }
}