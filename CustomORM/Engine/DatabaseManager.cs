using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace CustomORM.Engine
{
    // 1. MUST NOT be abstract if you want to use 'new DatabaseManager()'
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        //CREATE
        public void CreateTableFromClass<T>()
        {
            // 2. Create an instance because your SqlGenerator is NOT static
            SqlGenerator generator = new SqlGenerator();

            // 3. Call it using the Generic <T> to match your class
            string sql = generator.GenerateCreateTableSql<T>();

            Console.WriteLine("--- GENERATED SQL ---");
            Console.WriteLine(sql);
            Console.WriteLine("----------------------");

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                Console.WriteLine("SUCCESS: Table created in Supabase!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DATABASE ERROR: {ex.Message}");
            }
        }

        //INSERT

        public void Insert<T>(T entity)
        {
            SqlGenerator generator = new SqlGenerator();
            string sql = generator.GenerateInsertSql(entity);

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                Console.WriteLine("SUCCESS: Data inserted into Supabase!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"INSERT ERROR: {ex.Message}");
            }
        }

        //UPDATE

        public void Update<T>(T entity)
        {
            var sql = new SqlGenerator().GenerateUpdateSql(entity);
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("SUCCESS: Patient updated!");
        }


        //DELETE

        public void Delete<T>(T entity)
        {
            var sql = new SqlGenerator().GenerateDeleteSql(entity);

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("SUCCESS: Record deleted from Supabase!");
        }


        //LIST ALL

        public List<T> GetAll<T>() where T : new()
        {
            var list = new List<T>();
            var generator = new SqlGenerator();
            string sql = generator.GenerateSelectAllSql(typeof(T));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Turn the current row into a C# object
                        T entity = generator.MapReaderToObject<T>(reader);
                        list.Add(entity);
                    }
                }
            }
            return list;
        }
    }
}