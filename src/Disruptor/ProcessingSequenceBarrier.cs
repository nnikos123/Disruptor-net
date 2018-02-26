namespace Disruptor
{
    /// <summary>
    /// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s,
    ///  using the given WaitStrategy.
    /// </summary>
    internal sealed class ProcessingSequenceBarrier : ProcessingSequenceBarrier<ISequencer, IWaitStrategy>
    {
        public ProcessingSequenceBarrier(Sequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
            : base(sequencer, waitStrategy, cursorSequence, dependentSequences)
        {
        }
    }

    /// <summary>
    /// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s,
    ///  using the given WaitStrategy.
    /// </summary>
    internal class ProcessingSequenceBarrier<TSequencer, TWaitStrategy> : ISequenceBarrier
        where TWaitStrategy : IWaitStrategy
        where TSequencer : ISequencer
    {
        private readonly TSequencer _sequencer;
        private readonly TWaitStrategy _waitStrategy;
        private readonly Sequence _cursorSequence;
        private readonly ISequence _dependentSequence;
        private volatile bool _alerted;

        public ProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            _waitStrategy = waitStrategy;
            _cursorSequence = cursorSequence;
            _sequencer = sequencer;

            _dependentSequence = 0 == dependentSequences.Length ? (ISequence)cursorSequence : new FixedSequenceGroup(dependentSequences);
        }

        public long WaitFor(long sequence)
        {
            CheckAlert();

            var availableSequence = _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequence, this);

            if (availableSequence < sequence)
                return availableSequence;

            return _sequencer.GetHighestPublishedSequence(sequence, availableSequence);
        }

        public long Cursor => _dependentSequence.Value;

        public bool IsAlerted => _alerted;

        public void Alert()
        {
            _alerted = true;
            _waitStrategy.SignalAllWhenBlocking();
        }

        public void ClearAlert()
        {
            _alerted = false;
        }

        public void CheckAlert()
        {
            if(_alerted)
            {
                throw AlertException.Instance;
            }
        }
    }
}
