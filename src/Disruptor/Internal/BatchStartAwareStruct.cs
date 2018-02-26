namespace Disruptor.Internal
{
    public struct BatchStartAwareStruct : IBatchStartAware
    {
        private readonly IBatchStartAware _eventHandler;

        public BatchStartAwareStruct(object eventHandler)
        {
            _eventHandler = eventHandler as IBatchStartAware;
        }

        public void OnBatchStart(long batchSize)
        {
            _eventHandler?.OnBatchStart(batchSize);
        }
    }
}
