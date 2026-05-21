namespace Balancy.Models.SmartObjects.Conditions
{
    public class Custom : Base
    {
        /// <summary>
        /// Override this method in a partial class to provide custom evaluation logic.
        /// Called synchronously by the C++ core when this condition needs evaluation.
        /// </summary>
        /// <returns>True if the condition passes, false otherwise</returns>
        public virtual bool CanPassCustom()
        {
            return false;
        }
    }
}
