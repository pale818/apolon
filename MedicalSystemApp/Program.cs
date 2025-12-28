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
                Console.WriteLine("11. Run Migrations (Add Email Column)");
                Console.WriteLine("12. Rollback Last Migration");
                Console.WriteLine("13. Change database");
                Console.WriteLine("0. Exit");
                Console.Write("Choose an option: ");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("\n--- INITIALIZING DATABASE ---");
                        db.CreateTableFromClass<Patient>();
                        db.CreateTableFromClass<Checkup>();
                        db.CreateTableFromClass<Prescription>();
                        Console.WriteLine("All tables (Patients, Checkups, Prescriptions) created successfully!");
                        break;

                    case "2":
                        Console.WriteLine("\n--- ADD NEW RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription");
                        string typeChoice = Console.ReadLine().ToUpper();

                        if (typeChoice == "A")
                        {
                            Console.Write("Enter First Name: ");
                            string name = Console.ReadLine();
                            Console.Write("Enter Age: ");
                            int age = int.Parse(Console.ReadLine());
                            Console.Write("Enter Email (Must be unique): ");
                            string email = Console.ReadLine();
                            db.Insert(new Patient { FirstName = name, Age = age , Email = email});

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
                            Console.Write("Medication Name: ");
                            string med = Console.ReadLine();
                            Console.Write("Dosage: ");
                            string dose = Console.ReadLine();
                            Console.Write("Start Date: ");
                            DateTime startDate = DateTime.Parse(Console.ReadLine());
                            db.Insert(new Prescription { PatientId = pId, Medication = med, Dosage = dose, StartDate = startDate });
                        }
                        break;

                    case "3":
                        Console.WriteLine("\n--- UPDATE RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription");
                        string updChoice = Console.ReadLine().ToUpper();

                        if (updChoice == "A")
                        {
                            Console.Write("Enter Patient ID to update: ");
                            int id = int.Parse(Console.ReadLine());

                            // Step 1: Fetch the existing record (Snapshot)
                            var patient = db.GetWithFilter<Patient>("id", id).FirstOrDefault();
                            if (patient == null) { Console.WriteLine("Patient not found."); break; }

                            Console.WriteLine($"Updating Patient: {patient.FirstName}");
                            Console.WriteLine("1. Update Name | 2. Update Age | 3. Update Email | 4. Update All");
                            string part = Console.ReadLine();

                            // Step 2: Update only the chosen attributes
                            if (part == "1" || part == "4") { Console.Write("New Name: "); patient.FirstName = Console.ReadLine(); }
                            if (part == "2" || part == "4") { Console.Write("New Age: "); patient.Age = int.Parse(Console.ReadLine()); }
                            if (part == "3" || part == "4") { Console.Write("New Email: "); patient.Email = Console.ReadLine(); }

                            // Step 3: Send the modified object back to DB
                            db.Update(patient);
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

                            Console.WriteLine("1. Update Medication | 2. Update Dosage | 3. Update Both");
                            string part = Console.ReadLine();

                            if (part == "1" || part == "3") { Console.Write("New Medication: "); pres.Medication = Console.ReadLine(); }
                            if (part == "2" || part == "3") { Console.Write("New Dosage: "); pres.Dosage = Console.ReadLine(); }

                            db.Update(pres);
                        }
                        break;

                    case "4":
                        Console.WriteLine("\n--- DELETE RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription");
                        string delChoice = Console.ReadLine().ToUpper();
                        Console.Write("Enter ID to DELETE: ");
                        int idDel = int.Parse(Console.ReadLine());
                        Console.Write("Are you sure? (y/n): ");
                        if (Console.ReadLine().ToLower() == "y")
                        {
                            if (delChoice == "A") db.Delete(new Patient { Id = idDel });
                            else if (delChoice == "B") db.Delete(new Checkup { Id = idDel });
                            else if (delChoice == "C") db.Delete(new Prescription { Id = idDel });
                        }
                        break;

                    case "5":
                        Console.WriteLine("\n--- VIEW DATA ---");
                        Console.WriteLine("A. All Patients | B. All Checkups | C. All Prescriptions");
                        string listChoice = Console.ReadLine().ToUpper();

                        if (listChoice == "A")
                        {
                            var list = db.GetAll<Patient>();
                            foreach (var x in list) Console.WriteLine($"ID: {x.Id} | Name: {x.FirstName} | Age: {x.Age} | Email: {x.Email}");
                        }
                        else if (listChoice == "B")
                        {
                            var list = db.GetAll<Checkup>();
                            foreach (var x in list) Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | Date: {x.Date} | Notes: {x.Notes} | Type: {x.Type}");
                        }
                        else if (listChoice == "C")
                        {
                            var list = db.GetAll<Prescription>();
                            foreach (var x in list) Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | Med: {x.Medication} | Dose: {x.Dosage}");
                        }
                        break;

                    case "6":
                        Console.WriteLine("Enter Name to search:");
                        string searchName = Console.ReadLine();

                        // This uses Filtering and Ordering
                        var filteredPatients = db.GetWithFilter<Patient>("first_name", searchName, "age");

                        foreach (var p in filteredPatients)
                        {
                            Console.WriteLine($"Found: {p.FirstName}, Age: {p.Age}");
                        }
                        break;

                    case "7": // LAZY LOADING DEMO
                        Console.Write("Enter Patient ID for LAZY fetch: ");
                        if (int.TryParse(Console.ReadLine(), out int lazyId))
                        {
                            // STEP 1: Fetch ONLY the patient (Initial Lazy Load)
                            var patients = db.GetWithFilter<Patient>("id", lazyId);
                            var p = patients.FirstOrDefault();

                            if (p != null)
                            {
                                Console.WriteLine($"\n--- [LAZY] PATIENT RECORD ---");
                                Console.WriteLine($"Name: {p.FirstName}");
                                Console.WriteLine($"Email: {p.Email}");
                                // At this point, p.Checkups is an empty list
                                Console.WriteLine($"Checkups currently in memory: {p.Checkups.Count}");

                                Console.Write("\nDo you want to view this patient's checkup details? (y/n): ");
                                string c = Console.ReadLine()?.ToLower();

                                if (c == "y")
                                {
                                    Console.WriteLine("Fetching details from database...");
                                    // STEP 2: The "On-Demand" fetch triggered by user choice
                                    p.Checkups = db.GetWithFilter<Checkup>("patient_id", p.Id);

                                    Console.WriteLine($"\n--- CHECKUP HISTORY ---");
                                    if (p.Checkups.Any())
                                    {
                                        foreach (var ch in p.Checkups)
                                        {
                                            Console.WriteLine($"- {ch.Date:yyyy-MM-dd} [{ch.Type}]: {ch.Notes}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("No checkups found for this patient.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Returning to menu without loading details.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Patient not found.");
                            }
                        }
                        break;

                    case "8": // Proper Eager Loading with Generic JOIN
                        Console.Write("Enter Patient ID for Eager JOIN fetch: ");
                        if (int.TryParse(Console.ReadLine(), out int eagerId))
                        {
                            // We pass the Types and the Foreign Key column names
                            // This is generic: it doesn't know about "Patient" internally, it uses Reflection
                            var p = db.GetEagerJoined<Patient, Checkup, Prescription>(eagerId, "patient_id", "patient_id");

                            if (p != null)
                            {
                                Console.WriteLine($"\n[EAGER JOIN] MEDICAL FILE: {p.FirstName} (ID: {p.Id})");
                                Console.WriteLine($"Checkups found: {p.Checkups.Count}");
                                Console.WriteLine($"Prescriptions found: {p.Prescriptions.Count}");

                                Console.WriteLine("\n--- HISTORY ---");
                                p.Checkups.ForEach(c => Console.WriteLine($"Checkup: {c.Notes} - {c.Date:yyyy-MM-dd} - {c.Type}"));
                                p.Prescriptions.ForEach(pr => Console.WriteLine($"Meds: {pr.Medication} - Dasage: {pr.Dosage} - Start date: {pr.StartDate} "));
                            }
                            else { Console.WriteLine("Patient not found."); }
                        }
                        break;

                    case "9":
                        {
                            Console.WriteLine("--- TRANSACTION: REGISTER NEW PATIENT WITH CHECKUP ---");
                            Console.Write("Patient Name: ");
                            string name = Console.ReadLine();
                            Console.Write("Patient Age: ");
                            int age = int.Parse(Console.ReadLine());
                            Console.Write("Patient Email: ");
                            string email = Console.ReadLine();
                            Console.Write("Checkup Notes: ");
                            string notes = Console.ReadLine();
                           
                            db.ExecuteTransaction(conn =>
                            {
                                var p = new Patient { FirstName = name, Email = email, Age = age };
                                int newId = db.InsertTransaction(p, conn);

                                var c = new Checkup { PatientId = newId, Notes = notes, Date = DateTime.Now };
                                db.InsertTransaction(c, conn);

                                Console.WriteLine("Transaction complete: Patient and Checkup linked!");
                            });


                        }
                        break;

                    case "10":
                        {
                            Console.WriteLine("\n--- TRANSACTION: DELETE PATIENT & ALL HISTORY ---");
                            Console.Write("Enter Patient ID to PERMANENTLY delete: ");
                            if (int.TryParse(Console.ReadLine(), out int pId))
                            {
                                Console.Write($"Are you sure you want to delete Patient {pId} and all their medical records? (y/n): ");
                                if (Console.ReadLine().ToLower() == "y")
                                {
                                    try
                                    {
                                        db.ExecuteTransaction(conn =>
                                        {
                                            // 1. Delete dependent records first (Prescriptions)
                                            db.DeleteTransaction<Prescription>("patient_id", pId, conn);
                                            Console.WriteLine("- Deleted related prescriptions.");

                                            // 2. Delete dependent records (Checkups)
                                            db.DeleteTransaction<Checkup>("patient_id", pId, conn);
                                            Console.WriteLine("- Deleted related checkups.");

                                            // 3. Delete the main record (Patient)
                                            db.DeleteTransaction<Patient>("id", pId, conn);
                                            Console.WriteLine("- Deleted patient record.");

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
                            string sql = "ALTER TABLE patients ADD COLUMN IF NOT EXISTS phone_number VARCHAR(20);";
                            migrator.ApplyMigration("20251226_AddPhoneToPatient", sql);
                        }
                        break;
                    
                    case "12":
                        {
                            //var migrator = new MigrationManager(myConnectionString);
                            //migrator.RollbackLastMigration();

                            Console.WriteLine("\n--- ROLLING BACK LAST MIGRATION ---");
                            var migrator = new MigrationManager(currentConnString);

                            // This is the SQL required to reverse the specific change made in Case 10
                            string undoSql = "ALTER TABLE patients DROP COLUMN IF EXISTS phone_number;";

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