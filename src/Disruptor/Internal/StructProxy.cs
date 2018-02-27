using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Disruptor.Internal
{
    internal static class StructProxy
    {
        private static readonly ModuleBuilder _moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(nameof(StructProxy) + ".DynamicAssembly"), AssemblyBuilderAccess.Run)
                                                                              .DefineDynamicModule(nameof(StructProxy));

        private static readonly Dictionary<ProxyKey, Type> _proxyTypes = new Dictionary<ProxyKey, Type>();

        public static IBatchStartAware CreateBatchStartAware<T>(IEventHandler<T> eventHandler)
            => eventHandler is IBatchStartAware batchStartAware ? batchStartAware : new NoopBatchStartAwareStruct();

        public static TInterface CreateProxyInstance<TInterface>(TInterface target)
        {
            var targetType = target.GetType();

            if (targetType.IsValueType)
                return target;

            Type proxyType;
            lock (_proxyTypes)
            {
                var proxyKey = new ProxyKey(typeof(TInterface), targetType);
                if (!_proxyTypes.TryGetValue(proxyKey, out proxyType))
                {
                    proxyType = GenerateStructProxyType(typeof(TInterface), targetType);
                    _proxyTypes.Add(proxyKey, proxyType);
                }
            }

            if (proxyType == null)
                return target;

            return (TInterface)Activator.CreateInstance(proxyType, target);
        }

        private static Type GenerateStructProxyType(Type interfaceType, Type targetType)
        {
            if (!targetType.IsPublic)
                return null;

            var typeBuilder = _moduleBuilder.DefineType($"StructProxy_{targetType.Name}_{Guid.NewGuid():N}", TypeAttributes.Public, typeof(ValueType));
            typeBuilder.AddInterfaceImplementation(interfaceType);

            var field = typeBuilder.DefineField("_target", targetType, FieldAttributes.Private);

            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { targetType });

            var constructorGenerator = constructor.GetILGenerator();
            constructorGenerator.Emit(OpCodes.Ldarg_0);
            constructorGenerator.Emit(OpCodes.Ldarg_1);
            constructorGenerator.Emit(OpCodes.Stfld, field);
            constructorGenerator.Emit(OpCodes.Ret);

            var interfaceMap = targetType.GetInterfaceMap(interfaceType);

            for (var index = 0; index < interfaceMap.InterfaceMethods.Length; index++)
            {
                var interfaceMethodInfo = interfaceMap.InterfaceMethods[index];
                var targetMethodInfo = interfaceMap.TargetMethods[index];
                var parameters = interfaceMethodInfo.GetParameters();

                var method = typeBuilder.DefineMethod(interfaceMethodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, interfaceMethodInfo.ReturnType, parameters.Select(x => x.ParameterType).ToArray());
                method.SetImplementationFlags(method.GetMethodImplementationFlags() | MethodImplAttributes.AggressiveInlining);

                var methodGenerator = method.GetILGenerator();
                methodGenerator.Emit(OpCodes.Ldarg_0);
                methodGenerator.Emit(OpCodes.Ldfld, field);

                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    methodGenerator.Emit(OpCodes.Ldarg_S, (byte)parameterIndex + 1);
                }
                methodGenerator.Emit(OpCodes.Call, targetMethodInfo);
                methodGenerator.Emit(OpCodes.Ret);
            }

            return typeBuilder.CreateTypeInfo();
        }

        private struct ProxyKey : IEquatable<ProxyKey>
        {
            private readonly Type _interfaceType;
            private readonly Type _targetType;

            public ProxyKey(Type interfaceType, Type targetType)
            {
                _interfaceType = interfaceType;
                _targetType = targetType;
            }

            public bool Equals(ProxyKey other)
            {
                return _interfaceType == other._interfaceType && _targetType == other._targetType;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_interfaceType != null ? _interfaceType.GetHashCode() : 0) * 397) ^ (_targetType != null ? _targetType.GetHashCode() : 0);
                }
            }
        }
    }
}
