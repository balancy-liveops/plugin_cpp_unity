namespace Balancy.Data.SmartObjects
{
    public class CustomInfo : Balancy.Data.BaseData
    {
        private SmartList<CustomEventInfo> _customEventInfo;
        private SmartList<CustomDocumentInfo> _customDocumentInfo;

        public SmartList<CustomEventInfo> CustomEventInfo => _customEventInfo;
        public SmartList<CustomDocumentInfo> CustomDocumentInfo => _customDocumentInfo;

        public override void InitData()
        {
            base.InitData();

            _customEventInfo = GetListBaseDataParam<CustomEventInfo>("customEventInfo");
            _customDocumentInfo = GetListBaseDataParam<CustomDocumentInfo>("customDocumentInfo");
        }

        /// <summary>
        /// Find a CustomEventInfo by instance ID, or create a new one if not found.
        /// </summary>
        /// <param name="instanceId">The event instance ID</param>
        /// <returns>The existing or newly created CustomEventInfo</returns>
        public CustomEventInfo FindOrCreateEventInfo(string instanceId)
        {
            foreach (var eventInfo in _customEventInfo)
            {
                if (eventInfo.InstanceId == instanceId)
                    return eventInfo;
            }

            var newEntry = _customEventInfo.Add();
            newEntry.SetInstanceId(instanceId);
            return newEntry;
        }

        /// <summary>
        /// Find a CustomEventInfo by instance ID.
        /// </summary>
        /// <param name="instanceId">The event instance ID</param>
        /// <returns>The CustomEventInfo, or null if not found</returns>
        public CustomEventInfo FindEventInfo(string instanceId)
        {
            foreach (var eventInfo in _customEventInfo)
            {
                if (eventInfo.InstanceId == instanceId)
                    return eventInfo;
            }
            return null;
        }

        /// <summary>
        /// Delete a CustomEventInfo by instance ID.
        /// </summary>
        /// <param name="instanceId">The event instance ID</param>
        /// <returns>True if found and deleted, false otherwise</returns>
        public bool DeleteEventInfo(string instanceId)
        {
            int index = _customEventInfo.FindIndex(e => e.InstanceId == instanceId);
            if (index >= 0)
            {
                _customEventInfo.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Find a CustomDocumentInfo by UnnyId, or create a new one if not found.
        /// </summary>
        /// <param name="unnyId">The document's UnnyId</param>
        /// <returns>The existing or newly created CustomDocumentInfo</returns>
        public CustomDocumentInfo FindOrCreateDocumentInfo(string unnyId)
        {
            foreach (var docInfo in _customDocumentInfo)
            {
                if (docInfo.UnnyId == unnyId)
                    return docInfo;
            }

            var newEntry = _customDocumentInfo.Add();
            newEntry.SetUnnyId(unnyId);
            return newEntry;
        }

        /// <summary>
        /// Find a CustomDocumentInfo by UnnyId.
        /// </summary>
        /// <param name="unnyId">The document's UnnyId</param>
        /// <returns>The CustomDocumentInfo, or null if not found</returns>
        public CustomDocumentInfo FindDocumentInfo(string unnyId)
        {
            foreach (var docInfo in _customDocumentInfo)
            {
                if (docInfo.UnnyId == unnyId)
                    return docInfo;
            }
            return null;
        }

        /// <summary>
        /// Delete a CustomDocumentInfo by UnnyId.
        /// </summary>
        /// <param name="unnyId">The document's UnnyId</param>
        /// <returns>True if found and deleted, false otherwise</returns>
        public bool DeleteDocumentInfo(string unnyId)
        {
            int index = _customDocumentInfo.FindIndex(d => d.UnnyId == unnyId);
            if (index >= 0)
            {
                _customDocumentInfo.RemoveAt(index);
                return true;
            }
            return false;
        }
    }
}
