using System;
using Balancy.Models.SmartObjects;

namespace Balancy.Data.SmartObjects
{
    public class SmartInfo : Balancy.Data.BaseData 
    {
        
		private SmartList<Balancy.Data.SmartObjects.OfferInfo> _gameOffers;
		private SmartList<Balancy.Data.SmartObjects.OfferGroupInfo> _gameOfferGroups;
		private SmartList<Balancy.Data.SmartObjects.EventInfo> _gameEvents;
        
        
		public SmartList<Balancy.Data.SmartObjects.OfferInfo> GameOffers => _gameOffers;
		public SmartList<Balancy.Data.SmartObjects.OfferGroupInfo> GameOfferGroups => _gameOfferGroups;
		public SmartList<Balancy.Data.SmartObjects.EventInfo> GameEvents => _gameEvents;
        
        public override void InitData()
        {
            base.InitData();
            
			_gameOffers = GetListBaseDataParam<Balancy.Data.SmartObjects.OfferInfo>("gameOffers");
			_gameOfferGroups = GetListBaseDataParam<Balancy.Data.SmartObjects.OfferGroupInfo>("gameOfferGroups");
			_gameEvents = GetListBaseDataParam<Balancy.Data.SmartObjects.EventInfo>("gameEvents");
        }

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
    }
}
