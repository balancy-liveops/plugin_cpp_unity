using System;
using System.Collections.Generic;
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
            
            public bool Success
            {
                get { return success == 1; }
                set { success = (byte)(value ? 1 : 0); }
            }
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class PurchaseProductResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string ProductId;
            private int removeFromPending;
            public bool RemoveFromPending => removeFromPending == 1;
        }
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class AuthResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string UserId;
        }
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class LinkResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string UserId;
        }
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class CompletePurchaseData
        {
            private string guid;
        
            private long time;

            private List<string> items;
        
            private string orderId;

            public long Time
            {
                get { return time; }
                set { time = value; }
            }
        
            public string OrderId
            {
                get { return orderId; }
                set { orderId = value; }
            }
        
            public string Guid
            {
                get { return guid; }
                set { guid = value; }
            }

            public List<string> Items
            {
                get { return items; }
                set { items = value; }
            }
        }
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class InteropCompletePurchaseResponseData : ResponseData
        {
            public string data;
        }
        
        public class CompletePurchaseResponseData : ResponseData
        {
            private CompletePurchaseData data;
            
            public CompletePurchaseData Data
            {
                get => this.data;
                set => this.data = value;
            }
        }
    }
}
