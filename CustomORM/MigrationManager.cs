using Npgsql;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using CustomORM.Attributes;

namespace CustomORM
{
    public class MigrationManager
    {
        private readonly string _connectionString;

        public MigrationManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        // --- UPDATED METHOD SIGNATURE TO ACCEPT 3 ARGUMENTS ---
        public void ApplyMigration(string migrationName, string sql, bool executeSql = true)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // 1. ENSURE MIGRATIONS TABLE EXISTS
                string initSql = @"CREATE TABLE IF NOT EXISTS migrations (
                            id SERIAL PRIMARY KEY,
                            name VARCHAR(255) UNIQUE NOT NULL,
                            executed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP);";
                using (var initCmd = new NpgsqlCommand(initSql, conn)) initCmd.ExecuteNonQuery();

                // 2. CHECK IF APPLIED
                using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM migrations WHERE name = @name", conn))
                {
                    checkCmd.Parameters.AddWithValue("name", migrationName);
                    if (Convert.ToInt64(checkCmd.ExecuteScalar()) > 0)
                    {
                        Console.WriteLine($"Migration '{migrationName}' already applied.");
                        return;
                    }
                }

                // 3. APPLY
                using (var begin = new NpgsqlCommand("BEGIN", conn)) begin.ExecuteNonQuery();

                try
                {
                    // ONLY run the SQL if executeSql is true
                    if (executeSql)
                    {
                        using (var migrateCmd = new NpgsqlCommand(sql, conn)) migrateCmd.ExecuteNonQuery();
                    }

                    // Log the migration record
                    using (var logCmd = new NpgsqlCommand("INSERT INTO migrations (name) VALUES (@name)", conn))
                    {
                        logCmd.Parameters.AddWithValue("name", migrationName);
                        logCmd.ExecuteNonQuery();
                    }

                    using (var commit = new NpgsqlCommand("COMMIT", conn)) commit.ExecuteNonQuery();

                    if (executeSql)
                        Console.WriteLine($"Successfully applied: {migrationName}");
                }
                catch (Exception ex)
                {
                    using (var rollback = new NpgsqlCommand("ROLLBACK", conn)) rollback.ExecuteNonQuery();
                    Console.WriteLine($"FAILED: {ex.Message}");
                    throw;
                }
            }
        }

        public void RollbackLastMigration(string undoSql)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                string lastMig;
                using (var cmd = new NpgsqlCommand("SELECT name FROM migrations ORDER BY executed_at DESC LIMIT 1", conn))
                {
                    lastMig = cmd.ExecuteScalar()?.ToString();
                }

                if (string.IsNullOrEmpty(lastMig))
                {
                    Console.WriteLine("No migrations found to rollback.");
                    return;
                }

                using (var begin = new NpgsqlCommand("BEGIN", conn)) begin.ExecuteNonQuery();
                try
                {
                    using (var undoCmd = new NpgsqlCommand(undoSql, conn)) undoCmd.ExecuteNonQuery();

                    using (var del = new NpgsqlCommand("DELETE FROM migrations WHERE name = @name", conn))
                    {
                        del.Parameters.AddWithValue("name", lastMig);
                        del.ExecuteNonQuery();
                    }

                    using (var commit = new NpgsqlCommand("COMMIT", conn)) commit.ExecuteNonQuery();
                    Console.WriteLine($"Rollback success for: {lastMig}");
                }
                catch (Exception ex)
                {
                    using (var rollback = new NpgsqlCommand("ROLLBACK", conn)) rollback.ExecuteNonQuery();
                    Console.WriteLine($"Rollback failed: {ex.Message}");
                }
            }
        }

        public void AutoMigrate<T>()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) return;

            string tableName = tableAttr.Name;
            List<string> dbColumns = new List<string>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                string checkSql = "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
                using (var cmd = new NpgsqlCommand(checkSql, conn))
                {
                    cmd.Parameters.AddWithValue("table", tableName.ToLower());
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) dbColumns.Add(reader.GetString(0).ToLower());
                    }
                }

                var properties = type.GetProperties()
                    .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
                    .ToList();
                var classColumnNames = properties.Select(p => p.GetCustomAttribute<ColumnAttribute>().Name.ToLower()).ToList();

                foreach (var prop in properties)
                {
                    string colName = prop.GetCustomAttribute<ColumnAttribute>().Name.ToLower();

                    if (!dbColumns.Contains(colName))
                    {
                        string sqlType = GetSqlType(prop.PropertyType);
                        string alterSql = $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {colName} {sqlType};";

                        using (var cmd = new NpgsqlCommand(alterSql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        // This now works because the signature above was updated
                        ApplyMigration($"AutoAdd_{tableName}_{colName}", alterSql, false);

                        Console.WriteLine($"[Auto-Migration] ADDED column: {colName} to {tableName}");
                    }
                }

                foreach (var dbCol in dbColumns)
                {
                    if (dbCol == "id" || dbCol == "patient_id") continue;

                    if (!classColumnNames.Contains(dbCol))
                    {
                        Console.WriteLine($"\n[Auto-Migration] Found orphaned column in DB: {dbCol}");
                        Console.Write($"Do you want to DELETE '{dbCol}' from {tableName}? (y/n): ");

                        if (Console.ReadLine()?.ToLower() == "y")
                        {
                            string dropSql = $"ALTER TABLE {tableName} DROP COLUMN IF EXISTS {dbCol};";

                            using (var cmd = new NpgsqlCommand(dropSql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // This now works because the signature above was updated
                            ApplyMigration($"AutoDrop_{tableName}_{dbCol}", dropSql, false);

                            Console.WriteLine($"[Auto-Migration] DROPPED column: {dbCol}");
                        }
                    }
                }
            }
        }

        private string GetSqlType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(int)) return "INT";
            if (type == typeof(string)) return "VARCHAR(255)";
            if (type == typeof(DateTime)) return "TIMESTAMP";
            return "TEXT";
        }
    }
}