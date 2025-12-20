using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomORM.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public string DbType { get; set; } // e.g., "VARCHAR(100)"
        public bool IsNullable { get; set; } = true;

        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }
}
