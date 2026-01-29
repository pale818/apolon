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
            SqlGenerator generator = new SqlGenerator();

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
                Console.WriteLine("-----------------------------------------------------------------");
                Console.WriteLine($"SQL: {sql}");
                Console.WriteLine("-----------------------------------------------------------------");

                Console.WriteLine("SUCCESS: Table created !");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DATABASE ERROR: {ex.Message}");
            }
        }

        //INSERT

        public int Insert<T>(T entity)
        {
            string sql = generator.GenerateInsertSql(entity);

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                Console.WriteLine("\n[ERROR] This email already exists! Please try a different one.");
                return -1; // Return -1 to indicate failure due to duplicate
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] An unexpected error occurred: {ex.Message}");
                return -1;
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
            var sql = generator.GenerateUpdateSql(entity);
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
                Console.WriteLine("SUCCESS: Record updated!");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                Console.WriteLine("\n[ERROR] Update failed: This email is already taken by another user.");
            }
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
                        T entity = generator.MapReaderToObject<T>(reader);
                        list.Add(entity);
                    }
                }
            }
            return list;
        }


        //FILTER SEARCHING AND ORDERING
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
                            if (prop.PropertyType.IsEnum)
                            {
                                prop.SetValue(item, Enum.Parse(prop.PropertyType, val.ToString()));
                            }
                            else
                            {
                                prop.SetValue(item, Convert.ChangeType(val, prop.PropertyType));
                            }
                        }
                        result.Add(item);
                    }
                }
            }
            return result;
        }



        //NAVIGATIONAL PROPERTIES FOR PATIENT ( (1-many, many-1, 1-1)

        public T GetEntityWithDetails<T, TDetail>(int id, string foreignKeyColumn)
            where T : new()
            where TDetail : new()
        {
            var entities = GetWithFilter<T>("id", id);
            if (entities.Count == 0) return default;
            var entity = entities[0];

            var detailProperty = typeof(T).GetProperties()
                .FirstOrDefault(p => p.PropertyType == typeof(List<TDetail>));

            if (detailProperty != null)
            {
                var details = GetWithFilter<TDetail>(foreignKeyColumn, id);
                detailProperty.SetValue(entity, details);
            }

            return entity;
        }


        //EAGER JOIN( gets everything)
        public T GetEagerJoined<T, T1, T2>(int id, string fk1, string fk2)
            where T : new() where T1 : new() where T2 : new()
        {
            string sql = generator.GenerateTripleJoinSql<T, T1, T2>(fk1, fk2, id);
            T mainEntity = default;

            // Reflection to find the List properties on the Patient class
            var listProp1 = typeof(T).GetProperties().FirstOrDefault(p => p.PropertyType == typeof(List<T1>));
            var listProp2 = typeof(T).GetProperties().FirstOrDefault(p => p.PropertyType == typeof(List<T2>));

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (mainEntity == null) mainEntity = MapAliased<T>(reader, "p");

                var item1 = MapAliased<T1>(reader, "c");
                var item2 = MapAliased<T2>(reader, "pr");

                // Add to lists if they exist and aren't duplicates
                if (item1 != null && listProp1 != null)
                {
                    var list = (List<T1>)listProp1.GetValue(mainEntity);
                    var idProp = typeof(T1).GetProperties().First(p => p.GetCustomAttribute<KeyAttribute>() != null);
                    var itemId = idProp.GetValue(item1);

                    if (!list.Any(x => idProp.GetValue(x).Equals(itemId)))
                        list.Add(item1);
                }

                if (item2 != null && listProp2 != null)
                {
                    var list = (List<T2>)listProp2.GetValue(mainEntity);
                    var idProp = typeof(T2).GetProperties().First(p => p.GetCustomAttribute<KeyAttribute>() != null);
                    var itemId = idProp.GetValue(item2);

                    if (!list.Any(x => idProp.GetValue(x).Equals(itemId)))
                        list.Add(item2);
                }
            }
            return mainEntity;
        }
        public List<T> GetWhereIn<T>(string columnName, IEnumerable<int> values) where T : new()
        {
            var ids = values?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new List<T>();

            string sql = generator.GenerateSelectWhereInSql<T>(columnName, ids);

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);

            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", ids[i]);

            using var reader = cmd.ExecuteReader();
            var result = new List<T>();

            while (reader.Read())
            {
                result.Add(generator.MapReaderToObject<T>(reader));
            }

            return result;
        }

        //med check 
        public bool ExistsById<T>(string columnName, int id) where T : new()
        {
            string sql = generator.GenerateExistsByIdSql<T>(columnName);

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            object? scalar = cmd.ExecuteScalar();
            if (scalar == null || scalar == DBNull.Value) return false;

            return Convert.ToInt32(scalar) > 0;
        }



        // map aliased columns back to objects
        private T MapAliased<T>(NpgsqlDataReader reader, string prefix) where T : new()
        {
            var obj = new T();
            var props = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
                .ToList();

            var pk = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);
            if (pk == null) return default;

            string pkAlias = $"{prefix}_{pk.GetCustomAttribute<ColumnAttribute>().Name}";

            if (reader[pkAlias] == DBNull.Value) return default;

            foreach (var prop in props)
            {
                string alias = $"{prefix}_{prop.GetCustomAttribute<ColumnAttribute>().Name}";
                var dbValue = reader[alias];

                if (dbValue != DBNull.Value)
                {
                    if (prop.PropertyType.IsEnum)
                    {
                        prop.SetValue(obj, Enum.Parse(prop.PropertyType, dbValue.ToString()));
                    }
                    else
                    {
                        prop.SetValue(obj, Convert.ChangeType(dbValue, prop.PropertyType));
                    }
                }
            }
            return obj;
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
            catch (Exception ex)
            {
                Console.WriteLine($"Logic failed: {ex.Message}. Rolling back...");
                try
                {
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        using var rollback = new NpgsqlCommand("ROLLBACK", conn);
                        rollback.ExecuteNonQuery();
                    }
                }
                catch (Exception rollbackEx)
                {
                    Console.WriteLine($"Rollback also failed: {rollbackEx.Message}");
                }
                throw; 
            }
        }

        // DELETE TRANSACTION 
        public void DeleteTransaction<T>(string filterColumn, object filterValue, NpgsqlConnection conn)
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();

            string formattedValue = (filterValue is string s) ? $"'{s.Replace("'", "''")}'" : filterValue.ToString();

            string sql = $"DELETE FROM {tableAttr.Name} WHERE {filterColumn} = {formattedValue};";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }



    }
}