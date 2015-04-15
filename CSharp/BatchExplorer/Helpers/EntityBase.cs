using System.ComponentModel;
using System.Runtime.Serialization;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// Serves as the base for both models and viewmodels - provides all common functionality needed by both
    /// </summary>
    [DataContract]
    public class EntityBase : INotifyPropertyChanged
    {
        private bool isBusy;

        /// <summary>
        /// Is Busy
        /// Determines whether the control is busy
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return this.isBusy;
            }
            set
            {
                this.isBusy = value;
                this.FirePropertyChangedEvent("IsBusy");
            }
        }

        /// <summary>
        /// Raise a PropertyChanged event
        /// </summary>
        /// <param name="propertyName">The name of the property that has changed</param>
        protected void FirePropertyChangedEvent(string propertyName)
        {
            //PropertyChanged will be null if there are no listeners for this event on this object
            if (PropertyChanged != null)
            {
                //There is at least one listener, so raise the event
                PropertyChanged(this,new PropertyChangedEventArgs(propertyName));
            }
        }
        /// <summary>
        /// Event fired whenever a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
