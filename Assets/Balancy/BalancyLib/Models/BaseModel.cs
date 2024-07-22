using System;

namespace Balancy.Models
{
    public class BaseModel : JsonBasedObject
    {
        private string _unnyTemplateName;
        private string _unnyId;

        public string UnnyTemplateName => _unnyTemplateName;
        public string UnnyId => _unnyId;

        public override int GetHashCode()
        {
            return _unnyId.GetHashCode();
        }

        public void SetData(IntPtr p, string unnyId, string templateName)
        {
            base.SetData(p);
            _unnyId = unnyId;
            _unnyTemplateName = templateName;
        }

        public static string GetClassName<T>()
        {
            var fullName = typeof(T).FullName;
            var className = fullName?.Replace("Balancy.Models.", "");
            return className;
        }

        protected T GetModelByUnnyId<T>(string unnyId) where T: BaseModel
        {
            return CMS.GetModelByUnnyId<T>(unnyId);
        }

        protected T[] GetModelsByUnnyIds<T>(string[] unnyIds) where T : BaseModel
        {
            if (unnyIds == null || unnyIds.Length == 0)
                return Array.Empty<T>();

            var storeItems = new T[unnyIds.Length];
            for (int i = 0; i < unnyIds.Length; i++)
                storeItems[i] = GetModelByUnnyId<T>(unnyIds[i]);
            return storeItems;
        }
    }
}