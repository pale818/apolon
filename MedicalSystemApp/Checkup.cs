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
        public CheckupType Type { get; set; }
    }


    public enum CheckupType
    {
        GP, BLOOD, X_RAY, CT, MRI, ULTRA, EKG, ECHO, EYE, DERM, DENTA, MAMMO, EEG
    }
}
