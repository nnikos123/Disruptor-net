using System;
using Disruptor.Internal;

namespace Disruptor
{
    public static class DisruptorTypeFactory
    {
        public static IBatchEventProcessor<T> CreateEventProcessor<T>(this IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
            where T : class
        {
            var dataProviderProxy = StructProxy.CreateDataProvider(dataProvider);
            var sequenceBarrierProxy = StructProxy.CreateSequenceBarrier(sequenceBarrier);
            var eventHandlerProxy = StructProxy.CreateEventHandler(eventHandler);
            var batchStartAwareProxy = StructProxy.CreateBatchStartAware(eventHandler);

            var batchEventProcessorType = typeof(BatchEventProcessor<,,,,>).MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), batchStartAwareProxy.GetType());
            return (IBatchEventProcessor<T>)Activator.CreateInstance(batchEventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, batchStartAwareProxy);
        }

        public static ISequenceBarrier CreateSequenceBarrier(Sequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            var waitStrategyProxy = StructProxy.CreateWaitStrategy(waitStrategy);

            var sequencerBarrierType = typeof(ProcessingSequenceBarrier<,>).MakeGenericType(typeof(Sequencer), waitStrategyProxy.GetType());
            return (ISequenceBarrier)Activator.CreateInstance(sequencerBarrierType, sequencer, waitStrategyProxy, cursorSequence, dependentSequences);

        }
    }
}
