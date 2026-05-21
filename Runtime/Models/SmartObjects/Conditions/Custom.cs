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

        /// <summary>
        /// Override this method to subscribe to game events that affect this condition.
        /// Called by the SDK when the condition is added to the evaluation tree.
        /// When a subscribed event fires, call CustomConditions.ForceUpdate(UnnyId) to trigger re-evaluation.
        /// </summary>
        public virtual void Subscribe()
        {
        }

        /// <summary>
        /// Override this method to unsubscribe from game events.
        /// Called by the SDK when the condition is removed from the evaluation tree.
        /// </summary>
        public virtual void Unsubscribe()
        {
        }

        /// <summary>
        /// Triggers re-evaluation of this condition and notifies all subscribers.
        /// Convenience method equivalent to CustomConditions.ForceUpdate(UnnyId).
        /// </summary>
        public void ForceUpdate()
        {
            CustomConditions.ForceUpdate(UnnyId);
        }
    }
}
