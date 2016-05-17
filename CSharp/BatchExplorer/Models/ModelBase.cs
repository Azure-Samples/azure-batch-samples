//Copyright (c) Microsoft Corporation

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.BatchExplorer.Helpers;
using System.Diagnostics;

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
        public abstract List<PropertyModel> PropertyModel { get; } 

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
        /// Uses reflection to build a hierarchy of properties on a given object.
        /// BEWARE THAT BAD THINGS WILL HAPPEN WITH CIRCULAR REFERENCES.
        /// </summary>
        /// <param name="o">The object to process.</param>
        /// <param name="propertiesToOmit">The names of properties to omit</param>
        /// <returns>A list of <see cref="PropertyModel"/> objects which represent the properties of the object.</returns>
        protected List<PropertyModel> ObjectToPropertyModel(object o, List<string> propertiesToOmit = null)
        {
            if (propertiesToOmit == null)
            {
                propertiesToOmit = new List<string>();
            }
            propertiesToOmit.Add("CustomBehaviors");
            
            List<PropertyModel> result = ExtractPropertyCollection(o, propertiesToOmit);
            result.Add(new SimplePropertyModel(LastUpdateFromServerString, this.LastUpdatedTime.ToString()));
            return result;
        }

        private static List<PropertyModel> ExtractPropertyCollection(object o, List<string> propertiesToOmit)
        {
            Debug.Assert(propertiesToOmit != null);

            if (o == null)
            {
                return new List<PropertyModel>();
            }

            return o.GetType()
                    .GetProperties()
                    .Where(prop => !propertiesToOmit.Contains(prop.Name))
                    .SelectMany(prop => ExtractPropertyModel(o, prop, propertiesToOmit))
                    .ToList();
        }

        private static IEnumerable<PropertyModel> ExtractPropertyModel(object o, PropertyInfo property, List<string> propertiesToOmit)
        {
            Debug.Assert(o != null);
            Debug.Assert(propertiesToOmit != null);

            try
            {
                object propertyValue = property.GetValue(o);
                return ObjectToPropertyModelRecursive(property.Name, propertyValue, propertiesToOmit);
            }
            catch (TargetInvocationException)
            {
                // Skip. Cannot access all properties for bound objects
                return Enumerable.Empty<PropertyModel>();
            }

        }

        private static IEnumerable<PropertyModel> ObjectToPropertyModelRecursive(string propertyName, object propertyValue, List<string> propertiesToOmit = null)
        {
            if (propertyValue == null)
            {
                yield break;
            }

            Type objectType = propertyValue.GetType();
            bool isStringable = objectType.GetMethod("ToString", Type.EmptyTypes).DeclaringType != typeof(object);  // declares a nondefault ToString() which we will use to represent the value instead of recursing into a property tree
            IEnumerable enumerable = propertyValue as IEnumerable;

            if ((objectType == typeof(TimeSpan) || objectType == typeof(TimeSpan?)) && propertyValue.Equals(TimeSpan.MaxValue))
            {
                yield return new SimplePropertyModel(propertyName, "Unlimited");
            }
            else if (isStringable)
            {
                yield return new SimplePropertyModel(propertyName, propertyValue.ToString());
            }
            else if (enumerable != null)
            {
                string prefix = CollectionPropertyModel.GetItemDisplayPrefix(enumerable);

                List<PropertyModel> collectionModel =
                    enumerable.SelectMany((item, index) => ObjectToPropertyModelRecursive(prefix + index, item, propertiesToOmit))
                              .ToList();

                yield return new CollectionPropertyModel(propertyName, collectionModel);
            }
            else
            {
                yield return new CollectionPropertyModel(propertyName, ExtractPropertyCollection(propertyValue, propertiesToOmit));
            }
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
