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
                    string sqlValueType = GetSqlType(prop.PropertyType);
                    columnDef = $"{colAttr.Name} {sqlValueType}";

                    //UNIQUE Constraint
                    if (colAttr.IsUnique)
                        columnDef += " UNIQUE";

                    //NOT NULL Constraint
                    if (!colAttr.IsNullable)
                        columnDef += " NOT NULL";

                    //DEFAULT Constraint
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

            var pkProp = type.GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null &&
                                     p.GetCustomAttribute<ColumnAttribute>() != null);

            string pkColumnName = pkProp?.GetCustomAttribute<ColumnAttribute>()?.Name ?? "id";

            // get all columns, skip  primary ky 
            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null &&
                            p.GetCustomAttribute<KeyAttribute>() == null)
                .ToList();

            var columnNames = string.Join(", ", properties.Select(p => p.GetCustomAttribute<ColumnAttribute>().Name));

            // SMART VALUE MAPPING
            var values = properties.Select(p =>
            {
                var val = p.GetValue(obj);
                if (val == null) return "NULL";

                if (val is string)
                    return $"'{val.ToString().Replace("'", "''")}'";
                if (val is DateTime dt)
                    return $"'{dt:yyyy-MM-dd HH:mm:ss}'";

                if (val is bool b) return b ? "TRUE" : "FALSE";

                if (val is Enum e)
                    return $"'{e}'";

                if (val is float || val is double || val is decimal)
                    return Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);

                return val.ToString();
            });

            var valuesSql = string.Join(", ", values);

            return $"INSERT INTO {tableAttr.Name} ({columnNames}) VALUES ({valuesSql}) RETURNING {pkColumnName};";
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
                    formattedVal = $"'{dt.ToString("yyyy-MM-dd HH:mm:ss")}'";
                }
                else if (val is bool b) formattedVal = b ? "TRUE" : "FALSE";

                else if (val is Enum e)
                {
                    return $"'{e.ToString()}'"; 
                }
                else formattedVal = val.ToString();


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

                var dbValue = reader[colAttr.Name];

                if (dbValue != DBNull.Value)
                {
                    prop.SetValue(obj, Convert.ChangeType(dbValue, prop.PropertyType));
                }
            }
            return obj;
        }


        //TYPE CONVERSION

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

            
            if (!string.IsNullOrEmpty(orderByColumn))
            {
                string direction = ascending ? "ASC" : "DESC";
                sql += $" ORDER BY {orderByColumn} {direction}";
            }

            return sql + ";";
        }


        public string GenerateTripleJoinSql<T, T1, T2>(string foreignKey1, string foreignKey2, int id)
        {
            var mainType = typeof(T);
            var type1 = typeof(T1);
            var type2 = typeof(T2);

            var mainTable = mainType.GetCustomAttribute<TableAttribute>().Name;
            var table1 = type1.GetCustomAttribute<TableAttribute>().Name;
            var table2 = type2.GetCustomAttribute<TableAttribute>().Name;

            
            string GetAliasedColumns(Type t, string prefix)
            {
                var props = t.GetProperties()
                    .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
                    .Select(p => $"{prefix}.{p.GetCustomAttribute<ColumnAttribute>().Name} AS {prefix}_{p.GetCustomAttribute<ColumnAttribute>().Name}");
                return string.Join(", ", props);
            }

            string sql = $@"
                SELECT {GetAliasedColumns(mainType, "p")}, 
                       {GetAliasedColumns(type1, "c")}, 
                       {GetAliasedColumns(type2, "pr")}
                FROM {mainTable} p
                LEFT JOIN {table1} c ON p.id = c.{foreignKey1}
                LEFT JOIN {table2} pr ON p.id = pr.{foreignKey2}
                WHERE p.id = {id};";

            return sql;
        }

        public string GenerateSelectWhereInSql<T>(string columnName, List<int> ids)
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new Exception($"Class {type.Name} is missing the [Table] attribute.");

            if (ids == null || ids.Count == 0)
                throw new Exception("GenerateSelectWhereInSql: ids list is empty.");

            // Build parameter list: @p0,@p1,@p2...
            string paramList = string.Join(", ", ids.Select((_, i) => $"@p{i}"));

            return $"SELECT * FROM {tableAttr.Name} WHERE {columnName} IN ({paramList});";
        }

        //check medication to add perscription
        public string GenerateExistsByIdSql<T>(string columnName)
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new Exception($"Class {type.Name} is missing the [Table] attribute.");

            return $"SELECT COUNT(1) FROM {tableAttr.Name} WHERE {columnName} = @id;";
        }




    }
}