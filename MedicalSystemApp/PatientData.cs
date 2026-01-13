using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomORM.Attributes;

namespace MedicalSystemApp
{
    [Table("patient_data")]
    public class PatientData
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("first_name", DbType = "VARCHAR(100)")] // Added DbType
        public string FirstName { get; set; }

        [Column("age", DbType = "INT")] // Added DbType
        public int Age { get; set; }

        [Column("email", IsUnique = true, IsNullable = false)]
        public string Email { get; set; } // No two patients can have the same email

        // for testing auto migration and rollback
        //[Column("phone_number")] public string PhoneNumber { get; set; }

        [Column("bloodType")]
        public string? BloodType { get; set; }

        [Column("oib")]
        public string? Oib { get; set; }

    }
}
