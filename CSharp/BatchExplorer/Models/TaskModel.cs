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
        /// The name of this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Name { get { return this.Task.Name; } }

        /// <summary>
        /// The state of this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TaskState State { get { return this.Task.State; } }

        /// <summary>
        /// The creation time of this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime CreationTime { get { return this.Task.CreationTime; } }

        /// <summary>
        /// The commandline this task runs
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string CommandLine { get { return this.Task.CommandLine; } }

        /// <summary>
        /// The set of Windows Azure blobs downloaded to run the task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public IEnumerable<IResourceFile> ResourceFiles { get { return this.Task.ResourceFiles; } }

        /// <summary>
        /// The number of times to retry this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public int? MaxTaskRetryCount { get { return this.Task.TaskConstraints.MaxTaskRetryCount; } }

        /// <summary>
        /// Maximum time a task is allowed to run after it is created
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TimeSpan? MaxWallClockTime { get { return this.Task.TaskConstraints.MaxWallClockTime; } }

        /// <summary>
        /// Duration of time for which files in the task's working directory are retained, from the time execution completed
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public TimeSpan? RetentionTime { get { return this.Task.TaskConstraints.RetentionTime; } }
        
        /// <summary>
        /// The environmental settings
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public IEnumerable<IEnvironmentSetting> EnvironmentSettings { get { return this.Task.EnvironmentSettings; } }

        /// <summary>
        /// The set of output files for this task object
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public IEnumerable<ITaskFile> OutputFiles { get; private set; }

        /// <summary>
        /// True if there are output files to be had
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public bool HasOutputFiles
        {
            get { return (this.OutputFiles != null && this.OutputFiles.Any()); }
        }

        /// <summary>
        /// Statistics associated with this task
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public SortedDictionary<string, object> Statistics
        {
            get
            {
                if (this.Task.Statistics == null)
                {
                    return null; //abort
                }

                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.Task.Statistics);

                return results;
            }
        }

        #endregion

        #region Public UI Properties

        private ITaskFile selectedTaskFile;

        public ITaskFile SelectedTaskFile
        {
            get { return this.selectedTaskFile; }
            set
            {
                this.selectedTaskFile = value;
                this.FirePropertyChangedEvent("SelectedTaskFile");
            }
        }

        #endregion

        private ICloudTask Task { get; set; }
        private static readonly List<string> PropertiesToOmitFromDisplay = new List<string> {"Stats", "FilesToStage"};
        public TaskModel(JobModel parentJob, ICloudTask task)
        {
            this.ParentJob = parentJob;
            this.Task = task;
            this.LastUpdatedTime = DateTime.UtcNow;
        }

        #region ModelBase implementation
        public override SortedDictionary<string, object> PropertyValuePairs
        {
            get
            {
                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.Task, string.Empty, PropertiesToOmitFromDisplay);
                results.Add(LastUpdateFromServerString, this.LastUpdatedTime);
                return results;
            }
        }

        public override async System.Threading.Tasks.Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    //await this.Task.RefreshAsync(); TODO: This doesnt' work right now due to bug with OM, so must do GetTask directly
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));

                    System.Threading.Tasks.Task<ICloudTask> asyncTask = MainViewModel.dataProvider.Service.GetTaskAsync(
                        this.ParentJob.ParentWorkItemName,
                        this.ParentJob.Name,
                        this.Task.Name,
                        OptionsModel.Instance.ListDetailLevel);

                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new TaskOperation(TaskOperation.Refresh, this.ParentJob.ParentWorkItem.Name, this.ParentJob.Name, this.Task.Name)));
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
                        System.Threading.Tasks.Task<List<ITaskFile>> asyncTask = this.ListTaskFilesAsync();
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new TaskOperation(TaskOperation.ListTaskFiles, this.ParentJob.ParentWorkItem.Name, this.ParentJob.Name, this.Task.Name)));
                        
                        this.OutputFiles = await asyncTask;
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
                    new TaskOperation(TaskOperation.Delete, this.ParentJob.ParentWorkItem.Name, this.ParentJob.Name, this.Task.Name)));
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
                    new TaskOperation(TaskOperation.Terminate, this.ParentJob.ParentWorkItem.Name, this.ParentJob.Name, this.Task.Name)));
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
                new TaskOperation(TaskOperation.GetTaskFile, this.ParentJob.ParentWorkItem.Name, this.ParentJob.Name, this.Task.Name)));
            await asyncTask;
        }

        #endregion

        #region Private methods
        
        /// <summary>
        /// Lists the task files associated with this task
        /// </summary>
        /// <returns></returns>
        private async System.Threading.Tasks.Task<List<ITaskFile>> ListTaskFilesAsync()
        {
            List<ITaskFile> results = new List<ITaskFile>();
            IEnumerableAsyncExtended<ITaskFile> vmFiles = this.Task.ListTaskFiles(recursive: true);
            IAsyncEnumerator<ITaskFile> asyncEnumerator = vmFiles.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                results.Add(asyncEnumerator.Current);
            }
            
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
            ITaskFile file = await this.Task.GetTaskFileAsync(filePath);
            await file.CopyToStreamAsync(destinationStream);
        }

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
