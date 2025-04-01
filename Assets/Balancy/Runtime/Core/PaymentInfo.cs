using System.Runtime.InteropServices;

namespace Balancy.Core
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public class PaymentInfo
    {
        public float Price;
        [MarshalAs(UnmanagedType.LPStr)] public string Receipt;
        [MarshalAs(UnmanagedType.LPStr)] public string ProductId;
        [MarshalAs(UnmanagedType.LPStr)] public string Currency;
        [MarshalAs(UnmanagedType.LPStr)] public string OrderId;
    }
}