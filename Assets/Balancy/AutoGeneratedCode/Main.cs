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
                
				case "MyItem": return new Balancy.Models.MyItem();
				case "VectorType": return new Balancy.Models.VectorType();
				case "MyCustomTemplate2": return new Balancy.Models.MyCustomTemplate2();
				case "MyCustomTemplate3": return new Balancy.Models.MyCustomTemplate3();
				case "MyCustomTemplate": return new Balancy.Models.MyCustomTemplate();
                default: return null;
            }
        }
    }
}