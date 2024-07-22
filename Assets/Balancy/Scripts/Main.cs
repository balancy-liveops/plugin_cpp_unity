namespace Balancy
{
    public static partial class Main
    {
        static partial void PrepareCms();
        
        public static void Init(AppConfig config)
        {
            PrepareCms();
            Controller.Init(config);
        }
    }
}