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
        private static readonly Dictionary<Type, Type> _waitStrategyProxyTypes = new Dictionary<Type, Type>();

        public static IBatchStartAware CreateBatchStartAware<T>(IEventHandler<T> eventHandler)
            => eventHandler is IBatchStartAware batchStartAware ? batchStartAware : new NoopBatchStartAwareStruct();

        public static ISequenceBarrier CreateSequenceBarrier(ISequenceBarrier sequenceBarrier)
            => sequenceBarrier is ProcessingSequenceBarrier processingSequenceBarrier ? new ProcessingSequenceBarrierStruct(processingSequenceBarrier) : sequenceBarrier;

        public static IDataProvider<T> CreateDataProvider<T>(IDataProvider<T> dataProvider)
            where T : class
            => dataProvider is RingBuffer<T> ringBuffer ? new RingBufferDataProviderStruct<T>(ringBuffer) : dataProvider;

        public static IEventHandler<T> CreateEventHandler<T>(IEventHandler<T> eventHandler)
        {
            return CreateProxyInstance(eventHandler, _eventHandlerProxyTypes, GenerateEventHandlerProxyType<T>);
        }

        private static Type GenerateEventHandlerProxyType<T>(Type eventHandlerType)
        {
            if (!eventHandlerType.IsPublic)
                return null;

            var onEventMethodInfo = eventHandlerType.GetMethod(nameof(IEventHandler<T>.OnEvent));
            if (onEventMethodInfo == null)
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

            var onEventMethod = typeBuilder.DefineMethod(nameof(IEventHandler<T>.OnEvent), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, typeof(void), new[] { typeof(T), typeof(long), typeof(bool) });
            onEventMethod.SetImplementationFlags(onEventMethod.GetMethodImplementationFlags() | MethodImplAttributes.AggressiveInlining);

            var onEventGenerator = onEventMethod.GetILGenerator();
            onEventGenerator.Emit(OpCodes.Ldarg_0);
            onEventGenerator.Emit(OpCodes.Ldfld, field);
            onEventGenerator.Emit(OpCodes.Ldarg_1);
            onEventGenerator.Emit(OpCodes.Ldarg_2);
            onEventGenerator.Emit(OpCodes.Ldarg_3);
            onEventGenerator.Emit(OpCodes.Call, onEventMethodInfo);
            onEventGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo();
        }

        public static IWaitStrategy CreateWaitStrategy(IWaitStrategy waitStrategy)
        {
            return CreateProxyInstance(waitStrategy, _waitStrategyProxyTypes, GenerateWaitStrategyProxyType);
        }

        private static Type GenerateWaitStrategyProxyType(Type waitStrategyType)
        {
            if (!waitStrategyType.IsPublic)
                return null;

            var waitForMethodInfo = waitStrategyType.GetMethod(nameof(IWaitStrategy.WaitFor));
            if (waitForMethodInfo == null)
                return null;

            var signalAllWhenBlockingMethodInfo = waitStrategyType.GetMethod(nameof(IWaitStrategy.SignalAllWhenBlocking));
            if (signalAllWhenBlockingMethodInfo == null)
                return null;

            var typeBuilder = _moduleBuilder.DefineType($"StructProxy_{waitStrategyType.Name}_{Guid.NewGuid():N}", TypeAttributes.Public, typeof(ValueType));
            typeBuilder.AddInterfaceImplementation(typeof(IWaitStrategy));

            var field = typeBuilder.DefineField("_waitStrategy", waitStrategyType, FieldAttributes.Private);

            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { waitStrategyType });

            var constructorGenerator = constructor.GetILGenerator();
            constructorGenerator.Emit(OpCodes.Ldarg_0);
            constructorGenerator.Emit(OpCodes.Ldarg_1);
            constructorGenerator.Emit(OpCodes.Stfld, field);
            constructorGenerator.Emit(OpCodes.Ret);

            var waitForMethod = typeBuilder.DefineMethod(nameof(IWaitStrategy.WaitFor), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, typeof(long), new[] { typeof(long), typeof(Sequence), typeof(ISequence), typeof(ISequenceBarrier) });
            //waitForMethod.SetImplementationFlags(waitForMethod.GetMethodImplementationFlags() | MethodImplAttributes.AggressiveInlining);

            var waitForGenerator = waitForMethod.GetILGenerator();
            waitForGenerator.Emit(OpCodes.Ldarg_0);
            waitForGenerator.Emit(OpCodes.Ldfld, field);
            waitForGenerator.Emit(OpCodes.Ldarg_1);
            waitForGenerator.Emit(OpCodes.Ldarg_2);
            waitForGenerator.Emit(OpCodes.Ldarg_3);
            waitForGenerator.Emit(OpCodes.Ldarg_S, (byte)4);
            waitForGenerator.Emit(OpCodes.Call, waitForMethodInfo);
            waitForGenerator.Emit(OpCodes.Ret);

            var signalAllWhenBlockingMethod = typeBuilder.DefineMethod(nameof(IWaitStrategy.SignalAllWhenBlocking), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, typeof(void), Type.EmptyTypes);
            signalAllWhenBlockingMethod.SetImplementationFlags(signalAllWhenBlockingMethod.GetMethodImplementationFlags() | MethodImplAttributes.AggressiveInlining);

            var signalAllWhenBlockingGenerator = signalAllWhenBlockingMethod.GetILGenerator();
            signalAllWhenBlockingGenerator.Emit(OpCodes.Ldarg_0);
            signalAllWhenBlockingGenerator.Emit(OpCodes.Ldfld, field);
            signalAllWhenBlockingGenerator.Emit(OpCodes.Call, signalAllWhenBlockingMethodInfo);
            signalAllWhenBlockingGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo();
        }

        private static T CreateProxyInstance<T>(T target, Dictionary<Type, Type> proxyTypes, Func<Type, Type> proxyFactory)
        {
            var targetType = target.GetType();

            Type proxyType;
            lock (proxyTypes)
            {
                if (!proxyTypes.TryGetValue(targetType, out proxyType))
                {
                    proxyType = proxyFactory.Invoke(targetType);
                    proxyTypes.Add(targetType, proxyType);
                }
            }

            if (proxyType == null)
                return target;

            return (T)Activator.CreateInstance(proxyType, target);
        }
    }
}
