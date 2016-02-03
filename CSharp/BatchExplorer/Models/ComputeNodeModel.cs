//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The data model for the ComputeNode object
    /// </summary>
    public class ComputeNodeModel : ModelBase
    {
        #region Public properties
        /// <summary>
        /// The pool associated with this ComputeNode.
        /// </summary>
        public PoolModel ParentPool { get; private set; }

        /// <summary>
        /// The id of this ComputeNode
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Id { get { return this.ComputeNode.Id; } }

        /// <summary>
        /// The state of this ComputeNode
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public ComputeNodeState? State { get { return this.ComputeNode.State; } }


        /// <summary>
        /// The SchedulingState of this ComputeNode
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public SchedulingState? SchedulingState { get { return this.ComputeNode.SchedulingState; } }

        /// <summary>
        /// The allocation time of the ComputeNode.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime? CreationTime { get { return this.ComputeNode.AllocationTime; } }

        /// <summary>
        /// True if there are files available, otherwise false
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public bool HasFiles
        {
            get { return (this.Files != null && this.Files.Count > 0); }
        }
        
        /// <summary>
        /// The collection of files on this ComputeNode.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public List<NodeFile> Files 
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
        /// The collection of recent tasks for this ComputeNode.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public List<TaskInformation> RecentTasks
        {
            get
            {
                if (this.ComputeNode.RecentTasks != null)
                {
                    return this.ComputeNode.RecentTasks.ToList();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// True if this ComputeNode has start task info, false otherwise
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
        /// The start task info associated with this ComputeNode
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public StartTaskInformation StartTaskInfo { get { return this.ComputeNode.StartTaskInformation; } }

        #endregion

        private ComputeNode ComputeNode { get; set; }
        private List<NodeFile> files;
 
        public ComputeNodeModel(PoolModel parentPool, ComputeNode computeNode)
        {
            this.ComputeNode = computeNode;
            this.ParentPool = parentPool;
            this.LastUpdatedTime = DateTime.UtcNow;
        }

        #region ModelBase implementation
        
        public override List<PropertyModel> PropertyModel
        {
            get { return this.ObjectToPropertyModel(this.ComputeNode); }
        }

        public override async Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));
                    Task asyncTask = this.ComputeNode.RefreshAsync();
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new ComputeNodeOperation(ComputeNodeOperation.Refresh, this.ParentPool.Id, this.ComputeNode.Id)));
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
                        Task<List<NodeFile>> asyncTask = this.ListFilesAsync();
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new ComputeNodeOperation(ComputeNodeOperation.ListFiles, this.ParentPool.Id, this.ComputeNode.Id)));

                        this.Files = await asyncTask;
                    }
                    catch (Exception)
                    {
                        this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                        //Swallow the exception to stop popups from occuring for every bad ComputeNode
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

        #region Operations on ComputeNodes
        /// <summary>
        /// Reboots the ComputeNode.
        /// </summary>
        public async Task RebootAsync()
        {
            try
            {
                Task asyncTask = this.ComputeNode.RebootAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new ComputeNodeOperation(ComputeNodeOperation.Reboot, this.ParentPool.Id, this.ComputeNode.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Reimages the ComputeNode.
        /// </summary>
        public async Task ReimageAsync()
        {
            try
            {
                Task asyncTask = this.ComputeNode.ReimageAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new ComputeNodeOperation(ComputeNodeOperation.Reimage, this.ParentPool.Id, this.ComputeNode.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }
        
        /// <summary>
        /// Disables scheduling on the ComputeNode.
        /// </summary>
        public async Task DisableSchedulingAsync()
        {
            try
            {
                if (this.ComputeNode.SchedulingState != Microsoft.Azure.Batch.Common.SchedulingState.Disabled)
                {
                    Task asyncTask = this.ComputeNode.DisableSchedulingAsync(DisableComputeNodeSchedulingOption.Requeue);
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new ComputeNodeOperation(ComputeNodeOperation.DisableScheduling, this.ParentPool.Id, this.ComputeNode.Id)));
                    await asyncTask;
                    await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Enables scheduling on the ComputeNode.
        /// </summary>
        public async Task EnableSchedulingAsync()
        {
            try
            {
                if (this.ComputeNode.SchedulingState != Microsoft.Azure.Batch.Common.SchedulingState.Enabled)
                {
                    Task asyncTask = this.ComputeNode.EnableSchedulingAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new ComputeNodeOperation(ComputeNodeOperation.EnableScheduling, this.ParentPool.Id, this.ComputeNode.Id)));
                    await asyncTask;
                    await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Downloads an RDP file associated with this ComputeNode.
        /// </summary>
        /// <param name="destinationStream">The target stream to place the RDP file into</param>
        public async Task DownloadRDPAsync(Stream destinationStream)
        {
            try
            {
                Task asyncTask = this.ComputeNode.GetRDPFileAsync(destinationStream);
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new ComputeNodeOperation(ComputeNodeOperation.GetRdp, this.ParentPool.Id, this.ComputeNode.Id)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Downloads a file on this compute node.
        /// </summary>
        /// <param name="filePath">The path of the file on the node.</param>
        /// <param name="destinationStream">The target stream to place the file into</param>
        public async Task DownloadFileAsync(string filePath, Stream destinationStream)
        {
            Task asyncTask = this.DownloadFile(filePath, destinationStream);
            AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                asyncTask,
                new ComputeNodeOperation(ComputeNodeOperation.GetFile, this.ParentPool.Id, this.ComputeNode.Id)));
            await asyncTask;
        }

        /// <summary>
        /// Lists the files on this ComputeNode.
        /// </summary>
        /// <returns></returns>
        private async Task<List<NodeFile>> ListFilesAsync()
        {
            IPagedEnumerable<NodeFile> vmFiles = this.ComputeNode.ListNodeFiles(recursive: true);

            List<NodeFile> results = await vmFiles.ToListAsync();
            
            return results;
        }

        /// <summary>
        /// Downloads the contents of the specific file on the ComputeNode.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="destinationStream">The destination stream.</param>
        /// <returns></returns>
        private async Task DownloadFile(string filePath, Stream destinationStream)
        {
            NodeFile file = await this.ComputeNode.GetNodeFileAsync(filePath);
            await file.CopyToStreamAsync(destinationStream);
        }

        #endregion

        #region Private methods

        private void HandleException(Exception e)
        {
            //Swallow 404's and fire a message
            if (Microsoft.Azure.BatchExplorer.Helpers.Common.IsExceptionNotFound(e))
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
