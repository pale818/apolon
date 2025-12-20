using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomORM.Attributes;

namespace MedicalSystemApp
{
    [Table("patients")]
    public class Patient
    {
        [Key]
        [Column("id", DbType = "SERIAL")] // Added DbType
        public int Id { get; set; }

        [Column("first_name", DbType = "VARCHAR(100)")] // Added DbType
        public string FirstName { get; set; }

        [Column("age", DbType = "INT")] // Added DbType
        public int Age { get; set; }

    }
}
