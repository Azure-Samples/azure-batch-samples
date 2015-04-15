using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// Tracks async operations issued by the BatchExplorer 
    /// </summary>
    public class AsyncOperationTracker
    {
        #region Singleton properties
        //We don't require lazy instantiation for this type so this is okay.
        //See: http://csharpindepth.com/articles/general/singleton.aspx for other implementations of the singleton pattern
        private static readonly AsyncOperationTracker instance = new AsyncOperationTracker();

        public static AsyncOperationTracker Instance
        {
            get { return instance; }
        }

        #endregion
        
        private readonly LinkedList<AsyncOperationModel> trackedOperations;
        private readonly LinkedList<Task> trackedInternalOperations; 
        private readonly object trackedOperationsLock;
        private readonly object trackedInternalOperationsLock;
        internal const int DefaultMaxTrackedOperations = 50;

        private AsyncOperationTracker()
        {
            //Note: we use LinkedList here for O(1) AddLast and RemoveFirst
            this.trackedOperations = new LinkedList<AsyncOperationModel>();
            this.trackedOperationsLock = new object();

            this.trackedInternalOperations = new LinkedList<Task>();
            this.trackedInternalOperationsLock = new object();
        }
        
        /// <summary>
        /// Snapshot of the tracked operations.
        /// </summary>
        public IEnumerable<AsyncOperationModel> AsyncOperations 
        {
            get
            {
                lock (this.trackedOperationsLock)
                {
                    //Perform a shallow copy of our list and return that to avoid any threading issues with this list being read/modified in multiple threads
                    //TODO: Should we avoid doing this copy and do something more intelligent instead...?
                    IEnumerable<AsyncOperationModel> shallowCopy = new List<AsyncOperationModel>(this.trackedOperations);
                    return shallowCopy;
                }
            }
        }

        /// <summary>
        /// Snapshot of the tracked internal operations.
        /// </summary>
        internal IEnumerable<Task> InternalAsyncOperations
        {
            get
            {
                lock (this.trackedInternalOperationsLock)
                {
                    //Perform a shallow copy of our list and return that to avoid any threading issues with this list being read/modified in multiple threads
                    //TODO: Should we avoid doing this copy and do something more intelligent instead...?
                    IEnumerable<Task> shallowCopy = new List<Task>(this.trackedInternalOperations);
                    return shallowCopy;
                }
            }
        }

        /// <summary>
        /// Add the specified operation to the tracker.
        /// </summary>
        /// <param name="asyncOperation">The operation to track.</param>
        public void AddTrackedOperation(AsyncOperationModel asyncOperation)
        {
            lock (this.trackedOperationsLock)
            {
                //Check the current size of the collection and remove items if needed
                while(this.trackedOperations.Count >= OptionsModel.Instance.MaxTrackedOperations)
                {
                    this.trackedOperations.RemoveLast();
                }

                this.trackedOperations.AddFirst(asyncOperation);
            }
            Messenger.Default.Send(new Messages.AsyncOperationListChangedMessage());
        }
        
        /// <summary>
        /// Clears the cache of tracked operations.
        /// </summary>
        public void Clear()
        {
            lock (this.trackedOperationsLock)
            {
                this.trackedOperations.Clear();
            }
            Messenger.Default.Send(new Messages.AsyncOperationListChangedMessage());
        }

        /// <summary>
        /// Adds a tracked internal operation, which is not displayed in the AsyncOperationTracker list.
        /// </summary>
        /// <param name="internalAsyncOperation"></param>
        internal void AddTrackedInternalOperation(Task internalAsyncOperation)
        {
            lock (this.trackedInternalOperationsLock)
            {
                this.trackedInternalOperations.AddFirst(internalAsyncOperation);
            }
        }

        /// <summary>
        /// Removes a tracked internal operation.
        /// </summary>
        /// <param name="internalAsyncOperation"></param>
        internal void RemoveTrackedInternalOperation(Task internalAsyncOperation)
        {
            lock (this.trackedInternalOperationsLock)
            {
                this.trackedInternalOperations.Remove(internalAsyncOperation);
            }
        }

        public static async Task InternalOperationResultHandler()
        {
            AsyncOperationTracker asyncOperationTracker = AsyncOperationTracker.Instance;
            while (true)
            {
                IEnumerable<Task> pendingInternalOperations = asyncOperationTracker.InternalAsyncOperations;

                //Wait for any async operation to complete
                if (pendingInternalOperations.Any())
                {
                    Task whenAnyTask = Task.WhenAny(pendingInternalOperations);

                    try
                    {
                        await whenAnyTask; //This can throw
                    }
                    catch (Exception)
                    {
                        //Supress all errors -- they are processed in the following foreach
                    }
                }

                //Process all tasks which have completed and remove them from tracking
                foreach (Task internalOperation in pendingInternalOperations)
                {
                    if (internalOperation.IsCompleted)
                    {
                        if (internalOperation.IsFaulted)
                        {
                            string message = string.Format("Hit internal async operation error: {0}", internalOperation.Exception);
                            Messenger.Default.Send(new GenericDialogMessage(message));
                        }
                        asyncOperationTracker.RemoveTrackedInternalOperation(internalOperation);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
