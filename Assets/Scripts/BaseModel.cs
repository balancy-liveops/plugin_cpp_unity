namespace Balancy.Models
{
    public class BaseModel
    {
        private string unnyTemplateName;
        private string unnyId;

        public string UnnyTemplateName => unnyTemplateName;
        public string UnnyId => unnyId;
        // public int IntUnnyId => Utils.GetIntUnnyId(unnyId);

        public override int GetHashCode()
        {
            return unnyId.GetHashCode();
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
    }
}