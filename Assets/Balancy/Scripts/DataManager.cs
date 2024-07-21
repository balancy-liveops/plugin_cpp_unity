using System;
using System.Collections.Generic;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy
{
    internal class DataManager
    {
        private static readonly Dictionary<string, BaseModel> AllModels = new Dictionary<string, BaseModel>();
        private static Dictionary<string, string> Inheritance;
        
        public static T[] GetModels<T>(bool includeChildren) where T : BaseModel
        {
            //TODO add caching by type
            var templateName = BaseModel.GetClassName<T>();
            IntPtr ptr = LibraryMethods.Models.balancyGetModelUnnyIds(templateName, includeChildren, out int size);
            var strs = JsonBasedObject.ReadStringArrayValues(ptr, size);
            
            T[] result = new T[size];
            for (int i = 0; i < size; i++)
                result[i] = GetModelByUnnyId<T>(strs[i]);
            return result;
        }

        public static void RefreshAll()
        {
            AllModels.Clear();
            Inheritance = GetInheritance();
        }
        
        public static T GetModelByUnnyId<T>(string unnyId) where T: BaseModel
        {
            if (AllModels.TryGetValue(unnyId, out var model))
            {
                Debug.LogWarning($"RETURN CACEHD {unnyId}");
                return model as T;
            }

            var pointer = GetModelByUnnyId(unnyId);
            if (pointer == IntPtr.Zero)
                return null;

            var templateName = JsonBasedObject.GetTemplateName(pointer);
            
            var modelBase = CreateModel(pointer, unnyId, templateName);
            AllModels.Add(unnyId, modelBase);
            return modelBase as T;
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

        public static IntPtr GetModelByUnnyId(string unnyId)
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
                case "MyCustomTemplate": return new MyCustomTemplate();
                default:
                {
                    if (Inheritance.TryGetValue(templateName, out var parent))
                        return InstantiateByType(parent);
                    return new BaseModel();
                }
            }
        }
    }
}