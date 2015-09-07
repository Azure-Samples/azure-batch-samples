//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;

    public abstract class PropertyModel
    {
        public string PropertyName { get; private set; }

        protected PropertyModel(string propertyName)
        {
            this.PropertyName = propertyName;
        }
    }

    public class SimplePropertyModel : PropertyModel
    {
        public string PropertyValue { get; private set; }

        public SimplePropertyModel(string propertyName, string propertyValue)
            : base(propertyName)
        {
            this.PropertyValue = propertyValue;
        }
    }

    public class CollectionPropertyModel : PropertyModel
    {
        public List<PropertyModel> Items { get; private set; }

        public CollectionPropertyModel(string propertyName, List<PropertyModel> items)
            : base(propertyName)
        {
            this.Items = items;
        }
    }
}
