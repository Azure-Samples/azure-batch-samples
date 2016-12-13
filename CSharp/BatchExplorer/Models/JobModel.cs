//Copyright (c) Microsoft Corporation

using Microsoft.Azure.Batch.Conventions.Files;

namespace Microsoft.Azure.BatchExplorer.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Data;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.BatchExplorer.Helpers;
    using Microsoft.Azure.BatchExplorer.Messages;
    using ViewModels;

    /// <summary>
    /// The data model for the Job object
    /// </summary>
    public class JobModel : ModelBase
    {
        #region Public properties
               
        private bool isChecked;
        /// <summary>
        /// Marker for multi selection
        /// </summary>
        public bool IsChecked
        {
            get
            {
                return this.isChecked;
            }
            set
            {
                this.isChecked = value;
                this.FirePropertyChangedEvent("IsChecked");
            }
        }

        /// <summary>
        /// Gets the id of the job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Id { get { return this.Job.Id; } }

        /// <summary>
        /// Gets the display name of the job
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string DisplayName { get { return this.Job.DisplayName; } }
        
        /// <summary>
        /// Gets the creation time of the job.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime? CreationTime { get { return this.Job.CreationTime; } }

        /// <summary>
        /// Gets the state of the job.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public JobState? State { get { return this.Job.State; } }
        
        /// <summary>
        /// Gets the tasks associated with this job.
        /// </summary>
        public List<TaskModel> Tasks { get; private set; }
        
        /// <summary>
        /// Gets the task collection associated with this job.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public ICollectionView TaskCollection { get; private set; }

        /// <summary>
        /// Gets the name of the Azure blob storage container for the outputs of a CloudJob.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public string OutputStorageContainerName { get { return this.Job.OutputStorageContainerName(); } }
        #endregion

        #region Commands

        /// <summary>
        /// Enable the selected job
        /// </summary>
        public CommandBase EnableJob
        {
            get
            {
                return
                    new CommandBase(
                        (param) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.EnableAsync()));
            }
        }


        /// <summary>
        /// Disable the selected job in the specified way
        /// </summary>
        public CommandBase DisableJob
        {
            get
            {
                return new CommandBase(
                    (disableOption) =>
                    {
                        var castDisableOption = ((DisableJobOption)disableOption);
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DisableAsync(castDisableOption));
                    });
            }
        }

        /// <summary>
        /// Terminate the selected job
        /// </summary>
        public CommandBase TerminateJob
        {
            get
            {
                return new CommandBase(
                    (item) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.TerminateAsync()));
            }
        }

        /// <summary>
        /// Delete the selected item
        /// </summary>
        public CommandBase Delete
        {
            get
            {
                return new CommandBase(
                    (item) =>
                    {
                        var job = (item as JobModel);
                        if (job != null)
                        {
                            Messenger.Default.Register<MultibuttonDialogReturnMessage>(this, (message) =>
                            {
                                if (message.MessageBoxResult == MessageBoxResult.Yes)
                                {
                                    AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DeleteAsync());
                                }
                                Messenger.Default.Unregister<MultibuttonDialogReturnMessage>(this);
                            });
                            Messenger.Default.Send<LaunchMultibuttonDialogMessage>(new LaunchMultibuttonDialogMessage()
                            {
                                Caption = "Confirm delete",
                                DialogMessage = "Do you want to delete this item?",
                                MessageBoxButton = MessageBoxButton.YesNo
                            });
                        }
                    });
            }
        }

        /// <summary>
        /// Creates a popup to add task to the specified job.
        /// </summary>
        public CommandBase AddTask
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        JobModel job = (JobModel)o;
                        // Call a new window to show the Add Task UI
                        Messenger.Default.Send(new ShowAddTaskWindow(job.Id));
                    }
                );
            }
        }

        /// <summary>
        /// Refresh the selected item
        /// </summary>
        public CommandBase RefreshItem
        {
            get
            {
                return new CommandBase(
                    (item) =>
                    {
                        var job = (item as JobModel);
                        if (job != null)
                        {
                            Task refreshTask = job.RefreshAsync(ModelRefreshType.Children | ModelRefreshType.Basic).ContinueWith((t) =>
                            {
                                FirePropertyChangedEvent("Jobs");
                            });
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(refreshTask);
                        }
                    });
            }
        }


        #endregion

        private CloudJob Job { get; set; }
        
        public JobModel(CloudJob job)
        {
            this.Job = job;
            this.Tasks = new List<TaskModel>();
            this.LastUpdatedTime = DateTime.UtcNow;

            this.TaskCollection = CollectionViewSource.GetDefaultView(this.Tasks);
            this.UpdateTaskView();
        }

        #region ModelBase implementation

        public override List<PropertyModel> PropertyModel
        {
            get { return this.ObjectToPropertyModel(this.Job); }
        }

        public override async System.Threading.Tasks.Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
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
                            new JobOperation(JobOperation.Refresh, this.Job.Id)));
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
                        new JobOperation(JobOperation.ListTasks, this.Job.Id)));

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
                    new JobOperation(JobOperation.Enable, this.Job.Id)));
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
                    new JobOperation(JobOperation.Disable, this.Job.Id)));
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
        public async System.Threading.Tasks.Task DeleteAsync(bool refresh = true)
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Job.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobOperation(JobOperation.Delete, this.Job.Id)));
                await asyncTask;
                if (refresh)
                {
                    Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.Jobs));
                }
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
                    new JobOperation(JobOperation.Terminate, this.Job.Id)));
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
            IPagedEnumerable<CloudTask> taskList = this.Job.ListTasks(OptionsModel.Instance.ListDetailLevel);
            
            await taskList.ForEachAsync(item => results.Add(new TaskModel(this, item)));

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
