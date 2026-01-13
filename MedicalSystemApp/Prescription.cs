using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomORM.Attributes;

namespace MedicalSystemApp
{
    [Table("prescriptions")]
    public class Prescription
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("patient_id")]
        public int PatientId { get; set; }

        [Column("medication_id", IsNullable = false)]
        public int MedicationId { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("dosage")]
        public float Dosage { get; set; }
    }
}
