//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Models
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.BatchExplorer.Helpers;
    using Microsoft.Azure.BatchExplorer.Messages;

    /// <summary>
    /// The data model for the JobSchedule object
    /// </summary>
    public class JobScheduleModel : ModelBase
    {
        #region Properties
        
        /// <summary>
        /// The id of the JobSchedule.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Id { get { return this.JobSchedule.Id; } }

        /// <summary>
        /// The display name of the JobSchedule.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string DisplayName { get { return this.JobSchedule.DisplayName; } }

        /// <summary>
        /// The state of the JobSchedule.
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public JobScheduleState? State { get { return this.JobSchedule.State; } }
        
        #endregion

        private CloudJobSchedule JobSchedule { get; set; }
        
        /// <summary>
        /// Create a JobScheduleModel backed by a CloudJobSchedule
        /// </summary>
        public JobScheduleModel(CloudJobSchedule jobSchedule)
        {
            this.JobSchedule = jobSchedule;
            this.LastUpdatedTime = DateTime.UtcNow;
        }

        #region ModelBase implementation

        public override List<PropertyModel> PropertyModel
        {
            get { return this.ObjectToPropertyModel(this.JobSchedule); }
        }

        public override async Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    Task asyncTask = this.JobSchedule.RefreshAsync();
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new JobScheduleOperation(JobScheduleOperation.Refresh, this.JobSchedule.Id)));
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
        }

        #endregion

        #region Commands
        
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
                        var jobSchedule = (item as JobScheduleModel);
                        if (jobSchedule != null)
                        {
                            Task refreshTask = jobSchedule.RefreshAsync(ModelRefreshType.Children | ModelRefreshType.Basic).ContinueWith((t) =>
                            {
                                FirePropertyChangedEvent("JobSchedules");
                            });
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(refreshTask);
                        }
                    });
            }
        }


        /// <summary>
        /// Enable the selected job schedule
        /// </summary>
        public CommandBase EnableJobSchedule
        {
            get
            {
                return new CommandBase(
                    (param) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.EnableAsync()));
            }
        }



        /// <summary>
        /// Disable the selected job schedule in the specified way
        /// </summary>

        public CommandBase DisableJobSchedule
        {
            get
            {
                return new CommandBase(
                    (jobSchedule) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DisableAsync()));
            }
        }

        
        /// <summary>
        /// Terminate the selected job schedule
        /// </summary>
        public CommandBase TerminateJobSchedule
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
                        var jobSchedule = (item as JobScheduleModel);
                        if (jobSchedule != null)
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


        #endregion

        #region JobSchedule operations
        /// <summary>
        /// Terminates this JobSchedule
        /// </summary>
        private async Task TerminateAsync()
        {
            try
            {
                Task asyncTask = this.JobSchedule.TerminateAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobScheduleOperation(JobScheduleOperation.Terminate, this.JobSchedule.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Deletes this JobSchedule
        /// </summary>
        private async Task DeleteAsync()
        {
            try
            {
                Task asyncTask = this.JobSchedule.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobScheduleOperation(JobScheduleOperation.Delete, this.JobSchedule.Id)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Enables this JobSchedule
        /// </summary>
        private async Task EnableAsync()
        {
            try
            {
                Task asyncTask = this.JobSchedule.EnableAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobScheduleOperation(JobScheduleOperation.Enable, this.JobSchedule.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Disables this JobSchedule
        /// </summary>
        private async Task DisableAsync()
        {
            try
            {
                Task asyncTask = this.JobSchedule.DisableAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new JobScheduleOperation(JobScheduleOperation.Disable, this.JobSchedule.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private async Task<List<JobModel>> ListJobsAsync()
        {
            List<JobModel> results = new List<JobModel>();
            var detailLevel = new ODATADetailLevel() { SelectClause = "id,state,creationTime" };
            IPagedEnumerable<CloudJob> jobList = this.JobSchedule.ListJobs(detailLevel);

            await jobList.ForEachAsync(item => results.Add(new JobModel(item)));

            return results;
        }

        private async Task<List<JobModel>> ListJobsAsync(int min, int max)
        {
            List<JobModel> results = new List<JobModel>();

            string uppperBound = string.Format("job-{0:D10}", max);
            string lowerBound = string.Format("job-{0:D10}", min);

            // TODO: Need to use the detail level to limit amount of data we download from the server
            // TODO: and increase the performace of listing jobs in the UI
            //var detailLevel = new ODATADetailLevel() { SelectClause = "id,state,creationTime" };
            //if (string.IsNullOrEmpty(detailLevel.FilterClause))
            //{
            //    detailLevel.FilterClause = string.Empty;
            //}
            //detailLevel.FilterClause += string.Format("(jobId le '{0}' and jobId gt '{1}')", uppperBound, lowerBound);
            //IEnumerableAsyncExtended<CloudJob> jobList = this.JobSchedule.ListJobs(detailLevel);
            // END TODO

            IPagedEnumerable<CloudJob> jobList = this.JobSchedule.ListJobs();
            await jobList.ForEachAsync(item => results.Add(new JobModel(item)));

            return results;
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
