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
    }
}