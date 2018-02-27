using Disruptor.Internal;

namespace Disruptor
{
    /// <summary>
    /// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s,
    ///  using the given WaitStrategy.
    /// </summary>
    internal sealed class ProcessingSequenceBarrier : ProcessingSequenceBarrier<ISequencer, IWaitStrategy>
    {
        public ProcessingSequenceBarrier(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
            : base(sequencer, waitStrategy, cursorSequence, dependentSequences)
        {
        }

        public static ISequenceBarrier Create(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            return DisruptorTypeFactory.CreateSequenceBarrier(sequencer, waitStrategy, cursorSequence, dependentSequences);
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
        // ReSharper disable once FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TSequencer _sequencer;
        // ReSharper disable once FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TWaitStrategy _waitStrategy;
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
            if (_alerted)
            {
                throw AlertException.Instance;
            }

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
