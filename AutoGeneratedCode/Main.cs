using Balancy.Models;
namespace Balancy
{
    public static partial class Main
    {
        static partial void PrepareCms()
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