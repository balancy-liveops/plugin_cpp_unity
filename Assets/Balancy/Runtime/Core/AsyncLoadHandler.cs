namespace Balancy
{
    public class AsyncLoadHandler
    {
        public enum Status
        {
            Loading,
            Finished,
            Cancelled
        }

        private Status _status;

        public Status GetStatus() => _status;

        public void Cancel()
        {
            if (_status == Status.Loading)
                _status = Status.Cancelled;
        }

        public void Finish()
        {
            _status = Status.Finished;
        }

        private AsyncLoadHandler()
        {
            
        }

        public static AsyncLoadHandler CreateHandler(Status status = Status.Loading)
        {
            var handler = new AsyncLoadHandler
            {
                _status = status
            };
            return handler;
        } 
    }
}
