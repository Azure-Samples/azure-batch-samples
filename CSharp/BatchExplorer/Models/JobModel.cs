using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The data model for the Job object
    /// </summary>
    public class JobModel : ModelBase
    {
        #region Public properties

        /// <summary>
        /// The parent work item
        /// </summary>
        public WorkItemModel ParentWorkItem { get; private set; }

        /// <summary>
        /// The parent work item name
        /// </summary>
        /// <remarks>
        /// This field is used for the Extended WPF Toolkit which doesn't support referencing object members in data binding
        /// for instance we cannot do {Binding ParentWorkItem.Name}
        /// </remarks>
        public string ParentWorkItemName
        {
            get
            {
                return this.ParentWorkItem.Name;
            }
        }

        /// <summary>
        /// The name of this job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Name { get { return this.Job.Name; } }

        /// <summary>
        /// The creation time of the job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime CreationTime { get { return this.Job.CreationTime; } }

        /// <summary>
        /// The state of the job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public JobState State { get { return this.Job.State; } }
        
        /// <summary>
        /// The tasks associated with this job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public List<TaskModel> Tasks { get; private set; }

        private TaskModel selectedTask;
        public TaskModel SelectedTask
        {
            get
            {
                return this.selectedTask;
            }
            set
            {
                this.selectedTask = value;
                this.FirePropertyChangedEvent("SelectedTask");
            }
        }

        #endregion

        private ICloudJob Job { get; set; }
        
        /// <summary>
        /// The task collection associated with this job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public ICollectionView TaskCollection { get; private set; }

        public JobModel(WorkItemModel parentWorkItem, ICloudJob job)
        {
            this.Job = job;
            this.Tasks = new List<TaskModel>();
            this.LastUpdatedTime = DateTime.UtcNow;
            this.ParentWorkItem = parentWorkItem;

            this.TaskCollection = CollectionViewSource.GetDefaultView(this.Tasks);
            this.UpdateTaskView();
        }

        #region ModelBase implementation
        public override SortedDictionary<string, object> PropertyValuePairs
        {
            get
            {
                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.Job);
                results.Add(LastUpdateFromServerString, this.LastUpdatedTime);
                return results;
            }
        }

        public override async System.Threading.Tasks.Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            //await this.Job.RefreshAsync(); //TODO: This causes ListTasks below to throw due to a bug in the OM, so for now we use a trick 
            Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));

            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    System.Threading.Tasks.Task asyncTask = this.Job.RefreshAsync(OptionsModel.Instance.ListDetailLevel);
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new JobOperation(JobOperation.Refresh, this.ParentWorkItem.Name, this.Job.Name)));
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
            }

            if (refreshType.HasFlag(ModelRefreshType.Children))
            {
                try
                {
                    //Set this before the children load so that on revisit we know we have loaded the children (or are in the process)
                    this.HasLoadedChildren = true; 

                    System.Threading.Tasks.Task<List<TaskModel>> asyncTask = this.ListTasksAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask, 
                        new JobOperation(JobOperation.ListTasks, this.ParentWorkItem.Name, this.Job.Name)));

                    this.Tasks = await asyncTask;
                    this.TaskCollection = CollectionViewSource.GetDefaultView(this.Tasks);
                    this.UpdateTaskView();
                }
                catch (Exception e)
                {
                    this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                    this.HandleException(e);
                }
            }

            Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, false));
            Messenger.Default.Send(new JobUpdateCompleteMessage());
        }
        #endregion

        #region Job operations

        /// <summary>
        /// Enable this job
        /// </summary>
        public async System.Threading.Tasks.Task EnableAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Job.EnableAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobOperation(JobOperation.Enable, this.ParentWorkItem.Name, this.Job.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }
        
        /// <summary>
        /// Disable this job
        /// </summary>
        public async System.Threading.Tasks.Task DisableAsync(DisableJobOption option)
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Job.DisableAsync(option);
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobOperation(JobOperation.Disable, this.ParentWorkItem.Name, this.Job.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }
        /// <summary>
        /// Delete this job
        /// </summary>
        public async System.Threading.Tasks.Task DeleteAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Job.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobOperation(JobOperation.Delete, this.ParentWorkItem.Name, this.Job.Name)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Terminate the job
        /// </summary>
        public async System.Threading.Tasks.Task TerminateAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Job.TerminateAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobOperation(JobOperation.Terminate, this.ParentWorkItem.Name, this.Job.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private async System.Threading.Tasks.Task<List<TaskModel>> ListTasksAsync()
        {
            List<TaskModel> results = new List<TaskModel>();

            IEnumerableAsyncExtended<ICloudTask> taskList = this.Job.ListTasks(OptionsModel.Instance.ListDetailLevel);
            IAsyncEnumerator<ICloudTask> asyncEnumerator = taskList.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                results.Add(new TaskModel(this, asyncEnumerator.Current));
            }

            return results;
        }

        #endregion

        #region Public methods
        /// <summary>
        /// Updates the associated list of Tasks view
        /// </summary>
        public void UpdateTaskView()
        {
            this.FireChangesOnRefresh(ModelRefreshType.Children);
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
