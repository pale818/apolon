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

                // Check if already applied
                using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM migrations WHERE name = @name", conn))
                {
                    checkCmd.Parameters.AddWithValue("name", migrationName);
                    if (Convert.ToInt64(checkCmd.ExecuteScalar()) > 0)
                    {
                        Console.WriteLine($"Migration '{migrationName}' already applied.");
                        return;
                    }
                }

                // Apply within transaction
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Run the SQL change
                        using (var migrateCmd = new NpgsqlCommand(sql, conn, trans))
                        {
                            migrateCmd.ExecuteNonQuery();
                        }

                        // 2. Log the migration
                        using (var logCmd = new NpgsqlCommand("INSERT INTO migrations (name) VALUES (@name)", conn, trans))
                        {
                            logCmd.Parameters.AddWithValue("name", migrationName);
                            logCmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        Console.WriteLine($"Successfully applied: {migrationName}");
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        Console.WriteLine($"FAILED: {ex.Message}");
                    }
                }
            }
        }

        // LO5 Desired: Rollback functionality
        public void RollbackLastMigration()
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                string lastMig = "";

                using (var cmd = new NpgsqlCommand("SELECT name FROM migrations ORDER BY executed_at DESC LIMIT 1", conn))
                {
                    lastMig = cmd.ExecuteScalar()?.ToString();
                }

                if (string.IsNullOrEmpty(lastMig)) return;

                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        using (var del = new NpgsqlCommand("DELETE FROM migrations WHERE name = @name", conn, trans))
                        {
                            del.Parameters.AddWithValue("name", lastMig);
                            del.ExecuteNonQuery();
                        }
                        trans.Commit();
                        Console.WriteLine($"Rollback success for: {lastMig}");
                    }
                    catch { trans.Rollback(); }
                }
            }
        }



    }
}