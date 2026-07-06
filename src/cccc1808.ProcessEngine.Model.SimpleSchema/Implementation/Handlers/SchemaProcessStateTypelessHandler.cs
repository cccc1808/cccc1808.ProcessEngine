using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Handlers
{
    /// <summary>
    /// Универсальная реализация серализатора.
    /// На основе сохранения метаданных о типе в серализуемые данные.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class SchemaProcessStateTypelessHandler<TId> 
        : SchemaProcessStateTypelessHandler,
        ISchemaProcessStateHandler<TId>
    {
        public bool IsTokenSupport(string tokenId)
        {
            return true;
        }

        public JsonElement? SerializeProcessState(
            IProcessContainer<TId> process, 
            object state)
        {
            if (state is null)
            {
                return null;
            }

            var type = state.GetType();
            if (state is not ITypeContainer tContainer)
            {
                throw new ArgumentException($"Тип должен реализвать интефрейс. {type.FullName}. {nameof(ITypeContainer)}");
            }

            tContainer.AssemblyQualifiedName = type.AssemblyQualifiedName!;

            return JsonHelper.ToJsonElement(state);
        }

        public JsonElement? SerializeTokenState(IProcessContainer<TId> process, object state)
        {
            if (state is null)
            {
                return null;
            }

            var type = state.GetType();
            if (state is not ITypeContainer tContainer)
            {
                throw new ArgumentException($"Тип должен реализвать интефрейс. {type.FullName}. {nameof(ITypeContainer)}");
            }

            tContainer.AssemblyQualifiedName = type.AssemblyQualifiedName!;

            return JsonHelper.ToJsonElement(state);
        }

        public object DeserializeProcessState(JsonElement jsonState)
        {
            var typeContainer = jsonState.Deserialize<TypeContainerDto>();
            var type = Type.GetType(typeContainer.AssemblyQualifiedName, throwOnError: false);

            return jsonState.Deserialize(returnType: type)!;
        }

        public object DeserializeTokenState(string currentTokenId, JsonElement jsonState)
        {
            var typeContainer = jsonState.Deserialize<TypeContainerDto>();
            var type = Type.GetType(typeContainer.AssemblyQualifiedName, throwOnError: false);

            return jsonState.Deserialize(returnType: type)!;
        }
    }

    public class SchemaProcessStateTypelessHandler
    {
        public interface ITypeContainer
        {
            public string? AssemblyQualifiedName { get; set; }
        }

        public struct TypeContainerDto : ITypeContainer
        {
            public string AssemblyQualifiedName { get; set; }
        }
    }
}
