using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Localization;
using UnityEngine;
using UnityEngine.Scripting;


namespace Balancy.Core
{
    public delegate void ResponseCallback<T>(T responseData) where T : Responses.ResponseData;

    public class Responses
    {
        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
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


        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class PurchaseProductResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string ProductId;
            private int removeFromPending;
            public float PriceUSD;
            public bool RemoveFromPending => removeFromPending == 1;
        }

        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class AuthResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string UserId;
        }

        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class LinkResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string UserId;
        }

        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class CompletePurchaseData
        {
            [SerializeField] private string guid;

            [SerializeField] private long time;

            [SerializeField] private List<string> items;

            [SerializeField] private string orderId;

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
        
        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class HoolipayPendingData
        {
            [SerializeField] private string orderId;
            [SerializeField] private string paymentUrl;

            public string OrderId { get => orderId; set => orderId = value; }
            public string PaymentUrl { get => paymentUrl; set => paymentUrl = value; }
        }

        public class HoolipayPendingResponseData : ResponseData
        {
            private HoolipayPendingData data;
            public HoolipayPendingData Data { get => data; set => data = value; }
        }

        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class InteropCompletePurchaseResponseData : ResponseData
        {
            [MarshalAs(UnmanagedType.LPStr)] public string data;
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
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct InteropProductData {
            public IntPtr base_id;
            public int type;
            public IntPtr item_id;
            public IntPtr name;
            public IntPtr description;
            public IntPtr localized_name;
            public IntPtr localized_description;
            public IntPtr icon;
            public float price;
        };
        
        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal class InteropProductsResponseData : ResponseData
        {
            internal IntPtr data;
            internal int size;
        }

        [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal class InteropProductResponseData : ResponseData
        {
            internal IntPtr data;
        }

        public enum ProductType
        {
            Consumable = 1,
            NonConsumable = 2,
            Subscription = 3,
        }

        public class Product
        {
            internal string base_id;
            internal byte type;
            internal string item_id;
            internal string name;
            internal string description;
            // internal Constants.Platform platform;

            internal string localized_name;
            internal string localized_description;
            
            internal string icon;
            
            internal float price;

            internal LocalizedString localizedName;
            internal LocalizedString localizedDescription;

            public string ProductId
            {
                get { return base_id; }
            }

            // internal Constants.Platform Platform => platform;
            public ProductType Type
            {
                get
                {
                    switch (type)
                    {
                        case 1:
                            return ProductType.Consumable;
                        case 2:
                            return ProductType.NonConsumable;
                        case 3:
                            return ProductType.Subscription;
                        default:
                            return ProductType.Consumable;
                    }
                }
            }

            public string Icon
            {
                get { return icon; }
            }
            
            public string PlatformProductId
            {
                get { return item_id; }
            }

            public string Name
            {
                get { return name; }
            }

            public LocalizedString LocalizedName
            {
                get
                {
                    if (localizedName == null)
                    {
                        localizedName = new LocalizedString(localized_name);
                    }

                    return localizedName;
                }
            }

            public LocalizedString LocalizedDescription
            {
                get
                {
                    if (localizedDescription == null)
                    {
                        localizedDescription = new LocalizedString(localized_description);
                    }

                    return localizedDescription;
                }
            }

            public string Description
            {
                get { return description; }
            }

            // public string Icon
            // {
            //     get { return icon; }
            // }

            public float Price
            {
                get { return price; }
            }
        }

        public class ProductsResponseData : ResponseData
        {
            public List<Product> Products { get; internal set; }
        }

        public class ProductResponseData : ResponseData
        {
            public Product Product { get; set; }
        }

        //
        // [StructLayout(LayoutKind.Sequential, Pack = 1)]
        // internal struct InteropStringArray
        // {
        //     public IntPtr items;
        //     public uint count;
        // }
        //
        // [StructLayout(LayoutKind.Sequential, Pack = 1)]
        // internal struct InteropCompletePurchaseData
        // {
        //     [MarshalAs(UnmanagedType.LPStr)]
        //     public string guid;
        //     [MarshalAs(UnmanagedType.LPStr)]
        //     public string orderId;
        //     public InteropStringArray items;
        //     public ulong time;
        // }
        //
        // [StructLayout(LayoutKind.Sequential, Pack = 1)]
        // internal struct InteropCompletePurchaseResponseData 
        // {
        //     public InteropCompletePurchaseData data;
        //
        //     public byte success;
        //     public int ErrorCode;
        //     [MarshalAs(UnmanagedType.LPStr)]
        //     public string ErrorMessage;
        // }
    }
}
