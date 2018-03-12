using System.Threading;

namespace Disruptor
{
    public class SequenceLhsPadding
    {
        protected long _p1, _p2, _p3, _p4, _p5, _p6, _p7;
    }

    public class SequenceValue : SequenceLhsPadding
    {
        protected long _value;
    }

    public class SequenceRhsPadding : SequenceValue
    {
        protected long _p9, _p10, _p11, _p12, _p13, _p14, _p15;
    }

    /// <summary>
    /// <p>Concurrent sequence class used for tracking the progress of
    /// the ring buffer and event processors.Support a number
    /// of concurrent operations including CAS and order writes.</p>
    ///
    /// <p>Also attempts to be more efficient with regards to false
    /// sharing by adding padding around the volatile field.</p>
    /// </summary>
    public class Sequence : SequenceRhsPadding, ISequence
    {
        /// <summary>
        /// Set to -1 as sequence starting point
        /// </summary>
        public const long InitialCursorValue = -1;

        /// <summary>
        /// Construct a new sequence counter that can be tracked across threads.
        /// </summary>
        /// <param name="initialValue">initial value for the counter</param>
        public Sequence(long initialValue = InitialCursorValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Current sequence number
        /// </summary>
        public long Value => Volatile.Read(ref _value);

        /// <summary>
        /// Perform an ordered write of this sequence.  The intent is
        /// a Store/Store barrier between this write and any previous
        /// store.
        /// </summary>
        /// <param name="value">The new value for the sequence.</param>
        public void SetValue(long value)
        {
            // no synchronization required, the CLR memory model prevents Store/Store re-ordering
            _value = value;
        }

        /// <summary>
        /// Performs a volatile write of this sequence.  The intent is a Store/Store barrier between this write and any previous
        /// write and a Store/Load barrier between this write and any subsequent volatile read. 
        /// </summary>
        /// <param name="value"></param>
        public void SetValueVolatile(long value)
        {
            Volatile.Write(ref _value, value);
        }

        /// <summary>
        /// Atomically set the value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expectedSequence">the expected value for the sequence</param>
        /// <param name="nextSequence">the new value for the sequence</param>
        /// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
        public bool CompareAndSet(long expectedSequence, long nextSequence)
        {
            return Interlocked.CompareExchange(ref _value, nextSequence, expectedSequence) == expectedSequence;
        }

        /// <summary>
        /// Value of the <see cref="Sequence"/> as a String.
        /// </summary>
        /// <returns>String representation of the sequence.</returns>
        public override string ToString()
        {
            return _value.ToString();
        }

        ///<summary>
        /// Increments the sequence and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented sequence</returns>
        public long IncrementAndGet()
        {
            return Interlocked.Increment(ref _value);
        }

        ///<summary>
        /// Increments the sequence and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented sequence</returns>
        public long AddAndGet(long value)
        {
            return Interlocked.Add(ref _value, value);
        }
    }
}
