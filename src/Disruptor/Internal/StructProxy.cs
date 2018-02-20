using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Disruptor.Internal
{
    internal static class StructProxy
    {
        private static readonly ModuleBuilder _moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(nameof(StructProxy) + ".DynamicAssembly"), AssemblyBuilderAccess.Run)
                                                                              .DefineDynamicModule(nameof(StructProxy));

        private static readonly Dictionary<Type, Type> _eventHandlerProxyTypes = new Dictionary<Type, Type>();

        public static IBatchStartAware CreateBatchStartAware<T>(IEventHandler<T> eventHandler)
            => eventHandler is IBatchStartAware batchStartAware ? batchStartAware : new NoopBatchStartAwareStruct();

        public static ISequenceBarrier CreateSequenceBarrier(ISequenceBarrier sequenceBarrier)
            => sequenceBarrier is ProcessingSequenceBarrier processingSequenceBarrier ? new ProcessingSequenceBarrierStruct(processingSequenceBarrier) : sequenceBarrier;

        public static IDataProvider<T> CreateDataProvider<T>(IDataProvider<T> dataProvider)
            where T : class
            => dataProvider is RingBuffer<T> ringBuffer ? new RingBufferDataProviderStruct<T>(ringBuffer) : dataProvider;

        public static IEventHandler<T> CreateEventHandler<T>(IEventHandler<T> eventHandler)
        {
            var eventHandlerType = eventHandler.GetType();

            var proxyType = GetOrGenerateProxyType<T>(eventHandlerType);
            if (proxyType == null)
                return eventHandler;

            return (IEventHandler<T>)Activator.CreateInstance(proxyType, new[] { eventHandler });
        }

        private static Type GetOrGenerateProxyType<T>(Type eventHandlerType)
        {
            lock (_eventHandlerProxyTypes)
            {
                if (!_eventHandlerProxyTypes.TryGetValue(eventHandlerType, out var proxyType))
                {
                    proxyType = GenerateProxyType<T>(eventHandlerType);
                    _eventHandlerProxyTypes.Add(eventHandlerType, proxyType);
                }
                return proxyType;
            }
        }

        private static Type GenerateProxyType<T>(Type eventHandlerType)
        {
            if (!eventHandlerType.IsPublic)
                return null;

            var eventHandlerOnEventMethod = eventHandlerType.GetMethod(nameof(IEventHandler<T>.OnEvent));
            if (eventHandlerOnEventMethod == null)
                return null;

            var typeBuilder = _moduleBuilder.DefineType($"StructProxy_{eventHandlerType.Name}_{Guid.NewGuid():N}", TypeAttributes.Public, typeof(ValueType));

            typeBuilder.AddInterfaceImplementation(typeof(IEventHandler<T>));
            var field = typeBuilder.DefineField("_eventHandler", eventHandlerType, FieldAttributes.Private);

            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { eventHandlerType });

            var constructorGenerator = constructor.GetILGenerator();
            constructorGenerator.Emit(OpCodes.Ldarg_0);
            constructorGenerator.Emit(OpCodes.Ldarg_1);
            constructorGenerator.Emit(OpCodes.Stfld, field);
            constructorGenerator.Emit(OpCodes.Ret);

            var method = typeBuilder.DefineMethod(nameof(IEventHandler<T>.OnEvent), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, typeof(void), new[] { typeof(T), typeof(long), typeof(bool) });

            var methodGenerator = method.GetILGenerator();
            methodGenerator.Emit(OpCodes.Ldarg_0);
            methodGenerator.Emit(OpCodes.Ldfld, field);
            methodGenerator.Emit(OpCodes.Ldarg_1);
            methodGenerator.Emit(OpCodes.Ldarg_2);
            methodGenerator.Emit(OpCodes.Ldarg_3);
            methodGenerator.Emit(OpCodes.Callvirt, eventHandlerOnEventMethod);
            methodGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo();
        }
    }
}
