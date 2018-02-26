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
            return (IBatchEventProcessor<T>)Activator.CreateInstance(batchEventProcessorType, new object[] { dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, batchStartAwareProxy });
        }
    }
}
