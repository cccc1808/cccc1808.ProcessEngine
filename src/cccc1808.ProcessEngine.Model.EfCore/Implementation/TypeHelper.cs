using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation
{
    internal static class TypeHelper
    {
        public static Type? GetTypeByAssemblyQualifiedName(
            string assemblyQualifiedName
            )
        {
            return Type.GetType(assemblyQualifiedName, throwOnError: false);
        }


        public static bool IsImplementInterface<TInterface>(Type type)
        {
            return IsImplementInterface(type, typeof(TInterface));
        }

        public static bool IsImplementInterface(
            Type type,
            Type interfaceType
            )
        {
            return type.GetInterfaces().Any(e => e == interfaceType);
        }

        public static bool TryGetGenericInterfaceParameter(
            Type type,
            Type genericInterfaceType,
            int parameterNumber,
            out Type parameterType
            )
        {
            var typedWorkerInterface = type
                .GetInterfaces()
                .FirstOrDefault(e =>
                    e.IsGenericType
                    && e.GetGenericTypeDefinition() == genericInterfaceType
                    );

            if (typedWorkerInterface == null)
            {
                parameterType = null!;
                return false;
            }

            parameterType = typedWorkerInterface.GenericTypeArguments[parameterNumber];
            return true;
        }
    }
}
