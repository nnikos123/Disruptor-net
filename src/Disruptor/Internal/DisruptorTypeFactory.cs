using System;

namespace Disruptor.Internal
{
    internal static class DisruptorTypeFactory
    {
        public static IBatchEventProcessor<T> CreateEventProcessor<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
            where T : class
        {
            var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
            var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
            var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
            var batchStartAwareProxy = StructProxy.CreateBatchStartAware(eventHandler);

            var batchEventProcessorType = typeof(BatchEventProcessor<,,,,>).MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), batchStartAwareProxy.GetType());
            return (IBatchEventProcessor<T>)Activator.CreateInstance(batchEventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, batchStartAwareProxy);
        }

        public static ISequenceBarrier CreateSequenceBarrier(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            // TODO: generate proxy for sequencer
            var waitStrategyProxy = StructProxy.CreateProxyInstance(waitStrategy);

            var sequencerBarrierType = typeof(ProcessingSequenceBarrier<,>).MakeGenericType(sequencer.GetType(), waitStrategyProxy.GetType());
            return (ISequenceBarrier)Activator.CreateInstance(sequencerBarrierType, sequencer, waitStrategyProxy, cursorSequence, dependentSequences);
        }

        public static ISequencer CreateSingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
        {
            var waitStrategyProxy = StructProxy.CreateProxyInstance(waitStrategy);

            var sequencerType = typeof(SingleProducerSequencer<>).MakeGenericType(waitStrategyProxy.GetType());
            return (ISequencer)Activator.CreateInstance(sequencerType, bufferSize, waitStrategyProxy);
        }

        public static ISequencer CreateMultiProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
        {
            var waitStrategyProxy = StructProxy.CreateProxyInstance(waitStrategy);

            var sequencerType = typeof(MultiProducerSequencer).MakeGenericType(waitStrategyProxy.GetType());
            return (ISequencer)Activator.CreateInstance(sequencerType, bufferSize, waitStrategyProxy);
        }
    }
}
