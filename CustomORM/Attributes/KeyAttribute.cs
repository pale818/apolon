using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


 //for updating/deleting specific rows in table
namespace CustomORM.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class KeyAttribute: Attribute
    {
        public bool IsAutoIncrement { get; set; } = true;
    }
}
