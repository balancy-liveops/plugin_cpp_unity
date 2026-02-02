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
        private static readonly Dictionary<string, object> AllSingletons = new Dictionary<string, object>();
        private static readonly Dictionary<string, ConditionalTemplateSubscription> AllConditionalTemplateSubscriptions = new Dictionary<string, ConditionalTemplateSubscription>();
        private static Dictionary<string, string> Inheritance = null;

        public static Func<string, BaseModel> OnTypeRequested = null;

        private static bool IsReadyToUse = false;

        private class ConditionalTemplateSubscription
        {
            public int CallbackId;
            public string TemplateName;
            public Delegate Callback;
        }
        
        internal static void SetIsReady(bool value)
        {
            IsReadyToUse = value;
            if (IsReadyToUse)
                RefreshInheritance();
        }
        
        public static T[] GetModels<T>(bool includeChildren) where T : BaseModel
        {
            if (!IsReadyToUse)
                return Array.Empty<T>();
            
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
            if (!IsReadyToUse)
                return null;
            
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

        internal static void CleanUp()
        {
            AllModels.Clear();

            // CRITICAL: Clear all singleton callbacks in C++ before domain reload
            // This prevents crashes when C++ tries to invoke deallocated C# delegates
            LibraryMethods.Singletons.balancyClearAllSingletonCallbacks();

            // CRITICAL: Clear all conditional template callbacks in C++ before domain reload
            LibraryMethods.ConditionalTemplates.balancyClearAllConditionalTemplateCallbacks();

            AllSingletons.Clear();
            AllConditionalTemplateSubscriptions.Clear();
            Inheritance?.Clear();
        }

        /// <summary>
        /// Get singleton instance for a given template type.
        /// Supports both regular singletons and ConditionalTemplate-based singletons.
        /// For ConditionalTemplate singletons, returns the variant with highest priority whose condition passes.
        /// </summary>
        /// <typeparam name="T">Singleton template type (must be a BaseModel)</typeparam>
        /// <returns>Current singleton instance, may change based on conditions for ConditionalTemplates</returns>
        public static SmartObjects.BalancySingleton<T> GetSingleton<T>() where T : BaseModel
        {
            if (!IsReadyToUse)
                return null;

            var templateName = JsonBasedObject.GetModelClassName<T>();

            // Check if singleton wrapper already exists
            if (!AllSingletons.TryGetValue(templateName, out var wrapper))
            {
                // Create new singleton wrapper
                wrapper = new SmartObjects.BalancySingleton<T>();
                AllSingletons[templateName] = wrapper;
            }

            return (SmartObjects.BalancySingleton<T>)wrapper;
        }

        /// <summary>
        /// Subscribe to ConditionalTemplate status changes.
        /// The callback will be invoked whenever any document of type T (or its children) becomes active or inactive.
        /// </summary>
        /// <typeparam name="T">ConditionalTemplate type (must inherit from BaseModel)</typeparam>
        /// <param name="callback">Callback invoked with (model, isActive) when status changes</param>
        /// <param name="includeChildren">If true, includes child templates in notifications (currently always includes children)</param>
        /// <returns>Subscription key for unsubscribing later</returns>
        public static string SubscribeConditionalTemplate<T>(Action<T, bool> callback, bool includeChildren = true) where T : BaseModel
        {
            if (!IsReadyToUse || callback == null)
                return null;

            var templateName = JsonBasedObject.GetModelClassName<T>();
            var subscriptionKey = $"{templateName}_{Guid.NewGuid()}";

            // Create static callback that routes to user callback
            LibraryMethods.ConditionalTemplates.ConditionalTemplateChangedCallback staticCallback =
                (templateNameFromCpp, unnyId, passed) =>
                {
                    if (!string.IsNullOrEmpty(unnyId))
                    {
                        var model = GetModelByUnnyId<T>(unnyId);
                        if (model != null)
                        {
                            callback(model, passed);
                        }
                    }
                };

            int callbackId = LibraryMethods.ConditionalTemplates.balancySubscribeConditionalTemplateChanged(
                templateName,
                staticCallback);

            if (callbackId >= 0)
            {
                AllConditionalTemplateSubscriptions[subscriptionKey] = new ConditionalTemplateSubscription
                {
                    CallbackId = callbackId,
                    TemplateName = templateName,
                    Callback = staticCallback
                };
            }

            return callbackId >= 0 ? subscriptionKey : null;
        }

        /// <summary>
        /// Unsubscribe from ConditionalTemplate status changes.
        /// </summary>
        /// <param name="subscriptionKey">Subscription key returned from SubscribeConditionalTemplate</param>
        public static void UnsubscribeConditionalTemplate(string subscriptionKey)
        {
            if (string.IsNullOrEmpty(subscriptionKey))
                return;

            if (AllConditionalTemplateSubscriptions.TryGetValue(subscriptionKey, out var subscription))
            {
                LibraryMethods.ConditionalTemplates.balancyUnsubscribeConditionalTemplateChanged(
                    subscription.TemplateName,
                    subscription.CallbackId);

                AllConditionalTemplateSubscriptions.Remove(subscriptionKey);
            }
        }

        /// <summary>
        /// Get all currently active ConditionalTemplate documents of type T.
        /// Returns documents whose conditions are currently passing.
        /// </summary>
        /// <typeparam name="T">ConditionalTemplate type (must inherit from BaseModel)</typeparam>
        /// <param name="includeChildren">If true, includes child templates (currently always includes children)</param>
        /// <returns>Array of active documents</returns>
        public static T[] GetActiveConditionalTemplates<T>(bool includeChildren = true) where T : BaseModel
        {
            if (!IsReadyToUse)
                return Array.Empty<T>();

            var templateName = JsonBasedObject.GetModelClassName<T>();
            IntPtr arrayPtr = LibraryMethods.ConditionalTemplates.balancyGetActiveConditionalTemplates(
                templateName,
                out int size);

            if (arrayPtr == IntPtr.Zero || size == 0)
                return Array.Empty<T>();

            var strs = JsonBasedObject.ReadStringArrayValues(arrayPtr, size);
            T[] result = new T[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = GetModelByUnnyId<T>(strs[i]);
            }

            return result;
        }

        private static void RefreshInheritance()
        {
            Inheritance = GetInheritance();
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
                case "SmartObjects.Conditions.Logic": return new Balancy.Models.SmartObjects.Conditions.Logic();
				// case "SmartObjects.TimeConfig": return new Balancy.Models.SmartObjects.TimeConfig();
				case "SmartObjects.Reward": return new Balancy.Models.SmartObjects.Reward();
				case "SmartObjects.GameEvent": return new Balancy.Models.SmartObjects.GameEvent();
                case "LiveOps.DailyBonus": return new Balancy.Models.LiveOps.DailyBonus();
				// case "Notifications.RemoteNotification": return new Balancy.Models.Notifications.RemoteNotification();
				// case "Notifications.LocalNotification": return new Balancy.Models.Notifications.LocalNotification();
                case "SmartObjects.GameStoreBase": return new Balancy.Models.SmartObjects.GameStoreBase();
				case "SmartObjects.SmartConfig": return new Balancy.Models.SmartObjects.SmartConfig();
				case "SmartObjects.ItemWithAmount": return new Balancy.Models.SmartObjects.ItemWithAmount();
				// case "SmartObjects.ComfortPayCheck": return new Balancy.Models.SmartObjects.ComfortPayCheck();
				// case "SmartObjects.GameConfig": return new Balancy.Models.SmartObjects.GameConfig();
				case "SmartObjects.GameOfferGroup": return new Balancy.Models.SmartObjects.GameOfferGroup();
				case "SmartObjects.GameOffer": return new Balancy.Models.SmartObjects.GameOffer();
				case "SmartObjects.StoreItem": return new Balancy.Models.SmartObjects.StoreItem();
                case "LiveOps.Store.Page": return new Balancy.Models.LiveOps.Store.Page();
                case "LiveOps.Store.Slot": return new Balancy.Models.LiveOps.Store.Slot();
                case "LiveOps.BattlePass.RewardLine": return new Balancy.Models.LiveOps.BattlePass.RewardLine();
                case "LiveOps.BattlePass.BattlePassConfig": return new Balancy.Models.LiveOps.BattlePass.BattlePassConfig();
                case "LiveOps.BattlePass.GameEvent": return new Balancy.Models.LiveOps.BattlePass.GameEvent();
                case "LiveOps.Tasks.TaskItem": return new Balancy.Models.LiveOps.Tasks.TaskItem();
                case "LiveOps.Tasks.TaskCompleteLevels": return new Balancy.Models.LiveOps.Tasks.TaskCompleteLevels();
                case "LiveOps.Tasks.TaskCompleteLevelsStreak": return new Balancy.Models.LiveOps.Tasks.TaskCompleteLevelsStreak();
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
