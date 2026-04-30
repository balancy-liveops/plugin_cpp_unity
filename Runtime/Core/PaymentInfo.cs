using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace Balancy.Core
{
    [Preserve, StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public class PaymentInfo
    {
        public float Price;
        [MarshalAs(UnmanagedType.LPStr)] public string Receipt;
        [MarshalAs(UnmanagedType.LPStr)] public string ProductId;
        [MarshalAs(UnmanagedType.LPStr)] public string Currency;
        [MarshalAs(UnmanagedType.LPStr)] public string OrderId;
        public float PriceUSD;
    }
}