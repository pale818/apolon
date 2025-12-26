using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomORM.Attributes;


namespace MedicalSystemApp
{
    [Table("checkups")]
    public class Checkup
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // This links the Checkup to a Patient
        [Column("patient_id")]
        public int PatientId { get; set; }

        [Column("checkup_date")]
        public DateTime Date { get; set; }

        [Column("doctor_notes")]
        public string Notes { get; set; }

        [Column("checkup_type", DefaultValue = "'GP'")]
        public string Type { get; set; } // Defaults to 'GP' if nothing is entered [cite: 53]
    }

}
