namespace Balancy.Models
{
#pragma warning disable 649

	public class VectorType : BaseModel
	{
		private float x;
		public float X => x;
		private float y;
		public float Y => y;
		private float z;
		public float Z => z;

		public override void InitData()
		{
			base.InitData();
			x = GetFloatParam("x");
			y = GetFloatParam("y");
			z = GetFloatParam("z");
		}
	}
#pragma warning restore 649
}