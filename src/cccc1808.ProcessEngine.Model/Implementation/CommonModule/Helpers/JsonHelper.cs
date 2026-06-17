using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers
{
    public static class JsonHelper
    {
        public static JsonElement ToJsonElement<TEntity>(
            TEntity entity, 
            JsonSerializerOptions? options = null)
        {
            using var document = JsonSerializer.SerializeToDocument(
                entity, 
                entity.GetType(),
                options);
            return document.RootElement.Clone();
        }

        public static JsonElement Empty { get; }
            = ToJsonElement(new { });
    }
}
