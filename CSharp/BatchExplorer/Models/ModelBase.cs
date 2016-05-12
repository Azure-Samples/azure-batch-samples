//Copyright (c) Microsoft Corporation

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
            
            List<PropertyModel> result = this.ObjectToPropertyModelRecursive(o, propertiesToOmit);
            result.Add(new SimplePropertyModel(LastUpdateFromServerString, this.LastUpdatedTime.ToString()));
            return result;
        }

        private List<PropertyModel> ObjectToPropertyModelRecursive(object o, List<string> propertiesToOmit = null)
        {
            List<PropertyModel> results = new List<PropertyModel>();

            PropertyInfo[] properties = o.GetType().GetProperties();
            foreach (PropertyInfo propInfo in properties)
            {
                //Determine if this property is in the list of properties to omit -- if it is we skip it
                if (propertiesToOmit != null && propertiesToOmit.Contains(propInfo.Name))
                {
                    continue;
                }
                
                try
                {
                    PropertyModel propertyModel = null;

                    //Get the property type and value
                    object propertyValue = propInfo.GetValue(o);
                    Type propertyType = propertyValue == null ? typeof(object) : propertyValue.GetType();

                    //Find the implementer of the ToString method
                    Type toStringDeclaringType = propertyType.GetMethod("ToString", Type.EmptyTypes).DeclaringType;
                
                    //If the prop value is null, just write string.Empty
                    if (propertyValue == null)
                    {
                        //Don't track null properties in order to keep the list short
                        //propertyModel = new SimplePropertyModel(propInfo.Name, string.Empty);
                    }
                    else if ((propertyType == typeof(TimeSpan) || propertyType == typeof(TimeSpan?)) && propertyValue.Equals(TimeSpan.MaxValue))
                    {
                        propertyModel = new SimplePropertyModel(propInfo.Name, "Unlimited");
                    }
                    //For other types which have a ToString declared, we want to use the built in ToString()
                    else if (toStringDeclaringType != typeof(object))
                    {
                        propertyModel = new SimplePropertyModel(propInfo.Name, propertyValue.ToString());
                    }
                    //If we have a collection, enumerate the contents
                    else if (typeof(IEnumerable).IsAssignableFrom(propertyType))
                    {
                        IEnumerable enumerable = propertyValue as IEnumerable;
                        string prefix = CollectionPropertyModel.GetItemDisplayPrefix(enumerable);
                        List<PropertyModel> collectionModel = new List<PropertyModel>();
                        int i = 0;
                        foreach (object enumerableObject in enumerable)
                        {
                            List<PropertyModel> innerPropertyModels = this.ObjectToPropertyModelRecursive(enumerableObject, propertiesToOmit);

                            collectionModel.Add(new CollectionPropertyModel(prefix + i, innerPropertyModels));
                            i++;
                        }

                        propertyModel = new CollectionPropertyModel(propInfo.Name, collectionModel);
                    }
                    //For any complex properties without a ToString of their own, we recurse
                    else
                    {
                        propertyModel = new CollectionPropertyModel(propInfo.Name, this.ObjectToPropertyModelRecursive(propertyValue, propertiesToOmit));
                    }

                    if (propertyModel != null)
                    {
                        results.Add(propertyModel);
                    }
                }
                catch (TargetInvocationException)
                {
                    // Skip. Cannot access all properties for bound objects
                }
            }

            return results;
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
