using System.Runtime.CompilerServices;

namespace Disruptor.Internal
{
    internal struct RingBufferDataProviderStruct<T> : IDataProvider<T>
        where T : class
    {
        private readonly RingBuffer<T> _ringBuffer;

        public RingBufferDataProviderStruct(RingBuffer<T> ringBuffer)
        {
            _ringBuffer = ringBuffer;
        }

        public T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _ringBuffer[sequence];
        }
    }
}