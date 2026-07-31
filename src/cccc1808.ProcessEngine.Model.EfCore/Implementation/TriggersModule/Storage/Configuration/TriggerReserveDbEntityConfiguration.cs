using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Configuration
{
    public class TriggerReserveDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<TriggerReserveDbEntity<TId>>
    {
        public virtual void Configure(EntityTypeBuilder<TriggerReserveDbEntity<TId>> builder)
        {
            KeyIndex(builder);
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.Key"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual IndexBuilder<TriggerReserveDbEntity<TId>> KeyIndex(EntityTypeBuilder<TriggerReserveDbEntity<TId>> builder) 
        {
            // Уникальный
            return builder.HasIndex(e => new { e.Id, e.ReserveDate })
                .IsUnique();
        }
    }
}
