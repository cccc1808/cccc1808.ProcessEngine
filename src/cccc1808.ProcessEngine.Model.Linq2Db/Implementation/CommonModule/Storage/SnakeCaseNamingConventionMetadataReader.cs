using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using CaseConverter;

using LinqToDB.Mapping;
using LinqToDB.Metadata;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage
{
    public class SnakeCaseNamingConventionMetadataReader : IMetadataReader
    {
        private readonly Func<string, string> _namingConvention = name => name.ToSnakeCase();
        private readonly AttributeReader _reader = new();

        public MappingAttribute[] GetAttributes(Type type)
        {
            var attributes = _reader.GetAttributes(type);

            if (attributes.Any(x => x is TableAttribute))
            {
                return attributes;
            }

            return attributes.Concat([
                new TableAttribute()
            {
                // get type name and apply naming convention
                Name = _namingConvention(type.Name),
            }
            ]).ToArray();
        }

        public MappingAttribute[] GetAttributes(Type type, MemberInfo memberInfo)
        {
            var attributes = _reader.GetAttributes(type);

            if (attributes.Any(x => x is ColumnAttribute))
            {
                return attributes;
            }

            return attributes.Concat([
                new ColumnAttribute()
            {
                // get type name and apply naming convention
                Name = _namingConvention(memberInfo.Name),
            }
            ]).ToArray();
        }

        public MemberInfo[] GetDynamicColumns(Type type) => [];

        public string GetObjectID()
        {
            return nameof(SnakeCaseNamingConventionMetadataReader);
        }
    }
}
