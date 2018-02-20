namespace Disruptor.Internal
{
    internal struct NoopBatchStartAwareStruct : IBatchStartAware
    {
        public void OnBatchStart(long batchSize)
        {
        }
    }
}
