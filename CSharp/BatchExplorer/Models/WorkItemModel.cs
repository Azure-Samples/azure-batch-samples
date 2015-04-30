using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The data model for the WorkItem object
    /// </summary>
    public class WorkItemModel : ModelBase
    {
        private const string JobNamePrefix = "job-";

        #region Properties

        private List<JobModel> jobs;
        /// <summary>
        /// The jobs associated with this work item
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public List<JobModel> Jobs
        {
            get
            {
                return this.jobs;
            }
            set
            {
                this.jobs = value;
                this.FirePropertyChangedEvent("Jobs");
            }
        }

        /// <summary>
        /// The name of this work item
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Name { get { return this.WorkItem.Name; } }

        /// <summary>
        /// The creation time of this work item
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime CreationTime { get { return this.WorkItem.CreationTime; } }

        /// <summary>
        /// The last modified time of this work item
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime? LastModified { get { return this.WorkItem.LastModified; } }

        private JobModel selectedJob;
        public JobModel SelectedJob
        {
            get
            {
                return this.selectedJob;
            }
            set
            {
                this.selectedJob = value;

                //Load the Tasks in this job just in time if they haven't been loaded yet
                if (!this.selectedJob.HasLoadedChildren)
                {
                    this.selectedJob.RefreshAsync(ModelRefreshType.Children).ContinueWith(
                        (t) =>
                        {
                            FirePropertyChangedEvent("SelectedJob");
                            FirePropertyChangedEvent("TasksTabTitle");
                            FirePropertyChangedEvent("TaskTabVisible");
                        },
                        TaskContinuationOptions.NotOnFaulted);
                }

                this.FirePropertyChangedEvent("TasksTabTitle");
                this.FirePropertyChangedEvent("SelectedJob");
                FirePropertyChangedEvent("TaskTabVisible");
            }
        }

        public bool TaskTabVisible
        {
            get
            {
                if (this.SelectedJob == null || this.SelectedJob.Tasks == null)
                {
                    return false;
                }

                return true;
            }
        }

        public string TasksTabTitle
        {
            get
            {
                const string taskTabPrefix = "Tasks";
                if (this.SelectedJob == null || this.SelectedJob.Tasks == null)
                {
                    return taskTabPrefix;
                }

                return string.Format(CultureInfo.CurrentCulture, "{0} ({1})", taskTabPrefix, this.SelectedJob.Tasks.Count);
            }
        }
        #endregion

        private ICloudWorkItem WorkItem { get; set; }
        private int smallestJobId;

        /// <summary>
        /// Create a WorkItemModel backed by an ICloudWorkItem
        /// </summary>
        public WorkItemModel(ICloudWorkItem workItem)
        {
            this.WorkItem = workItem;
            this.LastUpdatedTime = DateTime.UtcNow;
            this.Jobs = new List<JobModel>();

            smallestJobId = 0;
        }

        public WorkItemModel(ICloudWorkItem workItem, ICloudJob mostRecentJob)
        {
            this.WorkItem = workItem;
            this.LastUpdatedTime = DateTime.UtcNow;

            // create a new job list and add the most recent job to the list
            JobModel recentJob = new JobModel(this, mostRecentJob);
            this.Jobs = new List<JobModel> { recentJob };

            smallestJobId = WorkItemModel.GetJobNumberFromJobName(recentJob.Name);
        }

        #region ModelBase implementation

        public override SortedDictionary<string, object> PropertyValuePairs
        {
            get
            {
                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.WorkItem);
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
                    System.Threading.Tasks.Task asyncTask = this.WorkItem.RefreshAsync();
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new WorkItemOperation(WorkItemOperation.Refresh, this.WorkItem.Name)));
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

                    System.Threading.Tasks.Task<List<JobModel>> asyncTask = this.ListJobsAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new WorkItemOperation(WorkItemOperation.ListJobs, this.WorkItem.Name)));

                    this.Jobs = await asyncTask;
                    this.FireChangesOnRefresh(ModelRefreshType.Children);
                }
                catch (Exception e)
                {
                    this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                    this.HandleException(e);
                }
            }
        }

        #endregion

        #region Commands
        /// <summary>
        /// Download the next N jobs
        /// </summary>
        public CommandBase DownloadNMoreJobs
        {
            get
            {
                return new CommandBase(
                    (param) =>
                    {
                        // only download 10 jobs - make this configurable

                        // job numbers are always greater than 0
                        // never query for a job less than 1.
                        int min = Math.Max(1, smallestJobId - 10);
                        int max = Math.Max(1, smallestJobId - 1);
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.UpdateJobsAsync(min, max));

                        smallestJobId = min;
                    });
            }
        }

        /// <summary>
        /// Download all jobs
        /// </summary>
        public CommandBase DownloadAllJobs
        {
            get
            {
                return new CommandBase(
                    (param) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.UpdateJobsAsync()));
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
                        var castItem = (item as ModelBase);
                        if (castItem != null)
                        {
                            Task refreshTask = castItem.RefreshAsync(ModelRefreshType.Children | ModelRefreshType.Basic).ContinueWith((t) =>
                            {
                                var workItem = castItem as WorkItemModel;
                                if (workItem != null)
                                {
                                    FirePropertyChangedEvent("WorkItems");
                                }

                                var job = castItem as JobModel;
                                if (job != null)
                                {
                                    FirePropertyChangedEvent("Jobs");
                                    FirePropertyChangedEvent("TasksTabTitle");
                                }
                            });
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(refreshTask);
                        }
                    });
            }
        }

        /// <summary>
        /// Enable the selected job
        /// </summary>
        public CommandBase EnableJob
        {
            get
            {
                return
                    new CommandBase(
                        (param) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedJob.EnableAsync()));
            }
        }

        /// <summary>
        /// Enable the selected work item
        /// </summary>
        public CommandBase EnableWorkItem
        {
            get
            {
                return new CommandBase(
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
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedJob.DisableAsync(castDisableOption));
                    });
            }
        }

        /// <summary>
        /// Disable the selected job in the specified way
        /// </summary>
        public CommandBase DisableWorkItem
        {
            get
            {
                return new CommandBase(
                    (workItem) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DisableAsync()));
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
                    (item) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedJob.TerminateAsync()));
            }
        }

        /// <summary>
        /// Terminate the selected item
        /// </summary>
        public CommandBase TerminateWorkItem
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
                        var castItem = (item as ModelBase);
                        if (castItem != null)
                        {
                            Messenger.Default.Register<MultibuttonDialogReturnMessage>(this, (message) =>
                            {
                                if (message.MessageBoxResult == MessageBoxResult.Yes)
                                {
                                    var itemType = item.GetType();
                                    if (itemType == typeof(WorkItemModel))
                                    {
                                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DeleteAsync());
                                    }
                                    else if (itemType == typeof(JobModel))
                                    {
                                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedJob.DeleteAsync());
                                    }
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
                        // Call a new window to show the Create Work Item UI
                        Messenger.Default.Send<ShowAddTaskWindow>(new ShowAddTaskWindow(job.ParentWorkItemName, job.Name));
                    }
                );
            }
        }

        #endregion

        #region WorkItem operations
        /// <summary>
        /// Terminates this work item
        /// </summary>
        private async System.Threading.Tasks.Task TerminateAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.WorkItem.TerminateAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new WorkItemOperation(WorkItemOperation.Terminate, this.WorkItem.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Deletes this work item
        /// </summary>
        private async System.Threading.Tasks.Task DeleteAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.WorkItem.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new WorkItemOperation(WorkItemOperation.Delete, this.WorkItem.Name)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Enables this work item
        /// </summary>
        private async System.Threading.Tasks.Task EnableAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.WorkItem.EnableAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new WorkItemOperation(WorkItemOperation.Enable, this.WorkItem.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Disables this work item
        /// </summary>
        private async System.Threading.Tasks.Task DisableAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.WorkItem.DisableAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new WorkItemOperation(WorkItemOperation.Disable, this.WorkItem.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private async System.Threading.Tasks.Task<List<JobModel>> ListJobsAsync()
        {
            List<JobModel> results = new List<JobModel>();
            var detailLevel = new ODATADetailLevel() { SelectClause = "name,state,creationTime" };
            IEnumerableAsyncExtended<ICloudJob> jobList = this.WorkItem.ListJobs(detailLevel);

            IAsyncEnumerator<ICloudJob> asyncEnumerator = jobList.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                results.Add(new JobModel(this, asyncEnumerator.Current));
            }
            return results;
        }

        private async System.Threading.Tasks.Task<List<JobModel>> ListJobsAsync(int min, int max)
        {
            List<JobModel> results = new List<JobModel>();

            string uppperBound = string.Format("job-{0:D10}", max);
            string lowerBound = string.Format("job-{0:D10}", min);

            // TODO: Need to use the detail level to limit amount of data we download from the server
            // TODO: and increase the performace of listing jobs in the UI
            //var detailLevel = new ODATADetailLevel() { SelectClause = "name,state,creationTime" };
            //if (string.IsNullOrEmpty(detailLevel.FilterClause))
            //{
            //    detailLevel.FilterClause = string.Empty;
            //}
            //detailLevel.FilterClause += string.Format("(jobName le '{0}' and jobName gt '{1}')", uppperBound, lowerBound);
            //IEnumerableAsyncExtended<ICloudJob> jobList = this.WorkItem.ListJobs(detailLevel);
            // END TODO

            IEnumerableAsyncExtended<ICloudJob> jobList = this.WorkItem.ListJobs();
            IAsyncEnumerator<ICloudJob> asyncEnumerator = jobList.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                results.Add(new JobModel(this, asyncEnumerator.Current));
            }
            return results;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// This method attempts to download a range of jobs based on the job name
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        /// <remarks>
        /// We know that the job naming format is deterministic because it is set by the server
        /// The jobs names are formatted job-0000000000 where 1 is added to each new job
        /// </remarks>
        private async Task UpdateJobsAsync(int min, int max)
        {
            try
            {
                this.IsBusy = true;
                System.Threading.Tasks.Task<List<JobModel>> asyncTask = this.ListJobsAsync(min, max);
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                                                                       asyncTask,
                                                                       new WorkItemOperation(
                                                                           WorkItemOperation.ListJobs,
                                                                           this.WorkItem.Name)));

                List<JobModel> newItems = await asyncTask;
                this.Jobs.AddRange(newItems.OrderByDescending(j => j.Name));
                this.FirePropertyChangedEvent("Jobs");
            }
            catch (Exception e)
            {
                // Notify user
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private async Task UpdateJobsAsync()
        {
            try
            {
                this.IsBusy = true;
                System.Threading.Tasks.Task<List<JobModel>> asyncTask = this.ListJobsAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                                                                       asyncTask,
                                                                       new WorkItemOperation(
                                                                           WorkItemOperation.ListJobs,
                                                                           this.WorkItem.Name)));

                List<JobModel> newItems = await asyncTask;
                this.Jobs = newItems.OrderByDescending(j => j.Name).ToList();
                this.FirePropertyChangedEvent("Jobs");
            }
            catch (Exception e)
            {
                // Notify user
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
            finally
            {
                this.IsBusy = false;
            }
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

        private static int GetJobNumberFromJobName(string jobName)
        {
            return Int32.Parse(jobName.Replace(JobNamePrefix, string.Empty));
        }
        #endregion
    }
}
