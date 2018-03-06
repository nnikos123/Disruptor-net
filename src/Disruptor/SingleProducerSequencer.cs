﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor.Internal;

namespace Disruptor
{
    public class SingleProducerSequencer : SingleProducerSequencer<IWaitStrategy>
    {
        public SingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
            : base(bufferSize, waitStrategy)
        {
        }

        public static ISequencer Create(int bufferSize, IWaitStrategy waitStrategy)
        {
            return DisruptorTypeFactory.CreateSingleProducerSequencer(bufferSize, waitStrategy);
            //return new SingleProducerSequencer(bufferSize, waitStrategy);
        }
    }

    public class SingleProducerSequencer<TWaitStrategy> : Sequencer<TWaitStrategy>
        where TWaitStrategy : IWaitStrategy
    {
        private SingleProducerSequencerFields _fields = new SingleProducerSequencerFields(Sequence.InitialCursorValue, Sequence.InitialCursorValue);

        public SingleProducerSequencer(int bufferSize, TWaitStrategy waitStrategy)
            : base(bufferSize, waitStrategy)
        {
        }

        /// <summary>
        /// Has the buffer got capacity to allocate another sequence.  This is a concurrent
        /// method so the response should only be taken as an indication of available capacity.
        /// </summary>
        /// <param name="requiredCapacity">requiredCapacity in the buffer</param>
        /// <returns>true if the buffer has the capacity to allocate the next sequence otherwise false.</returns>
        public override bool HasAvailableCapacity(int requiredCapacity)
        {
            return HasAvailableCapacity(requiredCapacity, false);
        }

        private bool HasAvailableCapacity(int requiredCapacity, bool doStore)
        {
            long nextValue = _fields.NextValue;

            long wrapPoint = (nextValue + requiredCapacity) - _bufferSize;
            long cachedGatingSequence = _fields.CachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                if (doStore)
                {
                    _cursor.SetValueVolatile(nextValue);
                }

                long minSequence = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
                _fields.CachedValue = minSequence;

                if (wrapPoint > minSequence)
                {
                    return false;
                }
            }

            return true;
        }

