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

            string myConnectionString = config.GetConnectionString("DefaultConnection");
            // 2. Initialize the Manager
            DatabaseManager db = new DatabaseManager(myConnectionString);

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

                Console.WriteLine("\n--- CUSTOM ORM MENU ---");
                Console.WriteLine("1. Create Table");
                Console.WriteLine("2. Insert");
                Console.WriteLine("3. Update ");
                Console.WriteLine("4. Delete ");
                Console.WriteLine("5. List All ");
                Console.WriteLine("6. Filter by name ");
                Console.WriteLine("7. Patient record");
                Console.WriteLine("8. Add Transaction");
                Console.WriteLine("9. Run Migrations (Add Email Column)");
                Console.WriteLine("10. Rollback Last Migration");
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
                            Console.Write("Enter Checkup Type (GP, MRI, X-RAY, etc.): ");
                            string type = Console.ReadLine();
                            db.Insert(new Checkup { PatientId = pId, Date = DateTime.Now, Notes = notes, Type= type });
                        }
                        else if (typeChoice == "C")
                        {
                            Console.Write("Enter Patient ID for this prescription: ");
                            int pId = int.Parse(Console.ReadLine());
                            Console.Write("Medication Name: ");
                            string med = Console.ReadLine();
                            Console.Write("Dosage: ");
                            string dose = Console.ReadLine();
                            db.Insert(new Prescription { PatientId = pId, Medication = med, Dosage = dose });
                        }
                        break;

                    case "3":
                        Console.WriteLine("\n--- UPDATE RECORD ---");
                        Console.WriteLine("A. Patient | B. Checkup | C. Prescription");
                        string updChoice = Console.ReadLine().ToUpper();

                        if (updChoice == "A")
                        {
                            Console.Write("Enter Patient ID to update: "); int id = int.Parse(Console.ReadLine());
                            Console.Write("New Name: "); string n = Console.ReadLine();
                            Console.Write("New Age: "); int a = int.Parse(Console.ReadLine());
                            Console.Write("New Email: "); string e = Console.ReadLine();
                            db.Update(new Patient { Id = id, FirstName = n, Age = a ,Email = e});
                        }
                        else if (updChoice == "B")
                        {
                            Console.Write("Enter Checkup ID to update: "); int id = int.Parse(Console.ReadLine());
                            Console.Write("New Notes: "); string notes = Console.ReadLine();
                            Console.Write("New Type: "); string type = Console.ReadLine();
                            db.Update(new Checkup { Id = id, Date = DateTime.Now, Notes = notes, Type = type});
                        }
                        else if (updChoice == "C")
                        {
                            Console.Write("Enter Prescription ID to update: "); int id = int.Parse(Console.ReadLine());
                            Console.Write("New Dosage: "); string dose = Console.ReadLine();
                            db.Update(new Prescription { Id = id, Dosage = dose });
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

                    case "7":
                        Console.Write("Enter Patient ID to view Full Medical Record: ");
                        if (int.TryParse(Console.ReadLine(), out int searchId))
                        {
                        
                            var p = db.GetEntityWithDetails<Patient, Checkup>(searchId, "patient_id");

                            if (p != null)
                            {
                                Console.WriteLine($"\n--- MEDICAL FILE: {p.FirstName} ---");
                                Console.WriteLine($"Email: {p.Email}");
                                Console.WriteLine($"Age: {p.Age}");

                                Console.WriteLine("\nCHECKUP HISTORY:");
                                foreach (var c in p.Checkups)
                                {
                                    Console.WriteLine($"- {c.Date:yyyy-MM-dd} [{c.Type}]: {c.Notes}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Patient not found.");
                            }
                        }
                        break;

                    case "8":
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
                    
                    case "9":
                        {
                            var migrator = new MigrationManager(myConnectionString);
                            // Demonstration: Adding a column that wasn't there before
                            string sql = "ALTER TABLE patients ADD COLUMN IF NOT EXISTS phone_number VARCHAR(20);";
                            migrator.ApplyMigration("20251226_AddPhoneToPatient", sql);
                        }
                        break;
                    
                    case "10":
                        {
                            var migrator = new MigrationManager(myConnectionString);
                            migrator.RollbackLastMigration();
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

            }






        }

    }
}