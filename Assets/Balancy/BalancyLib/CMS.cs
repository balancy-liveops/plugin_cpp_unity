using System;
using System.Collections.Generic;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using Balancy.Models.SmartObjects.Analytics;

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
            if (unnyId == null)
            {
                UnityEngine.Debug.LogError("Trying to request for NULL unnyId");
                return null;
            }
            
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

        internal static void ModelRefreshed(string unnyId, IntPtr newPointer)
        {
            if (AllModels.TryGetValue(unnyId, out var model))
                model.RefreshData(newPointer);
        }

        internal static void RefreshAll()
        {
            List<string> keysToRemove = new List<string>();
            foreach (var kvp in AllModels)
            {
                var pointer = GetModelByUnnyId(kvp.Key);
                if (pointer == IntPtr.Zero)
                {
                    kvp.Value.SetData(IntPtr.Zero);
                    keysToRemove.Add(kvp.Key);
                    continue;
                }
                
                kvp.Value.RefreshData(pointer);
            }

            foreach (var key in keysToRemove)
                AllModels.Remove(key);
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
                case "SmartObjects.Analytics.ABTest": return new ABTest();
                case "SmartObjects.Analytics.ABTestVariant": return new ABTestVariant();
                case "SmartObjects.SegmentOption": return new SegmentOption();
                case "SmartObjects.Item": return new Item();
                case "SmartObjects.Price": return new Balancy.Models.SmartObjects.Price();
				// case "SmartObjects.TimeConfig": return new Balancy.Models.SmartObjects.TimeConfig();
				case "SmartObjects.Reward": return new Balancy.Models.SmartObjects.Reward();
				case "SmartObjects.GameEvent": return new Balancy.Models.SmartObjects.GameEvent();
				// case "Notifications.RemoteNotification": return new Balancy.Models.Notifications.RemoteNotification();
				// case "Notifications.LocalNotification": return new Balancy.Models.Notifications.LocalNotification();
				// case "SmartObjects.SmartConfig": return new Balancy.Models.SmartObjects.SmartConfig();
				case "SmartObjects.ItemWithAmount": return new Balancy.Models.SmartObjects.ItemWithAmount();
				// case "SmartObjects.ComfortPayCheck": return new Balancy.Models.SmartObjects.ComfortPayCheck();
				// case "SmartObjects.GameConfig": return new Balancy.Models.SmartObjects.GameConfig();
				case "SmartObjects.GameOfferGroup": return new Balancy.Models.SmartObjects.GameOfferGroup();
				case "SmartObjects.GameOffer": return new Balancy.Models.SmartObjects.GameOffer();
				case "SmartObjects.StoreItem": return new Balancy.Models.SmartObjects.StoreItem();
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
