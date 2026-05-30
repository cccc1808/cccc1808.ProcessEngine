using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Test3.Новая_папка
{
    internal class TestDataContext : DataConnection
    {
        public TestDataContext(
            DataOptions options,
            string connectionString,
            Linq2DbMigrator.MappingSchemaContainer mappingSchemaContainer)
            : base(
                  options                    
                    .UseMappingSchema(mappingSchemaContainer.MappingSchema)
                    .UsePostgreSQL(connectionString)
                  )
        {
            
        }

        public async Task TruncateAllAsync()
        {
            await this.SetCommand(@"TRUNCATE TABLE ""public"".""trigger_db_entity_1"" CASCADE;").ExecuteAsync();
            await this.SetCommand(@"TRUNCATE TABLE ""public"".""process_error_db_entity_1"" CASCADE;").ExecuteAsync();
            await this.SetCommand(@"TRUNCATE TABLE ""public"".""process_db_entity_1"" CASCADE;").ExecuteAsync();
            await this.SetCommand(@"TRUNCATE TABLE ""public"".""child_process_db_entity"" CASCADE;").ExecuteAsync();
            await this.SetCommand(@"TRUNCATE TABLE ""public"".""parent_process_data_db_entity"" CASCADE;").ExecuteAsync();
        }
    }
}
