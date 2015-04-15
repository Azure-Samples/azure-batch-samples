using System;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ChangeTrackedAttribute : Attribute
    {
        private readonly ModelRefreshType refreshType;

        public ChangeTrackedAttribute(ModelRefreshType refreshType)
        {
            this.refreshType = refreshType;
        }

        /// <summary>
        /// Returns true if this property has been changed based on the refresh type
        /// </summary>
        /// <param name="changeRefreshType">The type of refresh which was performed</param>
        /// <returns></returns>
        public bool HasChanged(ModelRefreshType changeRefreshType)
        {
            return this.refreshType.HasFlag(changeRefreshType);
        }
    }
}
