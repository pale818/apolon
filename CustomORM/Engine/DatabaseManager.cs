using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using CustomORM.Attributes;
using System.Reflection;



namespace CustomORM.Engine
{
    // 1. MUST NOT be abstract if you want to use 'new DatabaseManager()'
    public class DatabaseManager
    {
        private readonly string _connectionString;
        private readonly SqlGenerator generator = new SqlGenerator();

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

        // We add optional parameters for conn and trans
        public int Insert<T>(T entity, NpgsqlConnection existingConn = null, NpgsqlTransaction existingTrans = null)
        {
            // 1. Generate SQL first
            string sql = generator.GenerateInsertSql(entity);

            // 3. STANDARD PATH:
            // Only used for non-transactional single inserts
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                Console.WriteLine($"DEBUG: INSERT CONN == NULL");

                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        //INSERT TRANSACTION- EXISTING CONNECTION
        public int InsertTransaction<T>(T entity, NpgsqlConnection conn)
        {
            var sql = generator.GenerateInsertSql(entity); // includes RETURNING id
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 30;
            return Convert.ToInt32(cmd.ExecuteScalar());
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


        //FILTER SEARCHING

        public List<T> GetWithFilter<T>(string filterCol = null, object filterVal = null, string orderCol = null) where T : new()
        {
            // Make sure 'generator' matches your variable name exactly!
            string sql = generator.GenerateSelectSql<T>(filterCol, filterVal, orderCol);
            var result = new List<T>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var properties = typeof(T).GetProperties()
                        .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
                        .ToList();

                    while (reader.Read())
                    {
                        var item = new T();
                        foreach (var prop in properties)
                        {
                            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                            var val = reader[colAttr.Name];
                            if (val != DBNull.Value)
                            {
                                prop.SetValue(item, val);
                            }
                        }
                        result.Add(item);
                    }
                }
            }
            return result;
        }
        


        //NAVIGATIONAL PROPERTIES FOR PATIENT

        // Instead of 'Patient', we use 'T' and 'TDetail'
        public T GetEntityWithDetails<T, TDetail>(int id, string foreignKeyColumn)
            where T : new()
            where TDetail : new()
        {
            // 1. Get the main entity (e.g., Patient)
            var entities = GetWithFilter<T>("id", id);
            if (entities.Count == 0) return default;
            var entity = entities[0];

            // 2. Use Reflection to find the List<TDetail> property and fill it
            var detailProperty = typeof(T).GetProperties()
                .FirstOrDefault(p => p.PropertyType == typeof(List<TDetail>));

            if (detailProperty != null)
            {
                var details = GetWithFilter<TDetail>(foreignKeyColumn, id);
                detailProperty.SetValue(entity, details);
            }

            return entity;
        }


        //TRANSACTIONS

        public void ExecuteTransaction(Action<NpgsqlConnection> action)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using (var begin = new NpgsqlCommand("BEGIN", conn))
                begin.ExecuteNonQuery();

            try
            {
                action(conn);

                using var commit = new NpgsqlCommand("COMMIT", conn);
                commit.ExecuteNonQuery();
            }
            catch
            {
                try
                {
                    using var rollback = new NpgsqlCommand("ROLLBACK", conn);
                    rollback.ExecuteNonQuery();
                }
                catch { }
                throw;
            }
        }



    }
}