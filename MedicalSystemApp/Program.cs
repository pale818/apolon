using CustomORM;
using CustomORM.Engine; // Points to your ENGINE folder
using MedicalSystemApp;      // Points to where your Patient class is
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;


namespace MedicalSystemApp
{
    class Program
    {
        static void Main(string[] args)
        {
            //CONNECTION TO DB


            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfiguration config = builder.Build();

            string dockerStr = config.GetConnectionString("DockerConnection");
            string supabaseStr = config.GetConnectionString("SupabaseConnection");

            // 2. Initialize the Manager
            string currentConnString = supabaseStr;
            DatabaseManager db = new DatabaseManager(currentConnString);


            Console.WriteLine("Checking connection and creating table...");

            /*3. THE MOMENT OF TRUTH:
            // This reads your Patient class attributes and creates the table
            db.CreateTableFromClass<Patient>();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();*/




            //COMMAND LINE INTERFACE:
            bool keepRunning = true;

            while (keepRunning)
            {

                Console.Clear(); 
                Console.WriteLine("\n--- CUSTOM ORM MENU ---");
                Console.WriteLine("1. Create Table");
                Console.WriteLine("2. Insert");
                Console.WriteLine("3. Update ");
                Console.WriteLine("4. Delete ");
                Console.WriteLine("5. List All ");
                Console.WriteLine("6. Filter by name ");
                Console.WriteLine("7. Patient record - Lazy loading");
                Console.WriteLine("8. Patient record - Eager loading");
                Console.WriteLine("9. Add Transaction");
                Console.WriteLine("10. Delete Transaction");
                Console.WriteLine("11. Run Migrations (Add Phone Column)");
                Console.WriteLine("12. Rollback Last Migration");
                Console.WriteLine("13. Change database");
                Console.WriteLine("14. Check for updates - migration");
                Console.WriteLine("0. Exit");
                Console.Write("Choose an option: ");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("\n--- INITIALIZING DATABASE ---");
                        db.CreateTableFromClass<PatientData>();
                        db.CreateTableFromClass<Patient>();
                        db.CreateTableFromClass<Checkup>();
                        db.CreateTableFromClass<Prescription>();
                        db.CreateTableFromClass<Medication>();
                        Console.WriteLine("All tables (PatientData Patients, Checkups, Prescriptions, Medication) created successfully!");
                        break;

                    case "2":
                        Console.WriteLine("\n--- ADD NEW RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription | D. Medication");
                        string typeChoice = Console.ReadLine().ToUpper();

                        if (typeChoice == "A")
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

                            // 1) insert PatientData first
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
                                break;
                            }

                            // 2) insert Patient referencing PatientData
                            int patientId = db.Insert(new Patient
                            {
                                PatientDataId = dataId
                            });

                            if (patientId <= 0)
                            {
                                Console.WriteLine("Failed to create Patient. (You may want to delete orphan PatientData in a transaction.)");
                                break;
                            }

                            Console.WriteLine($"Patient created. PatientId={patientId}, PatientDataId={dataId}");
                        }

                        else if (typeChoice == "B")
                        {
                            Console.Write("Enter Patient ID for this checkup: ");
                            int pId = int.Parse(Console.ReadLine());

                            Console.Write("Enter Doctor Notes: ");
                            string notes = Console.ReadLine();

                            // Showing the user the numeric options for the required medical types
                            Console.WriteLine("Select Checkup Type:");
                            Console.WriteLine("0:GP, 1:BLOOD, 2:X_RAY, 3:CT, 4:MRI, 5:ULTRA, 6:EKG, 7:ECHO, 8:EYE, 9:DERM, 10:DENTA, 11:MAMMO, 12:EEG");
                            Console.Write("Enter choice (0-12): ");

                            if (int.TryParse(Console.ReadLine(), out int enumIndex) && Enum.IsDefined(typeof(CheckupType), enumIndex))
                            {
                                // Cast the integer directly to the Enum type
                                CheckupType selectedType = (CheckupType)enumIndex;

                                db.Insert(new Checkup
                                {
                                    PatientId = pId,
                                    Date = DateTime.Now,
                                    Notes = notes,
                                    Type = selectedType
                                });
                            }
                            else
                            {
                                Console.WriteLine("Invalid choice. Defaulting to GP.");
                                db.Insert(new Checkup { PatientId = pId, Date = DateTime.Now, Notes = notes, Type = CheckupType.GP });
                            }
                        }
                        else if (typeChoice == "C")
                        {
                            Console.Write("Enter Patient ID for this prescription: ");
                            int pId = int.Parse(Console.ReadLine());
                            Console.Write("Enter Medication ID: ");        
                            int medId = int.Parse(Console.ReadLine());
                            Console.Write("Dosage: ");
                            float dose = float.Parse(Console.ReadLine());
                            Console.Write("Start Date: ");
                            DateTime startDate = DateTime.Parse(Console.ReadLine());
                            db.Insert(new Prescription { PatientId = pId, MedicationId = medId, Dosage = dose, StartDate = startDate });
                        }
                        else if (typeChoice == "D")
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
                        break;

