using System;
using System.Runtime.InteropServices;
using Balancy.Models;
using UnityEngine;

namespace Balancy
{
    public class DataManager
    {
        private void TestParams()
        {
            var model = GetModelByUnnyId("22");
            Debug.LogError($"Int param = {GetIntParam(model, "paramInt")}");
            Debug.LogError($"Float param = {GetFloatParam(model, "paramFloat")}");
            Debug.LogError($"String param = {GetStringParam(model, "paramString")}");
        
            Debug.LogError($"Bool true param = {GetBoolParam(model, "boolTrue")}");
            Debug.LogError($"Bool false param = {GetBoolParam(model, "boolFalse")}");
        
            int[] intArray = GetIntArrayParam(model, "paramIntArray");
            Debug.LogError($"Int array param = {string.Join(", ", intArray)}");
        
            float[] floatArray = GetFloatArrayParam(model, "paramFloatArray");
            Debug.LogError($"Float array param = {string.Join(", ", floatArray)}");

            bool[] boolArray = GetBoolArrayParam(model, "paramBoolArray");
            Debug.LogError($"Bool array param = {string.Join(", ", boolArray)}");
        
            string[] stringArray = GetStringArrayParam(model, "paramStringArray");
            Debug.LogError($"String array param = {string.Join(", ", stringArray)}");
        }
        
        public static int GetIntParam(IntPtr model, string paramName)
        {
            return LibraryMethods.Models.balancyGetIntParam(model, paramName);
        }
        
        private static float GetFloatParam(IntPtr model, string paramName)
        {
            return LibraryMethods.Models.balancyGetFloatParam(model, paramName);
        }
        
        private static bool GetBoolParam(IntPtr model, string paramName)
        {
            return LibraryMethods.Models.balancyGetBoolParam(model, paramName);
        }
        
        private static string GetStringParam(IntPtr model, string paramName)
        {
            return GetStringFromIntPtr(LibraryMethods.Models.balancyGetStringParam(model, paramName));
        }
        
        public static T GetModelByUnnyId<T>(string unnyId) where T: BaseModel
        {
            var pointer = GetModelByUnnyId(unnyId);
            if (pointer == IntPtr.Zero)
                return null;

            var templateName = GetStringParam(pointer, "unnyTemplateName");
            Debug.LogError($"TEMPLATE NAME = {templateName}");
            var modelBase = Marshal.PtrToStructure<T>(pointer);
            return modelBase;
        }

        public static IntPtr GetModelByUnnyId(string unnyId)
        {
            return LibraryMethods.Models.balancyGetModelByUnnyId(unnyId);
        }
        
        private static int[] GetIntArrayParam(IntPtr instance, string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetIntArrayParam(instance, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
            {
                return Array.Empty<int>();
            }

            int[] result = new int[size];
            Marshal.Copy(ptr, result, 0, size);
            return result;
        }

        private static float[] GetFloatArrayParam(IntPtr instance, string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetFloatArrayParam(instance, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
            {
                return Array.Empty<float>();
            }

            float[] result = new float[size];
            Marshal.Copy(ptr, result, 0, size);
            return result;
        }

        private static bool[] GetBoolArrayParam(IntPtr instance, string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetBoolArrayParam(instance, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
            {
                return Array.Empty<bool>();
            }

            byte[] byteResult = new byte[size];
            Marshal.Copy(ptr, byteResult, 0, size);
            bool[] boolResult = Array.ConvertAll(byteResult, b => b != 0);
            return boolResult;
        }


        private static string[] GetStringArrayParam(IntPtr instance, string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetStringArrayParam(instance, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
            {
                return Array.Empty<string>();
            }

            IntPtr[] ptrArray = new IntPtr[size];
            Marshal.Copy(ptr, ptrArray, 0, size);

            string[] result = new string[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = Marshal.PtrToStringAnsi(ptrArray[i]);
            }

            LibraryMethods.Models.balancyFreeStringArray(ptr);
            return result;
        }

        private static string GetStringFromIntPtr(IntPtr ptr)
        {
            return Marshal.PtrToStringAnsi(ptr);
        }
    }
}