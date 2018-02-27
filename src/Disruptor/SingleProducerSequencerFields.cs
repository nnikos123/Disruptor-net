using System.Runtime.InteropServices;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct SingleProducerSequencerFields
    {
        [FieldOffset(56)]
        public long NextValue;
        [FieldOffset(64)]
        public long CachedValue;

        public SingleProducerSequencerFields(long nextValue, long cachedValue)
        {
            NextValue = nextValue;
            CachedValue = cachedValue;
        }
    }
}