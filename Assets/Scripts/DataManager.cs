using System;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy
{
    public class DataManager
    {
        // private void TestParams()
        // {
        //     var model = GetModelByUnnyId("22");
        //     Debug.LogError($"Int param = {GetIntParam(model, "paramInt")}");
        //     Debug.LogError($"Float param = {GetFloatParam(model, "paramFloat")}");
        //     Debug.LogError($"String param = {GetStringParam(model, "paramString")}");
        //
        //     Debug.LogError($"Bool true param = {GetBoolParam(model, "boolTrue")}");
        //     Debug.LogError($"Bool false param = {GetBoolParam(model, "boolFalse")}");
        //
        //     int[] intArray = GetIntArrayParam(model, "paramIntArray");
        //     Debug.LogError($"Int array param = {string.Join(", ", intArray)}");
        //
        //     float[] floatArray = GetFloatArrayParam(model, "paramFloatArray");
        //     Debug.LogError($"Float array param = {string.Join(", ", floatArray)}");
        //
        //     bool[] boolArray = GetBoolArrayParam(model, "paramBoolArray");
        //     Debug.LogError($"Bool array param = {string.Join(", ", boolArray)}");
        //
        //     string[] stringArray = GetStringArrayParam(model, "paramStringArray");
        //     Debug.LogError($"String array param = {string.Join(", ", stringArray)}");
        // }
        
        public static T GetModelByUnnyId<T>(string unnyId) where T: BaseModel
        {
            var pointer = GetModelByUnnyId(unnyId);
            if (pointer == IntPtr.Zero)
                return null;

            var templateName = BaseModel.GetTemplateName(pointer);
            Debug.LogError($"TEMPLATE NAME = {templateName}");
            var modelBase = CreateModel(pointer, unnyId, templateName);
            return modelBase as T;
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
                default: return new BaseModel();
            }
        }
    }
}