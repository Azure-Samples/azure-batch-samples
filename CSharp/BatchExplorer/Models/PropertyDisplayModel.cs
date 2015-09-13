//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
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

        internal static string GetItemDisplayPrefix(IEnumerable collection)
        {
            Type elementType = ElementType(collection.GetType());
            if (elementType == null)
            {
                return "item";
            }
            return elementType.Name;
        }

        private static Type ElementType(Type collectionType)
        {
            Debug.Assert(collectionType != null);
            Debug.Assert(typeof(IEnumerable).IsAssignableFrom(collectionType));

            Type[] stronglyTypedInterfaces = collectionType.FindInterfaces((m, c) => m.IsConstructedGenericType && m.GetGenericTypeDefinition() == typeof(IEnumerable<>), null);

            if (stronglyTypedInterfaces == null || stronglyTypedInterfaces.Length == 0 || stronglyTypedInterfaces.Length > 1)
            {
                return null;  // not a strongly typed collection, or a strongly typed collection of more than one thing (yikes)
            }

            return stronglyTypedInterfaces[0].GetGenericArguments()[0];
        }
    }
}
