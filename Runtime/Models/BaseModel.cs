using System;

namespace Balancy.Models
{
    public class BaseModel : JsonBasedObject
    {
        private string _unnyTemplateName;
        private string _unnyId;

        public string UnnyTemplateName => _unnyTemplateName;
        public string UnnyId => _unnyId;
        public int IntUnnyId => int.TryParse(_unnyId, out var intId) ? intId : 0;

        public override int GetHashCode()
        {
            return _unnyId?.GetHashCode() ?? 0;
        }

        internal void SetData(IntPtr p, string unnyId, string templateName)
        {
            // Assign the identity fields BEFORE base.SetData: the base now
            // registers this wrapper in a HashSet keyed by GetHashCode(), which
            // reads _unnyId. Setting them first guarantees a valid hash code at
            // registration time (otherwise NullReferenceException in GetHashCode).
            _unnyId = unnyId;
            _unnyTemplateName = templateName;
            base.SetData(p);
        }
    }
}