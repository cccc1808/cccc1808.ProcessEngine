using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto
{
    public record TriggerRegistryDto(
        TriggerTypeUniqueDto Unique,
        TriggerMetadataDto Metadata);

    public readonly record struct TriggerTypeUniqueDto(
        string HandlerName,
        short Priority)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(HandlerName,  Priority);
        }
    }

    public readonly record struct TriggerMetadataDto(
        Type ImplementationType)
    {
        public static TriggerMetadataDto Create<T>()
            where T : ITriggerHandler
        {
            return new TriggerMetadataDto(
                ImplementationType: typeof(T));
        }
    }
}
