using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    [Obsolete("Для MemoryJoin.")]
    [Table("__stub_query_data", Schema = "__stub")]
    public class MemoryJoinStubEntity
    {
        [Column("id")]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("long1")]
        public long? Long1 { get; set; }

        [Column("long2")]
        public long? Long2 { get; set; }

        [Column("long3")]
        public long? Long3 { get; set; }

        [Column("int1")]
        public int? Int1 { get; set; }

        [Column("int2")]
        public int? Int2 { get; set; }

        [Column("int3")]
        public int? Int3 { get; set; }

        [Column("short1")]
        public short? Short1 { get; set; }

        [Column("short2")]
        public short? Short2 { get; set; }

        [Column("short3")]
        public short? Short3 { get; set; }

        [Column("double1")]
        public double? Double1 { get; set; }

        [Column("double2")]
        public double? Double2 { get; set; }

        [Column("double3")]
        public double? Double3 { get; set; }

        [Column("string1")]
        public string String1 { get; set; } = null!;

        [Column("string2")]
        public string String2 { get; set; } = null!;

        [Column("string3")]
        public string String3 { get; set; } = null!;

        [Column("date1")]
        public DateTime? Date1 { get; set; }

        [Column("date2")]
        public DateTime? Date2 { get; set; }

        [Column("date3")]
        public DateTime? Date3 { get; set; }

        [Column("guid1")]
        public Guid? Guid1 { get; set; }

        [Column("guid2")]
        public Guid? Guid2 { get; set; }

        [Column("guid3")]
        public Guid? Guid3 { get; set; }
    }
}
