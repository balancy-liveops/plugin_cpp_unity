using System;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class AppConfigAbiTests
    {
        private static readonly Type BaseType = typeof(AppConfig).Assembly.GetType("Balancy.CppBaseAppConfig", true);
        private static readonly Type ConfigType = typeof(AppConfig).Assembly.GetType("Balancy.CppAppConfig", true);

        [Test]
        public void InteropConfigsRemainSequentialAndBytePacked()
        {
            AssertPackedSequential(BaseType);
            AssertPackedSequential(ConfigType);
        }

        [Test]
        public void BaseConfigFieldOrderAndOffsetsMatchNativeAbi()
        {
            AssertLayout(BaseType,
                ("ApiGameId", IntPtr.Size),
                ("PublicKey", IntPtr.Size),
                ("Environment", sizeof(int)),
                ("UpdateType", sizeof(int)),
                ("UpdatePeriod", sizeof(int)),
                ("OnStatusUpdate", IntPtr.Size),
                ("OnProgressUpdateCallback", IntPtr.Size),
                ("LaunchType", sizeof(int)),
                ("BranchName", IntPtr.Size),
                ("CdnCustomUrl", IntPtr.Size),
                ("CdnTimeout", sizeof(int)),
                ("CdnRetries", sizeof(int)));
        }

        [Test]
        public void DerivedConfigFieldOrderAndOffsetsMatchNativeAbi()
        {
            var offset = Marshal.SizeOf(BaseType);
            AssertLayout(ConfigType, offset,
                ("Platform", sizeof(int)),
                ("DevicePlatform", sizeof(int)),
                ("AutoLogin", sizeof(byte)),
                ("DeviceId", IntPtr.Size),
                ("CustomId", IntPtr.Size),
                ("AppVersion", IntPtr.Size),
                ("BundleId", IntPtr.Size),
                ("EngineVersion", IntPtr.Size),
                ("DeviceModel", IntPtr.Size),
                ("DeviceName", IntPtr.Size),
                ("DeviceType", sizeof(int)),
                ("OperatingSystem", IntPtr.Size),
                ("OperatingSystemFamily", sizeof(int)),
                ("SystemMemorySize", sizeof(int)),
                ("SystemLanguage", IntPtr.Size));
        }

        [Test]
        public void InteropStringFieldsRemainMarshalablePointers()
        {
            foreach (var type in new[] { BaseType, ConfigType })
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (field.GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.LPStr)
                    Assert.That(field.FieldType, Is.EqualTo(typeof(string)), $"{type.Name}.{field.Name}");
            }
        }

        private static void AssertPackedSequential(Type type)
        {
            var layout = type.StructLayoutAttribute;
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.Value, Is.EqualTo(LayoutKind.Sequential));
            Assert.That(layout.Pack, Is.EqualTo(1));
        }

        private static void AssertLayout(Type type, params (string Name, int Size)[] fields)
        {
            AssertLayout(type, 0, fields);
        }

        private static void AssertLayout(Type type, int initialOffset, params (string Name, int Size)[] fields)
        {
            var offset = initialOffset;
            foreach (var field in fields)
            {
                Assert.That(Marshal.OffsetOf(type, field.Name).ToInt32(), Is.EqualTo(offset),
                    $"ABI offset changed for {type.Name}.{field.Name}");
                offset += field.Size;
            }
            Assert.That(Marshal.SizeOf(type), Is.EqualTo(offset), $"ABI size changed for {type.Name}");
        }
    }
}
