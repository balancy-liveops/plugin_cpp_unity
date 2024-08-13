
namespace Balancy.Models
{
    public class VectorType : Balancy.Models.BaseModel 
    {
        
		private float _x;
		private float _y;
		private float _z;
        
        
		public float X => _x;
		public float Y => _y;
		public float Z => _z;
        
        public override void InitData()
        {
            base.InitData();
            
			_x = GetFloatParam("x");
			_y = GetFloatParam("y");
			_z = GetFloatParam("z");
        }
        
    }
}
