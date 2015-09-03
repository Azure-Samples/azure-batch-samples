//Copyright (c) Microsoft Corporation

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// Model representing a pending or completed async operation.
    /// </summary>
    public class AsyncOperationModel : EntityBase
    {
        private static int globalOperationCount = 0;

        /// <summary>
        /// The TPL task corresponding to the operation.
        /// </summary>
        public Task AsyncTask { get; private set; }

        /// <summary>
        /// Operation object which describes what the operation is doing and what the target was.
        /// </summary>
        public Operation AsyncOperation { get; private set; }

        /// <summary>
        /// The global count of the operation (each operation will monotonically increasing from 1, 2, 3...)
        /// </summary>
        public int OperationNumber { get; private set; }

        /// <summary>
        /// The time at which this operation was started.
        /// </summary>
        public DateTime StartTimeLocal
        {
            get { return this.startTimeLocal; }
            private set
            {
                this.startTimeLocal = value;
                this.FirePropertyChangedEvent("StartTimeLocal");
            }
        }

        /// <summary>
        /// The time at which this operation was started.
        /// </summary>
        public DateTime StartTimeUtc
        {
            get { return this.startTimeUtc; }
            private set
            {
                this.startTimeUtc = value;
                this.FirePropertyChangedEvent("StartTimeUtc");
            }
        }

        /// <summary>
        /// The time at which this operation finished.
        /// </summary>
        public DateTime EndTimeUtc
        {
            get { return this.endTimeUtc; }
            private set
            {
                this.endTimeUtc = value;
                this.FirePropertyChangedEvent("EndTimeUtc");
            }
        }

        /// <summary>
        /// True if this operation is completed, false if it is still ongoing
        /// </summary>
        public bool IsCompleted
        {
            get { return this.isCompleted; }
            private set
            {
                this.isCompleted = value;
                this.FirePropertyChangedEvent("IsCompleted");
                this.FirePropertyChangedEvent("HasFault");
            }
        }

        /// <summary>
        /// True if this operation has completed successfully, false if it has failed
        /// </summary>
        public bool CompletedSuccessfully 
        { 
            get { return this.completedSuccessfully; }
            private set
            {
                this.completedSuccessfully = value;
                this.FirePropertyChangedEvent("CompletedSuccessfully");
                this.FirePropertyChangedEvent("HasFault");
            }
        }

        /// <summary>
        /// The exception associated with this operations failure
        /// </summary>
        public Exception FaultException
        {
            get { return this.faultException; }
            private set
            {
                this.faultException = value;
                this.FirePropertyChangedEvent("FaultException");
            }
        }

        /// <summary>
        /// The client request ID associated with the request which failed
        /// </summary>
        public Guid? FaultClientRequestId
        {
            get { return this.faultClientRequestId; }
            set 
            { 
                this.faultClientRequestId = value;
                this.FirePropertyChangedEvent("FaultClientRequestId");
            }
        }

        /// <summary>
        /// The server request ID associated with the request which failed
        /// </summary>
        public string FaultServerRequestId
        {
            get { return this.faultServerRequestId; }
            set
            {
                this.faultServerRequestId = value;
                this.FirePropertyChangedEvent("FaultServerRequestId");
            }
        }

        /// <summary>
        /// Determines if this operation has a fault.
        /// </summary>
        public bool HasFault
        {
            get { return this.IsCompleted && !this.CompletedSuccessfully; }
        }

        private bool completedSuccessfully;
        private bool isCompleted;
        private Exception faultException;
        private DateTime startTimeUtc;
        private DateTime endTimeUtc;
        private DateTime startTimeLocal;
        private Guid? faultClientRequestId;
        private string faultServerRequestId;

        public AsyncOperationModel(Task asyncTask, Operation asyncOperation)
        {
            this.AsyncTask = asyncTask;
            this.AsyncOperation = asyncOperation;

            //Increment the global operation count
            this.OperationNumber = Interlocked.Increment(ref globalOperationCount);
            this.StartTimeUtc = DateTime.UtcNow;
            this.StartTimeLocal = this.StartTimeUtc.ToLocalTime();

            //Register to be informed when this task completes
            asyncTask.ContinueWith(this.HandleTaskCompletion);
        }

        private void HandleTaskCompletion(Task t)
        {
            this.IsCompleted = true;
            this.EndTimeUtc = DateTime.UtcNow;
            if (t.Status == TaskStatus.RanToCompletion)
            {
                this.CompletedSuccessfully = true;
            }
            else if (t.Status == TaskStatus.Faulted ||
                     t.Status == TaskStatus.Canceled)
            {
                this.CompletedSuccessfully = false;
                this.FaultException = t.Exception;

                //Process exception for client/server request ID if applicable
                if (this.FaultException is BatchException)
                {
                    BatchException batchE = this.FaultException as BatchException;
                    this.FaultClientRequestId = batchE.RequestInformation.ClientRequestId;
                    this.FaultServerRequestId = batchE.RequestInformation.ServiceRequestId;
                }
                else if (this.FaultException is AggregateException)
                {
                    //If there is only 1 batch exception we hit
                    AggregateException agg = this.FaultException as AggregateException;
                    agg = agg.Flatten();
                    
                    BatchException batchE = agg.InnerExceptions.Where(e => e is BatchException).Cast<BatchException>().First();
                    this.FaultClientRequestId = batchE.RequestInformation.ClientRequestId;
                    this.FaultServerRequestId = batchE.RequestInformation.ServiceRequestId;

                }
            }
        }
    }
}
