using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.BatchExplorer.Helpers;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The different types of refresh available.
    /// </summary>
    [Flags]
    public enum ModelRefreshType
    {
        /// <summary>
        /// Refresh just this one object
        /// </summary>
        Basic = 1,

        /// <summary>
        /// Refresh this objects children
        /// </summary>
        Children = 2
    };

    public abstract class ModelBase : EntityBase
    {
        public const string LastUpdateFromServerString = "Last Update from Server";

        /// <summary>
        /// The time at which this model was last updated from the server
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime LastUpdatedTime { get; protected set; }

        /// <summary>
        /// Sorted dictionary of property values representing the state of this object
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public abstract SortedDictionary<string, object> PropertyValuePairs { get; }

        /// <summary>
        /// Determines if this model has loaded its children from the server or not
        /// </summary>
        public bool HasLoadedChildren { get; protected set; }

        /// <summary>
        /// Refreshes this model with the specified refresh type
        /// </summary>
        /// <param name="refreshType">The refresh type to use</param>
        /// <param name="showTrackedOperation">Show the refresh row in the tracking list</param>
        /// <returns></returns>
        public abstract System.Threading.Tasks.Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true);
        
        /// <summary>
        /// Uses reflection to generated a storaged Dictionary containing property names and their values.
        /// BEWARE THAT BAD THINGS WILL HAPPEN WITH CIRCULAR REFERENCES
        /// </summary>
        /// <param name="o">The object to convert to a sorted dictionary of values</param>
        /// <param name="namePrefix">Prefix to insert before property names of object o</param>
        /// <param name="propertiesToOmit">A list of names of properties to omit</param>
        /// <returns>A sorted dictionary whose keys are the names of properties (ex: A.B) and whose values are the value of that property (for example the value of A.B)</returns>
        protected static SortedDictionary<string, object> ObjectToSortedDictionary(object o, string namePrefix = "", List<string> propertiesToOmit = null)
        {
            //Short circuit for enumerables because we don't handle them at all
            var nameValuePairs = new SortedDictionary<string, object>();
            if (o == null || o is IEnumerable)
            {
                return nameValuePairs;
            }
            
            var properties = o.GetType().GetProperties();
            foreach (var propInfo in properties)
            {
                //Determine if this property is in the list of properties to omit -- if it is we skip it
                if (propertiesToOmit != null && propertiesToOmit.Contains(propInfo.Name))
                {
                    continue;
                }

                try
                {
                    //Get the property type and value
                    object propertyValue = propInfo.GetValue(o);
                    Type propertyType = propertyValue == null ? typeof(object) : propertyValue.GetType();
                
                    //Find the implementer of the ToString method
                    Type toStringDeclaringType = propertyType.GetMethod("ToString", Type.EmptyTypes).DeclaringType;
                    string formattedPropertyName = string.IsNullOrEmpty(namePrefix) ? propInfo.Name : string.Format(CultureInfo.CurrentCulture, "{0}.{1}", namePrefix, propInfo.Name);
                
                    //If the prop value is null, just write string.Empty
                    if (propertyValue == null)
                    {
                        nameValuePairs.Add(formattedPropertyName, string.Empty);
                    }
                    //For other types which have a ToString declared, we want to use the built in ToString()
                    else if (toStringDeclaringType != typeof(object))
                    {
                        nameValuePairs.Add(formattedPropertyName, propertyValue);
                    }
                    //For any complex properties without a ToString of their own, we recurse
                    else
                    {
                        var innerResults = ObjectToSortedDictionary(propInfo.GetValue(o), formattedPropertyName);
                        foreach (var innerResult in innerResults)
                        {
                            nameValuePairs.Add(innerResult.Key, innerResult.Value);
                        }
                    }
                }
                catch (TargetInvocationException)
                {
                    // Skip. Cannot access all properties for bound objects
                }
            }

            return nameValuePairs;
        }

        /// <summary>
        /// Uses reflection to generated a storaged Dictionary containing property names and their values.
        /// BEWARE THAT BAD THINGS WILL HAPPEN WITH CIRCULAR REFERENCES
        /// </summary>
        /// <param name="o">The object to convert to a sorted dictionary of values</param>
        /// <param name="namePrefix">Prefix to insert before property names of object o</param>
        /// <param name="propertiesToOmit">A list of names of properties to omit</param>
        /// <returns>A sorted dictionary whose keys are the names of properties (ex: A.B) and whose values are the value of that property (for example the value of A.B)</returns>
        protected static IDictionary<string, IDictionary<string, string>> ObjectToSortedDictionaryList(object o, string namePrefix = "", List<string> propertiesToOmit = null)
        {
            //Short circuit for enumerables because we don't handle them at all
            IDictionary<string, IDictionary<string, string>> propertySet = new Dictionary<string, IDictionary<string, string>>();

            if (o == null || o is IEnumerable)
            {
                return propertySet;
            }

            var properties = o.GetType().GetProperties();
            foreach (var propInfo in properties)
            {
                //Determine if this property is in the list of properties to omit -- if it is we skip it
                if (propertiesToOmit != null && propertiesToOmit.Contains(propInfo.Name))
                {
                    continue;
                }

                //Get the property type and value
                object propertyValue = propInfo.GetValue(o);
                Type propertyType = propertyValue == null ? typeof(object) : propertyValue.GetType();

                //Find the implementer of the ToString method
                Type toStringDeclaringType = propertyType.GetMethod("ToString", Type.EmptyTypes).DeclaringType;
                string formattedPropertyName = string.IsNullOrEmpty(namePrefix) ? propInfo.Name : string.Format(CultureInfo.CurrentCulture, "{0}.{1}", namePrefix, propInfo.Name);

                string[] parts = formattedPropertyName.Split('.');
                string group;
                string property;
                string value;

                // Add to the default list
                if (parts.Count() == 1)
                {
                    group = "Basic";
                    property = formattedPropertyName;
                }
                else
                {
                    group = parts[0];
                    property = string.Join(".", parts.Skip(1));
                }

                if (!propertySet.ContainsKey(group))
                {
                    propertySet.Add(group, new SortedDictionary<string, string>());
                }

                //If the prop value is null, just write string.Empty
                if (propertyValue == null)
                {
                    value = string.Empty;
                    //nameValuePairs.Add(formattedPropertyName, string.Empty);
                    propertySet[group].Add(property, string.Empty);
                }
                //For other types which have a ToString declared, we want to use the built in ToString()
                else if (toStringDeclaringType != typeof(object))
                {
                    //nameValuePairs.Add(formattedPropertyName, propertyValue);
                    //value = propertyValue.ToString();
                    propertySet[group].Add(property, propertyValue.ToString());
                }
                //For any complex properties without a ToString of their own, we recurse
                else
                {
                    var innerResults = ObjectToSortedDictionaryList(propInfo.GetValue(o), formattedPropertyName);
                    foreach (var innerResult in innerResults)
                    {
                        //nameValuePairs.Add(innerResult.Key, innerResult.Value);
                        propertySet[group].Add(innerResult.Key, innerResult.Value.ToString());
                    }
                }
            }

            return propertySet;
        }

        /// <summary>
        /// Fires a set of property changes on refresh based on the refresh type
        /// </summary>
        /// <param name="refreshType"></param>
        protected void FireChangesOnRefresh(ModelRefreshType refreshType)
        {
            Type myType = this.GetType();

            //Get all the public properties of this type
            PropertyInfo[] properties = myType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                //Find all attributes on this property
                Attribute[] attributes = Attribute.GetCustomAttributes(property, true);
                IEnumerable<ChangeTrackedAttribute> changeTrackedAttributes = attributes.OfType<ChangeTrackedAttribute>();
                bool hasPropertyChanged = changeTrackedAttributes.Any() && changeTrackedAttributes.All(attribute => attribute.HasChanged(refreshType));

                if (hasPropertyChanged)
                {
                    this.FirePropertyChangedEvent(property.Name); //Fire the event associated with this property on change
                }
            }
        }
    }
}
