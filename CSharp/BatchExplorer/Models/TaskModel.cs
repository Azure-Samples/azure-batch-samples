//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.ViewModels;
using System.Text;

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
        /// The set of Windows Azure blobs downloaded to run the task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public IEnumerable<ResourceFile> ResourceFiles { get { return this.Task.ResourceFiles; } }

        /// <summary>
        /// The set of Subtask information
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public IEnumerable<SubtaskModel> Subtasks { get { return this.SubtasksInfo; } }

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
                try
                {
                    if (this.attemptToLoadOutputs && (this.OutputFiles == null || !this.OutputFiles.Any()))
                    {
                        this.RefreshAsync(ModelRefreshType.Children);
                    }

                    this.attemptToLoadOutputs = false;
                    return (this.OutputFiles != null && this.OutputFiles.Any());
                }
                catch (Exception e)
                {

                }

                return false;
            }
        }
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

        #endregion

        private bool attemptToLoadOutputs;
        private CloudTask Task { get; set; }
        private IList<SubtaskModel> SubtasksInfo { get; set; }
        private static readonly List<string> PropertiesToOmitFromDisplay = new List<string> { "FilesToStage" };

        public TaskModel(JobModel parentJob, CloudTask task)
        {
            this.attemptToLoadOutputs = true;

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

                    IPagedEnumerable<SubtaskInformation> subtasks = this.Task.ListSubtasks(OptionsModel.Instance.ListDetailLevel);

                    this.SubtasksInfo = new List<SubtaskModel>();

                    await subtasks.ForEachAsync(item => this.SubtasksInfo.Add(new SubtaskModel(item)));

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

                        if (be.RequestInformation != null && be.RequestInformation.AzureError != null)
                        {

                            if (!string.IsNullOrEmpty(be.RequestInformation.AzureError.Code))
                            {
                                noOutputReasonBuilder.AppendLine(be.RequestInformation.AzureError.Code);
                            }

                            if (be.RequestInformation.AzureError.Message != null && !string.IsNullOrEmpty(be.RequestInformation.AzureError.Message.Value))
                            {
                                noOutputReasonBuilder.AppendLine(be.RequestInformation.AzureError.Message.Value);
                            }

                            if (be.RequestInformation.AzureError.Values != null)
                            {
                                noOutputReasonBuilder.AppendLine();

                                foreach (var errorDetail in be.RequestInformation.AzureError.Values)
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
        public async System.Threading.Tasks.Task DeleteAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Task.DeleteAsync();
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
        public async System.Threading.Tasks.Task TerminateAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Task.TerminateAsync();
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
        public async System.Threading.Tasks.Task GetTaskFileAsync(string filePath, Stream outputStream)
        {
            System.Threading.Tasks.Task asyncTask = this.DownloadTaskFile(filePath, outputStream);
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
        private async System.Threading.Tasks.Task<List<NodeFile>> ListTaskFilesAsync()
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
        private async System.Threading.Tasks.Task DownloadTaskFile(string filePath, Stream destinationStream)
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
