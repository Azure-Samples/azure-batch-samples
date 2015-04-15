using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The data model for the TVM object
    /// </summary>
    public class TvmModel : ModelBase
    {
        #region Public properties
        /// <summary>
        /// The pool associated with this TVM
        /// </summary>
        public PoolModel ParentPool { get; private set; }

        /// <summary>
        /// The name of this TVM
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Name { get { return this.VM.Name; } }

        /// <summary>
        /// The state of this TVM
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TVMState State { get { return this.VM.State; } }

        /// <summary>
        /// The allocation time of the TVM
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime CreationTime { get { return this.VM.VMAllocationTime; } }

        /// <summary>
        /// True if there are files available, otherwise false
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public bool HasFiles
        {
            get { return (this.Files != null && this.Files.Count > 0); }
        }
        
        /// <summary>
        /// The collection of files on this TVM
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public List<ITaskFile> Files 
        { 
            get
            {
                return this.files;
            }
            private set 
            { 
                this.files = value;
                FirePropertyChangedEvent("Files");
            }
        }

        /// <summary>
        /// True if there are any recent tasks, otherwise false
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public bool HasRecentTasks
        {
            get { return (this.RecentTasks != null && this.RecentTasks.Count > 0); }
        }
        /// <summary>
        /// The collection of recent tasks for this TVM
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public List<TaskInformation> RecentTasks
        {
            get
            {
                if (this.VM.RecentTasks != null)
                {
                    return this.VM.RecentTasks.ToList();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// True if this TVM has start task info, false otherwise
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public bool HasStartTaskInfo
        {
            get
            {
                return this.StartTaskInfo != null;
            }
        }

        /// <summary>
        /// The start task info associated with this TVM
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public StartTaskInformation StartTaskInfo { get { return this.VM.StartTaskInformation; } }

        #endregion

        private IVM VM { get; set; }
        private List<ITaskFile> files;
 
        public TvmModel(PoolModel parentPool, IVM vm)
        {
            this.VM = vm;
            this.ParentPool = parentPool;
            this.LastUpdatedTime = DateTime.UtcNow;
        }

        #region ModelBase implementation

        public override SortedDictionary<string, object> PropertyValuePairs
        {
            get
            {
                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.VM);
                results.Add(LastUpdateFromServerString, this.LastUpdatedTime);
                return results;
            }
        }

        public override async Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));
                    System.Threading.Tasks.Task asyncTask =  this.VM.RefreshAsync();
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new VMOperation(VMOperation.Refresh, this.ParentPool.Name, this.VM.Name)));
                    }
                    else
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(asyncTask);
                    }

                    await asyncTask;
                    this.LastUpdatedTime = DateTime.UtcNow;

                    //
                    // Fire property change events for this models properties
                    //
                    this.FireChangesOnRefresh(ModelRefreshType.Basic);

                }
                catch (Exception e)
                {
                    this.HandleException(e);
                }
                finally
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, false));
                }
            }

            if (refreshType.HasFlag(ModelRefreshType.Children))
            {
                try
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.LowerRight, true));
                    //Set this before the children load so that on revisit we know we have loaded the children (or are in the process)
                    this.HasLoadedChildren = true;
                    
                    try
                    {
                        System.Threading.Tasks.Task<List<ITaskFile>> asyncTask = this.ListFilesAsync();
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new VMOperation(VMOperation.ListVMFiles, this.ParentPool.Name, this.VM.Name)));

                        this.Files = await asyncTask;
                    }
                    catch (Exception)
                    {
                        this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                        //Swallow the exception to stop popups from occuring for every bad VM
                    }

                    this.FireChangesOnRefresh(ModelRefreshType.Children);
                }
                catch (Exception e)
                {
                    this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                    this.HandleException(e);
                }
                finally
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.LowerRight, false));
                }
            }
        }

        #endregion

        #region Operations on VMs

        /// <summary>
        /// Reboots the TVM
        /// </summary>
        public async System.Threading.Tasks.Task RebootAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.VM.RebootAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new VMOperation(VMOperation.Reboot, this.ParentPool.Name, this.VM.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Reimages the TVM
        /// </summary>
        public async System.Threading.Tasks.Task ReimageAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.VM.ReimageAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new VMOperation(VMOperation.Reimage, this.ParentPool.Name, this.VM.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }
        
        /// <summary>
        /// Downloads an RDP file associated with this TVM
        /// </summary>
        /// <param name="destinationStream">The target stream to place the RDP file into</param>
        public async System.Threading.Tasks.Task DownloadRDPAsync(Stream destinationStream)
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.VM.GetRDPFileAsync(destinationStream);
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new VMOperation(VMOperation.GetRdp, this.ParentPool.Name, this.VM.Name)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Downloads a file on this TVM
        /// </summary>
        /// <param name="filePath">The path of the file on the tVM</param>
        /// <param name="destinationStream">The target stream to place the file into</param>
        public async System.Threading.Tasks.Task DownloadFileAsync(string filePath, Stream destinationStream)
        {
            System.Threading.Tasks.Task asyncTask = this.DownloadVMFile(filePath, destinationStream);
            AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                asyncTask,
                new VMOperation(VMOperation.GetVMFile, this.ParentPool.Name, this.VM.Name)));
            await asyncTask;
        }

        /// <summary>
        /// Lists the files on this TVM
        /// </summary>
        /// <returns></returns>
        private async System.Threading.Tasks.Task<List<ITaskFile>> ListFilesAsync()
        {
            List<ITaskFile> results = new List<ITaskFile>();
            IEnumerableAsyncExtended<ITaskFile> vmFiles = this.VM.ListVMFiles(recursive: true);
            IAsyncEnumerator<ITaskFile> asyncEnumerator = vmFiles.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                results.Add(asyncEnumerator.Current);
            }
            
            return results;
        }

        /// <summary>
        /// Downloads the contents of the specific file on the VM.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="destinationStream">The destination stream.</param>
        /// <returns></returns>
        private async System.Threading.Tasks.Task DownloadVMFile(string filePath, Stream destinationStream)
        {
            ITaskFile file = await this.VM.GetVMFileAsync(filePath);
            await file.CopyToStreamAsync(destinationStream);
        }

        #endregion

        #region Private methods

        private void HandleException(Exception e)
        {
            //Swallow 404's and fire a message
            if (Common.IsExceptionNotFound(e))
            {
                Messenger.Default.Send(new ModelNotFoundAfterRefresh(this));
            }
            else
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        #endregion
    }
}
