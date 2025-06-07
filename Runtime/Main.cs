namespace Balancy
{
    public static class Main
    {
        public static bool IsReadyToUse => Controller.IsReadyToUse;
        
        public static void Init(AppConfig config)
        {
            Controller.Init(config);
        }
        
        public static void Stop()
        {
            Controller.Stop();
        }
    }
}