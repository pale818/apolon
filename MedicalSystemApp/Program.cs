using CustomORM;
using CustomORM.Engine;
using MedicalSystemApp;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MedicalSystemApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfiguration config = builder.Build();

            string dockerStr = config.GetConnectionString("DockerConnection");
            string supabaseStr = config.GetConnectionString("SupabaseConnection");

            string currentConnString = supabaseStr;
            DatabaseManager db = new DatabaseManager(currentConnString);

            bool keepRunning = true;

            while (keepRunning)
            {
                Console.Clear();
                PrintMenu();
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        HandleCreateTables(db);
                        break;

                    case "2":
                        HandleInsert(db);
                        break;

                    case "3":
                        HandleUpdate(db);
                        break;

                    case "4":
                        HandleDelete(db);
                        break;

                    case "5":
                        HandleListAll(db);
                        break;

                    case "6":
                        HandleFilterByName(db);
                        break;

                    case "7":
                        HandlePatientLazy(db);
                        break;

                    case "8":
                        HandlePatientEager(db);
                        break;

                    case "9":
                        HandleAddTransaction(db);
                        break;

                    case "10":
                        HandleChangeDatabase(ref db, ref currentConnString, dockerStr, supabaseStr);
                        break;

                    case "11":
                        HandleMigration(currentConnString);
                        break;

                    case "0":
                        keepRunning = false;
                        Console.WriteLine("Exiting...");
                        break;

                    default:
                        Console.WriteLine("Invalid choice!");
                        break;
                }

                if (keepRunning)
                    Pause();
            }
        }

        static void PrintMenu()
        {
            Console.WriteLine("\n--- CUSTOM ORM MENU ---");
            Console.WriteLine("1. Create Tables");
            Console.WriteLine("2. Insert");
            Console.WriteLine("3. Update ");
            Console.WriteLine("4. Delete ");
            Console.WriteLine("5. List All ");
            Console.WriteLine("6. Filter by name ");
            Console.WriteLine("7. Patient record - Lazy loading");
            Console.WriteLine("8. Patient record - Eager loading");
            Console.WriteLine("9. Add Transaction");
            Console.WriteLine("10. Change database");
            Console.WriteLine("11. Check for updates - migration");
            Console.WriteLine("0. Exit");
            Console.Write("Choose an option: ");
        }

        static void Pause()
        {
            Console.WriteLine("\n-------------------------------------------");
            Console.WriteLine("Action finished. Press [ENTER] to return to menu...");
            Console.ReadLine();
        }

        // ------------------ OPTION 1 ------------------
        static void HandleCreateTables(DatabaseManager db)
        {
            Console.WriteLine("\n--- INITIALIZING DATABASE ---");
            db.CreateTableFromClass<PatientData>();
            db.CreateTableFromClass<Patient>();
            db.CreateTableFromClass<Checkup>();
            db.CreateTableFromClass<Prescription>();
            db.CreateTableFromClass<Medication>();
            Console.WriteLine("All tables created successfully!");
        }

        // ------------------ OPTION 2 ------------------
        static void HandleInsert(DatabaseManager db)
        {
            Console.WriteLine("\n--- ADD NEW RECORD ---");
            Console.WriteLine("A. Patient | B. Checkup | C. Prescription | D. Medication");
            string typeChoice = Console.ReadLine().ToUpper();

            switch (typeChoice)
            {
                case "A":
                    InsertPatient(db);
                    break;
                case "B":
                    InsertCheckup(db);
                    break;
                case "C":
                    InsertPrescription(db);
                    break;
                case "D":
                    InsertMedication(db);
                    break;
                default:
                    Console.WriteLine("Invalid type choice.");
                    break;
            }
        }

        static void InsertPatient(DatabaseManager db)
        {
            Console.WriteLine("\n--- ADD NEW PATIENT (1:1 Patient + PatientData) ---");

            Console.Write("Enter First Name: ");
            string name = Console.ReadLine();

            Console.Write("Enter Age: ");
            int age = int.Parse(Console.ReadLine());

            Console.Write("Enter Email (Must be unique): ");
            string email = Console.ReadLine();

            Console.Write("Enter Blood Type (optional): ");
            string bloodType = Console.ReadLine();

            Console.Write("Enter OIB (optional): ");
            string oib = Console.ReadLine();

            int dataId = db.Insert(new PatientData
            {
                FirstName = name,
                Age = age,
                Email = email,
                BloodType = string.IsNullOrWhiteSpace(bloodType) ? null : bloodType,
                Oib = string.IsNullOrWhiteSpace(oib) ? null : oib
            });

            if (dataId <= 0)
            {
                Console.WriteLine("Failed to create PatientData.");
                return;
            }

            int patientId = db.Insert(new Patient
            {
                PatientDataId = dataId
            });

            if (patientId <= 0)
            {
                Console.WriteLine("Failed to create Patient. (You may want to delete orphan PatientData in a transaction.)");
                return;
            }

            Console.WriteLine($"Patient created. PatientId={patientId}, PatientDataId={dataId}");
        }

        static void InsertCheckup(DatabaseManager db)
        {
            Console.Write("Enter Patient ID for this checkup: ");
            int pId = int.Parse(Console.ReadLine());

            Console.Write("Enter Doctor Notes: ");
            string notes = Console.ReadLine();

            Console.WriteLine("Select Checkup Type:");
            Console.WriteLine("0:GP, 1:BLOOD, 2:X_RAY, 3:CT, 4:MRI, 5:ULTRA, 6:EKG, 7:ECHO, 8:EYE, 9:DERM, 10:DENTA, 11:MAMMO, 12:EEG");
            Console.Write("Enter choice (0-12): ");

            if (int.TryParse(Console.ReadLine(), out int enumIndex) && Enum.IsDefined(typeof(CheckupType), enumIndex))
            {
                db.Insert(new Checkup
                {
                    PatientId = pId,
                    Date = DateTime.Now,
                    Notes = notes,
                    Type = (CheckupType)enumIndex
                });
            }
            else
            {
                Console.WriteLine("Invalid choice. Defaulting to GP.");
                db.Insert(new Checkup
                {
                    PatientId = pId,
                    Date = DateTime.Now,
                    Notes = notes,
                    Type = CheckupType.GP
                });
            }
        }

        static void InsertPrescription(DatabaseManager db)
        {
            Console.Write("Enter Patient ID for this prescription: ");
            int pId = int.Parse(Console.ReadLine());

            Console.Write("Enter Medication ID: ");
            int medId = int.Parse(Console.ReadLine());

            if (!db.ExistsById<Medication>("id", medId))
            {
                Console.WriteLine("Medication does not exist. Exiting.");
                return;
            }

            Console.Write("Dosage: ");
            float dose = float.Parse(Console.ReadLine());

            Console.Write("Start Date: ");
            DateTime startDate = DateTime.Parse(Console.ReadLine());

            db.Insert(new Prescription
            {
                PatientId = pId,
                MedicationId = medId,
                Dosage = dose,
                StartDate = startDate
            });
        }

        static void InsertMedication(DatabaseManager db)
        {
            Console.WriteLine("\n--- ADD NEW MEDICATION ---");

            Console.Write("Medication Name (unique): ");
            string medName = Console.ReadLine();

            Console.Write("Description (optional): ");
            string desc = Console.ReadLine();

            Console.Write("Manufacturer (optional): ");
            string manuf = Console.ReadLine();

            db.Insert(new Medication
            {
                Name = medName,
                Description = string.IsNullOrWhiteSpace(desc) ? null : desc,
                Manufacturer = string.IsNullOrWhiteSpace(manuf) ? null : manuf,
            });
        }

        // ------------------ OPTION 3 ------------------
        static void HandleUpdate(DatabaseManager db)
        {
            Console.WriteLine("\n--- UPDATE RECORD ---");
            Console.WriteLine("A. Patient | B. Checkup | C. Prescription | D. Medication");
            string updChoice = Console.ReadLine().ToUpper();

            switch (updChoice)
            {
                case "A":
                    UpdatePatient(db);
                    break;
                case "B":
                    UpdateCheckup(db);
                    break;
                case "C":
                    UpdatePrescription(db);
                    break;
                case "D":
                    UpdateMedication(db);
                    break;
                default:
                    Console.WriteLine("Invalid type choice.");
                    break;
            }
        }

        static void UpdatePatient(DatabaseManager db)
        {
            Console.Write("Enter Patient ID to update: ");
            int id = int.Parse(Console.ReadLine());

            var patient = db.GetWithFilter<Patient>("id", id).FirstOrDefault();
            if (patient == null) { Console.WriteLine("Patient not found."); return; }

            var data = db.GetWithFilter<PatientData>("id", patient.PatientDataId).FirstOrDefault();
            if (data == null) { Console.WriteLine("PatientData not found (broken 1:1 link)."); return; }

            Console.WriteLine($"Updating Patient: {data.FirstName} (Email: {data.Email})");
            Console.WriteLine("1. Update Name | 2. Update Age | 3. Update Email | 4. Blood Type | 5. OIB | 6. Update All");
            string part = Console.ReadLine();

            if (part == "1" || part == "6") { Console.Write("New Name: "); data.FirstName = Console.ReadLine(); }
            if (part == "2" || part == "6") { Console.Write("New Age: "); data.Age = int.Parse(Console.ReadLine()); }
            if (part == "3" || part == "6") { Console.Write("New Email: "); data.Email = Console.ReadLine(); }
            if (part == "4" || part == "6") { Console.Write("New Blood Type: "); data.BloodType = Console.ReadLine(); }
            if (part == "5" || part == "6") { Console.Write("New OIB: "); data.Oib = Console.ReadLine(); }

            db.Update(data);
            Console.WriteLine("Patient updated (PatientData updated).");
        }

        static void UpdateCheckup(DatabaseManager db)
        {
            Console.Write("Enter Checkup ID to update: ");
            int id = int.Parse(Console.ReadLine());

            var checkup = db.GetWithFilter<Checkup>("id", id).FirstOrDefault();
            if (checkup == null) { Console.WriteLine("Checkup not found."); return; }

            Console.WriteLine("1. Update Notes | 2. Update Type | 3. Update Both");
            string part = Console.ReadLine();

            if (part == "1" || part == "3") { Console.Write("New Notes: "); checkup.Notes = Console.ReadLine(); }
            if (part == "2" || part == "3")
            {
                Console.WriteLine("0:GP, 1:BLOOD, 2:X_RAY, 3:CT, 4:MRI, 5:ULTRA, 6:EKG, 7:ECHO, 8:EYE, 9:DERM, 10:DENTA, 11:MAMMO, 12:EEG");
                Console.Write("Enter choice (0-12): ");
                if (int.TryParse(Console.ReadLine(), out int enumIndex) && Enum.IsDefined(typeof(CheckupType), enumIndex))
                    checkup.Type = (CheckupType)enumIndex;
            }

            db.Update(checkup);
        }

        static void UpdatePrescription(DatabaseManager db)
        {
            Console.Write("Enter Prescription ID to update: ");
            int id = int.Parse(Console.ReadLine());

            var pres = db.GetWithFilter<Prescription>("id", id).FirstOrDefault();
            if (pres == null) { Console.WriteLine("Prescription not found."); return; }

            Console.WriteLine("1. Update Medication ID| 2. Update Dosage | 3. Update Both");
            string part = Console.ReadLine();

            if (part == "1" || part == "3")
            {
                Console.Write("New Medication: ");
                pres.MedicationId = int.Parse(Console.ReadLine());
            }

            if (part == "2" || part == "3")
            {
                Console.Write("New Dosage: ");
                pres.Dosage = float.Parse(Console.ReadLine());
            }

            if (!db.ExistsById<Medication>("id", pres.MedicationId))
            {
                Console.WriteLine("Medication does not exist. Exiting.");
                return;
            }

            db.Update(pres);
        }

        static void UpdateMedication(DatabaseManager db)
        {
            Console.Write("Enter Medication ID to update: ");
            int id = int.Parse(Console.ReadLine());

            var med = db.GetWithFilter<Medication>("id", id).FirstOrDefault();
            if (med == null) { Console.WriteLine("Medication not found."); return; }

            Console.WriteLine("1. Update Name | 2. Update Description | 3. Update Manufacturer | 4. Update All");
            string part = Console.ReadLine();

            if (part == "1" || part == "4") { Console.Write("New Name: "); med.Name = Console.ReadLine(); }
            if (part == "2" || part == "4") { Console.Write("New Description: "); med.Description = Console.ReadLine(); }
            if (part == "3" || part == "4") { Console.Write("New Manufacturer: "); med.Manufacturer = Console.ReadLine(); }

            db.Update(med);
        }

        // ------------------ OPTION 4 ------------------
        static void HandleDelete(DatabaseManager db)
        {
            Console.WriteLine("\n--- DELETE RECORD ---");
            Console.WriteLine("A. Patient | B. Checkup | C. Prescription | D. Medication");
            string delChoice = Console.ReadLine().ToUpper();

            Console.Write("Enter ID to DELETE: ");
            int idDel = int.Parse(Console.ReadLine());

            Console.Write("Are you sure? (y/n): ");
            if (Console.ReadLine().ToLower() != "y")
                return;

            switch (delChoice)
            {
                case "A":
                    DeletePatientWithHistory(db, idDel);
                    break;
                case "B":
                    db.Delete(new Checkup { Id = idDel });
                    break;
                case "C":
                    db.Delete(new Prescription { Id = idDel });
                    break;
                case "D":
                    db.Delete(new Medication { Id = idDel });
                    break;
                default:
                    Console.WriteLine("Invalid type choice.");
                    break;
            }
        }

        static void DeletePatientWithHistory(DatabaseManager db, int patientId)
        {
            var patient = db.GetWithFilter<Patient>("id", patientId).FirstOrDefault();
            if (patient == null)
            {
                Console.WriteLine("Patient not found.");
                return;
            }

            db.ExecuteTransaction(conn =>
            {
                db.DeleteTransaction<Prescription>("patient_id", patientId, conn);
                db.DeleteTransaction<Checkup>("patient_id", patientId, conn);

                db.DeleteTransaction<Patient>("id", patientId, conn);
                db.DeleteTransaction<PatientData>("id", patient.PatientDataId, conn);
            });

            Console.WriteLine("Patient + PatientData deleted (and history).");
        }

        // ------------------ OPTION 5 ------------------
        static void HandleListAll(DatabaseManager db)
        {
            Console.WriteLine("\n--- VIEW DATA ---");
            Console.WriteLine("A. All Patients | B. All Checkups | C. All Prescriptions | D. Medication");
            string listChoice = Console.ReadLine().ToUpper();

            switch (listChoice)
            {
                case "A":
                    ListAllPatients(db);
                    break;
                case "B":
                    ListAllCheckups(db);
                    break;
                case "C":
                    ListAllPrescriptions(db);
                    break;
                case "D":
                    ListAllMedications(db);
                    break;
                default:
                    Console.WriteLine("Invalid type choice.");
                    break;
            }
        }

        static void ListAllPatients(DatabaseManager db)
        {
            var patients = db.GetAll<Patient>();
            if (patients == null || !patients.Any())
            {
                Console.WriteLine("No patients found.");
                return;
            }

            var dataIds = patients.Select(x => x.PatientDataId).Distinct().ToList();
            var patientDataRows = db.GetWhereIn<PatientData>("id", dataIds);
            var dataById = patientDataRows.ToDictionary(x => x.Id, x => x);

            foreach (var x in patients)
            {
                if (dataById.TryGetValue(x.PatientDataId, out var pd))
                    Console.WriteLine($"ID: {x.Id} | Name: {pd.FirstName} | Age: {pd.Age} | Email: {pd.Email}");
                else
                    Console.WriteLine($"ID: {x.Id} | PatientDataId: {x.PatientDataId} | [MISSING PatientData]");
            }
        }

        static void ListAllCheckups(DatabaseManager db)
        {
            var list = db.GetAll<Checkup>();
            foreach (var x in list)
                Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | Date: {x.Date} | Notes: {x.Notes} | Type: {x.Type}");
        }

        static void ListAllPrescriptions(DatabaseManager db)
        {
            var list = db.GetAll<Prescription>();
            foreach (var x in list)
                Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | MedId: {x.MedicationId} | Dose: {x.Dosage}");
        }

        static void ListAllMedications(DatabaseManager db)
        {
            var list = db.GetAll<Medication>();
            foreach (var x in list)
                Console.WriteLine($"ID: {x.Id} | Med: {x.Name} | Description: {x.Description} | Manufacturer: {x.Manufacturer}");
        }

        // ------------------ OPTION 6 ------------------
        static void HandleFilterByName(DatabaseManager db)
        {
            Console.WriteLine("Enter Name to search:");
            string searchName = Console.ReadLine();

            var filteredData = db.GetWithFilter<PatientData>("first_name", searchName, "age");
            if (filteredData == null || !filteredData.Any())
            {
                Console.WriteLine("No matching patients found.");
                return;
            }

            var pdIds = filteredData.Select(x => x.Id).Distinct().ToList();
            var matchingPatients = db.GetWhereIn<Patient>("patient_data_id", pdIds);

            var patientByDataId = matchingPatients.ToDictionary(x => x.PatientDataId, x => x);

            foreach (var pd in filteredData)
            {
                if (patientByDataId.TryGetValue(pd.Id, out var p))
                    Console.WriteLine($"Found: {pd.FirstName}, Age: {pd.Age} | PatientId: {p.Id} | Email: {pd.Email}");
                else
                    Console.WriteLine($"Found PatientData but no Patient row: {pd.FirstName} (PatientDataId: {pd.Id})");
            }
        }

        // ------------------ OPTION 7 ------------------
        static void HandlePatientLazy(DatabaseManager db)
        {
            Console.Write("Enter Patient ID for LAZY fetch: ");
            if (!int.TryParse(Console.ReadLine(), out int lazyId))
                return;

            var p = db.GetWithFilter<Patient>("id", lazyId).FirstOrDefault();
            if (p == null)
            {
                Console.WriteLine("Patient not found.");
                return;
            }

            var pd = db.GetWithFilter<PatientData>("id", p.PatientDataId).FirstOrDefault();
            if (pd == null)
            {
                Console.WriteLine("PatientData not found (broken 1:1 link).");
                return;
            }

            Console.WriteLine($"\n--- [LAZY] PATIENT RECORD ---");
            Console.WriteLine($"PatientId: {p.Id}");
            Console.WriteLine($"Name: {pd.FirstName}");
            Console.WriteLine($"Email: {pd.Email}");
            Console.WriteLine($"Age: {pd.Age}");
            Console.WriteLine($"BloodType: {pd.BloodType ?? "-"}");
            Console.WriteLine($"OIB: {pd.Oib ?? "-"}");

            Console.WriteLine($"\nCheckups currently in memory: {p.Checkups.Count}");
            Console.WriteLine($"Prescriptions currently in memory: {p.Prescriptions.Count}");

            Console.Write("\nDo you want to view this patient's checkup details? (y/n): ");
            string c1 = Console.ReadLine()?.ToLower();
            if (c1 == "y")
            {
                p.Checkups = db.GetWithFilter<Checkup>("patient_id", p.Id);
                Console.WriteLine($"\n--- CHECKUP HISTORY ---");
                if (p.Checkups.Any())
                    foreach (var ch in p.Checkups)
                        Console.WriteLine($"- {ch.Date:yyyy-MM-dd} [{ch.Type}]: {ch.Notes}");
                else
                    Console.WriteLine("No checkups found for this patient.");
            }

            Console.Write("\nDo you want to view this patient's prescriptions? (y/n): ");
            string c2 = Console.ReadLine()?.ToLower();
            if (c2 == "y")
            {
                p.Prescriptions = db.GetWithFilter<Prescription>("patient_id", p.Id);
                Console.WriteLine($"\n--- PRESCRIPTIONS ---");

                if (p.Prescriptions.Any())
                {
                    foreach (var pr in p.Prescriptions)
                    {
                        string medName = $"MedicationId={pr.MedicationId}";
                        var med = db.GetWithFilter<Medication>("id", pr.MedicationId).FirstOrDefault();
                        if (med != null) medName = med.Name;

                        Console.WriteLine($"- Med: {medName} | Dose: {pr.Dosage} | Start: {pr.StartDate:yyyy-MM-dd}");
                    }
                }
                else Console.WriteLine("No prescriptions found for this patient.");
            }
        }

        // ------------------ OPTION 8 ------------------
        static void HandlePatientEager(DatabaseManager db)
        {
            Console.Write("Enter Patient ID for Eager JOIN fetch: ");
            if (!int.TryParse(Console.ReadLine(), out int eagerId))
                return;

            var p = db.GetEagerJoined<Patient, Checkup, Prescription>(eagerId, "patient_id", "patient_id");
            if (p == null)
            {
                Console.WriteLine("Patient not found.");
                return;
            }

            var pd = db.GetWithFilter<PatientData>("id", p.PatientDataId).FirstOrDefault();
            if (pd == null)
            {
                Console.WriteLine("PatientData not found (broken 1:1 link).");
                return;
            }

            Console.WriteLine($"\n[EAGER JOIN] MEDICAL FILE: {pd.FirstName} (PatientId: {p.Id})");
            Console.WriteLine($"Email: {pd.Email}");
            Console.WriteLine($"Age: {pd.Age}");
            Console.WriteLine($"BloodType: {pd.BloodType ?? "-"}");
            Console.WriteLine($"OIB: {pd.Oib ?? "-"}");

            Console.WriteLine($"\nCheckups found: {p.Checkups.Count}");
            Console.WriteLine($"Prescriptions found: {p.Prescriptions.Count}");

            var medNameById = new Dictionary<int, string>();
            if (p.Prescriptions != null && p.Prescriptions.Any())
            {
                var medIds = p.Prescriptions.Select(x => x.MedicationId).Distinct().ToList();
                var meds = db.GetWhereIn<Medication>("id", medIds);
                foreach (var m in meds) medNameById[m.Id] = m.Name;
            }

            Console.WriteLine("\n--- HISTORY ---");
            p.Checkups.ForEach(c => Console.WriteLine($"Checkup: {c.Notes} - {c.Date:yyyy-MM-dd} - {c.Type}"));

            p.Prescriptions.ForEach(pr =>
            {
                string medName = medNameById.TryGetValue(pr.MedicationId, out var nm) ? nm : $"MedicationId={pr.MedicationId}";
                Console.WriteLine($"Meds: {medName} - Dosage: {pr.Dosage} - Start: {pr.StartDate:yyyy-MM-dd}");
            });
        }

        // ------------------ OPTION 9 ------------------
        static void HandleAddTransaction(DatabaseManager db)
        {
            Console.WriteLine("--- TRANSACTION: REGISTER NEW PATIENT + FIRST CHECKUP ---");

            Console.Write("Patient Name: ");
            string name = Console.ReadLine();

            Console.Write("Patient Age: ");
            int age = int.Parse(Console.ReadLine());

            Console.Write("Patient Email: ");
            string email = Console.ReadLine();

            Console.Write("Blood Type (optional): ");
            string bloodType = Console.ReadLine();

            Console.Write("OIB (optional): ");
            string oib = Console.ReadLine();

            Console.Write("Checkup Notes: ");
            string notes = Console.ReadLine();

            Console.WriteLine("Select Checkup Type:");
            Console.WriteLine("0:GP, 1:BLOOD, 2:X_RAY, 3:CT, 4:MRI, 5:ULTRA, 6:EKG, 7:ECHO, 8:EYE, 9:DERM, 10:DENTA, 11:MAMMO, 12:EEG");
            Console.Write("Enter choice (0-12): ");

            CheckupType selectedType = CheckupType.GP;
            if (int.TryParse(Console.ReadLine(), out int enumIndex) && Enum.IsDefined(typeof(CheckupType), enumIndex))
                selectedType = (CheckupType)enumIndex;

            try
            {
                db.ExecuteTransaction(conn =>
                {
                    var pd = new PatientData
                    {
                        FirstName = name,
                        Age = age,
                        Email = email,
                        BloodType = string.IsNullOrWhiteSpace(bloodType) ? null : bloodType,
                        Oib = string.IsNullOrWhiteSpace(oib) ? null : oib
                    };

                    int patientDataId = db.InsertTransaction(pd, conn);

                    var p = new Patient { PatientDataId = patientDataId };
                    int patientId = db.InsertTransaction(p, conn);

                    var c = new Checkup
                    {
                        PatientId = patientId,
                        Notes = notes,
                        Date = DateTime.Now,
                        Type = selectedType
                    };

                    db.InsertTransaction(c, conn);

                    Console.WriteLine($"Transaction complete!");
                    Console.WriteLine($"- PatientId: {patientId}");
                    Console.WriteLine($"- PatientDataId: {patientDataId}");
                    Console.WriteLine($"- Checkup added: {selectedType}");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Transaction failed! Rolled back. Error: {ex.Message}");
            }
        }

        // ------------------ OPTION 10 ------------------
        static void HandleChangeDatabase(ref DatabaseManager db, ref string currentConnString, string dockerStr, string supabaseStr)
        {
            Console.WriteLine("\n--- SWITCH DATABASE ---");
            Console.WriteLine("1. Docker (Local) | 2. Supabase (Cloud)");
            string c = Console.ReadLine();

            currentConnString = (c == "1") ? dockerStr : supabaseStr;
            Console.WriteLine(c == "1" ? "Switched to DOCKER." : "Switched to SUPABASE.");

            db = new DatabaseManager(currentConnString);
        }

        // ------------------ OPTION 11 ------------------
        static void HandleMigration(string currentConnString)
        {
            Console.WriteLine("\n--- AUTO MIGRATION ---");
            try
            {
                var migrator = new MigrationManager(currentConnString);

                Console.WriteLine("Checking PatientData...");
                migrator.AutoMigrate<PatientData>();

                Console.WriteLine("Checking Patients...");
                migrator.AutoMigrate<Patient>();

                Console.WriteLine("Checking Checkups...");
                migrator.AutoMigrate<Checkup>();

                Console.WriteLine("Checking Prescriptions...");
                migrator.AutoMigrate<Prescription>();

                Console.WriteLine("\nScan complete. Database is up to date with models.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration Error: {ex.Message}");
            }
        }
    }
}
