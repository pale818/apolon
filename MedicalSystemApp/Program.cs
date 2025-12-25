using CustomORM.Engine; // Points to your ENGINE folder
using MedicalSystemApp;      // Points to where your Patient class is
using System;
using Microsoft.Extensions.Configuration;


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
                            db.Insert(new Patient { FirstName = name, Age = age });
                        }
                        else if (typeChoice == "B")
                        {
                            Console.Write("Enter Patient ID for this checkup: ");
                            int pId = int.Parse(Console.ReadLine());
                            Console.Write("Enter Doctor Notes: ");
                            string notes = Console.ReadLine();
                            db.Insert(new Checkup { PatientId = pId, Date = DateTime.Now, Notes = notes });
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
                            db.Update(new Patient { Id = id, FirstName = n, Age = a });
                        }
                        else if (updChoice == "B")
                        {
                            Console.Write("Enter Checkup ID to update: "); int id = int.Parse(Console.ReadLine());
                            Console.Write("New Notes: "); string notes = Console.ReadLine();
                            db.Update(new Checkup { Id = id, Date = DateTime.Now, Notes = notes });
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
                            foreach (var x in list) Console.WriteLine($"ID: {x.Id} | Name: {x.FirstName} | Age: {x.Age}");
                        }
                        else if (listChoice == "B")
                        {
                            var list = db.GetAll<Checkup>();
                            foreach (var x in list) Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | Date: {x.Date} | Notes: {x.Notes}");
                        }
                        else if (listChoice == "C")
                        {
                            var list = db.GetAll<Prescription>();
                            foreach (var x in list) Console.WriteLine($"ID: {x.Id} | Patient: {x.PatientId} | Med: {x.Medication} | Dose: {x.Dosage}");
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