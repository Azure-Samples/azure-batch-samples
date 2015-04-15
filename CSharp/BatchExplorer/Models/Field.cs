using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.BatchExplorer
{
    public class Field
    {
        public string Value { get; set; }

        public Field(Object obj)
        {
            if (obj != null)
                Value = obj.ToString();
            else
                Value = "";
        }
    }
}
