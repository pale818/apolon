using System;
using System.Linq;
using System.Reflection;
using System.Text;
using CustomORM.Attributes;

namespace CustomORM.Engine
{
    public class SqlGenerator
    {
        public string GenerateCreateTableSql<T>()
        {
            var type = typeof(T);

            // Find the [Table] attribute on the class
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new Exception($"Class {type.Name} is missing the [Table] attribute.");

            var sql = new StringBuilder();
            sql.Append($"CREATE TABLE IF NOT EXISTS {tableAttr.Name} (");

            // Look for all properties that have a [Column] attribute
            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
                .ToList();

            var columnDefinitions = new List<string>();

            foreach (var prop in properties)
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                var pkAttr = prop.GetCustomAttribute<KeyAttribute>();

                // Build the column string: "column_name DATA_TYPE"
                string columnDef = $"{colAttr.Name} {colAttr.DbType}";

                // If it's a Primary Key, add the constraints
                if (pkAttr != null)
                {
                    columnDef += " PRIMARY KEY";
                    if (pkAttr.IsAutoIncrement && colAttr.DbType.ToUpper() == "SERIAL")
                    {
                        // Note: In Postgres, 'SERIAL' already implies auto-increment
                    }
                }
                else if (!colAttr.IsNullable)
                {
                    columnDef += " NOT NULL";
                }

                columnDefinitions.Add(columnDef);
            }

            sql.Append(string.Join(", ", columnDefinitions));
            sql.Append(");");

            return sql.ToString();
        }
    }
}