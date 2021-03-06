﻿using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
    /// and delegating the available events to an <see cref="IEventHandler{T}"/>.
    /// 
    /// If the <see cref="IEventHandler{T}"/> also implements <see cref="ILifecycleAware"/> it will be notified just after the thread
    /// is started and just before the thread is shutdown.
    /// 
    /// This class is kept mainly for compatibility reasons.
    /// 
    /// Consider using <see cref="BatchEventProcessorFactory.Create{T}"/> to create your <see cref="IEventProcessor"/>.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    public class BatchEventProcessor<T> : BatchEventProcessor<T, IDataProvider<T>, ISequenceBarrier, IEventHandler<T>, BatchEventProcessorFactory.BatchStartAware>
        where T : class
    {
        /// <summary>
        /// Construct a BatchEventProcessor that will automatically track the progress by updating its sequence when
        /// the <see cref="IEventHandler{T}.OnEvent"/> method returns.
        /// 
        /// Consider using <see cref="BatchEventProcessorFactory.Create{T}"/> to create your <see cref="IEventProcessor"/>.
        /// </summary>
        /// <param name="dataProvider">dataProvider to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        public BatchEventProcessor(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
            : base(dataProvider, sequenceBarrier, eventHandler, new BatchEventProcessorFactory.BatchStartAware(eventHandler))
        {
        }
    }

    /// <summary>
    /// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
    /// and delegating the available events to an <see cref="IEventHandler{T}"/>.
    /// 
    /// If the <see cref="IEventHandler{T}"/> also implements <see cref="ILifecycleAware"/> it will be notified just after the thread
    /// is started and just before the thread is shutdown.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <typeparam name="TDataProvider">the type of the <see cref="IDataProvider{T}"/> used.</typeparam>
    /// <typeparam name="TSequenceBarrier">the type of the <see cref="ISequenceBarrier"/> used.</typeparam>
    /// <typeparam name="TEventHandler">the type of the <see cref="IEventHandler{T}"/> used.</typeparam>
    /// <typeparam name="TBatchStartAware">the type of the <see cref="IBatchStartAware"/> used.</typeparam>
    public class BatchEventProcessor<T, TDataProvider, TSequenceBarrier, TEventHandler, TBatchStartAware> : IBatchEventProcessor<T>
        where T : class

        where TDataProvider : IDataProvider<T>
        where TSequenceBarrier : ISequenceBarrier
        where TEventHandler : IEventHandler<T>
        where TBatchStartAware : IBatchStartAware
    {
        private static class RunningStates
        {
            public const int Idle = 0;
            public const int Halted = Idle + 1;
            public const int Running = Halted + 1;
        }

        // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TDataProvider _dataProvider;
        private TSequenceBarrier _sequenceBarrier;
        private TEventHandler _eventHandler;
        private TBatchStartAware _batchStartAware;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private readonly Sequence _sequence = new Sequence();
        private readonly ITimeoutHandler _timeoutHandler;
        private readonly ManualResetEventSlim _started = new ManualResetEventSlim();
        private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler();
        private volatile int _running;

        /// <summary>
        /// Construct a BatchEventProcessor that will automatically track the progress by updating its sequence when
        /// the <see cref="IEventHandler{T}.OnEvent"/> method returns.
        /// 
        /// Consider using <see cref="BatchEventProcessorFactory.Create{T}"/> to create your <see cref="IEventProcessor"/>.
        /// </summary>
        /// <param name="dataProvider">dataProvider to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        /// <param name="batchStartAware"></param>
        public BatchEventProcessor(TDataProvider dataProvider, TSequenceBarrier sequenceBarrier, TEventHandler eventHandler, TBatchStartAware batchStartAware)
        {
            _dataProvider = dataProvider;
            _sequenceBarrier = sequenceBarrier;
            _eventHandler = eventHandler;
            _batchStartAware = batchStartAware;

            if (eventHandler is ISequenceReportingEventHandler<T> sequenceReportingEventHandler)
                sequenceReportingEventHandler.SetSequenceCallback(_sequence);
            
            _timeoutHandler = eventHandler as ITimeoutHandler;
        }

        /// <summary>
        /// <see cref="IEventProcessor.Sequence"/>
        /// </summary>
        public ISequence Sequence => _sequence;

        /// <summary>
        /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
        /// It will call <see cref="ISequenceBarrier.Alert"/> to notify the thread to check status.
        /// </summary>
        public void Halt()
        {
            _running = RunningStates.Halted;
            _sequenceBarrier.Alert();
        }

        /// <summary>
        /// <see cref="IEventProcessor.IsRunning"/>
        /// </summary>
        public bool IsRunning => _running != RunningStates.Idle;

        /// <summary>
        /// Set a new <see cref="IExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IEventHandler{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        public void SetExceptionHandler(IExceptionHandler<T> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        }

        /// <summary>
        /// Waits before the event processor enters the <see cref="IsRunning"/> state.
        /// </summary>
        /// <param name="timeout">maximum wait duration</param>
        public void WaitUntilStarted(TimeSpan timeout)
        {
            _started.Wait(timeout);
        }

        /// <summary>
        /// It is ok to have another thread rerun this method after a halt().
        /// </summary>
        public void Run()
        {
#pragma warning disable 420
            if (Interlocked.CompareExchange(ref _running, RunningStates.Running, RunningStates.Idle) == RunningStates.Running)
#pragma warning restore 420
            {
                throw new InvalidOperationException("Thread is already running");
            }
            _sequenceBarrier.ClearAlert();

            NotifyStart();

            try
            {
                if (_running == RunningStates.Halted)
                {
                    return;
                }

                T evt = null;
                var nextSequence = _sequence.Value + 1L;

                while (true)
                {
                    try
                    {
                        var availableSequence = _sequenceBarrier.WaitFor(nextSequence);

                        _batchStartAware.OnBatchStart(availableSequence - nextSequence + 1);

                        while (nextSequence <= availableSequence)
                        {
                            evt = _dataProvider[nextSequence];
                            _eventHandler.OnEvent(evt, nextSequence, nextSequence == availableSequence);
                            nextSequence++;
                        }

                        _sequence.SetValue(availableSequence);
                    }
                    catch (TimeoutException)
                    {
                        NotifyTimeout(_sequence.Value);
                    }
                    catch (AlertException)
                    {
                        if (_running != RunningStates.Running)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _exceptionHandler.HandleEventException(ex, nextSequence, evt);
                        _sequence.SetValue(nextSequence);
                        nextSequence++;
                    }
                }
            }
            finally
            {
                NotifyShutdown();
                _running = RunningStates.Idle;
            }
        }

        private void NotifyTimeout(long availableSequence)
        {
            try
            {
                _timeoutHandler?.OnTimeout(availableSequence);
            }
            catch (Exception ex)
            {
                _exceptionHandler.HandleEventException(ex, availableSequence, null);
            }
        }

        private void NotifyStart()
        {
            var lifecycleAware = _eventHandler as ILifecycleAware;
            if (lifecycleAware != null)
            {
                try
                {
                    lifecycleAware.OnStart();
                }
                catch (Exception e)
                {
                    _exceptionHandler.HandleOnStartException(e);
                }
            }
            
            _started.Set();
        }

        private void NotifyShutdown()
        {
            var lifecycleAware = _eventHandler as ILifecycleAware;
            if (lifecycleAware != null)
            {
                try
                {
                    lifecycleAware.OnShutdown();
                }
                catch (Exception e)
                {
                    _exceptionHandler.HandleOnShutdownException(e);
                }
            }

            _started.Reset();
        }
    }
}
