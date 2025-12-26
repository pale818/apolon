using CustomORM.Attributes;
using Npgsql;
using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CustomORM.Engine
{
    public class SqlGenerator
    {
        //CREATE TABLE 
        public string GenerateCreateTableSql<T>()
        {
            var type = typeof(T);

            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new Exception($"Class {type.Name} is missing the [Table] attribute.");

            var sql = new StringBuilder();
            sql.Append($"CREATE TABLE IF NOT EXISTS {tableAttr.Name} (");

            // Look for all properties that have a [Column] attribute
            var properties = type.GetProperties();
               

            var columnDefinitions = new List<string>();

            foreach (var prop in properties)
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                if (colAttr == null) continue;

                var pkAttr = prop.GetCustomAttribute<KeyAttribute>();           

                string columnDef;
                
                if (pkAttr != null)
                {
                    columnDef = $"{colAttr.Name} SERIAL PRIMARY KEY";
                }
                else
                {
                    // For normal columns, we use our smart GetSqlType helper
                    string sqlValueType = GetSqlType(prop.PropertyType);
                    columnDef = $"{colAttr.Name} {sqlValueType}";

                    // 1. UNIQUE Constraint
                    if (colAttr.IsUnique)
                        columnDef += " UNIQUE";

                    // 2. NOT NULL Constraint
                    if (!colAttr.IsNullable)
                        columnDef += " NOT NULL";

                    // 3. DEFAULT Constraint
                    if (!string.IsNullOrEmpty(colAttr.DefaultValue))
                        columnDef += $" DEFAULT {colAttr.DefaultValue}";
                }

                columnDefinitions.Add(columnDef);
            }

            sql.Append(string.Join(", ", columnDefinitions));
            sql.Append(");");

            return sql.ToString();
        }

        //INSERT
        public string GenerateInsertSql(object obj)
        {
            var type = obj.GetType();
            var tableAttr = type.GetCustomAttribute<TableAttribute>();

            // Get all columns, but SKIP the Primary Key (Serial) since DB handles it
            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null &&
                            p.GetCustomAttribute<KeyAttribute>() == null)
                .ToList();

            var columnNames = string.Join(", ", properties.Select(p => p.GetCustomAttribute<ColumnAttribute>().Name));

            // SMART VALUE MAPPING
            var values = properties.Select(p => {
                var val = p.GetValue(obj);
                if (val == null) return "NULL";

                if (val is string)
                    return $"'{val.ToString().Replace("'", "''")}'";
                if (val is DateTime dt)
                    return $"'{dt.ToString("yyyy-MM-dd HH:mm:ss")}'"; 

                if (val is bool b) return b ? "TRUE" : "FALSE";

                return val.ToString();
            });

            var valuesSql = string.Join(", ", values);

            return $"INSERT INTO {tableAttr.Name} ({columnNames}) VALUES ({valuesSql}) RETURNING id;";
        }


        //UPDATE

        public string GenerateUpdateSql(object obj)
        {
            var type = obj.GetType();
            var tableAttr = type.GetCustomAttribute<TableAttribute>();

            string pkName = "";
            object pkValue = null;
            var columnUpdates = new List<string>();

            foreach (var prop in type.GetProperties())
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                if (colAttr == null) continue;

                var val = prop.GetValue(obj);
                // SMART VALUE MAPPING
                string formattedVal;
                if (val == null) formattedVal = "NULL";
                else if (val is string)
                {
                    formattedVal = $"'{val.ToString().Replace("'", "''")}'";
                }
                else if (val is DateTime dt)
                {
                    // Forces the date into a format Postgres always understands: 2025-12-26 15:30:00
                    formattedVal = $"'{dt.ToString("yyyy-MM-dd HH:mm:ss")}'";
                }
                else if (val is bool b) formattedVal = b ? "TRUE" : "FALSE";
                else formattedVal = val.ToString();


                // If it's the Key, we use it for the WHERE clause
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    pkName = colAttr.Name;
                    pkValue = val;
                }
                else 
                {
                    columnUpdates.Add($"{colAttr.Name} = {formattedVal}");
                }
            }

            return $"UPDATE {tableAttr.Name} SET {string.Join(", ", columnUpdates)} WHERE {pkName} = {pkValue};";
        }

        //DELETE

        public string GenerateDeleteSql(object obj)
        {
            var type = obj.GetType();
            var tableAttr = type.GetCustomAttribute<TableAttribute>();

            string pkName = "";
            object pkValue = null;

            foreach (var prop in type.GetProperties())
            {
                // Find the property with the [Key] attribute
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                    pkName = colAttr.Name;
                    pkValue = prop.GetValue(obj);
                    break;
                }
            }

            if (string.IsNullOrEmpty(pkName))
                throw new Exception("Delete failed: No [Key] attribute found on class.");

            return $"DELETE FROM {tableAttr.Name} WHERE {pkName} = {pkValue};";
        }



        //LIST ALL 

        // 1. Simple SQL string builder
        public string GenerateSelectAllSql(Type type)
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return $"SELECT * FROM {tableAttr.Name};";
        }

        public T MapReaderToObject<T>(NpgsqlDataReader reader) where T : new()
        {
            var obj = new T();
            var type = typeof(T);

            foreach (var prop in type.GetProperties())
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                if (colAttr == null) continue;

                // Find the value in the database row by column name
                var dbValue = reader[colAttr.Name];

                if (dbValue != DBNull.Value)
                {
                    // Convert database types (like Int64) to C# types (like Int32)
                    prop.SetValue(obj, Convert.ChangeType(dbValue, prop.PropertyType));
                }
            }
            return obj;
        }


        //FOR DATE TIME IN CHECKUPS

        private static string GetSqlType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(int)) return "INT";
            if (type == typeof(string)) return "VARCHAR(255)";
            if (type == typeof(DateTime)) return "TIMESTAMP"; 
            return "TEXT";
        }



        //FILTER SEARCHING

        public string GenerateSelectSql<T>(string filterColumn = null, object filterValue = null, string orderByColumn = null, bool ascending = true)
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();

            string sql = $"SELECT * FROM {tableAttr.Name}";

            // Handling Filtering (WHERE)
            if (!string.IsNullOrEmpty(filterColumn) && filterValue != null)
            {
                if (filterValue is string strVal)
                {
                   
                    sql += $" WHERE {filterColumn} LIKE '{strVal.Replace("'", "''")}%'";
                }
                else
                {
                    string formattedValue = (filterValue is DateTime dt)
                        ? $"'{dt.ToString("yyyy-MM-dd HH:mm:ss")}'"
                        : filterValue.ToString();
                    sql += $" WHERE {filterColumn} = {formattedValue}";
                }
            }

            // Handling Ordering (ORDER BY)
            if (!string.IsNullOrEmpty(orderByColumn))
            {
                string direction = ascending ? "ASC" : "DESC";
                sql += $" ORDER BY {orderByColumn} {direction}";
            }

            return sql + ";";
        }

    }
}