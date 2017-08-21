//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Conventions.Files;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.ViewModels;
using Microsoft.WindowsAzure.Storage.Blob;


namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The data model for the Task object
    /// </summary>
    public class TaskModel : ModelBase
    {
        #region Public properties

        /// <summary>
        /// The parent job of this task
        /// </summary>
        public JobModel ParentJob { get; set; }

        /// <summary>
        /// The id of this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Id { get { return this.Task.Id; } }

        /// <summary>
        /// The state of this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TaskState? State { get { return this.Task.State; } }

        /// <summary>
        /// The creation time of this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime? CreationTime { get { return this.Task.CreationTime; } }

        /// <summary>
        /// The commandline this task runs
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string CommandLine { get { return this.Task.CommandLine; } }

        /// <summary>
        /// The exit code of the task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public int? ExitCode { get { return this.Task?.ExecutionInformation?.ExitCode; } }

        /// <summary>
        /// The set of Windows Azure blobs downloaded to run the task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public IEnumerable<ResourceFile> ResourceFiles { get { return this.Task.ResourceFiles; } }
        
        /// <summary>
        /// The number of times to retry this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public int? MaxTaskRetryCount { get { return this.Task.Constraints.MaxTaskRetryCount; } }

        /// <summary>
        /// Maximum time a task is allowed to run after it is created
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TimeSpan? MaxWallClockTime { get { return this.Task.Constraints.MaxWallClockTime; } }

        /// <summary>
        /// Duration of time for which files in the task's working directory are retained, from the time execution completed
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TimeSpan? RetentionTime { get { return this.Task.Constraints.RetentionTime; } }

        /// <summary>
        /// The environmental settings
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public IEnumerable<EnvironmentSetting> EnvironmentSettings { get { return this.Task.EnvironmentSettings; } }

        /// <summary>
        /// The set of output files for this task object
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public IEnumerable<NodeFile> OutputFiles { get; private set; }

        /// <summary>
        /// True if there are output files to be had
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public bool HasOutputFiles
        {
            get
            {
                if (this.attemptToLoadOutputs && (this.OutputFiles == null || !this.OutputFiles.Any()))
                {
                    AsyncOperationTracker.Instance.AddTrackedInternalOperation(
                        this.RefreshAsync(ModelRefreshType.Children));
                }

                this.attemptToLoadOutputs = false;
                return (this.OutputFiles != null && this.OutputFiles.Any());
            }
        }

        /// <summary>
        /// The set of linked storage output files for this task object
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public List<ICloudBlob> LinkedStorageOutputFiles { get; private set; }

        /// <summary>
        /// True if there are linked storage output files to be had
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public bool LinkedStorageHasOutputFiles
        {
            get
            {
                if (this.attemptToLoadLinkedStorageOutputs && (this.LinkedStorageOutputFiles == null || !this.LinkedStorageOutputFiles.Any()))
                {
                    AsyncOperationTracker.Instance.AddTrackedInternalOperation(
                        this.RefreshAsync(ModelRefreshType.Children));
                }

                this.attemptToLoadLinkedStorageOutputs = false;
                return (this.LinkedStorageOutputFiles != null && this.LinkedStorageOutputFiles.Any());
            }
        }

        /// <summary>
        /// The set of Subtask information
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public IEnumerable<SubtaskModel> Subtasks { get { return this.SubtasksInfo; } }

        #endregion

        #region Public UI Properties

        private NodeFile selectedTaskFile;

        public NodeFile SelectedTaskFile
        {
            get { return this.selectedTaskFile; }
            set
            {
                this.selectedTaskFile = value;
                this.FirePropertyChangedEvent("SelectedTaskFile");
            }
        }

        private string noOutputFilesReason;
        public string NoOutputFilesReason
        {
            get
            {
                if (string.IsNullOrEmpty(this.noOutputFilesReason))
                {
                    return "No outputs available. Try refreshing the Task.";
                }

                return this.noOutputFilesReason;
            }
            set
            {
                this.noOutputFilesReason = value;
                this.FirePropertyChangedEvent("NoOutputFilesReason");
            }
        }

        private ICloudBlob selectedTaskLinkedStorageFile;

        public ICloudBlob SelectedTaskLinkedStorageFile
        {
            get { return this.selectedTaskLinkedStorageFile; }
            set
            {
                this.selectedTaskLinkedStorageFile = value;
                this.FirePropertyChangedEvent("SelectedTaskLinkedStorageFile");
            }
        }

        #endregion

        private bool attemptToLoadOutputs;
        private bool attemptToLoadLinkedStorageOutputs;
        private CloudTask Task { get; set; }
        private IList<SubtaskModel> SubtasksInfo { get; set; }
        private static readonly List<string> PropertiesToOmitFromDisplay = new List<string> { "FilesToStage" };

        public TaskModel(JobModel parentJob, CloudTask task)
        {
            this.attemptToLoadOutputs = true;
            this.attemptToLoadLinkedStorageOutputs = true;

            this.ParentJob = parentJob;
            this.Task = task;
            this.LastUpdatedTime = DateTime.UtcNow;
            this.SubtasksInfo = null;
        }

        #region ModelBase implementation

        public override List<PropertyModel> PropertyModel
        {
            get { return this.ObjectToPropertyModel(this.Task, PropertiesToOmitFromDisplay); }
        }

        public override async System.Threading.Tasks.Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            this.attemptToLoadOutputs = true;
            this.attemptToLoadLinkedStorageOutputs = true;

            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    //await this.Task.RefreshAsync(); TODO: This doesnt' work right now due to bug with OM, so must do GetTask directly
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));

                    System.Threading.Tasks.Task<CloudTask> asyncTask = MainViewModel.dataProvider.Service.GetTaskAsync(
                        this.ParentJob.Id,
                        this.Task.Id,
                        OptionsModel.Instance.ListDetailLevel);

                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new TaskOperation(TaskOperation.Refresh, this.ParentJob.Id, this.Task.Id)));
                    }
                    else
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(asyncTask);
                    }

                    this.Task = await asyncTask;
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
                        System.Threading.Tasks.Task<List<NodeFile>> asyncTask = this.ListTaskFilesAsync();
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new TaskOperation(TaskOperation.ListFiles, this.ParentJob.Id, this.Task.Id)));

                        this.OutputFiles = await asyncTask;
                    }
                    catch (BatchException be)
                    {
                        StringBuilder noOutputReasonBuilder = new StringBuilder();

                        if (be.RequestInformation != null && be.RequestInformation.BatchError != null)
                        {

                            if (!string.IsNullOrEmpty(be.RequestInformation.BatchError.Code))
                            {
                                noOutputReasonBuilder.AppendLine(be.RequestInformation.BatchError.Code);
                            }

                            if (be.RequestInformation.BatchError.Message != null && !string.IsNullOrEmpty(be.RequestInformation.BatchError.Message.Value))
                            {
                                noOutputReasonBuilder.AppendLine(be.RequestInformation.BatchError.Message.Value);
                            }

                            if (be.RequestInformation.BatchError.Values != null)
                            {
                                noOutputReasonBuilder.AppendLine();

                                foreach (var errorDetail in be.RequestInformation.BatchError.Values)
                                {
                                    noOutputReasonBuilder.AppendLine(string.Format("{0}: {1}", errorDetail.Key, errorDetail.Value));
                                }
                            }

                            this.NoOutputFilesReason = noOutputReasonBuilder.ToString();
                        }
                    }
                    catch (Exception)
                    {
                        this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                        //Swallow the exception to stop popups from occuring for every bad VM
                    }

                    IPagedEnumerable<SubtaskInformation> subtasks = this.Task.ListSubtasks(OptionsModel.Instance.ListDetailLevel);

                    this.SubtasksInfo = new List<SubtaskModel>();

                    System.Threading.Tasks.Task asyncListSubtasksTask = subtasks.ForEachAsync(item => this.SubtasksInfo.Add(new SubtaskModel(item)));

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncListSubtasksTask,
                        new TaskOperation(TaskOperation.ListSubtasks, this.ParentJob.Id, this.Task.Id)));

                    await asyncListSubtasksTask;

                    // Linked Storage Account Files
                    var storageContainer = this.ParentJob.OutputStorageContainerName;

                    if (!String.IsNullOrWhiteSpace(storageContainer))
                    {
                        try
                        {
                            var blobClient = MainViewModel.dataProvider.CurrentAccount.LinkedStorageBlobClient;
                            if (blobClient != null)
                            {
                                var container = blobClient.GetContainerReference(storageContainer);

                                if (container != null)
                                {
                                    var files = container.ListBlobs(Task.Id, true).ToList();
                                    LinkedStorageOutputFiles = new List<ICloudBlob>();
                                    foreach (var file in files)
                                    {
                                        var cloudAppendBlob = file as ICloudBlob;
                                        if (cloudAppendBlob != null)
                                        {
                                            LinkedStorageOutputFiles.Add(cloudAppendBlob);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            this.HandleException(e);
                        }
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

        #region Task operations

        /// <summary>
        /// Delete this task
        /// </summary>
        public async Task DeleteAsync()
        {
            try
            {
                var asyncTask = this.Task.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new TaskOperation(TaskOperation.Delete, this.ParentJob.Id, this.Task.Id)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Terminate the task
        /// </summary>
        public async Task TerminateAsync()
        {
            try
            {
                var asyncTask = this.Task.TerminateAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new TaskOperation(TaskOperation.Terminate, this.ParentJob.Id, this.Task.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Downloads a task file 
        /// </summary>
        /// <param name="filePath">The file path to download</param>
        /// <param name="outputStream">The stream to download the file into</param>
        /// <returns></returns>
        public async Task GetTaskFileAsync(string filePath, Stream outputStream)
        {
            var asyncTask = this.DownloadTaskFile(filePath, outputStream);
            AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                asyncTask,
                new TaskOperation(TaskOperation.GetFile, this.ParentJob.Id, this.Task.Id)));
            await asyncTask;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Lists the task files associated with this task
        /// </summary>
        /// <returns></returns>
        private async Task<List<NodeFile>> ListTaskFilesAsync()
        {
            IPagedEnumerable<NodeFile> vmFiles = this.Task.ListNodeFiles(recursive: true);

            List<NodeFile> results = await vmFiles.ToListAsync();

            return results;
        }

        /// <summary>
        /// Downloads the contents of the specific file of the task.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="destinationStream">The destination stream.</param>
        /// <returns></returns>
        private async Task DownloadTaskFile(string filePath, Stream destinationStream)
        {
            NodeFile file = await this.Task.GetNodeFileAsync(filePath);
            await file.CopyToStreamAsync(destinationStream);
        }

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
