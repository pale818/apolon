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
        [Column("id", DbType = "SERIAL")] 
        public int Id { get; set; }

        [Column("patient_data_id", IsUnique = true, IsNullable = false)]
        public int PatientDataId { get; set; }

        //  NAVIGATIONAL PROPERTIES 
        // don't exist in the 'patients' table
        public List<Checkup> Checkups { get; set; } = new List<Checkup>();
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    }
}
