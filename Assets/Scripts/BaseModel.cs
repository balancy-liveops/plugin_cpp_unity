using System;
using System.Runtime.InteropServices;

namespace Balancy.Models
{
    public class BaseModel
    {
        private string _unnyTemplateName;
        private string _unnyId;

        public string UnnyTemplateName => _unnyTemplateName;
        public string UnnyId => _unnyId;
        // public int IntUnnyId => Utils.GetIntUnnyId(unnyId);


        protected IntPtr Pointer;
        
        public BaseModel(IntPtr p, string unnyId, string templateName)
        {
            Pointer = p;
            _unnyId = unnyId;
            _unnyTemplateName = templateName;
        }

        public virtual void InitData()
        {
            
        }

        public override int GetHashCode()
        {
            return _unnyId.GetHashCode();
        }
		
        internal BaseModel CloneModel()
        {
            return (BaseModel)this.MemberwiseClone();
        }

		
        // public static bool operator== (BaseModel obj1, BaseModel obj2)
        // {
        // 	return string.IsNullOrEmpty(obj1?.UnnyId) || string.IsNullOrEmpty(obj2?.UnnyId)
        // 		? obj1 == obj2
        // 		: string.Equals(obj1.UnnyId, obj2.UnnyId);
        // }
        //
        // public static bool operator!= (BaseModel obj1, BaseModel obj2)
        // {
        // 	return string.IsNullOrEmpty(obj1?.UnnyId) || string.IsNullOrEmpty(obj2?.UnnyId)
        // 		? obj1 != obj2
        // 		: !string.Equals(obj1.UnnyId, obj2.UnnyId);
        // }
        
        internal static int GetIntParam(IntPtr model, string paramName)
        {
            return LibraryMethods.Models.balancyGetIntParam(model, paramName);
        }
        
        internal static float GetFloatParam(IntPtr model, string paramName)
        {
            return LibraryMethods.Models.balancyGetFloatParam(model, paramName);
        }
        
        internal static bool GetBoolParam(IntPtr model, string paramName)
        {
            return LibraryMethods.Models.balancyGetBoolParam(model, paramName);
        }
        
        internal static string GetStringParam(IntPtr model, string paramName)
        {
            return GetStringFromIntPtr(LibraryMethods.Models.balancyGetStringParam(model, paramName));
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