        internal long NextInternal(int n)
        {
            long nextValue = _fields.NextValue;

            long nextSequence = nextValue + n;
            long wrapPoint = nextSequence - _bufferSize;
            long cachedGatingSequence = _fields.CachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                _cursor.SetValueVolatile(nextValue);

                var spinWait = default(SpinWait);
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue)))
                {
                    spinWait.SpinOnce(); // LockSupport.parkNanos(1L);
                }

                _fields.CachedValue = minSequence;
            }

            _fields.NextValue = nextSequence;

            return nextSequence;
        }

        /// <summary>
        /// Claim the next event in sequence for publishing.
        /// </summary>
        /// <returns></returns>
        public override long Next()
        {
            return NextInternal(1);
        }

        /// <summary>
        /// Claim the next n events in sequence for publishing.  This is for batch event producing.  Using batch producing requires a little care and some math.
        /// <code>
        ///     int n = 10;
        ///     long hi = sequencer.next(n);
        ///     long lo = hi - (n - 1);
        ///     for (long sequence = lo; sequence &lt;= hi; sequence++) {
        ///        // Do work.
        ///     }
        ///     sequencer.publish(lo, hi);
        /// </code>
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <returns>the highest claimed sequence value</returns>
        public override long Next(int n)
        {
            if (n < 1)
            {
                throw new ArgumentException("n must be > 0");
            }

            return NextInternal(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long TryNextInternal(int n)
        {
            if (!HasAvailableCapacity(n, true))
            {
                throw InsufficientCapacityException.Instance;
            }

            var nextSequence = _fields.NextValue + n;
            _fields.NextValue = nextSequence;

            return nextSequence;
        }

        /// <summary>
        /// Attempt to claim the next event for publishing.  Will return the
        /// number of the slot if there is at least one slot available.
        /// 
        /// Have a look at <see cref="Next()"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <returns>the claimed sequence value</returns>
        /// <exception cref="InsufficientCapacityException">there is no space available in the ring buffer.</exception>
        public override long TryNext()
        {
            return TryNextInternal(1);
        }

        /// <summary>
        /// Attempt to claim the next <code>n</code> events in sequence for publishing.
        /// Will return the highest numbered slot if there is at least <code>n</code> slots
        /// available.
        /// 
        /// Have a look at <see cref="Next(int)"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <returns>the claimed sequence value</returns>
        /// <exception cref="InsufficientCapacityException">there is no space available in the ring buffer.</exception>
        public override long TryNext(int n)
        {
            if (n < 1)
            {
                throw new ArgumentException("n must be > 0");
            }

            return TryNextInternal(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryNextInternal(int n, out long sequence)
        {
            if (!HasAvailableCapacity(n, true))
            {
                sequence = default(long);
                return false;
            }

            var nextSequence = _fields.NextValue + n;
            _fields.NextValue = nextSequence;

            sequence = nextSequence;
            return true;
        }

        /// <summary>
        /// Attempt to claim the next event for publishing.  Will return the
        /// number of the slot if there is at least one slot available.
        /// 
        /// Have a look at <see cref="Next()"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <param name="sequence">the claimed sequence value</param>
        /// <returns>true of there is space available in the ring buffer, otherwise false.</returns>
        public override bool TryNext(out long sequence)
        {
            return TryNextInternal(1, out sequence);
        }

        /// <summary>
        /// Attempt to claim the next <code>n</code> events in sequence for publishing.
        /// Will return the highest numbered slot if there is at least <code>n</code> slots
        /// available.
        /// 
        /// Have a look at <see cref="Next(int)"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <param name="sequence">the claimed sequence value</param>
        /// <returns>true of there is space available in the ring buffer, otherwise false.</returns>
        public override bool TryNext(int n, out long sequence)
        {
            if (n < 1)
            {
                throw new ArgumentException("n must be > 0");
            }


            return TryNextInternal(n, out sequence);
        }

        /// <summary>
        /// Get the remaining capacity for this sequencer. return The number of slots remaining.
        /// </summary>
        public override long GetRemainingCapacity()
        {
            var nextValue = _fields.NextValue;

            var consumed = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
            var produced = nextValue;
            return BufferSize - (produced - consumed);
        }

        /// <summary>
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        public override void Claim(long sequence)
        {
            _fields.NextValue = sequence;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PublishInternal(long sequence)
        {
            _cursor.SetValue(sequence);

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="sequence">sequence to be published</param>
        public override void Publish(long sequence)
        {
            PublishInternal(sequence);
        }

        /// <summary>
        /// Batch publish sequences.  Called when all of the events have been filled.
        /// </summary>
        /// <param name="lo">first sequence number to publish</param>
        /// <param name="hi">last sequence number to publish</param>
        public override void Publish(long lo, long hi)
        {
            PublishInternal(hi);
        }

        /// <summary>
        /// Confirms if a sequence is published and the event is available for use; non-blocking.
        /// </summary>
        /// <param name="sequence">sequence of the buffer to check</param>
        /// <returns>true if the sequence is available for use, false if not</returns>
        public override bool IsAvailable(long sequence) => sequence <= _cursor.Value;

        /// <summary>
        /// Get the highest sequence number that can be safely read from the ring buffer.  Depending
        /// on the implementation of the Sequencer this call may need to scan a number of values
        /// in the Sequencer.  The scan will range from nextSequence to availableSequence.  If
        /// there are no available values <code>&amp;gt;= nextSequence</code> the return value will be
        /// <code>nextSequence - 1</code>.  To work correctly a consumer should pass a value that
        /// it 1 higher than the last sequence that was successfully processed.
        /// </summary>
        /// <param name="nextSequence">The sequence to start scanning from.</param>
        /// <param name="availableSequence">The sequence to scan to.</param>
        /// <returns>The highest value that can be safely read, will be at least <code>nextSequence - 1</code>.</returns>
        public override long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return availableSequence;
        }
    }
}