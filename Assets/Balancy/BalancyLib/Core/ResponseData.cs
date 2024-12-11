using System.Runtime.InteropServices;

namespace Balancy.Core
{
    public delegate void ResponseCallback<T>(T responseData) where T : Responses.ResponseData;
    
    public class Responses
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class ResponseData
        {
            private byte success;
            public int ErrorCode;
            [MarshalAs(UnmanagedType.LPStr)] public string ErrorMessage;
            
            public bool Success => success == 1;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class PurchaseProductResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string productId;
        }
    }
}
