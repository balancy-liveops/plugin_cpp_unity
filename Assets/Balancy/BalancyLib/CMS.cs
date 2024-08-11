using System;
using System.Collections.Generic;
using Balancy.Models;
using Balancy.Models.SmartObjects;

namespace Balancy
{
    public static class CMS
    {
        private static readonly Dictionary<string, BaseModel> AllModels = new Dictionary<string, BaseModel>();
        private static Dictionary<string, string> Inheritance;

        public static Func<string, BaseModel> OnTypeRequested = null;
        
        public static T[] GetModels<T>(bool includeChildren) where T : BaseModel
        {
            //TODO add caching by type
            var templateName = JsonBasedObject.GetModelClassName<T>();
            IntPtr ptr = LibraryMethods.Models.balancyGetModelUnnyIds(templateName, includeChildren, out int size);
            var strs = JsonBasedObject.ReadStringArrayValues(ptr, size);
            
            T[] result = new T[size];
            for (int i = 0; i < size; i++)
                result[i] = GetModelByUnnyId<T>(strs[i]);
            return result;
        }

        public static T GetModelByUnnyId<T>(string unnyId) where T: BaseModel
        {
            if (AllModels.TryGetValue(unnyId, out var model))
                return model as T;

            var pointer = GetModelByUnnyId(unnyId);
            if (pointer == IntPtr.Zero)
                return null;

            var templateName = JsonBasedObject.GetTemplateName(pointer);
            
            var modelBase = CreateModel(pointer, unnyId, templateName);
            AllModels.Add(unnyId, modelBase);
            return modelBase as T;
        }

        internal static void ModelRefreshed(string unnyId)
        {
            AllModels.Remove(unnyId);
        }

        internal static void RefreshAll()
        {
            AllModels.Clear();
            Inheritance = GetInheritance();
        }

        private static Dictionary<string, string> GetInheritance()
        {
            var result = new Dictionary<string, string>();
            IntPtr ptr = LibraryMethods.General.balancyGetInheritance(out int size);
            var strs = JsonBasedObject.ReadStringArrayValues(ptr, size);

            for (int i = 0; i < size; i+=2)
                result.Add(strs[i], strs[i+1]);
            return result;
        }

        private static IntPtr GetModelByUnnyId(string unnyId)
        {
            return LibraryMethods.Models.balancyGetModelByUnnyId(unnyId);
        }
        
        private static BaseModel CreateModel(IntPtr pointer,string unnyId, string templateName)
        {
            BaseModel model = InstantiateByType(templateName);
            model.SetData(pointer, unnyId, templateName);
            model.InitData();
            return model;
        }
        
        private static BaseModel InstantiateByType(string templateName)
        {
            switch (templateName)
            {
                case "SmartObjects.Item": return new Item();
                default:
                {
                    var model = OnTypeRequested?.Invoke(templateName);
                    if (model != null)
                        return model;
                    
                    if (Inheritance.TryGetValue(templateName, out var parent))
                        return InstantiateByType(parent);
                    
                    return new BaseModel();
                }
            }
        }
    }
}