                    case "3":
                        Console.WriteLine("\n--- UPDATE RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription | D. Medication");
                        string updChoice = Console.ReadLine().ToUpper();

                        if (updChoice == "A")
                        {
                            Console.Write("Enter Patient ID to update: ");
                            int id = int.Parse(Console.ReadLine());

                            // Fetch Patient row
                            var patient = db.GetWithFilter<Patient>("id", id).FirstOrDefault();
                            if (patient == null) { Console.WriteLine("Patient not found."); break; }

                            // Fetch related PatientData row
                            var data = db.GetWithFilter<PatientData>("id", patient.PatientDataId).FirstOrDefault();
                            if (data == null) { Console.WriteLine("PatientData not found (broken 1:1 link)."); break; }

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

                        else if (updChoice == "B")
                        {
                            Console.Write("Enter Checkup ID to update: ");
                            int id = int.Parse(Console.ReadLine());

                            // Step 1: Fetch existing
                            var checkup = db.GetWithFilter<Checkup>("id", id).FirstOrDefault();
                            if (checkup == null) { Console.WriteLine("Checkup not found."); break; }

                            Console.WriteLine("1. Update Notes | 2. Update Type | 3. Update Both");
                            string part = Console.ReadLine();

                            if (part == "1" || part == "3") { Console.Write("New Notes: "); checkup.Notes = Console.ReadLine(); }
                            if (part == "2" || part == "3")
                            {
                                Console.WriteLine("0:GP, 1:BLOOD, 2:X_RAY, 3:CT, 4:MRI, 5:ULTRA, 6:EKG, 7:ECHO, 8:EYE, 9:DERM, 10:DENTA, 11:MAMMO, 12:EEG");
                                Console.Write("Enter choice (0-12): ");
                                if (int.TryParse(Console.ReadLine(), out int enumIndex))
                                    checkup.Type = (CheckupType)enumIndex;
                            }

                            db.Update(checkup);
                        }
                        else if (updChoice == "C")
                        {
                            Console.Write("Enter Prescription ID to update: ");
                            int id = int.Parse(Console.ReadLine());

                            // Step 1: Fetch existing
                            var pres = db.GetWithFilter<Prescription>("id", id).FirstOrDefault();
                            if (pres == null) { Console.WriteLine("Prescription not found."); break; }

                            Console.WriteLine("1. Update Medication ID| 2. Update Dosage | 3. Update Both");
                            string part = Console.ReadLine();

                            if (part == "1" || part == "3") { Console.Write("New Medication: "); pres.MedicationId = int.Parse(Console.ReadLine()); }
                            if (part == "2" || part == "3") { Console.Write("New Dosage: "); pres.Dosage = float.Parse(Console.ReadLine()); }

                            db.Update(pres);
                        }
                        else if (updChoice == "D")
                        {
                            Console.Write("Enter Medication ID to update: ");
                            int id = int.Parse(Console.ReadLine());

                            var med = db.GetWithFilter<Medication>("id", id).FirstOrDefault();
                            if (med == null) { Console.WriteLine("Medication not found."); break; }

                            Console.WriteLine("1. Update Name | 2. Update Description | 3. Update Manufacturer | 4. Update All");
                            string part = Console.ReadLine();

                            if (part == "1" || part == "4") { Console.Write("New Name: "); med.Name = Console.ReadLine(); }

                            if (part == "2" || part == "4") { Console.Write("New Description: "); med.Description = Console.ReadLine(); }

                            if (part == "3" || part == "4") { Console.Write("New Manufacturer: "); med.Manufacturer = Console.ReadLine(); }

                            db.Update(med);
                        }
                        break;

                    case "4":
                        Console.WriteLine("\n--- DELETE RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription | D. Medication");
                        string delChoice = Console.ReadLine().ToUpper();
                        Console.Write("Enter ID to DELETE: ");
                        int idDel = int.Parse(Console.ReadLine());
                        Console.Write("Are you sure? (y/n): ");
                        if (Console.ReadLine().ToLower() == "y")
                        {
                            if (delChoice == "A")
                            {
                                // Find patient to get PatientDataId
                                var patient = db.GetWithFilter<Patient>("id", idDel).FirstOrDefault();
                                if (patient == null) { Console.WriteLine("Patient not found."); break; }

                                // Optional: also delete checkups/prescriptions (recommended)
                                // If you already have transaction delete case 10, you can keep this simple,
                                // but here's the full safe delete:
                                db.ExecuteTransaction(conn =>
                                {
                                    db.DeleteTransaction<Prescription>("patient_id", idDel, conn);
                                    db.DeleteTransaction<Checkup>("patient_id", idDel, conn);

                                    db.DeleteTransaction<Patient>("id", idDel, conn);
                                    db.DeleteTransaction<PatientData>("id", patient.PatientDataId, conn);
                                });

                                Console.WriteLine("Patient + PatientData deleted (and history).");
                            }
                            else if (delChoice == "B") db.Delete(new Checkup { Id = idDel });
                            else if (delChoice == "C") db.Delete(new Prescription { Id = idDel });
                            else if (delChoice == "D") db.Delete(new Medication { Id = idDel });

                            else if (delChoice == "B") db.Delete(new Checkup { Id = idDel });
                            else if (delChoice == "C") db.Delete(new Prescription { Id = idDel });
                            else if (delChoice == "D") db.Delete(new Medication { Id = idDel });

                        }
                        break;

                    case "5":
                        Console.WriteLine("\n--- VIEW DATA ---");
                        Console.WriteLine("A. All Patients | B. All Checkups | C. All Prescriptions | D. Medication");
                        string listChoice = Console.ReadLine().ToUpper();

                        if (listChoice == "A")
                        {
                            var patients = db.GetAll<Patient>();

                            if (patients == null || !patients.Any())
                            {
                                Console.WriteLine("No patients found.");
                                break;
                            }

                            // Fetch all PatientData rows for these patients in ONE query
                            var dataIds = patients.Select(x => x.PatientDataId).Distinct().ToList();
                            var patientDataRows = db.GetWhereIn<PatientData>("id", dataIds);

                            var dataById = patientDataRows.ToDictionary(x => x.Id, x => x);

                            foreach (var x in patients)
                            {
                                if (dataById.TryGetValue(x.PatientDataId, out var pd))
                                {
                                    Console.WriteLine($"ID: {x.Id} | Name: {pd.FirstName} | Age: {pd.Age} | Email: {pd.Email}");
                                }
                                else
                                {
                                    Console.WriteLine($"ID: {x.Id} | PatientDataId: {x.PatientDataId} | [MISSING PatientData]");
                                }
                            }
                        }
                        else if (listChoice == "B")
                        {
                            var list = db.GetAll<Checkup>();
                            foreach (var x in list)
                                Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | Date: {x.Date} | Notes: {x.Notes} | Type: {x.Type}");
                        }
                        else if (listChoice == "C")
                        {
                            var list = db.GetAll<Prescription>();
                            foreach (var x in list)
                                Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | MedId: {x.MedicationId} | Dose: {x.Dosage}");
                        }
                        else if (listChoice == "D")
                        {
                            var list = db.GetAll<Medication>();
                            foreach (var x in list)
                                Console.WriteLine($"ID: {x.Id} | Med: {x.Name} | Description: {x.Description} | Manufacturer: {x.Manufacturer}");
                        }
                        break;


                    case "6":
                        Console.WriteLine("Enter Name to search:");
                        string searchName = Console.ReadLine();

                        // Search in PatientData (not Patient)
                        var filteredData = db.GetWithFilter<PatientData>("first_name", searchName, "age");

                        if (filteredData == null || !filteredData.Any())
                        {
                            Console.WriteLine("No matching patients found.");
                            break;
                        }

                        // Find Patient rows that point to these PatientData ids
                        var pdIds = filteredData.Select(x => x.Id).Distinct().ToList();
                        var matchingPatients = db.GetWhereIn<Patient>("patient_data_id", pdIds);

                        // Map Patient by PatientDataId for quick lookup
                        var patientByDataId = matchingPatients.ToDictionary(x => x.PatientDataId, x => x);

                        foreach (var pd in filteredData)
                        {
                            if (patientByDataId.TryGetValue(pd.Id, out var p))
                            {
                                Console.WriteLine($"Found: {pd.FirstName}, Age: {pd.Age} | PatientId: {p.Id} | Email: {pd.Email}");
                            }
                            else
                            {
                                Console.WriteLine($"Found PatientData but no Patient row: {pd.FirstName} (PatientDataId: {pd.Id})");
                            }
                        }
                        break;


                    case "7": // LAZY LOADING DEMO (Patient + PatientData + Checkups + Prescriptions + Medication lookup)
                        Console.Write("Enter Patient ID for LAZY fetch: ");
                        if (int.TryParse(Console.ReadLine(), out int lazyId))
                        {
                            // STEP 1: Fetch ONLY the Patient row
                            var p = db.GetWithFilter<Patient>("id", lazyId).FirstOrDefault();

                            if (p != null)
                            {
                                // STEP 1.1: Fetch ONLY the PatientData row (1:1)
                                var pd = db.GetWithFilter<PatientData>("id", p.PatientDataId).FirstOrDefault();
                                if (pd == null)
                                {
                                    Console.WriteLine("PatientData not found (broken 1:1 link).");
                                    break;
                                }

                                Console.WriteLine($"\n--- [LAZY] PATIENT RECORD ---");
                                Console.WriteLine($"PatientId: {p.Id}");
                                Console.WriteLine($"Name: {pd.FirstName}");
                                Console.WriteLine($"Email: {pd.Email}");
                                Console.WriteLine($"Age: {pd.Age}");
                                Console.WriteLine($"BloodType: {pd.BloodType ?? "-"}");
                                Console.WriteLine($"OIB: {pd.Oib ?? "-"}");

                                // Show that navigation collections start empty (lazy)
                                Console.WriteLine($"\nCheckups currently in memory: {p.Checkups.Count}");
                                Console.WriteLine($"Prescriptions currently in memory: {p.Prescriptions.Count}");

                                // --- LAZY LOAD CHECKUPS ---
                                Console.Write("\nDo you want to view this patient's checkup details? (y/n): ");
                                string c1 = Console.ReadLine()?.ToLower();

                                if (c1 == "y")
                                {
                                    Console.WriteLine("Fetching checkups from database...");
                                    p.Checkups = db.GetWithFilter<Checkup>("patient_id", p.Id);

                                    Console.WriteLine($"Checkups currently in memory: {p.Checkups.Count}");
                                    Console.WriteLine($"\n--- CHECKUP HISTORY ---");

                                    if (p.Checkups.Any())
                                    {
                                        foreach (var ch in p.Checkups)
                                            Console.WriteLine($"- {ch.Date:yyyy-MM-dd} [{ch.Type}]: {ch.Notes}");
                                    }
                                    else
                                    {
                                        Console.WriteLine("No checkups found for this patient.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Skipping checkups.");
                                }

                                // --- LAZY LOAD PRESCRIPTIONS ---
                                Console.Write("\nDo you want to view this patient's prescriptions? (y/n): ");
                                string c2 = Console.ReadLine()?.ToLower();

                                if (c2 == "y")
                                {
                                    Console.WriteLine("Fetching prescriptions from database...");
                                    p.Prescriptions = db.GetWithFilter<Prescription>("patient_id", p.Id);

                                    Console.WriteLine($"Prescriptions currently in memory: {p.Prescriptions.Count}");
                                    Console.WriteLine($"\n--- PRESCRIPTIONS ---");

                                    if (p.Prescriptions.Any())
                                    {
                                        foreach (var pr in p.Prescriptions)
                                        {
                                            // Lazy resolve medication on demand (1 query per prescription)
                                            string medName = $"MedicationId={pr.MedicationId}";
                                            var med = db.GetWithFilter<Medication>("id", pr.MedicationId).FirstOrDefault();
                                            if (med != null) medName = med.Name;

                                            Console.WriteLine($"- Med: {medName} | Dose: {pr.Dosage} | Start: {pr.StartDate:yyyy-MM-dd}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("No prescriptions found for this patient.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Skipping prescriptions.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Patient not found.");
                            }
                        }
                        break;


                    case "8": // Eager Loading (Patient + Checkups + Prescriptions) + PatientData (1:1) + Medication names via IN
                        Console.Write("Enter Patient ID for Eager JOIN fetch: ");
                        if (int.TryParse(Console.ReadLine(), out int eagerId))
                        {
                            // This still joins Patient->Checkups->Prescriptions
                            var p = db.GetEagerJoined<Patient, Checkup, Prescription>(eagerId, "patient_id", "patient_id");

                            if (p != null)
                            {
                                // NEW: fetch PatientData for 1:1
                                var pd = db.GetWithFilter<PatientData>("id", p.PatientDataId).FirstOrDefault();
                                if (pd == null)
                                {
                                    Console.WriteLine("PatientData not found (broken 1:1 link).");
                                    break;
                                }

                                Console.WriteLine($"\n[EAGER JOIN] MEDICAL FILE: {pd.FirstName} (PatientId: {p.Id})");
                                Console.WriteLine($"Email: {pd.Email}");
                                Console.WriteLine($"Age: {pd.Age}");
                                Console.WriteLine($"BloodType: {pd.BloodType ?? "-"}");
                                Console.WriteLine($"OIB: {pd.Oib ?? "-"}");

                                Console.WriteLine($"\nCheckups found: {p.Checkups.Count}");
                                Console.WriteLine($"Prescriptions found: {p.Prescriptions.Count}");

                                // Fetch medications used in prescriptions (single query via IN)
                                var medNameById = new Dictionary<int, string>();
                                if (p.Prescriptions != null && p.Prescriptions.Any())
                                {
                                    var medIds = p.Prescriptions.Select(x => x.MedicationId).Distinct().ToList();
                                    var meds = db.GetWhereIn<Medication>("id", medIds);

                                    foreach (var m in meds)
                                        medNameById[m.Id] = m.Name;
                                }

                                Console.WriteLine("\n--- HISTORY ---");
                                p.Checkups.ForEach(c =>
                                    Console.WriteLine($"Checkup: {c.Notes} - {c.Date:yyyy-MM-dd} - {c.Type}")
                                );

                                p.Prescriptions.ForEach(pr =>
                                {
                                    string medName = medNameById.TryGetValue(pr.MedicationId, out var nm)
                                        ? nm
                                        : $"MedicationId={pr.MedicationId}";

                                    Console.WriteLine($"Meds: {medName} - Dosage: {pr.Dosage} - Start: {pr.StartDate:yyyy-MM-dd}");
                                });
                            }
                            else
                            {
                                Console.WriteLine("Patient not found.");
                            }
                        }
                        break;



                    case "9":
                        {
                            Console.WriteLine("--- TRANSACTION: REGISTER NEW PATIENT (1:1) + FIRST CHECKUP ---");

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
                                    // 1) Insert PatientData
                                    var pd = new PatientData
                                    {
                                        FirstName = name,
                                        Age = age,
                                        Email = email,
                                        BloodType = string.IsNullOrWhiteSpace(bloodType) ? null : bloodType,
                                        Oib = string.IsNullOrWhiteSpace(oib) ? null : oib
                                    };

                                    int patientDataId = db.InsertTransaction(pd, conn);

                                    // 2) Insert Patient that points to PatientData (1:1)
                                    var p = new Patient
                                    {
                                        PatientDataId = patientDataId
                                    };

                                    int patientId = db.InsertTransaction(p, conn);

                                    // 3) Insert Checkup linked to Patient
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
                        break;


                    case "10":
                        {
                            Console.WriteLine("\n--- TRANSACTION: DELETE PATIENT & ALL HISTORY (incl. PatientData 1:1) ---");
                            Console.Write("Enter Patient ID to PERMANENTLY delete: ");
                            if (int.TryParse(Console.ReadLine(), out int pId))
                            {
                                Console.Write($"Are you sure you want to delete Patient {pId} and all their medical records? (y/n): ");
                                if (Console.ReadLine().ToLower() == "y")
                                {
                                    try
                                    {
                                        // First fetch Patient to get PatientDataId (outside tx is OK)
                                        var patient = db.GetWithFilter<Patient>("id", pId).FirstOrDefault();
                                        if (patient == null)
                                        {
                                            Console.WriteLine("Patient not found.");
                                            break;
                                        }

                                        int patientDataId = patient.PatientDataId;

                                        db.ExecuteTransaction(conn =>
                                        {
                                            // 1) Delete dependent records first
                                            db.DeleteTransaction<Prescription>("patient_id", pId, conn);
                                            Console.WriteLine("- Deleted related prescriptions.");

                                            db.DeleteTransaction<Checkup>("patient_id", pId, conn);
                                            Console.WriteLine("- Deleted related checkups.");

                                            // 2) Delete Patient row
                                            db.DeleteTransaction<Patient>("id", pId, conn);
                                            Console.WriteLine("- Deleted patient record.");

                                            // 3) Delete PatientData row (1:1)
                                            db.DeleteTransaction<PatientData>("id", patientDataId, conn);
                                            Console.WriteLine("- Deleted patient data record.");

                                            Console.WriteLine("\nTransaction successful: All records wiped.");
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Transaction deletion failed! Changes rolled back. Error: {ex.Message}");
                                    }
                                }
                            }
                        }
                        break;


                    case "11":
                        {
                            var migrator = new MigrationManager(currentConnString);
                            // Demonstration: Adding a column that wasn't there before
                            string sql = "ALTER TABLE patient_data ADD COLUMN IF NOT EXISTS phone_number VARCHAR(20);";
                            migrator.ApplyMigration("20251226_AddPhoneToPatientData", sql);
                        }
                        break;
                    
                    case "12":
                        {
                            //var migrator = new MigrationManager(myConnectionString);
                            //migrator.RollbackLastMigration();

                            Console.WriteLine("\n--- ROLLING BACK LAST MIGRATION ---");
                            var migrator = new MigrationManager(currentConnString);

                            // This is the SQL required to reverse the specific change made in Case 10
                            string undoSql = "ALTER TABLE patient_data DROP COLUMN IF EXISTS phone_number;";

                            migrator.RollbackLastMigration(undoSql);
                        }
                        break;
                    case "13": // Add a new option to your menu
                        {
                            Console.WriteLine("\n--- SWITCH DATABASE ---");
                            Console.WriteLine("1. Docker (Local) | 2. Supabase (Cloud)");
                            string c = Console.ReadLine();

                            if (c == "1")
                            {
                                currentConnString = dockerStr;
                                Console.WriteLine("Switched to DOCKER.");
                            }
                            else
                            {
                                currentConnString = supabaseStr;
                                Console.WriteLine("Switched to SUPABASE.");
                            }

                            // Re-initialize the db object with the new connection string
                            db = new DatabaseManager(currentConnString);
                        }
                        break;

                    case "14":
                        Console.WriteLine("\n--- CHECKING FOR MODEL CHANGES (Auto-Migration) ---");
                        try
                        {
                            var migrator = new MigrationManager(currentConnString);

                            // This will check each class against its respective table in the DB
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
                {
                    Console.WriteLine("\n-------------------------------------------");
                    Console.WriteLine("Action finished. Press [ENTER] to return to menu...");
                    Console.ReadLine(); // This pauses the program so you can see the result
                }

            }






        }

    }
}