using System;
using System.Collections.Generic;
using Npgsql;

namespace CustomORM
{
    public class MigrationManager
    {
        private readonly string _connectionString;

        public MigrationManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        // LO5 Desired: Execute existing migrations with tracking

        

        public void ApplyMigration(string migrationName, string sql)
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

                // 3. APPLY (Using manual BEGIN/COMMIT for Pooler Compatibility)
                using (var begin = new NpgsqlCommand("BEGIN", conn)) begin.ExecuteNonQuery();

                try
                {
                    // Run the actual migration SQL
                    using (var migrateCmd = new NpgsqlCommand(sql, conn)) migrateCmd.ExecuteNonQuery();

                    // Log the migration record
                    using (var logCmd = new NpgsqlCommand("INSERT INTO migrations (name) VALUES (@name)", conn))
                    {
                        logCmd.Parameters.AddWithValue("name", migrationName);
                        logCmd.ExecuteNonQuery();
                    }

                    using (var commit = new NpgsqlCommand("COMMIT", conn)) commit.ExecuteNonQuery();
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

        // LO5 Desired: Rollback functionality
        

        public void RollbackLastMigration(string undoSql)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // 1. Get the name of the last migration
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
                    // 2. Run the UNDO SQL (e.g., ALTER TABLE patients DROP COLUMN phone_number)
                    using (var undoCmd = new NpgsqlCommand(undoSql, conn)) undoCmd.ExecuteNonQuery();

                    // 3. Remove the log
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

    }
}