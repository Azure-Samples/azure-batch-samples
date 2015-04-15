using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class CreateWorkItemViewModel : EntityBase
    {
        #region Services
        private readonly IDataProvider batchService;
        #endregion

        #region Public UI Properties
        // Basic Work Item Details
        private string workItemName;
        public string WorkItemName
        {
            get
            {
                return this.workItemName;
            }
            set
            {
                this.workItemName = value;
                this.FirePropertyChangedEvent("WorkItemName");
            }
        }

        private string priority;
        public string Priority
        {
            get
            {
                return this.priority;
            }
            set
            {
                this.priority = value;
                this.FirePropertyChangedEvent("Priority");
            }
        }

        private string maxRetryCount;
        public string MaxRetryCount
        {
            get
            {
                return this.maxRetryCount;
            }
            set
            {
                this.maxRetryCount = value;
                this.FirePropertyChangedEvent("MaxRetryCount");
            }
        }

        private bool isCreateScheduleSelected;
        public bool IsCreateScheduleSelected
        {
            get
            {
                return this.isCreateScheduleSelected;
            }
            set
            {
                this.isCreateScheduleSelected = value;
                this.FirePropertyChangedEvent("IsCreateScheduleSelected");
            }
        }

        private bool isCreateJobManagerSelected;
        public bool IsCreateJobManagerSelected
        {
            get
            {
                return this.isCreateJobManagerSelected;
            }
            set
            {
                this.isCreateJobManagerSelected = value;
                this.FirePropertyChangedEvent("IsCreateJobManagerSelected");
            }
        }

        private TimeSpan? maxWallClockTime;
        public TimeSpan? MaxWallClockTime
        {
            get
            {
                return this.maxWallClockTime;
            }
            set
            {
                this.maxWallClockTime = value;
                this.FirePropertyChangedEvent("MaxWallClockTime");
            }
        }

        // Schedule
        private DateTime? doNotRunUntil;
        public DateTime? DoNotRunUntil
        {
            get
            {
                return this.doNotRunUntil;
            }
            set
            {
                this.doNotRunUntil = value;
                this.FirePropertyChangedEvent("DoNotRunUntil");
            }
        }

        private DateTime? doNotRunAfter;
        public DateTime? DoNotRunAfter
        {
            get
            {
                return this.doNotRunAfter;
            }
            set
            {
                this.doNotRunAfter = value;
                this.FirePropertyChangedEvent("DoNotRunAfter");
            }
        }

        private TimeSpan? startWindow;
        public TimeSpan? StartWindow
        {
            get
            {
                return this.startWindow;
            }
            set
            {
                this.startWindow = value;
                this.FirePropertyChangedEvent("StartWindow");
            }
        }

        private TimeSpan? recurrenceInterval;
        public TimeSpan? RecurrenceInterval
        {
            get
            {
                return this.recurrenceInterval;
            }
            set
            {
                this.recurrenceInterval = value;
                this.FirePropertyChangedEvent("RecurrenceInterval");
            }
        }

        // Job Manager
        private string jobManagerName;
        public string JobManagerName
        {
            get
            {
                return this.jobManagerName;
            }
            set
            {
                this.jobManagerName = value;
                this.FirePropertyChangedEvent("JobManagerName");
            }
        }

        private string commandLine;
        public string CommandLine
        {
            get
            {
                return this.commandLine;
            }
            set
            {
                this.commandLine = value;
                this.FirePropertyChangedEvent("CommandLine");
            }
        }

        private string maxTaskRetryCount;
        public string MaxTaskRetryCount
        {
            get
            {
                return this.maxTaskRetryCount;
            }
            set
            {
                this.maxTaskRetryCount = value;
                this.FirePropertyChangedEvent("MaxTaskRetryCount");
            }
        }

        private TimeSpan? maxTaskWallClockTime;
        public TimeSpan? MaxTaskWallClockTime
        {
            get
            {
                return this.maxTaskWallClockTime;
            }
            set
            {
                this.maxTaskWallClockTime = value;
                this.FirePropertyChangedEvent("MaxWallClockTime");
            }
        }

        private TimeSpan? retentionTime;
        public TimeSpan? RetentionTime
        {
            get
            {
                return this.retentionTime;
            }
            set
            {
                this.retentionTime = value;
                this.FirePropertyChangedEvent("RetentionTime");
            }
        }

        private List<bool> availableKillOnCompletionOptions;
        public List<bool> AvailableKillOnCompletionOptions
        {
            get
            {
                return this.availableKillOnCompletionOptions;
            }
            set
            {
                this.availableKillOnCompletionOptions = value;
                this.FirePropertyChangedEvent("AvailableKillOnCompletionOptions");
            }
        }

        private bool killOnCompletionSelectedItem;
        public bool KillOnCompletionSelectedItem
        {
            get
            {
                return this.killOnCompletionSelectedItem;
            }
            set
            {
                this.killOnCompletionSelectedItem = value;
                this.FirePropertyChangedEvent("KillOnCompletionSelectedItem");
            }
        }

        // Pool Settings
        private bool useAutoPool;
        public bool UseAutoPool
        {
            get
            {
                return this.useAutoPool;
            }
            set
            {
                this.useAutoPool = value;
                this.FirePropertyChangedEvent("UseAutoPool");
            }
        }

        private string poolName;
        public string PoolName
        {
            get
            {
                return this.poolName;
            }
            set
            {
                this.poolName = value;
                this.FirePropertyChangedEvent("PoolName");
            }
        }

        private string autoPoolPrefix;
        public string AutoPoolPrefix
        {
            get
            {
                return this.autoPoolPrefix;
            }
            set
            {
                this.autoPoolPrefix = value;
                this.FirePropertyChangedEvent("AutoPoolPrefix");
            }
        }

        private List<string> availableLifeTimeOptions;
        public List<string> AvailableLifeTimeOptions
        {
            get
            {
                return this.availableLifeTimeOptions;
            }
            set
            {
                this.availableLifeTimeOptions = value;
                this.FirePropertyChangedEvent("AvailableLifeTimeOptions");
            }
        }

        private string selectedLifetimeOption;
        public string SelectedLifetimeOption
        {
            get
            {
                return this.selectedLifetimeOption;
            }
            set
            {
                this.selectedLifetimeOption = value;
                this.FirePropertyChangedEvent("SelectedLifetimeOption");
            }
        }


        private List<bool> availableKeepAliveOptions;
        public List<bool> AvailableKeepAliveOptions
        {
            get
            {
                return this.availableKeepAliveOptions;
            }
            set
            {
                this.availableKeepAliveOptions = value;
                this.FirePropertyChangedEvent("AvailableKeepAliveOptions");
            }
        }

        private bool selectedKeepAliveItem;
        public bool SelectedKeepAliveItem
        {
            get
            {
                return this.selectedKeepAliveItem;
            }
            set
            {
                this.selectedKeepAliveItem = value;
                this.FirePropertyChangedEvent("SelectedKeepAliveItem");
            }
        }

        public bool IsCreateWorkItemButtonEnabled
        {
            get
            {
                return (!string.IsNullOrEmpty(this.WorkItemName));
            }
        }
        #endregion

        #region Commands
        public CommandBase CreateWorkItem
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        try
                        {
                            this.IsBusy = true;
                            await this.CreateWorkItemAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }

        public CommandBase CancelCreateWorkItem
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // TODO: Close window
                    }
                );
            }
        }
        #endregion

        public CreateWorkItemViewModel(IDataProvider batchService)
        {
            this.batchService = batchService;
            this.IsBusy = false;

            // pre-populate values
            this.AvailableKeepAliveOptions = new List<bool> { true, false };
            this.AvailableLifeTimeOptions = new List<string> { "Job", "WorkItem" };
            this.AvailableKillOnCompletionOptions = new List<bool> { true, false };

            this.SelectedKeepAliveItem = this.AvailableKeepAliveOptions[0];
            this.SelectedLifetimeOption = this.AvailableLifeTimeOptions[0];
            this.KillOnCompletionSelectedItem = this.AvailableKillOnCompletionOptions[0];
        }

        private async Task CreateWorkItemAsync()
        {
            try
            {
                if (this.IsInputValid())
                {
                    CreateWorkItemOptions options = new CreateWorkItemOptions()
                    {
                        AutoPoolPrefix = this.AutoPoolPrefix,
                        LifeTimeOption = this.SelectedLifetimeOption,
                        CommandLine = this.CommandLine,
                        DoNotRunAfter = this.DoNotRunAfter,
                        DoNotRunUntil = this.DoNotRunUntil,
                        CreateJobManager = this.IsCreateJobManagerSelected,
                        CreateSchedule = this.IsCreateScheduleSelected,
                        JobManagerName = this.JobManagerName,
                        KillOnCompletion = this.KillOnCompletionSelectedItem,
                        MaxRetryCount = this.GetNullableIntValue(this.MaxRetryCount),
                        MaxTaskRetryCount = this.GetNullableIntValue(this.MaxTaskRetryCount),
                        MaxTaskWallClockTime = this.MaxTaskWallClockTime,
                        MaxWallClockTime = this.MaxWallClockTime,
                        PoolName = this.PoolName,
                        Priority = this.GetNullableIntValue(this.Priority),
                        RecurrenceInterval = this.RecurrenceInterval,
                        RetentionTime = this.RetentionTime,
                        KeepAlive = this.SelectedKeepAliveItem,
                        SelectedLifetimeOption = this.SelectedLifetimeOption,
                        StartWindow = this.StartWindow,
                        UseAutoPool = this.UseAutoPool,
                        WorkItemName = this.WorkItemName
                    };

                    await this.batchService.CreateWorkItem(options);

                    Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.WorkItems));

                    Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully created work item {0}", this.WorkItemName)));
                    this.WorkItemName = string.Empty; //So that the user cannot accidentally try to create the same work item twice
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValid()
        {
            // TODO: Validate string to int inputs. e.g. Retry Count should be an int
            if (string.IsNullOrEmpty(this.WorkItemName))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Work Item Name"));
                return false;
            }

            if (this.IsCreateScheduleSelected)
            {
                // nothing to validate
            }

            if (!this.UseAutoPool && string.IsNullOrEmpty(this.PoolName))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Pool Name"));
                return false;
            }

            if (this.IsCreateJobManagerSelected)
            {
                if (string.IsNullOrEmpty(this.JobManagerName))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Job Manager Name"));
                    return false;
                }
                if (string.IsNullOrEmpty(this.CommandLine))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Command Line"));
                    return false;
                }
            }

            return true;
        }

        private int? GetNullableIntValue(string content)
        {
            int value;
            int? output = null;
            if (Int32.TryParse(content, out value))
            {
                output = value;
            }

            return output;
        }
    }
}
