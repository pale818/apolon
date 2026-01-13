using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomORM.Attributes;

namespace MedicalSystemApp
{
    
    [Table("medications")]
    public class Medication
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name", IsNullable = false, IsUnique = true)]
        public string Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("manufacturer")]
        public string? Manufacturer { get; set; }

    }

}

