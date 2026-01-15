using System;
using Balancy.Models.SmartObjects;

namespace Balancy.Data.SmartObjects
{
    public class SmartInfo : Balancy.Data.BaseData
    {

		private SmartList<Balancy.Data.SmartObjects.OfferInfo> _gameOffers;
		private SmartList<Balancy.Data.SmartObjects.OfferGroupInfo> _gameOfferGroups;
		private SmartList<Balancy.Data.SmartObjects.EventInfo> _gameEvents;
		private int _activeEventsCount;
		private int _activeOffersCount;
		private int _activeSingleOffersCount;
		private int _activeGroupOffersCount;


		public SmartList<Balancy.Data.SmartObjects.OfferInfo> GameOffers => _gameOffers;
		public SmartList<Balancy.Data.SmartObjects.OfferGroupInfo> GameOfferGroups => _gameOfferGroups;
		public SmartList<Balancy.Data.SmartObjects.EventInfo> GameEvents => _gameEvents;
		public int ActiveEventsCount => _activeEventsCount;
		public int ActiveOffersCount => _activeOffersCount;
		public int ActiveSingleOffersCount => _activeSingleOffersCount;
		public int ActiveGroupOffersCount => _activeGroupOffersCount;
        
        public override void InitData()
        {
            base.InitData();

			_gameOffers = GetListBaseDataParam<Balancy.Data.SmartObjects.OfferInfo>("gameOffers");
			_gameOfferGroups = GetListBaseDataParam<Balancy.Data.SmartObjects.OfferGroupInfo>("gameOfferGroups");
			_gameEvents = GetListBaseDataParam<Balancy.Data.SmartObjects.EventInfo>("gameEvents");

			InitAndSubscribeForParamChange("activeEventsCount", Update_activeEventsCount);
			InitAndSubscribeForParamChange("activeOffersCount", Update_activeOffersCount);
			InitAndSubscribeForParamChange("activeSingleOffersCount", Update_activeSingleOffersCount);
			InitAndSubscribeForParamChange("activeGroupOffersCount", Update_activeGroupOffersCount);
        }

        private void Update_activeEventsCount() { _activeEventsCount = GetIntParam("activeEventsCount"); }
		private void Update_activeOffersCount() { _activeOffersCount = GetIntParam("activeOffersCount"); }
		private void Update_activeSingleOffersCount() { _activeSingleOffersCount = GetIntParam("activeSingleOffersCount"); }
		private void Update_activeGroupOffersCount() { _activeGroupOffersCount = GetIntParam("activeGroupOffersCount"); }

        public Balancy.Data.SmartObjects.EventInfo FindEventInfo(IntPtr ptr) => FindElementInList(_gameEvents, ptr);
        public Balancy.Data.SmartObjects.OfferInfo FindOfferInfo(IntPtr ptr) => FindElementInList(_gameOffers, ptr);
        public Balancy.Data.SmartObjects.OfferGroupInfo FindOfferGroupInfo(IntPtr ptr) => FindElementInList(_gameOfferGroups, ptr);

        public EventInfo GetGameEvent(GameEvent gameEvent)
        {
	        foreach (var eventInfo in GameEvents)
	        {
		        if (eventInfo?.GameEventUnnyId == gameEvent.UnnyId)
			        return eventInfo;
	        }

	        return null;
        }

        public bool HasGameEvent(GameEvent gameEvent)
        {
	        return GetGameEvent(gameEvent) != null;
        }

        public Balancy.Data.SmartObjects.OfferInfo FindOfferInfo(string instanceId)
        {
	        foreach (var offerInfo in GameOffers)
	        {
		        if (offerInfo?.InstanceId == instanceId)
			        return offerInfo;
	        }

	        return null;
        }
        
        public Balancy.Data.SmartObjects.OfferGroupInfo FindOfferGroupInfo(string instanceId)
		{
	        foreach (var offerInfo in GameOfferGroups)
	        {
		        if (offerInfo?.InstanceId == instanceId)
			        return offerInfo;
	        }

	        return null;
		}
    }
}
