namespace Balancy
{
    public static partial class Main
    {
        static partial void PrepareCms();

        public static bool IsReadyToUse => Controller.IsReadyToUse;
        
        public static void Init(AppConfig config)
        {
            PrepareCms();
            Controller.Init(config);
        }
        
        public static void Stop()
        {
            Controller.Stop();
        }
    }
}