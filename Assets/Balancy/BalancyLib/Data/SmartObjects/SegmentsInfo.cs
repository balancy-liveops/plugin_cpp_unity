namespace Balancy.Data.SmartObjects
{
    public class SegmentsInfo : Balancy.Data.BaseData 
    {
        
        private SmartList<Balancy.Data.SmartObjects.SegmentInfo> _segments;
        
        
        public SmartList<Balancy.Data.SmartObjects.SegmentInfo> Segments => _segments;
        
        public override void InitData()
        {
            base.InitData();
            
            _segments = GetListBaseDataParam<Balancy.Data.SmartObjects.SegmentInfo>("segments");
        }
        
    }
}