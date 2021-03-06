using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class LongArrayEventHandler : IEventHandler<long[]>
    {
        private PaddedLong _value;

        public long Count { get; private set; }

        public ManualResetEvent Signal { get; private set; }

        public long Value => _value.Value;
        public long BatchesProcessedCount;

        public void Reset(ManualResetEvent signal, long expectedCount)
        {
            _value.Value = 0L;
            Signal = signal;
            Count = expectedCount;
            BatchesProcessedCount = 0;
        }

        public void OnEvent(long[] value, long sequence, bool endOfBatch)
        {
            for (var i = 0; i < value.Length; i++)
            {
                _value.Value = _value.Value + value[i];
            }

            if (endOfBatch)
                BatchesProcessedCount++;
            
            if (--Count == 0)
            {
                Signal?.Set();
            }
        }
    }
}