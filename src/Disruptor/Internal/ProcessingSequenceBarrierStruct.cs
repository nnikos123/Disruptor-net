using System.Runtime.CompilerServices;

namespace Disruptor.Internal
{
    internal struct ProcessingSequenceBarrierStruct<TSequencer, TWaitStrategy> : ISequenceBarrier
        where TSequencer : ISequencer
        where TWaitStrategy : IWaitStrategy
    {
        private readonly ProcessingSequenceBarrier<TSequencer, TWaitStrategy> _sequenceBarrier;

        public ProcessingSequenceBarrierStruct(ProcessingSequenceBarrier<TSequencer, TWaitStrategy> sequenceBarrier)
        {
            _sequenceBarrier = sequenceBarrier;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long WaitFor(long sequence)
        {
            return _sequenceBarrier.WaitFor(sequence);
        }

        public long Cursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _sequenceBarrier.Cursor; }
        }

        public bool IsAlerted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _sequenceBarrier.IsAlerted; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Alert()
        {
            _sequenceBarrier.Alert();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAlert()
        {
            _sequenceBarrier.ClearAlert();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckAlert()
        {
            _sequenceBarrier.CheckAlert();
        }
    }
}
